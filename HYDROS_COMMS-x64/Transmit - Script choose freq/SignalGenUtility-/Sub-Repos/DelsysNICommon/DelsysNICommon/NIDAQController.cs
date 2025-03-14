using NationalInstruments;
using NationalInstruments.DAQmx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using DAQmx = NationalInstruments.DAQmx;
using Task = NationalInstruments.DAQmx.Task;

namespace DelsysNICommon;

public sealed class NIDAQController
{

    public NIDAQ Part = new NIDAQ();

    public Device DaqDevice;
    public string DaqModel;
    public uint CounterCount;

    private Dictionary<string, DAQmx.Task> TaskList = new Dictionary<string, DAQmx.Task>();

    //public static NIDAQController Instance { get; } = new NIDAQController();
    public NIDAQController() { }

    /// <summary>
    /// Connects controller to an NI-DAQ device 
    /// Sets mapping from fully qualified name to pin name (e.g. /Dev1/port0/line0 to P0.0)
    /// </summary>
    public bool Initialize(int deviceType)
    {
        bool found = FindDevice(out DaqDevice, out DaqModel, deviceType);

        if (!found)
            return false;

        Part.InitPinDict(DaqDevice);
        foreach (var pin in Part.DIOPins)
        {
            SetPinsIntoTristate(Part.GetNameByPin(pin));
        }

        return true;
    }

    public bool Initialize(string deviceName)
    {
        try
        {
            DaqDevice = DaqSystem.Local.LoadDevice(deviceName);
            DaqModel = DaqDevice.ProductType;
            if (DaqDevice == null)
                return false;

            Part.InitPinDict(DaqDevice);
            foreach (var pin in Part.DIOPins)
            {
                SetPinsIntoTristate(Part.GetNameByPin(pin));
            }
            return true;
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
            return false;
        }

    }
    private bool FindDevice(out Device device, out string deviceModel, int deviceType)
    {
        string[] devices = DaqSystem.Local.Devices;

        device = null;
        deviceModel = "";

        if (devices.Length == 0) return false;

        foreach (var ni in devices)
        {
            var temp = DaqSystem.Local.LoadDevice(ni);
            if (temp.ProductType.Contains(deviceType.ToString()))
            {
                device = temp;
                deviceModel = device.ProductType;
                return true;
            }
        }

        return false;
    }

    public string[] GetConnectedNIDevices()
    {
        return DaqSystem.Local.Devices;
    }

