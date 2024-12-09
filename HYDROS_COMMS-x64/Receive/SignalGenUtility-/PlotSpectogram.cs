using SciChart.Charting.Visuals;
using SciChart.Charting.Model.DataSeries.Heatmap2DArrayDataSeries;
using SciChart.Charting.Visuals.RenderableSeries;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SciChart.Charting.Model.DataSeries;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using SciChart.Charting.Visuals.Axes;
using SciChart.Data.Model;
using System.Diagnostics;

namespace DelsysSigNIalGen
{
    public class SpectPlot
    {
        private static int plotInitCalls = 0;
        public static SpectrogramDemoView SpectInit()
        {
            int samplingRate = 1_000_000; // Hz -- not ever referenced, window size is based on it instead
            if (plotInitCalls == 0)
            {
                plotInitCalls = 1;
                var spectrogramView = new SpectrogramDemoView(samplingRate);
                return spectrogramView;
            }
            return null;
        }
    }

    public class SpectrogramDemoView : UserControl
    {
        private readonly DispatcherTimer updateTimer;
        private readonly DispatcherTimer _timer;
        private readonly XyDataSeries<double, double> _xyDataSeries = new XyDataSeries<double>();

        private readonly object _tickLocker = new object();

        private readonly double[] _re;
        private readonly double[] _im;

        private readonly double[,] _spectrogramBuffer = new double[100, 102_400];

        private readonly IDataSeries _uniformHeatmapDataSeries;
        private double[] _waveform;
        private double[] myWav;

        private readonly FastLineRenderableSeries _lineRenderableSeries = new FastLineRenderableSeries();
        private readonly FastUniformHeatmapRenderableSeries _heatmapRenderableSeries = new FastUniformHeatmapRenderableSeries();

        private int updateCalls = 0;
        private bool timerGoing = true;
        private readonly Random _random = new Random();

        public SpectrogramDemoView(int samplingRate)
        {
            updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _re = new double[102_400];
            _im = new double[102_400];

            // Ensure the dimensions match the spectrogram buffer
            _uniformHeatmapDataSeries = new UniformHeatmapDataSeries<double, double, double>(_spectrogramBuffer, 0, 976, 0, 0.001); // stepx & stepy unused


            var _lineRenderableSeries = new FastLineRenderableSeries { DataSeries = _xyDataSeries };
            var _heatmapRenderableSeries = new FastUniformHeatmapRenderableSeries { DataSeries = _uniformHeatmapDataSeries };

            Application.Current.Dispatcher.Invoke(() =>
            {
                var heatmapSurface = (SciChartSurface)Application.Current.MainWindow.FindName("HeatmapSurface");
                heatmapSurface.RenderableSeries.Clear();
                heatmapSurface.RenderableSeries.Add(_lineRenderableSeries);

                var xAxis = heatmapSurface.XAxis as NumericAxis;
                if (xAxis != null)
                {
                    // Define the visible range
                    var visibleRange = new DoubleRange(0, 50_000); // 500_000 // 1_000_000 // Adapt based on differences -- variable for chirp

                    // Set the visible range for the X-axis
                    xAxis.VisibleRange = visibleRange;
                }
            });
        }

        public void nextCall(float[] wavF, int calls)
        {
            Trace.WriteLine($"Spect Call: {calls}, Spect Data Len: {wavF.Length}");
            // convert
            double[] waveform = Array.ConvertAll(wavF, item => (double)item);
            myWav = waveform;

            updateCalls = 0;

            while ((updateCalls + 1) * 102_400 < waveform.Length + 1) // this works for specific packet, but not arbitrary length/cut data
            {
                UpdateXyDataSeries(updateCalls, waveform);
                updateCalls++;
                Thread.Sleep(1000); // for 5s waveform // other 50ms for calculations
            }
        }

