using CommunityToolkit.HighPerformance;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlzEx.Controls;
using DelsysNICommon;
using DelsysSigNIalGen.Model;
using DelsysSigNIalGen.Utilities;
using DelsysTestLib.NIDAQ;
using DelsysTestLib.Util;
using MathNet.Filtering;
using MathNet.Numerics;
using Microsoft.Win32;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using Plotter.ViewModel.Plots;
using Plotter.ViewModel.RenderableSeries;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals;
using SciChart.Core.Extensions;
using SciChart.Data.Model;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Data;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Filtering.FIR;
using MathNet.Filtering.Windowing;
using System.Reactive.Subjects;
using Org.BouncyCastle.Bcpg;
using System.Windows.Media.Imaging;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static CommunityToolkit.Mvvm.ComponentModel.__Internals.__TaskExtensions.TaskAwaitableWithoutEndValidation;

namespace DelsysSigNIalGen.ViewModel;

partial class MainViewModel : ObservableObject
{
    public static MainViewModel Instance { get; } = new MainViewModel();
    public UniformXyDataSeries<double> PlotData { get; set; }
    public UniformXyDataSeries<double> PlotDataFFT { get; set; }
    FixtureFramework _ff => FixtureFramework.Instance;
    Timer _timer;
    List<string> pins;

    // Added -- aengel
    bool modemInitialized = false;
    TcpClient client;
    TcpClient pcmClient;
    NetworkStream stream;
    NetworkStream pcmStream;
    TcpClient clientDec;
    TcpClient pcmClientDec;
    NetworkStream streamDec;
    NetworkStream pcmStreamDec;
    BlockingCollection<float[]> waveBuff = new BlockingCollection<float[]>();
    BlockingCollection<double[]> readBuff = new BlockingCollection<double[]>();
    int lastI = 0;
    private static readonly object _lock = new object();
    private double noiseThreshold = 0.00032;