    /// <summary>
    /// TODO: monitor external task using task events
    /// </summary>
    public void AddExternalTask(DAQmx.Task task, string taskName)
    {
        /*TaskList.Add(taskName, task);

        task.Done += ExternalTaskDone;
        task.DigitalChangeDetection += ExternalTaskChanged;

        string lines = task.DIChannels[0].PhysicalName;
        string lines = task.Stream.ChannelsToWrite
        */


        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a persistent single channel output task for the given digital pins
    /// </summary>
    private DAQmx.Task CreateDOTask(params string[] pinNames)
    {

        List<string> pinIds = pinNames.Select(pin => Part.PinNameToID[pin]).ToList();

        string lineChannels = string.Join(", ", pinIds.ToArray());

        DAQmx.Task doTask = new DAQmx.Task();

        doTask.DOChannels.CreateChannel(lineChannels, "", ChannelLineGrouping.OneChannelForAllLines);

        TaskList.TryAdd(lineChannels, doTask);
        doTask.Control(TaskAction.Verify);
        doTask.Control(TaskAction.Commit);
        doTask.Control(TaskAction.Unreserve);   // so multiple tasks can reserve the same pins

        return doTask;
    }

    CounterSingleChannelReader counterReader;
    public DAQmx.Task CreateCountTask(CICountEdgesActiveEdge edge, CICountEdgesCountDirection countDir, string counterName, string pinName)
    {
        DAQmx.Task doTask = new DAQmx.Task();

        doTask.CIChannels.CreateCountEdgesChannel(DaqDevice.DeviceID + "/" + counterName, "", edge, 0, countDir);
        doTask.CIChannels.All.CountEdgesTerminal = "/" + DaqDevice.DeviceID + "/" + pinName;
        counterReader = new CounterSingleChannelReader(doTask.Stream);

        TaskList.TryAdd(pinName, doTask);
        doTask.Control(TaskAction.Verify);
        doTask.Control(TaskAction.Commit);
        doTask.Control(TaskAction.Unreserve);   // so multiple tasks can reserve the same pins

        return doTask;
    }
    public DAQmx.Task CreateCountFreqTask(CIFrequencyStartingEdge edge, string counterName, string pinName)
    {
        DAQmx.Task doTask = new DAQmx.Task();

        doTask.CIChannels.CreateFrequencyChannel(DaqDevice.DeviceID + "/" + counterName, "", 0, 50, edge, CIFrequencyMeasurementMethod.DynamicAveraging, 0.5, 1, CIFrequencyUnits.Hertz);
        doTask.CIChannels.All.CountEdgesTerminal = "/" + DaqDevice.DeviceID + "/" + pinName;
        counterReader = new CounterSingleChannelReader(doTask.Stream);

        TaskList.TryAdd(pinName, doTask);

        doTask.Control(TaskAction.Verify);
        doTask.Control(TaskAction.Commit);
        doTask.Control(TaskAction.Unreserve);   // so multiple tasks can reserve the same pins

        return doTask;
    }

    public void BeginCountTask(string pinName, string ctr)
    {
        var task = CreateCountTask(CICountEdgesActiveEdge.Rising, CICountEdgesCountDirection.Up, ctr, pinName);
        task.Start();
    }
    public void BeginFreqCountTask(string pinName, string ctr)
    {
        var task = CreateCountFreqTask(CIFrequencyStartingEdge.Rising, ctr, pinName);
        task.Start();
    }

    public void StopCountTask(string pinName, string ctr)
    {
        TaskList.TryGetValue(pinName, out DAQmx.Task doTask);
        doTask?.Stop();
        TaskList.Remove(pinName);
    }
    public UInt32? DigitalCounterRead(string pinName)
    {
        // IT READS HERE
        //bool found = TaskList.TryGetValue(taskName, out DAQmx.Task analogReadTask);
        TaskList.TryGetValue(pinName, out DAQmx.Task countTask);

        if (countTask == null) return null;

        CounterSingleChannelReader counterReader = new CounterSingleChannelReader(countTask.Stream);
        UInt32 data = counterReader.ReadSingleSampleUInt32();
        return data;
    }
    public double DigitalCounterReadFreq(string pinName)
    {
        // IT READS HERE
        //bool found = TaskList.TryGetValue(taskName, out DAQmx.Task analogReadTask);
        TaskList.TryGetValue(pinName, out DAQmx.Task countTask);

        if (countTask == null) return -1.0;

        CounterSingleChannelReader counterReader = new CounterSingleChannelReader(countTask.Stream);
        double data = counterReader.ReadSingleSampleDouble();
        return data;
    }

    public void SetPinsIntoTristate(params string[] pinNames)
    {
        List<string> pinIds = pinNames.Select(pin => Part.PinNameToID[pin]).ToList();

        string lineChannels = string.Join(", ", pinIds.ToArray());

        DAQmx.Task doTask = new DAQmx.Task();

        doTask.DOChannels.CreateChannel(lineChannels, "", ChannelLineGrouping.OneChannelForAllLines);

        TaskList.TryAdd(lineChannels, doTask);

        doTask.DOChannels.All.Tristate = true;

        doTask.Control(TaskAction.Verify);
        doTask.Control(TaskAction.Commit);
        doTask.Control(TaskAction.Unreserve);   // so multiple tasks can reserve the same pins
    }

    private void DaqDigitalWriteSync(bool[] dataArray, string[] pinNames)
    {
        DAQmx.Task digitalWriteTask = CreateDOTask(pinNames);

        DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(digitalWriteTask.Stream);
        writer.WriteSingleSampleMultiLine(true, dataArray);

        digitalWriteTask.WaitUntilDone();
        digitalWriteTask.Stop();
    }

    public void DigitalWrite(bool[] dataArray, params string[] pinNames)
    {
        DaqDigitalWriteSync(dataArray, pinNames);
        UpdateDaqModel(dataArray, pinNames);
    }

    public void DigitalWrite(bool value, params string[] pinNames)
    {
        bool[] dataArray = Enumerable.Repeat(value, pinNames.Length).ToArray();
        DaqDigitalWriteSync(dataArray, pinNames);
        UpdateDaqModel(dataArray, pinNames);
    }

    public void DigitalWrite(int data, params string[] pinNames)
    {
        int width = pinNames.Length;
        string binary = Convert.ToString(data, 2).PadLeft(width, '0').Substring(0, width);
        bool[] dataArray = binary.Select(c => c == '1').ToArray();

        DaqDigitalWriteSync(dataArray, pinNames);
        UpdateDaqModel(dataArray, pinNames);
    }

    internal void DigitalWrite(bool[] dataArray, DigitalSingleChannelWriter writer, params string[] pinNames)
    {
        writer.WriteSingleSampleMultiLine(true, dataArray);
        UpdateDaqModel(dataArray, pinNames);
    }

    public void SetToDefault()
    {
        var DOPins = Part.DIOPins.Where(p => p.PinDirection == Pin.IN_OUT.OUTPUT);

        var defaultHI = DOPins.Where(p => (bool)p.DefaultValue).Select(p => Part.GetNameByPin(p));
        var defaultLO = DOPins.Where(p => !(bool)p.DefaultValue).Select(p => Part.GetNameByPin(p));

        DigitalWrite(true, defaultHI.ToArray());
        DigitalWrite(false, defaultLO.ToArray());
    }

    private DAQmx.Task CreateDITask(params string[] pinNames)
    {

        List<string> pinIds = pinNames.Select(pin => Part.PinNameToID[pin]).ToList();

        string lineChannels = string.Join(", ", pinIds.ToArray());

        DAQmx.Task diTask = new DAQmx.Task();

        diTask.DIChannels.CreateChannel(lineChannels, "", ChannelLineGrouping.OneChannelForAllLines);

        TaskList.TryAdd(lineChannels, diTask);
        diTask.Control(TaskAction.Verify);
        diTask.Control(TaskAction.Commit);
        diTask.Control(TaskAction.Unreserve);

        return diTask;
    }

    public UInt32 DigitalRead(params string[] pinNames)
    {
        DAQmx.Task digitalReadTask = CreateDITask(pinNames);

        DigitalSingleChannelReader reader = new DigitalSingleChannelReader(digitalReadTask.Stream);

        UInt32 data = reader.ReadSingleSamplePortUInt32();

        digitalReadTask.WaitUntilDone();
        digitalReadTask.Stop();

        return data;
    }

    private DAQmx.Task CreateAITask(params string[] pinNames)
    {
        List<string> chIds = pinNames.Select(pin => Part.PinNameToID[pin]).ToList();

        string channels = string.Join(", ", chIds.ToArray());
        string channelName = channels.Replace('/', '-');

        DAQmx.Task aiTask = new DAQmx.Task();

        aiTask.AIChannels.CreateVoltageChannel(channels, "", AITerminalConfiguration.Rse, -10, 10, AIVoltageUnits.Volts);

        aiTask.Stream.ConfigureInputBuffer(1_000_000); // 1M sample buffer

        aiTask.Control(TaskAction.Verify);
        aiTask.Control(TaskAction.Commit);
        aiTask.Control(TaskAction.Unreserve);

        return aiTask;
    }
    private DAQmx.Task CreateAITask(double range, params string[] pinNames)
    {
        try
        {
            List<string> chIds = pinNames.Select(pin => Part.PinNameToID[pin]).ToList();

            string channels = string.Join(", ", chIds.ToArray());
            string channelName = channels.Replace('/', '-');

            DAQmx.Task aiTask = new DAQmx.Task();

            aiTask.AIChannels.CreateVoltageChannel(channels, "", AITerminalConfiguration.Rse, -range, range, AIVoltageUnits.Volts);

            aiTask.Control(TaskAction.Verify);
            aiTask.Control(TaskAction.Commit);
            aiTask.Control(TaskAction.Unreserve);

            return aiTask;

        }
        catch (Exception e)
        {
            Trace.WriteLine($"Error creating AI task : {e}");
            return null;
        }

    }

    public double[] AnalogReadVoltageSync(int samples, string pinName, int SampleRate)
    {
        int duration = samples / SampleRate;
        Task analogReadTask = CreateAITask(pinName);
        analogReadTask.Timing.ConfigureSampleClock("", 1_000_000, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples);
        AnalogSingleChannelReader reader = new AnalogSingleChannelReader(analogReadTask.Stream);

        Stopwatch s = Stopwatch.StartNew();
        analogReadTask.Start();

        List<double> data = new List<double>();

        while (s.Elapsed.TotalSeconds < duration)
        {
            double[] tmp = new double[0];
            try { tmp = reader.ReadMultiSample(-1); }
            catch (DaqException)
            {

            }
            data.AddRange(tmp);
            Thread.Sleep(10);
        }

        analogReadTask.Stop();
        analogReadTask.Control(TaskAction.Unreserve);
        analogReadTask.Dispose();

        return data.ToArray();
    }

    public List<double[]> AnalogReadVoltageSync(int samplesPerChannel, double rate, params string[] pinNames)
    {
        Task analogReadTask = CreateAITask(pinNames);
        AnalogMultiChannelReader reader = new AnalogMultiChannelReader(analogReadTask.Stream);
        analogReadTask.Timing.ConfigureSampleClock("", rate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples);

        //reader.ReadWaveform(samplesPerChannel);
        double[,] data = reader.ReadMultiSample(samplesPerChannel);

        analogReadTask.WaitUntilDone();
        analogReadTask.Dispose();

        return ConvertToList(data);
    }

    DAQmx.Task analogReadTask = null;
    AnalogMultiChannelReader analogReader = null;
    public bool RequestedStop = false;

    public void AnalogBeginReadVoltage(string taskName, string[] channelNames, int samplesPerChannel, double sampleRate, AsyncCallback completeCallback, double range = 10)
    {
        if (analogReadTask == null)
        {
            analogReadTask = CreateAITask(range, channelNames);
            Trace.WriteLine("Task created");
        }

        RequestedStop = false;

        if (!analogReadTask.IsDone) return; // if there is a task still running you cant really do this
        
        try
        {
            analogReadTask.Timing.ConfigureSampleClock("", 1_000_000, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, samplesPerChannel * 2);
            analogReadTask.Control(TaskAction.Verify);

            analogReader = new AnalogMultiChannelReader(analogReadTask.Stream);
            analogReader.SynchronizeCallbacks = true;

            int channels = channelNames.Length;
            double[,] dataBuffer = new double[channels, samplesPerChannel];
            //analogReader.BeginMemoryOptimizedReadMultiSample(samplesPerChannel, completeCallback, null, dataBuffer);

        }
        catch (Exception exp)
        {
            Trace.WriteLine(exp);
        }
    }
    public double[,] AnalogContinueReadVoltage(string taskName, IAsyncResult result, AsyncCallback beginCallback)
    {
        double[,] data = analogReader.EndMemoryOptimizedReadMultiSample(result, out int samplesRead);
        analogReader.BeginMemoryOptimizedReadMultiSample(data.GetLength(1), beginCallback, null, data);
        return data;
    }
    public double[,] AnalogContinueReadVoltageProducer(int samplesToRead)
    {
        try
        {
            double[,] data = analogReader.ReadMultiSample(samplesToRead);
            return data;
        }
        catch(Exception exp)
        {
            Trace.WriteLine("Producer: " + exp);
            return null;
        }

    }
    public void AnalogEndReadVoltage(string taskName)
    {
        try
        {
            RequestedStop = true;
            analogReadTask?.Stop();
            analogReadTask?.Dispose();
            analogReader = null;
            analogReadTask = null;
        }
        catch (Exception e)
        {
            Trace.WriteLine(e);
        }

    }

    public List<double[]> ConvertToList(double[,] data)
    {
        List<double[]> channels = new List<double[]>();

        for (int i = 0; i < data.GetLength(0); i++)
        {
            double[] samples = new double[data.GetLength(1)];

            for (int n = 0; n < data.GetLength(1); n++)
                samples[n] = data[i, n];

            channels.Add(samples);
        }

        return channels;
    }

    public DAQmx.Task CreateAOTask(params string[] pinNames)
    {
        List<string> chIds = pinNames.Select(pin => Part.PinNameToID[pin]).ToList();

        string channels = string.Join(", ", chIds.ToArray());

        DAQmx.Task aoTask = new DAQmx.Task();

        // add parameters
        aoTask.AOChannels.CreateVoltageChannel(channels, "", -5, 5, AOVoltageUnits.Volts);

        aoTask.Control(TaskAction.Unreserve);

        return aoTask;
    }
    public DAQmx.Task CreateAOTask(double min, double max, params string[] pinNames)
    {
        List<string> chIds = pinNames.Select(pin => Part.PinNameToID[pin]).ToList();

        string channels = string.Join(", ", chIds.ToArray());

        DAQmx.Task aoTask = new DAQmx.Task();

        // add parameters
        aoTask.AOChannels.CreateVoltageChannel(channels, "", min, max, AOVoltageUnits.Volts);

        aoTask.Control(TaskAction.Unreserve);

        return aoTask;
    }
    public void AnalogWriteVoltageSync(double[] data, double rate, string pinName)
    {
        Task analogWriteTask = CreateAOTask(0, 5.0, pinName);
        analogWriteTask.Timing.ConfigureSampleClock("", rate, SampleClockActiveEdge.Falling, SampleQuantityMode.FiniteSamples);
        analogWriteTask.Timing.SamplesPerChannel = data.Length;

        AnalogSingleChannelWriter writer = new AnalogSingleChannelWriter(analogWriteTask.Stream);

        writer.WriteMultiSample(false, data);

        analogWriteTask.Control(TaskAction.Verify);
        analogWriteTask.Control(TaskAction.Commit);

        analogWriteTask.Start();

        analogWriteTask.WaitUntilDone();
        analogWriteTask.Dispose();
    }

    public void AnalogWriteVoltage(double volt, string pinName)
    {
        Task analogWriteTask;
        if (pinName.Contains("AO"))
        {
            if (volt < 0)
            {
                analogWriteTask = CreateAOTask(pinName);
            }
            else
            {
                analogWriteTask = CreateAOTask(0, 5.0, pinName);
            }
        }
        else
        {
            throw new ArgumentException("Cannot write to analog input pin");
        }

        AnalogSingleChannelWriter writer = new AnalogSingleChannelWriter(analogWriteTask.Stream);

        writer.WriteSingleSample(true, volt);

        analogWriteTask.Control(TaskAction.Verify);
        analogWriteTask.Control(TaskAction.Commit);

        analogWriteTask.Start();
        analogWriteTask.Dispose();
    }

    public void AnalogBeginWriteVoltage(string taskName, double[] samples, double sampleRate)
    {
        DAQmx.Task aoTask = new DAQmx.Task();
        TaskList.Add(taskName, aoTask);


        aoTask.AOChannels.CreateVoltageChannel($"{DaqDevice.DeviceID}/ao0", "", -10, 10, AOVoltageUnits.Volts);

        aoTask.Timing.ConfigureSampleClock("", sampleRate, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples);
        aoTask.Timing.SamplesPerChannel = samples.Length;

        var aoWriter = new AnalogSingleChannelWriter(aoTask.Stream);

        aoWriter.WriteMultiSample(true, samples);
    }

    public void AnalogEndWriteVoltage(string taskName)
    {
        TaskList.TryGetValue(taskName, out DAQmx.Task analogWriteTask);
        analogWriteTask?.Stop();
        analogWriteTask?.Dispose();
        TaskList.Remove(taskName);
    }

    private void UpdateDaqModel(bool[] data, string[] pinNames)
    {
        var newValues = data.Cast<Object>().ToList();
        Part.UpdatePins(pinNames, newValues);
        //Pin.SaveState();
    }

    public void SerialWriteManual(string clkPinName, string serPinName, bool[] data)
    {

        Task serialTask = CreateDOTask(clkPinName, serPinName);

        DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(serialTask.Stream);
        bool[] lines = new bool[2];

        for (int i = data.Length - 1; i >= 0; i--) // takes the highest bits and puts them in first
        {
            lines[0] = false;
            lines[1] = data[i];
            DigitalWrite(lines, writer, clkPinName, serPinName);
            Thread.Sleep(1);
            lines[0] = true;
            DigitalWrite(lines, writer, clkPinName, serPinName);
        }

        // write back to low
        lines = new bool[2];
        DigitalWrite(lines, writer, clkPinName, serPinName);

        serialTask.Dispose();
    }

    public void SerialWrite(string clkPinName, string serPinName, bool[] data)
    {
        // Buffered write works only for port 0 pins

        if (!clkPinName.Contains("P0") || !serPinName.Contains("P0"))
        {
            SerialWriteManual(clkPinName, serPinName, data);
            return;
        }

        else if (DaqDevice.COSampleClockSupported)
        {

            DAQmx.Task serialTask = new DAQmx.Task();
            DAQmx.Task sampleTask = new DAQmx.Task(); //initial task

            string clkLine = Part.PinNameToID[clkPinName];
            string serLine = Part.PinNameToID[serPinName];


            int clkIndex = 1;
            int serIndex = 0;
            serialTask.DOChannels.CreateChannel(clkLine + ", " + serLine, "", ChannelLineGrouping.OneChannelForAllLines);

            //internal ctr not supported on 6501.... only do this if internal ctr is supported
            double rate = 1000000;
            int holdTime = 3;

            int length = data.Length * holdTime;

            sampleTask.COChannels.CreatePulseChannelFrequency($"/{DaqDevice.DeviceID}/ctr0", "", COPulseFrequencyUnits.Hertz, COPulseIdleState.Low, 0, rate, 0.5);
            sampleTask.Timing.ConfigureImplicit(SampleQuantityMode.ContinuousSamples, 1000);

            string sampleClk = "Ctr0InternalOutput";

            serialTask.Timing.ConfigureSampleClock(sampleClk, rate, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples, length);

            serialTask.Control(TaskAction.Verify);
            serialTask.Control(TaskAction.Commit);
            serialTask.Control(TaskAction.Unreserve);

            DigitalSingleChannelWriter serialWriter = new DigitalSingleChannelWriter(serialTask.Stream);

            DigitalWaveform serialData = new DigitalWaveform(length, 2);

            int n = data.Length - 1;
            int counter = 0;

            for (int i = 0; i < length; i++)
            {
                if (counter >= holdTime)
                {
                    counter = 0;
                    n--;
                }

                bool clk = (counter == (holdTime / 2));
                bool ser = data[n];

                serialData.Samples[i].States[clkIndex] = clk ? DigitalState.ForceDown : DigitalState.ForceUp;
                serialData.Samples[i].States[serIndex] = ser ? DigitalState.ForceUp : DigitalState.ForceDown;


                // Keep model in sync
                UpdateDaqModel(new[] { clk, ser }, new[] { clkPinName, serPinName });

                counter++;
            }

            serialWriter.WriteWaveform(true, serialData);

            sampleTask.Start();


            serialTask.WaitUntilDone();
            serialTask.Dispose();

            sampleTask.Stop();
            sampleTask.Dispose();

            return;
        }

        //need to manually write
        else
        {
            SerialWriteManual(clkPinName, serPinName, data);
            return;
        }
    }

    public void SerialWriteFallingEdge(string clkPinName, string serPinName, bool[] data)
    {
        Task serialTask = CreateDOTask(clkPinName, serPinName);
        DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(serialTask.Stream);
        bool[] lines = new bool[2];

        for (int i = data.Length - 1; i >= 0; i--)
        {
            lines[0] = true;
            lines[1] = data[i];
            DigitalWrite(lines, writer, clkPinName, serPinName);

            lines[0] = false;
            DigitalWrite(lines, writer, clkPinName, serPinName);
        }

        serialTask.Dispose();
    }

    public void SerialWrite(string clkPinName, string serPinName, bool[] data, string ser2PinName, bool[] data2)
    {

        Task serialTask = CreateDOTask(clkPinName, serPinName, ser2PinName);
        DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(serialTask.Stream);
        bool[] lines = new bool[3];


        for (int i = data.Length - 1; i >= 0; i--)
        {
            lines[0] = false;
            lines[1] = data[i];
            lines[2] = data2[i];

            DigitalWrite(lines, writer, clkPinName, serPinName, ser2PinName);

            lines[0] = true;
            DigitalWrite(lines, writer, clkPinName, serPinName, ser2PinName);
        }

        serialTask.Dispose();
    }

    public double ReadOneVoltage(string pin)
    {
        Task myTask = new Task();
        myTask.AIChannels.CreateVoltageChannel(Part.PinNameToID[pin], "", AITerminalConfiguration.Rse, 0, 5, AIVoltageUnits.Volts);
        AnalogSingleChannelReader reader = new AnalogSingleChannelReader(myTask.Stream);

        return reader.ReadMultiSample(100).Average();
    }
    public void StartFunctionFromFloatArray(string pinName, float[] data, double clkRate, bool regenerate, int sine, int carrierFreq, float voltageAmp)
    {
        TaskList.TryGetValue(pinName, out DAQmx.Task doTask);
        if (doTask != null)
            StopGenerateFunction(pinName);

        var chId = Part.PinNameToID[pinName];
        DAQmx.Task aoTask = new DAQmx.Task();
        TaskList.Add(pinName, aoTask);

        // add parameters
        //amp = (amp+dc_offset < 0.26) ? 0.26 : amp;
        //amp = (amp+dc_offset > 4.5) ? 4.5 : amp;
        aoTask.AOChannels.CreateVoltageChannel(chId, "", -10, 10, AOVoltageUnits.Volts);

        if (regenerate)
            aoTask.Stream.WriteRegenerationMode = WriteRegenerationMode.AllowRegeneration; // allows buffer to be regenerated
        else
            aoTask.Stream.WriteRegenerationMode = WriteRegenerationMode.DoNotAllowRegeneration; // prevents buffer from being regenerated

        aoTask.Control(TaskAction.Verify);
        aoTask.Control(TaskAction.Commit);
        aoTask.Control(TaskAction.Unreserve);
        aoTask.Timing.SampleClockRate = clkRate;

        aoTask.Timing.ConfigureSampleClock("",
                                           aoTask.Timing.SampleClockRate,
                                           SampleClockActiveEdge.Rising,
                                           SampleQuantityMode.ContinuousSamples, data.Length);
        

        AnalogSingleChannelWriter writer = new AnalogSingleChannelWriter(aoTask.Stream);

        double[] dblData = new double[data.Length];
        if (sine==1)
        {
            // Sine Wave
            double samplingFrequency = 1000000; // Sampling frequency in Hz
            for (int i = 0; i < data.Length; i++)
            {
                double t = i / samplingFrequency; // Time index
                dblData[i] = voltageAmp * Math.Sin(2 * Math.PI * carrierFreq * t); // Sine wave for row 0
            }
        }
        else if (sine == 0)
        {
            // write data to buffer
            dblData = Array.ConvertAll(data, x => (double)x);
        }

        writer.WriteMultiSample(false,dblData);
        aoTask.Start();
    }

    public void StartFunctionFromFloatArrayTwo(string pinName, string pinName2, float[] data, float[] data2, double clkRate, bool regenerate, int sine, int carrierFreq, int carrierFreq2, float voltageAmp, float voltageAmp2)
    {
        TaskList.TryGetValue(pinName, out DAQmx.Task doTask);
        if (doTask != null)
            StopGenerateFunction(pinName);

        var chId = Part.PinNameToID[pinName];
        DAQmx.Task aoTask = new DAQmx.Task();
        TaskList.Add(pinName, aoTask);

        // add parameters
        //amp = (amp+dc_offset < 0.26) ? 0.26 : amp;
        //amp = (amp+dc_offset > 4.5) ? 4.5 : amp;
        var foo = chId + "," + Part.PinNameToID[pinName2];
        aoTask.AOChannels.CreateVoltageChannel(foo, "", -10, 10, AOVoltageUnits.Volts);

        if (regenerate)
            aoTask.Stream.WriteRegenerationMode = WriteRegenerationMode.AllowRegeneration; // allows buffer to be regenerated
        else
            aoTask.Stream.WriteRegenerationMode = WriteRegenerationMode.DoNotAllowRegeneration; // prevents buffer from being regenerated

        aoTask.Control(TaskAction.Verify);
        aoTask.Control(TaskAction.Commit);
        aoTask.Control(TaskAction.Unreserve);
        aoTask.Timing.SampleClockRate = clkRate;

        aoTask.Timing.ConfigureSampleClock("",
                                           aoTask.Timing.SampleClockRate,
                                           SampleClockActiveEdge.Rising,
                                           SampleQuantityMode.ContinuousSamples, data.Length);


        AnalogMultiChannelWriter writer =
         new AnalogMultiChannelWriter(aoTask.Stream);

        //write data to buffer
        double[] dblData = Array.ConvertAll(data, x => (double)x);
        double[] dblData2 = Array.ConvertAll(data2, x => (double)x);
        var newData = ConvertTo2DArray(dblData, dblData2, 2, dblData.Length - 1, sine, carrierFreq, carrierFreq2, voltageAmp, voltageAmp2);
        writer.WriteMultiSample(false, newData);
        aoTask.Start();
    }
    public double[,] ConvertTo2DArray(double[] data, double[] data2, int rows, int cols, int sine, int carrierFreq, int carrierFreq2, float voltageAmp, float voltageAmp2)
    {
        double[,] result = new double[rows, data.Length];
        if (sine == 3)
        {
            double samplingFrequency = 1000000; // Sampling frequency in Hz
            for (int i = 0; i < cols; i++)
            {
                double t = i / samplingFrequency; // Time index
                result[0, i] = voltageAmp * Math.Sin(2 * Math.PI * carrierFreq * t); // Sine wave for row 0
                result[1, i] = voltageAmp2 * Math.Sin(2 * Math.PI * carrierFreq2 * t); // Sine wave for row 1
            }
        }
        if (sine == 2)
        {
            double samplingFrequency = 1000000; // Sampling frequency in Hz
            for (int i = 0; i < cols; i++)
            {
                double t = i / samplingFrequency; // Time index
                result[0, i] = data[i];
                result[1, i] = voltageAmp2 * Math.Sin(2 * Math.PI * carrierFreq2 * t); // Sine wave for row 1
            }
        }
        if (sine == 1)
        {
            double samplingFrequency = 1000000; // Sampling frequency in Hz
            for (int i = 0; i < cols; i++)
            {
                double t = i / samplingFrequency; // Time index
                result[0, i] = voltageAmp2 * Math.Sin(2 * Math.PI * carrierFreq2 * t);
                result[1, i] = data2[i];
            }
        }
        else if (sine==0)
        {
            for (int i = 0; i < data.Length - 1; i++)
            {
                result[0, i] = data[i];
                result[1, i] = data2[i];
            }
        }
        return result;
    }

    public void SaveArrayToCsv(double[] data, string filePath)
    {
        var csvContent = string.Join(",", data.Select(d => d.ToString()));
        System.IO.File.WriteAllText(filePath, csvContent);
    }



    public void StartGenerateFunction(string pinName, WaveformType type, double freq, double amp, double clkRate, int samplesPerBuffer, double dc_offset = 0.0)
    {
        TaskList.TryGetValue(pinName, out DAQmx.Task doTask);
        if (doTask != null)
            StopGenerateFunction(pinName);

        var chId = Part.PinNameToID[pinName];
        DAQmx.Task aoTask = new DAQmx.Task();
        TaskList.Add(pinName, aoTask);

        // add parameters
        //amp = (amp+dc_offset < 0.26) ? 0.26 : amp;
        //amp = (amp+dc_offset > 4.5) ? 4.5 : amp;
        aoTask.AOChannels.CreateVoltageChannel(chId, "", -5, 5, AOVoltageUnits.Volts);
        aoTask.Control(TaskAction.Verify);
        aoTask.Control(TaskAction.Commit);
        aoTask.Control(TaskAction.Unreserve);
        double[] _data = { };
        switch (type)
        {
            case WaveformType.SineWave:
                _data = FunctionGenerator.GenerateSineWave(freq, amp, clkRate, dc_offset);
                break;
            case WaveformType.SAWTOOTH:
                _data = FunctionGenerator.GenerateSawWave(freq, amp, clkRate, samplesPerBuffer, dc_offset);
                break;
            case WaveformType.SQUARE:
                _data = FunctionGenerator.GenerateSquareWave(freq, amp, clkRate, samplesPerBuffer, dc_offset);
                break;
            case WaveformType.TRIANGLE:
                _data = FunctionGenerator.GenerateTriangleWaveform(freq, amp, clkRate, samplesPerBuffer, dc_offset);
                break;
            default:
                break;
        }
        aoTask.Timing.SampleClockRate = clkRate;
        aoTask.Timing.ConfigureSampleClock("",
                                           aoTask.Timing.SampleClockRate,
                                           SampleClockActiveEdge.Rising,
                                           SampleQuantityMode.ContinuousSamples, _data.Length);

        AnalogSingleChannelWriter writer =
         new AnalogSingleChannelWriter(aoTask.Stream);

        //write data to buffer
        writer.WriteMultiSample(false, _data);

        aoTask.Start();
    }

    public void StopGenerateFunction(string pinName)
    {
        try
        {
            if (TaskList.TryGetValue(pinName, out DAQmx.Task doTask))
            {
                doTask.Dispose();
                TaskList.Remove(pinName);
            }
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Error stopping function generator: {e}");
        }


    }

    public void DestroyAllTasks()
    {
        foreach (var task in TaskList)
        {
            task.Value.Control(TaskAction.Unreserve);

        }
    }

    public void StartSineSweep(string pinName, double amp, double startFreq, double stopFreq, double step, double clkRate)
    {
        TaskList.TryGetValue(pinName, out DAQmx.Task doTask);
        if (doTask != null)
            StopGenerateFunction(pinName);

        var chId = Part.PinNameToID[pinName];
        DAQmx.Task aoTask = new DAQmx.Task();
        TaskList.Add(pinName, aoTask);

        // add parameters
        //amp = (amp+dc_offset < 0.26) ? 0.26 : amp;
        //amp = (amp+dc_offset > 4.5) ? 4.5 : amp;
        aoTask.AOChannels.CreateVoltageChannel(chId, "", -5, 5, AOVoltageUnits.Volts);
        aoTask.Control(TaskAction.Verify);
        aoTask.Control(TaskAction.Commit);
        aoTask.Control(TaskAction.Unreserve);
        double[] _data = FunctionGenerator.GenerateSweep(amp, startFreq, stopFreq, step);
        aoTask.Timing.SampleClockRate = clkRate;
        aoTask.Timing.ConfigureSampleClock("",
                                           aoTask.Timing.SampleClockRate,
                                           SampleClockActiveEdge.Rising,
                                           SampleQuantityMode.ContinuousSamples, _data.Length);

        AnalogSingleChannelWriter writer =
         new AnalogSingleChannelWriter(aoTask.Stream);

        //write data to buffer
        writer.WriteMultiSample(false, _data);

        aoTask.Start();
    }
}