        public void UpdateXyDataSeries(int calls, double[] wav, double num = 102_400)
        {
            if (calls * 102_400 + 102_400 > wav.Length)
            {
                return;
            }

            lock (_tickLocker)
            {
                //double meanRe = 0;
                //double meanIm = 0;
                int length = Math.Min(102_400, wav.Length);
                var complexSignal = new Complex[length];
                for (int i = 0; i < length; i++)
                {
                    //complexSignal[i] = new Complex(2.0 * Math.Sin(2 * Math.PI * i / 20) +
                    //        5 * Math.Sin(2 * Math.PI * i / 10) +
                    //        2.0 * _random.NextDouble(), -10); // EXAMPLE
                    if (i + calls * 102_400 < wav.Length)
                    {
                        complexSignal[i] = new Complex(wav[i + calls * 102_400], -10); // MINE
                        //meanRe = meanRe + wav[i + calls * 102_400];
                        //meanIm = meanIm + (-10);
                    }
                }

                //meanRe = meanRe / length;
                //meanIm = meanIm / length;

                //for (int i = 0; i < length; i++)
                //{
                //    complexSignal[i] = new Complex(complexSignal[i].Real - meanRe, complexSignal[i].Imaginary - meanIm);
                //}

                // SUBTRACT MEAN HERE?
                Fourier.Forward(complexSignal, FourierOptions.Matlab);

                for (int j = 0; j < 102_400; j++)
                {
                    double mag = complexSignal[j].Magnitude;
                    _re[j] = 20 * Math.Log10(mag); // / 102_400 for normalization of mag
                    _im[j] = j;
                }

                //for (int k = 0; k < length; k++)
                //{
                //    _im[k] = _im[k] * (10); // factor
                //}

                _xyDataSeries.Clear();
                _xyDataSeries.Append(_im, _re);
            }
        }
    }
}





//private static SpectrogramData CalculateSpectrogram(double[] signal, int samplingRate)
//{
//    int windowSize = 1024; // Size of each FFT window
//    int overlap = windowSize / 2; // Overlap between windows

//    int numWindows = (signal.Length - windowSize) / (windowSize - overlap);
//    double[,] spectrogram = new double[numWindows, windowSize / 2];
//    double[] frequencies = new double[windowSize / 2];
//    double[] times = new double[numWindows];

//    Trace.WriteLine($"STARTING LOOP, numWindows: {numWindows}");
//    for (int i = 0; i < numWindows; i++)
//    {
//        double[] window = signal.Skip(i * (windowSize - overlap)).Take(windowSize).ToArray();
//        var windowedSignal = window.Select((v, index) => v * MathNet.Numerics.Window.Hamming(windowSize)[index]).ToArray();
//        Complex32[] fftResult = new Complex32[windowSize];
//        for (int j = 0; j < windowSize; j++)
//        {
//            fftResult[j] = new Complex32((float)windowedSignal[j], 0);
//        }

//        Fourier.Forward(fftResult, FourierOptions.Matlab);

//        for (int j = 0; j < windowSize / 2; j++)
//        {
//            spectrogram[i, j] = fftResult[j].Magnitude;
//            if (i == 0)
//            {
//                frequencies[j] = j * samplingRate / (double)windowSize;
//            }
//        }

//        times[i] = i * (windowSize - overlap) / (double)samplingRate;

//        if (i % 1000 == 0)
//        {
//            Trace.WriteLine($"i: {i}");
//        }
//    }

//    return new SpectrogramData
//    {
//        Frequencies = frequencies,
//        Times = times,
//        Sxx = spectrogram
//    };
//}

//public static void PlotSpectrogram(SpectrogramData data)
//{
//    Application.Current.Dispatcher.Invoke(() =>
//    {
//        var heatmapSurface = (SciChartSurface)Application.Current.MainWindow.FindName("HeatmapSurface");

//        // Create and fill the heatmap data
//        int cellHeight = data.Sxx.GetLength(0);
//        int cellWidth = data.Sxx.GetLength(1);
//        var dataArray = new double[cellWidth, cellHeight];
//        for (int i = 0; i < cellWidth; i++)
//        {
//            for (int j = 0; j < cellHeight; j++)
//            {
//                dataArray[i, j] = data.Sxx[j, i]; // swap x & y!!
//            }
//        }