    public MainViewModel()
    {
        AISampleRate = 1_000_000; // 1MHz
        AIPullRate = (int)(AISampleRate * 0.1);
        AOSampleRate = 1_000_000; // 1MHz
        AnalogInRange = "± 5 V";

        //int a = fftDLLLayer.fftwFunc(); // for testing dll layer

        // Init Modem -- below is done to be super super sure we don't make two instances of our socket connections
        if (modemInitialized == false)
        {
            modemInitialized = true;
            try
            {
                (NetworkStream streamTest, NetworkStream pcmStreamTest, TcpClient clientTest, TcpClient pcmClientTest, NetworkStream streamTestDec, NetworkStream pcmStreamTestDec, TcpClient clientTestDec, TcpClient pcmClientTestDec) = ModemProgram.ModemInit(waveBuff);
                if (streamTest != null)
                {
                    (client, pcmClient) = (clientTest, pcmClientTest);
                    (stream, pcmStream) = (streamTest, pcmStreamTest);
                    (clientDec, pcmClientDec) = (clientTestDec, pcmClientTestDec);
                    (streamDec, pcmStreamDec) = (streamTestDec, pcmStreamTestDec);
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                MessageBox.Show($"Could not connect to modem. Please check your connection.\n{e.Message}");
            }
        }

        AI_Names = new List<string>();
        foreach (var value in HardwareModel.Instance.PhysicalHardware)
        {
            if (value.Value.PinType == "AI")
            {
                AI_Names.Add(value.Key);
            }
        }

        PlotData = new UniformXyDataSeries<double>()
        {
            XStep = 1.0 / AISampleRate,
            XStart = 0,
            FifoCapacity = 30_000_000,
        };

        //_ff.AIRead.GotInputData += InputData_Sender;
        pins = new List<string>();

        PlotViewModel = new LiveLinePlotViewModel(10) // Changed from 5, want to show 10s at a time
        {
            YAxisTitle = "Voltage",
            IsCursorModifierEnabled = true,

        };
        PlotViewModel.RenderableSeriesViewModels.Add(new TrignoChannelRenderableSeriesViewModel()
        {
            DataSeries = PlotData,
            YAxisId = PlotViewModel.YAxisId,
            ResamplingMode = SciChart.Data.Numerics.ResamplingMode.Auto,
        });
        _timer = new Timer(Timerfunction, null, 1000, 1000);

        Task.Run(()=>Consumer());
        Task.Run(() => WriterThread());
    }

    partial void OnIsContinuousChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            PlotData.FifoCapacity = SamplesInMemory;
        }
        else
        {
            PlotData.FifoCapacity = null;
        }
    }
    partial void OnSamplesInMemoryChanged(int oldValue, int newValue)
    {
        if (IsContinuous)
            PlotData.FifoCapacity = SamplesInMemory;
        else
            PlotData.FifoCapacity = null;
    }

    private void WriterThread()
    {
        while (!IsAnalogInNotRunning)
        {
            if (PlotData.Count > (SamplesInMemory))
            {
                SaveAndRemoveChunk((int)PlotData.XStart, AISampleRate, DataFilePath);
            }
        }
    }

    private void Consumer()
    {
        List<double> currData = new List<double>();
        int calls = 0;
        while (true)
        {
            try
            {
                if (!_ff.AIRead.DataBuffer.IsEmpty())
                {
                    if (_ff.AIRead.DataBuffer.Count > 1)
                    {
                        Trace.WriteLine("in queue " + _ff.AIRead.DataBuffer.Count);
                    }

                    _ff.AIRead.DataBuffer.TryTake(out var item);
                    if (item != null)
                    {
                        var bufferSpan = new ReadOnlySpan2D<double>(item);
                        var data = bufferSpan.GetRow(0).ToArray();
                        PlotData.Append(data);
                        currData.AddRange(data);
                    }
                    if (currData.Count >= 500_000)
                    {
                        readBuff.Add(currData.ToArray());
                        calls++;
                        currData.Clear();
                    }
                }
            }
            catch (Exception exp)
            {
                Trace.WriteLine("consumer" + exp);
            }
        }
    }

    public async Task GetWaveBuff()
    {
        int waveCalls = 0;
        while (!ModemProgram.globalStopped)
        {
            float[] takenData = null;
            try
            {
                takenData = waveBuff.Take(); // See if data loaded in
            }
            catch (InvalidOperationException) { }

            if (takenData != null)
            {
                if (waveCalls == 0)
                {
                    PlotData.Clear();
                    PlotData.Clear();
                    StartNICard();
                }
                PlotWaveBuff(takenData);
                waveCalls++;
            }
        }
        System.Diagnostics.Debug.WriteLine("\r\nWaveform Plotting Buffer Done.");
    }

    public void PlotWaveBuff(float[] takenData)
    {
        AOSampleRate = 1_000_000; // Hz

        _ff.StartGeneratedSignalFromFloatArray(Hw.GetPinAddress(TF_PIN.AO0), takenData, AOSampleRate, false);
        ZoomExtents = true;

        double durationMs = takenData.Length / 1_000;
        //Trace.WriteLine($"durationMs: {durationMs}");
        Task.Delay((int)durationMs).Wait();

        _ff.StopGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0)); // keep @ same val
    }

    public void Timerfunction(object state)
    {
        if (ZoomExtents)
            PlotViewModel.ViewportManager.ZoomExtentsY();
    }

    [RelayCommand]
    public void StartNICard()
    {
        PlotData.XStep = 1.0 / AISampleRate;
        ZoomExtents = true;
        IsAnalogInRunning = true;
        //set range based on scale
        PlotData.Clear();
        pins.Add(AnalogInChannel);

        _ff.AIRead.SAMPLE_RATE = (int)AISampleRate;
        _ff.AIRead.INPUT_BUFFER = (int)AIPullRate;
        double range = 5;
        try
        {
            range = Double.Parse(AnalogInRange.Split(" ")[1]);
        }
        catch (Exception e)
        {
            MessageBox.Show("Please select a range " + e.Message);

            return;
        }
        if (!IsContinuous)
        {
            Task.Run(() => WriterThread());
        }
        _ff.AIRead.StartAnalogRead(range, [..pins], pins.Last());
    }

    [RelayCommand]
    public void StopNICard()
    {
        Trace.WriteLine("SHUTTING DOWN NI CARD");
        ZoomExtents = false;
        IsAnalogInRunning = false;
        if (pins.Count != 0)
        {
            pins.Clear();
        }
        _ff.AIRead.StopAnalogRead();
        Task.Delay(1000).Wait();
        if (!IsContinuous)
        {
            using (StreamWriter writer = new StreamWriter(DataFilePath, true))
            {
                var yValues = PlotData.YValues.ToArray();
                for (int i = 0; i < yValues.Length; i++)
                {
                    double yValue = yValues[i];
                    writer.WriteLine($"{yValue}");
                }
            }
        }
        ModemProgram.globalStopped = true;

    }

    [RelayCommand]
    public void ExportToCSV()
    {
        //Trace.WriteLine("Exporting to CSV");
        var yValues = PlotData.YValues.ToArray();
        //SaveFileDialog saveDialog = new SaveFileDialog();
        //saveDialog.Filter = "CSV file (*.csv)|All Files (*.*)"; // Specify 
        //saveDialog.ShowDialog();
        //using (StreamWriter file = new StreamWriter(saveDialog.FileName + ".csv"))
        //{
        //    for (int i = 0; i < yValues.Length; i++)
        //    {
        //        file.Write(yValues[i] + "\n");
        //    }
        //}
        //Trace.WriteLine("Finished exporting to CSV");

        Trace.WriteLine("Exporting to .bin");
        using (FileStream fs = new FileStream($"..\\finalExportedBins\\FullAfterOutput_{ModemProgram.trialNum}.bin", FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            for (int i = 0; i < yValues.Length; i++)
            {
                if (i % 1_000_000 == 0)
                {
                    Trace.WriteLine($"i: {i}");
                }
                writer.Write(yValues[i]);
            }
        }
        Trace.WriteLine("Finished exporting to .bin");
    }

    [RelayCommand]
    private void SetSignalOnLine()
    {
        Trace.WriteLine("Setting signal on line----------------------------------------------------------------");
        try
        {
            _ff.StopTestSignal(Hw.GetPinAddress(TF_PIN.AO0));
            _ff.StopGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0));
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }

        try
        {
            switch (SignalShape)
            {
                case 0:
                    _ff.StartGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0), Frequency, Amplitude * 2, WaveformType.SineWave, DcOffset);
                    //Task.Run(()=>Hw.SetCalibratedAmpSine((int)Freq, Pk_PkVoltage));
                    break;
                case 1:
                    _ff.StartGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0), Frequency, Amplitude * 2, WaveformType.SAWTOOTH, DcOffset);
                    break;
                case 2:
                    _ff.StartTestSignal(Hw.GetPinAddress(TF_PIN.AO0), DcOffset); // DC wave generation
                    break;
                case 3:
                    _ff.StartGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0), Frequency, Amplitude * 2, WaveformType.SQUARE, DcOffset);
                    break;
                case 4:
                    _ff.StartGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0), Frequency, Amplitude * 2, WaveformType.TRIANGLE, DcOffset);
                    break;
            }
            Trace.WriteLine($"Signal Shape: {SignalShape}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            MessageBox.Show("OOPS it looks like there was an issue with that... : \n" + ex.Message);
        }

    }
    Stopwatch sw = new Stopwatch();
    private void InputData_Sender(object sender, GotInputDataEventArgs e)
    {
        Task.Run(() =>
        {
            //sw.Restart();
            var bufferSpan = new ReadOnlySpan2D<double>(e.SamplesBuffer);
            var data = bufferSpan.GetRow(0).ToArray();
            for (int i = 0; i < data.Length; i+=1000)
                PlotData.Append(data[i..(i+999)]);
            //sw.Stop();
            //Trace.WriteLine(sw.Elapsed.TotalMilliseconds);
        });
    }

    [RelayCommand]
    private void ConnectNICard()
    {
        if (_ff.MODEL == null)
            _ff.InitNICard(6363);

        if (_ff.MODEL == null)
        {
            Hw.IS_NI_CONNECTED = false;
            if (MessageBox.Show("Could not find NI card.\n Should the program try again?", "", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                Environment.Exit(0);
            }
        }
        else
        { Hw.IS_NI_CONNECTED = true; }
    }

    [RelayCommand]
    private void OpenWavFile()
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "Wav files (*.wav)|*.wav";
        if (openFileDialog.ShowDialog() == true)
        {
            WavFileLocation = openFileDialog.FileName;
            WavFileSelected = true;
            using (var audioFile = new AudioFileReader(WavFileLocation))
            {
                AOSampleRate = audioFile.WaveFormat.SampleRate;
            }
        }
        else
        {
            WavFileSelected = false;
        }
    }
    [RelayCommand]
    private void StartSweep()
    {
        _ff.StartSweep(Hw.GetPinAddress(TF_PIN.AO0), SweepAmplitude, SweepStartFreq, SweepEndFreq, SweepStep);
    }

    [RelayCommand]
    private void SetSignalFromFile()
    {
        // Possibly replace with stream
        //(TcpClient clientTest, TcpClient pcmClientTest) = ModemProgram.ModemInit(waveBuff, spectBuff);
        //if (clientTest != null)
        //{
        //    (client, pcmClient) = (clientTest, pcmClientTest);
        //}
        PlotData.YValues.Clear(); // reset anything that could be read
        waveBuff = new BlockingCollection<float[]>();
        Task.Run(() => ModemProgram.GetVals(stream, pcmStream, waveBuff));
        Task.Run(() => GetWaveBuff());
        Task.Run(() => ModemProgram.decodeControls(streamDec));
        Task.Run(() => ReadPackets());
        //Task.Run(() => WriteCont());
    }

    private void WriteCont()
    {
        using (FileStream fs = new FileStream($"..\\realtimeBins\\FullContOutput_{ModemProgram.trialNum}.bin", FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            int calls = 0;
            int localLastI = 0;
            while (calls == 0)
            {
                Thread.Sleep(100);
                double[] dataChunkFirst = PlotData.YValues.Skip(localLastI).Take(100_000).ToArray();
                int chunkLengthFirst = dataChunkFirst.Length;
                if (chunkLengthFirst != 0)
                {
                    foreach (double value in dataChunkFirst)
                    {
                        writer.Write(value);
                    }
                    localLastI = chunkLengthFirst;
                    calls++;
                }
            }
            Thread.Sleep(1_000);

            bool isDone = false;
            while (calls > 0 && !isDone)
            {
                double[] dataChunk = PlotData.YValues.Skip(localLastI).Take(1_000_000).ToArray();
                int chunkLength = dataChunk.Length;

                localLastI = localLastI + chunkLength;
                isDone = (ModemProgram.globalStopped == true);
                calls++;
                contWrite(writer, dataChunk);
                if (calls % 10 == 0)
                {
                    Trace.WriteLine($"BINARY WRITING CALL: {calls} -----------------------------------------------------------------");
                }
                Thread.Sleep(500);
            }
        }
        Trace.WriteLine("DONE WRITING REAL-TIME .BIN");
    }

    private void contWrite(BinaryWriter writer, double[] values)
    {
        foreach (double value in values)
        {
            writer.Write(value);
        }
    }


    [RelayCommand]
    private void GoToLiveLine()
    {
        PlotViewModel.GoToLiveTrace();
    }

    [RelayCommand]
    private void RunWavFormOnce()
    {
        Task.Run(() =>
        {
            if (WavFileSelected)
            {
                float[] floatArray;
                int length = 0;
                using (var audioFile = new AudioFileReader(WavFileLocation))
                {
                    floatArray = new float[audioFile.Length / sizeof(float)];
                    int bytesRead = audioFile.Read(floatArray, 0, floatArray.Length);
                    AOSampleRate = audioFile.WaveFormat.SampleRate;
                    length = audioFile.TotalTime.Seconds;
                }
                PlotData.Clear();
                StartNICard();
                _ff.StartGeneratedSignalFromFloatArray(Hw.GetPinAddress(TF_PIN.AO0), floatArray, AOSampleRate, false);
                ZoomExtents = true;
                Task.Delay(length * 1000).Wait();
                _ff.StopGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0));
                StopNICard();

                Trace.WriteLine("wav played once");
            }
            else
            {
                MessageBox.Show("Please select a file first");
            }
        });
    }
    [RelayCommand]
    private void RunWavFormOnceAndBoostAudio() // sends all zeros & finds noise level
    {
        Task.Run(() =>
        {
            if (WavFileSelected)
            {
                float[] floatArray;
                int length = 0;
                using (var audioFile = new AudioFileReader(WavFileLocation))
                {
                    floatArray = new float[audioFile.Length / sizeof(float)];
                    int bytesRead = audioFile.Read(floatArray, 0, floatArray.Length);
                    AOSampleRate = audioFile.WaveFormat.SampleRate;
                    length = audioFile.TotalTime.Seconds;
                }
                PlotData.Clear();
                StartNICard();
                _ff.StartGeneratedSignalFromFloatArray(Hw.GetPinAddress(TF_PIN.AO0), floatArray, AOSampleRate, false);
                ZoomExtents = true;
                Task.Delay(length * 1000).Wait();
                _ff.StopGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0));
                findNoiseThreshold(); // after waveform is stopped
                StopNICard();

                Trace.WriteLine("wav played once");
            }
            else
            {
                MessageBox.Show("Please select a file first");
            }
        });
    }

    [RelayCommand]
    private void ChangeSaveFileLocation()
    {
        SaveFileDialog saveDialog = new SaveFileDialog();
        saveDialog.Filter = "CSV file (*.csv)|All Files (*.*)"; // Specify 
        saveDialog.ShowDialog();
        DataFilePath = saveDialog.FileName + ".csv";
    }

    // Function to map a value from one range to another
    static float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax)
    {
        return outputMin + (outputMax - outputMin) * ((value - inputMin) / (inputMax - inputMin));
    }

    public void SaveAndRemoveChunk(int startIndex, int count, string filePath)
    {
        try
        {
            // Check if startIndex and count are within range
            if (startIndex < 0 || startIndex >= PlotData.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "Invalid start index.");

            if (count <= 0 || startIndex + count > PlotData.Count)
                throw new ArgumentOutOfRangeException(nameof(count), "Invalid count or exceeds buffer size.");

            var chunk = PlotData.YValues.Chunk(count).First();
            // Save chunkBuffer to file
            SaveDataSeriesToFile(chunk, filePath);

            var beforeRemoval = PlotData.XStart;
            // Remove the chunk from sciChartBuffer
            PlotData.RemoveRange((int)beforeRemoval, count);
            PlotData.XStart = beforeRemoval + (count / AISampleRate);
        }
        catch (Exception ex)
        {
            // Handle exceptions according to your application's needs
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private void SaveDataSeriesToFile(double[] dataSeries, string filePath)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                for (int i = 0; i < dataSeries.Length; i++)
                {
                    double yValue = dataSeries[i];
                    writer.WriteLine($"{yValue}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving data to file: {ex.Message}");
        }
    }


    public (double[], int) ButterHighpassFilter(double[] data, double cutoffFrequency, double samplingFrequency, int filterOrder = 2)
    {
        // Normalize the cutoff frequency to be between 0 and 1
        double normalizedCutoff = cutoffFrequency / (samplingFrequency / 2.0);

        // Create the high-pass filter
        var filter = OnlineFilter.CreateHighpass(ImpulseResponse.Finite, samplingFrequency, cutoffFrequency, filterOrder);

        // Apply the filter to the data
        double[] filteredData = filter.ProcessSamples(data);

        return (filteredData, data.Length);
    }

    private void ReadPackets()
    {
        int calls = 0;
        while (!ModemProgram.globalStopped)
        {
            double[] takenData = null;
            try
            {
                takenData = readBuff.Take(); // See if data loaded in
            }
            catch (InvalidOperationException) { }
            if (takenData != null)
            {
                float[] dfDowned = ModemProgram.DownsampleDownshiftNew(takenData, calls);
                ModemProgram.decodeMyWav(pcmStreamDec, dfDowned);
                calls++;
            }
        }
        Debug.WriteLine("\r\nReading Waveform Buffer Done.");
        //while (calls == 0)
        //{
        //    Thread.Sleep(100);
        //    double[] dataChunkFirst = PlotData.YValues.Skip(0).Take(100_000).ToArray();
        //    int chunkLengthFirst = dataChunkFirst.Length;
        //    if (chunkLengthFirst != 0)
        //    {
        //        float[] dfDowned = ModemProgram.DownsampleDownshiftNew(dataChunkFirst, calls);
        //        ModemProgram.decodeMyWav(pcmStreamDec, dfDowned);
        //        lastI = chunkLengthFirst;
        //        calls++;
        //    }
        //}
        //while (calls > 0)
        //{
        //    // new method to start reading same amount-ish every time
        //    Thread.Sleep(1_000); // Don't overwhelm reader -- change this to something else in the future

        //    lock (_lock) // should prevent dynamic changes... but doesn't? -- cause it's different threads bro
        //    {
        //        //yValues = PlotData.YValues.ToArray();
        //    }

        //    double[] subset = PlotData.YValues.Skip(lastI).Take(1_000_000).ToArray();

        //    lastI = lastI + 1_000_000;

        //    float[] dfDowned = ModemProgram.DownsampleDownshiftNew(subset, calls);
        //    //outputAsCSV(Array.ConvertAll(dfDowned, item => (double)item), $"U:\\Users Common\\AnnaE\\csvsUW\\AFTERDOWNED_{calls}.csv");

        //    ModemProgram.decodeMyWav(pcmStreamDec, dfDowned); // [40..]
        //    if (ModemProgram.globalStopped == true)
        //    {
        //        break;
        //    }
        //    calls++;
        //}
    }

    private void findNoiseThreshold()
    {
        double[] yValues = PlotData.YValues.ToArray();
        Trace.WriteLine($"Len of data: {yValues.Length}");

        double cutoffFrequency = 10e3; // 10 kHz
        double samplingFrequency = 1_000_000;
        int filterOrder = 2;

        (double[] dfHighPassed, _) = ButterHighpassFilter(yValues, cutoffFrequency, samplingFrequency, filterOrder);
        Trace.WriteLine($"Len highpassed: {dfHighPassed.Length}");
        float[] dfDowned = ModemProgram.DownsampleDownshiftNew(dfHighPassed, -1);
        Trace.WriteLine($"Len Downed: {dfDowned.Length}");
        double[] dfFilterDowned = Array.ConvertAll(dfDowned[40..], item => (double)item);  // remove first 40 (spike)

        //outputAsCSV(dfFilterDowned, "U:\\Users Common\\AnnaE\\analyzeCSVs\\ResOut.csv");

        noiseThreshold = Math.Max(Math.Abs(dfFilterDowned.Max()), Math.Abs(dfFilterDowned.Min())) + 0.00005; // plus slight wiggle room
        Trace.WriteLine($"Max threshold: {dfFilterDowned.Max()}");
        Trace.WriteLine($"Min threshold: {dfFilterDowned.Min()}");
        Trace.WriteLine($"Noise threshold: {noiseThreshold}");
    }

    private static void outputAsCSV(double[] array, string filename) // USE IN DEBUGGING
    {
        Trace.WriteLine($"Starting writing to {filename}");
        try
        {
            using (StreamWriter writer = new StreamWriter(filename))
            {
                for (int i = 0; i < array.Length; i++)
                {
                    writer.WriteLine(array[i]);
                }
            }
            Console.WriteLine("Data successfully written to " + filename);
        }
        catch (Exception ex)
        {
        }
    }
}
public class BooleanInverterAndVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Check if the value is a boolean and invert it
        if (value is bool boolValue)
        {
            // Convert the inverted boolean to Visibility
            return !boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Convert Visibility back to inverted boolean
        if (value is Visibility visibilityValue)
        {
            return visibilityValue != Visibility.Visible;
        }

        return false;
    }
}
