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
using ControlzEx.Standard;
using DelsysSigNIalGen;

namespace DelsysSigNIalGen.ViewModel;

// THIS IS THE TRANSMIT PROJECT
partial class MainViewModel : ObservableObject
{
    public static MainViewModel Instance { get; } = new MainViewModel();
    public UniformXyDataSeries<double> PlotData { get; set; }
    FixtureFramework _ff => FixtureFramework.Instance;
    Timer _timer;
    List<string> pins;

    // Added -- aengel
    bool modemInitialized = false;
    TcpClient client;
    TcpClient pcmClient;
    NetworkStream stream;
    NetworkStream pcmStream;
    BlockingCollection<float[]> waveBuff = new BlockingCollection<float[]>();
    BlockingCollection<float[]> waveBuff2 = new BlockingCollection<float[]>();
    BlockingCollection<double[]> readBuff = new BlockingCollection<double[]>();

    public MainViewModel()
    {
        AISampleRate = 1_000_000; // 1MHz
        AIPullRate = (int)(AISampleRate * 0.1);
        AOSampleRate = 1_000_000; // 1MHz
        AnalogInRange = "± 5 V"; // You can still toggle in app

        int a = fftDLLLayer.fftwFunc(); // for testing dll layer

        // Init Modem -- below is done to be super super sure we don't make two instances of our socket connections
        if (modemInitialized == false)
        {
            modemInitialized = true;
            (NetworkStream streamTest, NetworkStream pcmStreamTest, TcpClient clientTest, TcpClient pcmClientTest) = ModemProgram.ModemInit();
            if (streamTest != null)
            {
                (client, pcmClient) = (clientTest, pcmClientTest);
                (stream, pcmStream) = (streamTest, pcmStreamTest);
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
                        //PlotData.Append(data);
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

    // Wait to get the encoded waveforms from the modem
    public async Task GetWaveBuff(CancellationTokenSource cts3)
    {
        int waveCalls = 0;

        while (!ModemProgram.globalStopped && !cts3.Token.IsCancellationRequested)
        {
            float[] takenData = null;
            try
            {
                takenData = waveBuff.Take(); // See if data loaded in
            }
            catch (InvalidOperationException) { }

            if (takenData != null)
            {
                // First data starts the whole program
                if (waveCalls == 0)
                {
                    PlotData.Clear();
                    PlotData.Clear();
                    StartNICard();
                }
                Trace.WriteLine($"Added waveforms #{waveCalls}!");
                TransmitWave(takenData);
                waveCalls++;
            }

            if (waveCalls == ModemProgram.numPackets)
            {
                cts3.Cancel();
                break;
            }
        }
        System.Diagnostics.Debug.WriteLine("\r\nBuffer to Transducer Done.");
    }
    public async Task GetWaveBuff2(CancellationTokenSource cts3)
    {
        int waveCalls = 0;

        while (!ModemProgram.globalStopped && !cts3.Token.IsCancellationRequested)
        {
            float[] takenData = null;
            float[] takenData2 = null;
            try
            {
                takenData = waveBuff.Take(); // See if data loaded in
                takenData2 = waveBuff2.Take(); // See if data loaded in
            }
            catch (InvalidOperationException) { }

            if (takenData != null & takenData2 != null)
            {
                // First data starts the whole program
                if (waveCalls == 0)
                {
                    PlotData.Clear();
                    PlotData.Clear();
                    StartNICard();
                }
                Trace.WriteLine($"Added waveforms #{waveCalls}!");
                TransmitWave(takenData,takenData2);
                waveCalls++;
            }
              
            if (waveCalls == ModemProgram.numPackets)
            {
                cts3.Cancel();
                break;
            }
        }
        System.Diagnostics.Debug.WriteLine("\r\nBuffer to Transducer Done.");
    }

    // Send loaded signal to transducer
    public void TransmitWave(float[] takenData)
    {
        AOSampleRate = 1_000_000; // Hz

        // Send to transducer over TF.PIN.#
        _ff.StartGeneratedSignalFromFloatArray(Hw.GetPinAddress(TF_PIN.AO0), takenData, AOSampleRate, false, ModemProgram.sine, ModemProgram.secretCarrierFrequency, ModemProgram.voltageAmplitude);
        ZoomExtents = true;

        double durationMs = takenData.Length / 1_000; // This works for AOSampleRate of 1MHz
        Task.Delay((int)durationMs).Wait();

        // Make the transducer continue the same last value (should be 0) until next signal is received
        _ff.StopGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0));
    }

    public void TransmitWave(float[] takenData, float[] takenData2)
    {
        AOSampleRate = 1_000_000; // Hz

        // Send to transducer over TF.PIN.#
        _ff.StartGeneratedSignalFromFloatArray(Hw.GetPinAddress(TF_PIN.AO0), Hw.GetPinAddress(TF_PIN.AO1), takenData, takenData2, AOSampleRate, false, ModemProgram.sine, ModemProgram.secretCarrierFrequency, ModemProgram.secretCarrierFrequency2, ModemProgram.voltageAmplitude, ModemProgram.voltageAmplitude2);
        ZoomExtents = true;

        double durationMs = takenData.Length / 1_000; // This works for AOSampleRate of 1MHz
        Task.Delay((int)durationMs).Wait();

        // Make the transducer continue the same last value (should be 0) until next signal is received
        _ff.StopGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO0));
        _ff.StopGeneratedSignal(Hw.GetPinAddress(TF_PIN.AO1));
    }

    public void Timerfunction(object state)
    {
        if (ZoomExtents)
            PlotViewModel.ViewportManager.ZoomExtentsY();
    }

    [RelayCommand]
    public void StartNICard()
    {
        ModemProgram.globalStopped = false;
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
        ModemProgram.globalStopped = true;
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
    }

    [RelayCommand]
    public void ExportToCSV() // I changed this to export to a binary file. Shouldn't matter in transmit project, since we are not interested in received signal here.
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
        using (FileStream fs = new FileStream($"C:\\Users\\TJoe\\Documents\\finalExportedBins\\FullAfterOutput_{ModemProgram.trialNum}.bin", FileMode.Create, FileAccess.Write, FileShare.None))
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
    //private void InputData_Sender(object sender, GotInputDataEventArgs e)
    //{
    //    Task.Run(() =>
    //    {
    //        //sw.Restart();
    //        var bufferSpan = new ReadOnlySpan2D<double>(e.SamplesBuffer);
    //        var data = bufferSpan.GetRow(0).ToArray();
    //        for (int i = 0; i < data.Length; i+=1000)
    //            PlotData.Append(data[i..(i+999)]);
    //        //sw.Stop();
    //        //Trace.WriteLine(sw.Elapsed.TotalMilliseconds);
    //    });
    //}

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
    public void PlayData() // Send packets
    {
        ModemProgram.numTransducers = 2; // number of transducers
        ModemProgram.sine = 0; // set 0 to not tranmit sine, set 1 to transmit only t1 sine, set 2 to transmit only t2 sine, set 3 to transmit both sine

<<<<<<< Updated upstream
        ModemProgram.numPackets = 10; // number of packets to send
        ModemProgram.secretCarrierFrequency = 100000; // Carrier Frequency;
        ModemProgram.secretCarrierFrequency2 = 101000; // Carrier Frequency;

        ModemProgram.voltageAmplitude = 20; // +/- voltageAmplitude is the max/min waveform voltages
=======
        ModemProgram.numPackets = 25; // number of packets to send
        ModemProgram.secretCarrierFrequency = 300000; // Carrier Frequency;
        ModemProgram.secretCarrierFrequency2 = 100000; // Carrier Frequency;

        ModemProgram.voltageAmplitude = 10; // +/- voltageAmplitude is the max/min waveform voltages
>>>>>>> Stashed changes
        ModemProgram.voltageAmplitude2 = 10; // +/- voltageAmplitude is the max/min waveform voltages

        ModemProgram.globalStopped = false;
        PlotData.YValues.Clear(); // reset anything that could be read
        waveBuff = new BlockingCollection<float[]>();


        // Scaling Amplitude for DAC
        ModemProgram.voltageAmplitude = ModemProgram.voltageAmplitude/4; // +/- voltageAmplitude is the max/min waveform voltages
        ModemProgram.voltageAmplitude2 = ModemProgram.voltageAmplitude2/4; // +/- voltageAmplitude is the max/min waveform voltages
        // Generate encoded waveforms
        CancellationTokenSource cts = new CancellationTokenSource();
        if (ModemProgram.numTransducers == 2)
        {
            waveBuff2 = new BlockingCollection<float[]>();
            Task.Run(() => ModemProgram.GetVals(stream, pcmStream, waveBuff, waveBuff2, cts));
            // Generate Wave Buff
            CancellationTokenSource cts3 = new CancellationTokenSource();
            Task.Run(() => GetWaveBuff2(cts3));
        }
        else if (ModemProgram.numTransducers == 1)
        {
            Task.Run(() => ModemProgram.GetVals(stream, pcmStream, waveBuff, cts));
            // Generate Wave Buff
            CancellationTokenSource cts3 = new CancellationTokenSource();
            Task.Run(() => GetWaveBuff(cts3));
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
                // Define input range
                float inputMin = floatArray.Min();
                float inputMax = floatArray.Max();

                // Define output range (+5V to -5V)
                float outputMin = -5f;
                float outputMax = 5f;

                // Linearly scale the values
                float[] scaledValues = new float[floatArray.Length];
                for (int i = 0; i < floatArray.Length; i++)
                {
                    scaledValues[i] = Map(floatArray[i], inputMin, inputMax, outputMin, outputMax);
                }
                PlotData.Clear();
                StartNICard();
                //_ff.StartGeneratedSignalFromFloatArray(Hw.GetPinAddress(TF_PIN.AO0), scaledValues, AOSampleRate, false);
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
    private void RepeatFile() // Runs waveform repeatedly until stopped
    {
        StartNICard();
        if (WavFileSelected)
        {
            float[] floatArray;
            using (var audioFile = new AudioFileReader(WavFileLocation))
            {
                floatArray = new float[audioFile.Length / sizeof(float)];
                int bytesRead = audioFile.Read(floatArray, 0, floatArray.Length);
                AOSampleRate = audioFile.WaveFormat.SampleRate;
            }
            // Define input range
            float inputMin = floatArray.Min();
            float inputMax = floatArray.Max();

            // Define output range (+5V to -5V)
            float outputMin = -5f;
            float outputMax = 5f;

            // Linearly scale the values
            float[] scaledValues = new float[floatArray.Length];
            for (int i = 0; i < floatArray.Length; i++)
            {
                scaledValues[i] = Map(floatArray[i], inputMin, inputMax, outputMin, outputMax);
            }
            //_ff.StartGeneratedSignalFromFloatArray(Hw.GetPinAddress(TF_PIN.AO0), scaledValues, AOSampleRate, true);
        }
        else
        {
            MessageBox.Show("Please select a file first");
        }
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

    // Useful in debugging, call to export an array
    private static void outputAsCSV(double[] array, string filename)
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
