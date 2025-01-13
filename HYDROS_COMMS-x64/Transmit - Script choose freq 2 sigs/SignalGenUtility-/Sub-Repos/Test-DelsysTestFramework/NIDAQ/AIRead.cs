using DelsysNICommon;
using DelsysTestLib.NIDAQ;
using DelsysTestLib.Util;
using System.Collections.Concurrent;


namespace DelsysTestFramework.NIDAQ
{
    public class AIRead
    {
        NIDAQController DAQ => FixtureFramework.Instance.DAQ;

        public int SAMPLE_RATE { get; set; } = 50_000;
        public int INPUT_BUFFER { get; set; } = 10_000;
        private string[] CHANNELS;
        public int[] CHANNELS_NUMBER;
        public double[,] samplesBuffer;

        public event EventHandler<GotInputDataEventArgs>? GotInputData;


        public AIRead()
        {
            samplesBuffer = new double[1, INPUT_BUFFER];
            CHANNELS = new string[] { "" };
            CHANNELS_NUMBER = new int[] { 0 };
        }
        public double ReadVoltageAI(string pin)
        {
            double[] voltage_Dut_V = DAQ.AnalogReadVoltageSync(INPUT_BUFFER, pin, SAMPLE_RATE); // samples in 0.05 seconds
            return voltage_Dut_V.Average();
        }
        public double TakeSampleAI(string pin)
        {
            return DAQ.ReadOneVoltage(pin);
        }
        public bool ReadAnalogInputAsDigital(string pin)
        {
            return TakeSampleAI(pin) > 2.5;
        }
        public string[] GetAIPinNames()
        {
            var states = new List<string>();
            foreach (var pin in DAQ.Part.AIPins)
            {
                states.Add(DAQ.Part.GetNameByPin(pin));
            }
            return states.ToArray();
        }
        public void StopAnalogRead()
        {
            DAQ.AnalogEndReadVoltage(currentAnalogTaskName);
            currentAnalogTaskName = "";
        }
        public void StopAnalogRead(double range)
        {
            DAQ.AnalogEndReadVoltage(currentAnalogTaskName);
            currentAnalogTaskName = "";
        }


        private string currentAnalogTaskName = "";
        private AsyncCallback analogCallback;
        public void StartAnalogRead(string[] CHANNELS, string taskName = "MonitorTask10")
        {
            this.CHANNELS = CHANNELS;
            for (int i = 0; i < CHANNELS.Length; i++)
            {
                CHANNELS_NUMBER[i] = int.Parse(CHANNELS[i].Substring(2));
            }

            samplesBuffer = new double[1, INPUT_BUFFER];
            analogCallback = new AsyncCallback(OnInputData);


            DAQ.AnalogBeginReadVoltage(taskName, CHANNELS, INPUT_BUFFER, SAMPLE_RATE, analogCallback);
            currentAnalogTaskName = taskName;
        }

        public void StartAnalogRead(double range, string[] CHANNELS, string taskName = "MonitorTask")
        {
            if (currentAnalogTaskName != "")
                DAQ.AnalogEndReadVoltage(currentAnalogTaskName);

            this.CHANNELS = CHANNELS;
            samplesBuffer = new double[1, INPUT_BUFFER];
            analogCallback = new AsyncCallback(OnInputData);
            currentAnalogTaskName = $"{taskName}{range}";
            DAQ.AnalogBeginReadVoltage($"{taskName}{range}", CHANNELS, INPUT_BUFFER, SAMPLE_RATE, analogCallback, range);
            Producer();
        }
        public void StartPsigAnalogRead(double range)
        {
            if (currentAnalogTaskName != "")
                DAQ.AnalogEndReadVoltage(currentAnalogTaskName);

            samplesBuffer = new double[1, INPUT_BUFFER];
            analogCallback = new AsyncCallback(OnInputData);
            currentAnalogTaskName = $"Psig{range}";
            this.CHANNELS = new string[] { "AI30", "AI31" };
            DAQ.AnalogBeginReadVoltage($"Psig{range}", CHANNELS, INPUT_BUFFER, SAMPLE_RATE, analogCallback, range);
        }

        public bool NiFailed = false;
        public BlockingCollection<double[,]> DataBuffer = new BlockingCollection<double[,]>();
        public object LockQueue = new object();
        public void OnInputData(IAsyncResult result)
        {
            Task.Run(() =>
            {
                try
                {
                    if (DAQ.RequestedStop) // if none are running then don't continue
                        return;

                    samplesBuffer = DAQ.AnalogContinueReadVoltage(currentAnalogTaskName, result, analogCallback);
                    DataBuffer.TryAdd(samplesBuffer, 100);
                    //GotInputData?.Invoke(this, new GotInputDataEventArgs(CHANNELS_NUMBER[0], samplesBuffer, SAMPLE_RATE, INPUT_BUFFER));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("OnInputData:" + ex);
                }
            });
        }
        
        public void Producer()
        {
            Task.Run(() =>
            {
                while (!DAQ.RequestedStop)
                {
                    double[,] data = DAQ.AnalogContinueReadVoltageProducer(INPUT_BUFFER);
                    DataBuffer.TryAdd(data, 100);
                }
            });
        }
    }
}