//        //int cellHeight = data.Sxx.GetLength(0);
//        //int cellWidth = data.Sxx.GetLength(1);
//        //var dataArray = new double[cellHeight, cellWidth];
//        //for (int i = 0; i < cellHeight; i++)
//        //{
//        //    for (int j = 0; j < cellWidth; j++)
//        //    {
//        //        dataArray[i, j] = data.Sxx[i, j]; // swap x & y!!
//        //    }
//        //}

//        double startX = 0;
//        double stepX = (double) 5.0 / 9763.0; // durS / numWindows // note switched axes
//        double startY = 0;
//        double stepY = (double) 1_000_000.0 / 1024.0; // samplingFreq / windowSize // note switched axes
//        var heatmapDataSeries = new UniformHeatmapDataSeries<double, double, double>(dataArray, startX, stepX, startY, stepY);

//        var gradientStops = new GradientStopCollection();
//        gradientStops.Add(new GradientStop(Colors.Blue, 0));     // Example: Blue at minimum value
//        gradientStops.Add(new GradientStop(Colors.Green, 0.5));  // Example: Green at midpoint
//        gradientStops.Add(new GradientStop(Colors.Red, 1));      // Example: Red at maximum value

//        ObservableCollection<GradientStop> _gradientStops = new ObservableCollection<GradientStop>();
//        _gradientStops.Add(new GradientStop(Colors.Blue, 0.0));
//        _gradientStops.Add(new GradientStop(Colors.Green, 0.5));
//        _gradientStops.Add(new GradientStop(Colors.Red, 1.0));
//        var heatmapRenderableSeries = new FastUniformHeatmapRenderableSeries
//        {
//            DataSeries = heatmapDataSeries,
//            ColorMap = new HeatmapColorPalette
//            {
//                Maximum = data.Sxx.Cast<double>().Max(),
//                Minimum = data.Sxx.Cast<double>().Min(),
//                GradientStops = _gradientStops,
//            }
//        };

//        heatmapSurface.RenderableSeries.Add(heatmapRenderableSeries);
//        Trace.WriteLine("A");
//    });
//}
//    }
//}

//public static void PlotSpectrogram(double[] waveform, int samplingRate)
//{
//    //var application = new Application();
//    //application.Startup += (s, e) =>
//    //{
//    //    var window = new System.Windows.Window
//    //    {
//    //        Width = 800,
//    //        Height = 600,
//    //        Content = new SpectrogramDemoView(waveform, samplingRate)
//    //    };
//    //    window.Show();
//    //};
//    //application.Run();
//}

//LineRenderableSeries.DataSeries = _xyDataSeries;
//HeatmapRenderableSeries.DataSeries = _uniformHeatmapDataSeries;

//var gradientStops = new ObservableCollection<GradientStop>();
//gradientStops.Add(new GradientStop(Colors.Blue, 0.0));
//gradientStops.Add(new GradientStop(Colors.Green, 0.5));
//gradientStops.Add(new GradientStop(Colors.Red, 1.0));

//int cellHeight = _spectrogramBuffer.GetLength(0);
//int cellWidth = _spectrogramBuffer.GetLength(1);
//var dataArray = new double[cellHeight, cellWidth];
//for (int i = 0; i < cellHeight; i++)
//{
//    for (int j = 0; j < cellWidth; j++)
//    {
//        dataArray[i, j] = _spectrogramBuffer[i, j];
//    }
//}

// Ensure _lineRenderableSeries and _heatmapRenderableSeries are properly initialized
// Example: Replace with your actual series initialization

//var _heatmapRenderableSeries = new FastUniformHeatmapRenderableSeries
//{
//    DataSeries = _uniformHeatmapDataSeries,
//    ColorMap = new HeatmapColorPalette
//    {
//        Maximum = 110_000,//dataArray.Cast<double>().Max(),
//        Minimum = 0,//dataArray.Cast<double>().Min(),
//        GradientStops = gradientStops,
//    }
//}; 

// Dispatcher.Invoke example usage

//public class SpectrogramData
//{
//    public double[] Frequencies { get; set; }
//    public double[] Times { get; set; }
//    public double[,] Sxx { get; set; }
//}

