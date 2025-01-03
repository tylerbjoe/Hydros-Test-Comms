using DelsysNICommon.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DelsysNICommon.FixtureFramework
{
    /// <summary>
    /// Initializes connection to base station tester. 
    /// Controls switches on base station tester.
    /// </summary>
    public class BSTFramework
    {
        public static BSTFramework Instance { get; } = new BSTFramework(); 
        public static bool FixtureConnected => Daq != null;
        public static int PizzaID = -1;

        //Pizza Lines: 
        private const string SER_CLK = "P0.0";      // Clock for DIO control/programmable frequency
        private const string SER_DATA_A = "P0.1";   // Serial Data input for analog frequency control
        private const string CLEAR = "P0.2";        // clear for shift register (DIO ctrl)
        private const string HIZ = "P0.3";          // output enable DIO ctrl
        private const string LATCH_EN1 = "P0.4";    // storage register for clock input on DIO ctrl (Main)
        private const string LATCH_EN2 = "P0.5";    // storage register for clock input (Main)
        private const string SER_DATA_B = "P0.6";   // Serial Data input for loading Avanti Specific Shift Registers
        private const string SER_DATA_C = "P0.7";   // Serial Data input for loading the Avanti Select (Latch) Double Shift Register
        private const string PROG_F_EN = "P2.3";
        private const string F_EN4 = "P2.4";
        private const string F_EN3 = "P2.5";
        private const string F_EN2 = "P2.6";
        private const string F_EN1 = "P2.7";
        private static string[] PIZZA_ID_PINS = { "P2.0", "P1.7", "P1.6", "P1.5" };

        //analog input reading 
        //TO DO: Expand section for reading NI channels
        private double INPUT_SAMPLE_RATE = 2000;
        private int INPUT_BUFFER_SIZE = 500;
        private static int AI_CHANNELS_TOTAL = 80; //default 80
        public double[,] SamplesBuffer;
        private static string[] CHANNELS = new string[AI_CHANNELS_TOTAL];

        public event EventHandler<AnalogDataEventArgs> AnalogData;

        //Hardware switches that can be flipped on the pizza tester.
        public enum HW_SWITCH
        {
            ADC0, ADC1, ADC2, ADC3, HALL, PG, PWR
        }

        //parts connected to 6255
        public static NIDAQController Daq;
        private static MM74HC595MTC selectSr1;
        private static MM74HC595MTC selectSr2;
        private static MM74HC595MTC freqSr;
        private static List<MM74HC595MTC> ctrlSrs;

        public BSTFramework() { }

        /// <summary>
        /// Initalizes the daq and serial lines for the test fixture. Can only be done once.
        /// </summary>
        /// <returns></returns>
        public static bool InitializeFixture()
        {
            Console.WriteLine("Initialize NI...");
            Daq = new NIDAQController();// NIDAQController.Instance;

            bool daqFound = Daq.Initialize(6501);
            if (!daqFound)
            {
                daqFound = Daq.Initialize(6255);
                if (!daqFound)
                {
                    Console.WriteLine("Connection to NI was not found");
                    Daq = null;
                    return false;
                }
            }
            Console.WriteLine($"Connected to NI {Daq.DaqModel}");

            selectSr1 = new MM74HC595MTC();      // avanti select 1
            selectSr2 = new MM74HC595MTC();      // avanti select 2
            freqSr = new MM74HC595MTC();      // freq control

            Daq.Part.GetPin("P0.0").ConnectPins(freqSr.Clk, selectSr1.Clk, selectSr2.Clk);
            Daq.Part.GetPin("P0.1").ConnectPins(freqSr.SerIn);
            Daq.Part.GetPin("P0.2").ConnectPins(selectSr1.Clear, selectSr2.Clear);
            Daq.Part.GetPin("P0.3").ConnectPins(freqSr.Hiz, selectSr1.Hiz, selectSr2.Hiz);
            Daq.Part.GetPin("P0.4").ConnectPins(selectSr1.LatchEn);
            Daq.Part.GetPin("P0.5").ConnectPins(selectSr2.LatchEn);
            Daq.Part.GetPin("P0.7").ConnectPins(selectSr1.SerIn);

            selectSr1.SerOut.ConnectPins(selectSr2.SerIn);

            ctrlSrs = new List<MM74HC595MTC>();

            for (int i = 0; i < 16; i++)
            {
                var sr = new MM74HC595MTC();
                sr.Label = i.ToString();

                Daq.Part.GetPin("P0.0").ConnectPins(sr.Clk);
                Daq.Part.GetPin("P0.2").ConnectPins(sr.Clear);
                Daq.Part.GetPin("P0.3").ConnectPins(sr.Hiz);
                Daq.Part.GetPin("P0.6").ConnectPins(sr.SerIn);

                if (i < 8) selectSr1.OutputPins[i].ConnectPins(sr.LatchEn);
                if (i >= 8) selectSr2.OutputPins[i - 8].ConnectPins(sr.LatchEn);

                ctrlSrs.Add(sr);
            }

            if (Daq.DaqModel.Contains("6255"))
            {
                for (int q = 0; q < AI_CHANNELS_TOTAL; q++)
                {
                    CHANNELS[q] = $"AI{q}";
                }
                Console.WriteLine($"Enabled {AI_CHANNELS_TOTAL} Analog Channels");
            }

            SetPizzaID();
            return true;
        }

        /// <summary>
        /// Resets the entire test fixture to defaults.
        /// </summary>
        public static void Reset()
        {
            Daq.DigitalWrite(true, CLEAR);
            Daq.DigitalWrite(false, LATCH_EN1, LATCH_EN2, HIZ);

            int[] selectAll = Enumerable.Range(0, 16).ToArray();

            SetSwitchExclusive(HW_SWITCH.PG, true, selectAll);

            Thread.Sleep(2000); // Hold

            SetSwitchExclusive(HW_SWITCH.PG, false, selectAll);

            Daq.DigitalWrite(true, CLEAR);

            Daq.DigitalWrite(false, F_EN4, F_EN3, F_EN2, F_EN1);
            Daq.SerialWrite(SER_CLK, SER_DATA_A, Enumerable.Repeat(false, 16).ToArray());
            Daq.DigitalWrite(true, F_EN4, F_EN3, F_EN2, F_EN1);

            //toggle and clear
            CaptureSelect();
            Daq.DigitalWrite(true, CLEAR);

        }


        /// <summary>
        /// Powers on all sensors in the list.
        /// </summary>
        /// <param name="sensorNumbers"></param>
        public static void PowerOnSensors(List<int> sensorNumbers)
        {
            PowerOnSensors(sensorNumbers.ToArray());
        }

        /// <summary>
        /// Powers on all sensors.
        /// </summary>
        public static void PowerOnAllSensors()
        {
            int[] sensorGroup1 = Enumerable.Range(0, 8).ToArray();
            int[] sensorGroup2 = Enumerable.Range(8, 8).ToArray();

            PowerOnSensors(sensorGroup1);
            PowerOnSensors(sensorGroup2);
        }

        /// <summary>
        /// Powers off all sensors in the list.
        /// </summary>
        /// <param name="sensorNumbers"></param>
        public static void PowerOffSensors(List<int> sensorNumbers)
        {
            ToggleSwitch(HW_SWITCH.PG, 2000, [.. sensorNumbers]);
        }

        /// <summary>
        /// Powers off all sensors.
        /// </summary>
        public static void PowerOffAllSensors()
        {
            int[] sensorGroup = Enumerable.Range(0, 16).ToArray();
            ToggleSwitch(HW_SWITCH.PG, 2000, sensorGroup);
        }


        /// <summary>
        /// Sets mode for sensor while keeping all old modes the same.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="newVal"></param>
        /// <param name="sensorInd"></param>
        public static void SetSwitch(HW_SWITCH mode, bool newVal, params int[] sensorInd) //literally idk what this does, maybe delete
        {
            foreach (int index in sensorInd)
            {
                bool[] mask8 = ctrlSrs[index].State.ToArray();
                mask8[(int)mode] = newVal;

                SetSwitchMask(mask8, sensorInd);
            }
        }

        /// <summary>
        /// Sets a specific mode for each sensor, overwrites any old mode it is in.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="newVal"></param>
        /// <param name="sensorInd"></param>
        public static void SetSwitchExclusive(HW_SWITCH mode, bool newVal, params int[] sensorInd)
        {
            bool[] mask8 = new bool[8];
            mask8[(int)mode] = newVal; //what is the purpose of this

            SetSwitchMask(mask8, sensorInd);
        }

        /// <summary>
        /// Toggles a specific mode on/off. 
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="delayMs"></param>
        /// <param name="sensorInd"></param>
        public static void ToggleSwitch(HW_SWITCH mode, int delayMs, params int[] sensorInd)
        {
            SetSwitch(mode, true, sensorInd);

            Thread.Sleep(delayMs); // Hold

            SetSwitch(mode, false, sensorInd);
        }

        /// <summary>
        /// Enables or disabled the analog override.
        /// </summary>
        /// <param name="enable"></param>
        public static void SetAnalogOverride(bool enable)
        {
            Daq.DigitalWrite(enable, PROG_F_EN);
        }

        /// <summary>
        /// Set frequency for specified analog channel. 
        /// Will automatically enable the analog override in order to set frequency.
        /// </summary>
        /// <param name="channelNumber"></param>
        /// <param name="frequency"></param>
        public static void SetAnalogFrequency(int channelNumber, int frequency)
        {
            string fen = F_EN1;
            if (channelNumber == 1) fen = F_EN1;
            if (channelNumber == 2) fen = F_EN2;
            if (channelNumber == 3) fen = F_EN3;
            if (channelNumber == 4) fen = F_EN4;
            Daq.DigitalWrite(true, PROG_F_EN);
            SetSignalFrequency(frequency, fen);
        }

        private static void SetSignalFrequency(int frequency, string F_EN)
        {
            int freq = frequency * 2048;

            Daq.DigitalWrite(true, PROG_F_EN, CLEAR);

            bool[] OCT = new bool[4];
            bool[] DAC = new bool[10];
            bool[] CNF = new bool[2];

            int OCT_int = (int)(3.322 * Math.Log10(freq / 1039));
            string OCTstring = Convert.ToString(OCT_int, 2);

            OCT = OCTstring.Select(s => s.Equals('1')).ToArray();
            Array.Reverse(OCT);
            Array.Resize(ref OCT, 4);
            Array.Reverse(OCT);

            int DAC_int = (int)(2048 - (2078 * Math.Pow(2, 10 + OCT_int) / freq));
            DAC = Convert.ToString(DAC_int, 2).Select(s => s.Equals('1')).ToArray();
            Array.Reverse(DAC);
            Array.Resize(ref DAC, 10);
            Array.Reverse(DAC);

            CNF[0] = false;
            CNF[1] = false;
            Array.Reverse(CNF);

            bool[] D = new bool[16];
            Array.Copy(OCT, D, 4);
            Array.Copy(DAC, 0, D, 4, 10);
            Array.Copy(CNF, 0, D, 14, 2);

            Daq.DigitalWrite(false, LATCH_EN1, LATCH_EN2, SER_DATA_B, F_EN);
            Thread.Sleep(1000);

            Array.Reverse(D);
            Daq.SerialWrite(SER_CLK, SER_DATA_A, D);

            Daq.DigitalWrite(true, F_EN);
            Thread.Sleep(1000);
        }


        private static void SetSwitchMask(bool[] mask8, params int[] sensorInd)
        {
            Daq.DigitalWrite(false, CLEAR);
            Daq.DigitalWrite(true, CLEAR);
            Daq.DigitalWrite(false, HIZ, LATCH_EN1, LATCH_EN2);

            DeselectAll();

            // Select sensor
            bool[] ser16 = new bool[16];
            Array.ForEach(sensorInd, i => ser16[i] = true);

            // Set mode
            bool[] mode16 = new bool[16];
            Array.Copy(mask8, mode16, 7);

            // serial write same time
            Daq.SerialWrite(SER_CLK, SER_DATA_C, ser16, SER_DATA_B, mode16);
            CaptureSelect();
        }

        private static void PowerOnSensors(params int[] sensorInd)
        {
            ToggleSwitch(HW_SWITCH.PG, 2000, sensorInd);
            //Thread.Sleep(2000);
            ToggleSwitch(HW_SWITCH.HALL, 2000, sensorInd);
        }

        private static void CaptureSelect()
        {
            Daq.DigitalWrite(true, LATCH_EN1, LATCH_EN2);
            Thread.Sleep(100);
            Daq.DigitalWrite(false, LATCH_EN1, LATCH_EN2);
        }

        private static void DeselectAll()
        {
            Daq.DigitalWrite(false, CLEAR);
            CaptureSelect();
            Daq.DigitalWrite(true, CLEAR);
        }

        private static void SetPizzaID()
        {
            PizzaID = 0;

            for (int i = 0; i < 4; i++)
            {
                byte x = (byte)Daq.DigitalRead(PIZZA_ID_PINS[i]);

                PizzaID |= x > 0 ? 1 : 0;
                if (i < 3) PizzaID <<= 1;
            }
        }

        private static void PrintState()
        {
            foreach (var sr in ctrlSrs)
            {
                Console.WriteLine(sr.State + ": " + String.Join(",", sr.GetOutputs().Select(b => b ? 1 : 0)));
            }
            Console.WriteLine("---------------------");
        }

        //TO DO: keep track of device state
        private static void UpdateDeviceState(bool[] avantiBitmask, bool[] componentBitmask, LinkedList<LinkedList<bool>> deviceState, out LinkedList<LinkedList<bool>> new_DeviceState)
        {
            LinkedList<LinkedList<bool>> newDeviceState = new LinkedList<LinkedList<bool>>();
            for (int i = 0; i < 16; ++i)
            {
                if (avantiBitmask[i])
                {
                    LinkedList<bool> newAvantiState = new LinkedList<bool>();
                    for (int j = 0; j < 7; ++j)
                    {
                        newAvantiState.AddLast(componentBitmask[j]);
                    }
                    newDeviceState.AddLast(newAvantiState);
                }
                else
                {
                    LinkedList<bool> newAvantiState = new LinkedList<bool>();
                    for (int j = 0; j < 7; ++j)
                    {
                        newAvantiState.AddLast(deviceState.ElementAt(i).ElementAt(j));
                    }
                    newDeviceState.AddLast(newAvantiState);
                }
            }
            new_DeviceState = newDeviceState;
        }


        #region NI analog input read (TO DO)

        public void startAnalogRead()
        {
            SamplesBuffer = new double[1, INPUT_BUFFER_SIZE];
            AsyncCallback taskCallback = new AsyncCallback(OnInputData);
            Daq.AnalogBeginReadVoltage("MonitorTask", CHANNELS, INPUT_BUFFER_SIZE, INPUT_SAMPLE_RATE, taskCallback);
        }

        public void OnInputData(IAsyncResult result)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                AsyncCallback taskCallback = new AsyncCallback(OnInputData);
                SamplesBuffer = Daq.AnalogContinueReadVoltage("MonitorTask", result, taskCallback);

                if (SamplesBuffer == null) return;

                AnalogData?.Invoke(this, new AnalogDataEventArgs(1, SamplesBuffer, INPUT_SAMPLE_RATE, INPUT_BUFFER_SIZE));
            });
        }

        public void stopAnalogRead()
        {
            Daq.AnalogEndReadVoltage("MonitorTask");
        }

        //read in all pins sync based on total samples
        public List<double[]> readAnalogAllPinsSync(int samplePerChannel)
        {
            List<double[]> data = Daq.AnalogReadVoltageSync(samplePerChannel, INPUT_SAMPLE_RATE, CHANNELS);
            return data;
        }

        #endregion 

    }
}