//public static void MainPLOT()
//{
//    string filePath = @"C:\Users\AEngel\Desktop\Real_Time_Transforms\TransformExampleApp\outCSVs\moddedB0.csv";

//    // Read the CSV file
//    var dataLines = File.ReadAllLines(filePath);

//    // Parse the CSV data
//    double[] waveform = dataLines.Skip(1).Select(double.Parse).ToArray();

//    //SpectrogramData spectrogramData = CalculateSpectrogram(waveform, 1_000_000);

//    if (plotInitCalls == 0)
//    {
//        plotInitCalls = 1;
//        var spectrogramView = new SpectrogramDemoView(waveform, 1_000_000);
//    }
//}

//public SpectrogramDemoViewBefore(double[] waveform, int samplingRate)
//{
//_waveform = waveform;

//_re = new double[100_000];
//_im = new double[100_000];

//// Ensure the dimensions match the spectrogram buffer
//_uniformHeatmapDataSeries = new UniformHeatmapDataSeries<double, double, double>(_spectrogramBuffer, 0, 976, 0, 0.001);


//var _lineRenderableSeries = new FastLineRenderableSeries { DataSeries = _xyDataSeries };
//var _heatmapRenderableSeries = new FastUniformHeatmapRenderableSeries { DataSeries = _uniformHeatmapDataSeries };


//Application.Current.Dispatcher.Invoke(() =>
//{
//    var heatmapSurface = (SciChartSurface)Application.Current.MainWindow.FindName("HeatmapSurface");
//    heatmapSurface.RenderableSeries.Clear();
//    // heatmapSurface.RenderableSeries.Add(_heatmapRenderableSeries); // Add series if needed ?
//    heatmapSurface.RenderableSeries.Add(_lineRenderableSeries); 

//    var xAxis = heatmapSurface.XAxis as NumericAxis;
//    if (xAxis != null)
//    {
//        // Define the visible range
//        var visibleRange = new DoubleRange(0, 200_000); // Adapt based on differences

//        // Set the visible range for the X-axis
//        xAxis.VisibleRange = visibleRange;
//    }

//});

//FirstCreateSeries();

//_timer = new DispatcherTimer(DispatcherPriority.Render)
//{
//    Interval = TimeSpan.FromMilliseconds(100),
//    IsEnabled = true
//};
//_timer.Tick += TimerOnTick;

//}

//private void TimerOnTick(object sender, EventArgs e)
//{
//    if ((updateCalls + 1) * 100_000 < myWav.Length)
//    {
//        UpdateXyDataSeries(updateCalls, myWav);
//        updateCalls++;
//    }
//    else
//    {
//        timerGoing = false;
//        updateTimer.Stop();
//        //shouldStopTimer = true;
//    }
//    //UpdateDataOnTimerTick();
//}

//private void FirstCreateSeries(double[] wav)
//{
//    for (int x = 0; x < 100; x++)
//    {
//        UpdateXyDataSeries(0, wav);
//        for (int y = 0; y < 100_000; y++)
//            _pastFrame[x, y] = _xyDataSeries.YValues[y];
//    }
//}

//private void UpdateDataOnTimerTick()
//{
//    updateCalls++;
//    using (_xyDataSeries.SuspendUpdates())
//    {
//        UpdateXyDataSeries(updateCalls);
//        UpdateSpectrogramHeatmapSeries(_xyDataSeries);
//    }
//}
//private void UpdateSpectrogramHeatmapSeries(XyDataSeries<double, double> series)
//{
//    // Compute the new spectrogram frame
//    for (int x = 99; x >= 0; x--)
//        for (int y = 0; y < 100_000; y++)
//            _spectrogramBuffer[x, y] = (x == 99) ? series.YValues[y] : _pastFrame[x + 1, y];

//    // Preserve the past frame, as current spectrogram is computed based on last + Xy fft values
//    Array.Copy(_spectrogramBuffer, _pastFrame, _spectrogramBuffer.Length);

//    // Forces Heatmap to redraw after updating values
//    _uniformHeatmapDataSeries.InvalidateParentSurface(RangeMode.None);
//}