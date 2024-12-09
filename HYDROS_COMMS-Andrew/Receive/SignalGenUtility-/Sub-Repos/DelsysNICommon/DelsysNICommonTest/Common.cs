using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NationalInstruments;
using System.Threading;
using NationalInstruments.DAQmx;
using DelsysNICommon;
using NationalInstruments.Restricted;

namespace PizzaTester
{
    public class PizzaTesterFramework
    {

        public const string SER_CLK = "P0.0";      // Clock for DIO control/programmable frequency
        public const string SER_DATA_A = "P0.1";   // Serial Data input for analog frequency control
        public const string CLEAR = "P0.2";        // clear for shift register (DIO ctrl)
        public const string HIZ = "P0.3";          // output enable DIO ctrl
        public const string LATCH_EN1 = "P0.4";    // storage register for clock input on DIO ctrl (Main)
        public const string LATCH_EN2 = "P0.5";    // storage register for clock input (Main)
        public const string SER_DATA_B = "P0.6";   // Serial Data input for loading Avanti Specific Shift Registers
        public const string SER_DATA_C = "P0.7";   // Serial Data input for loading the Avanti Select (Latch) Double Shift Register

        public const string PROG_F_EN = "P2.3";
        public const string F_EN4 = "P2.4";
        public const string F_EN3 = "P2.5";
        public const string F_EN2 = "P2.6";
        public const string F_EN1 = "P2.7";

        enum MODE
        {
            ADC0 = 0, ADC1, ADC2, ADC3, HALL, PG, PWR
        }



        static NIDAQController Daq;
        static MM74HC595MTC selectSr1;
        static MM74HC595MTC selectSr2;
        static MM74HC595MTC freqSr;

        static List<MM74HC595MTC> ctrlSrs;

        static void ModelInitialize()
        {

            Daq = NIDAQController.Instance;
            Daq.Initialize();



            selectSr1 = new MM74HC595MTC();      // avanti select 1
            selectSr2 = new MM74HC595MTC();      // avanti select 2

            freqSr = new MM74HC595MTC();      // freq control

            Daq.Part.GetPin("P0.0").ConnectPins(freqSr.Clk, selectSr1.Clk, selectSr2.Clk);
            Daq.Part.GetPin("P0.1").ConnectPins(freqSr.SerIn);
            Daq.Part.GetPin("P0.2").ConnectPins(selectSr1.Clear, selectSr2.Clear);
            Daq.Part.GetPin("P0.3").ConnectPins(freqSr.Hiz, selectSr1.Hiz, selectSr2.Hiz);
            Daq.Part.GetPin("P0.4").ConnectPins(selectSr1.LatchEn);
            Daq.Part.GetPin("P0.5").ConnectPins(selectSr2.LatchEn);
            //Daq.Part.GetPin("P0.6").ConnectPins(ctrlSr.SerIn);
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


        }

/*        static void Main(string[] args)
        {
            ModelInitialize();
            ResetTesterState();

            //SetMode(MODE.PG, true, 0);
            PrintState();
        }*/



        static void SetSignalFrequency(int frequency, string F_EN)
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



        static void SetModeMask(bool[] mask8, params int[] sensorInd)
        {
            Daq.DigitalWrite(true, CLEAR);
            Daq.DigitalWrite(false, HIZ, LATCH_EN1, LATCH_EN2);


            DeselectAll();

            // Select sensor
            bool[] ser16 = new bool[16];
            sensorInd.SafeForEach(i => ser16[i] = true);

            // Set mode
            bool[] mode16 = new bool[16];
            Array.Copy(mask8, mode16, 8);

            // serial write same time
            Daq.SerialWrite(SER_CLK, SER_DATA_C, ser16, SER_DATA_B, mode16);
            CaptureSelect();
        }

        static void SetModeOverwrite(MODE mode, bool newVal, params int[] sensorInd)
        {
            bool[] mask8 = new bool[8];
            mask8[(int)mode] = newVal;

            SetModeMask(mask8, sensorInd);
        }


        static void SetMode(MODE mode, bool newVal, params int[] sensorInd)
        {

            foreach (int index in sensorInd)
            {
                bool[] mask8 = ctrlSrs[index].State.ToArray();
                mask8[(int)mode] = newVal;

                SetModeMask(mask8, sensorInd);
            }
        }


        static void OnOffMode(MODE mode, int delayMs, params int[] sensorInd)
        {
            SetModeOverwrite(mode, true, sensorInd);

            Thread.Sleep(delayMs); // Hold

            SetModeOverwrite(mode, false, sensorInd);
        }

        static void PowerCycleSensors(params int[] sensorInd)
        {

            OnOffMode(MODE.PG, 2000, sensorInd);
            OnOffMode(MODE.HALL, 2000, sensorInd);

        }

        static void PowerCycleAllSensors()
        {
            int[] sensorGroup1 = Enumerable.Range(0, 8).ToArray();
            int[] sensorGroup2 = Enumerable.Range(8, 8).ToArray();

            PowerCycleSensors(sensorGroup1);
            PowerCycleSensors(sensorGroup2);
        }

        static void CaptureSelect()
        {
            Daq.DigitalWrite(true, LATCH_EN1, LATCH_EN2);
            Thread.Sleep(3);
            Daq.DigitalWrite(false, LATCH_EN1, LATCH_EN2);
        }

        static void DeselectAll()
        {
            Daq.DigitalWrite(false, CLEAR);
            CaptureSelect();
            Daq.DigitalWrite(true, CLEAR);
        }

        static void ResetTesterState()
        {
            Daq.DigitalWrite(true, CLEAR);
            Daq.DigitalWrite(false, LATCH_EN1, LATCH_EN2, HIZ);

            int[] selectAll = Enumerable.Range(0, 16).ToArray();
            OnOffMode(MODE.PG, 2000, selectAll);


            Daq.DigitalWrite(false, F_EN4, F_EN3, F_EN2, F_EN1);
            Daq.SerialWrite(SER_CLK, SER_DATA_A, Enumerable.Repeat(false, 16).ToArray());
            Daq.DigitalWrite(true, F_EN4, F_EN3, F_EN2, F_EN1);
        }


        static void PrintState()
        {
            foreach (var sr in ctrlSrs)
            {
                Console.WriteLine(sr.Label + ": " + String.Join(",", sr.GetOutputs().Select(b => b ? 1 : 0)));
            }
            Console.WriteLine("---------------------");
        }



        // Pizza tester interface

        public static void InitializeSerialLines(out DigitalMultiChannelWriter writer)
        {
            ModelInitialize();
            ResetTesterState();
            writer = null;
        }

        public static string GetPizzaID() { return ""; }



        public static int ActivateComponent(bool[] avantiBitmask, bool[] componentBitmask, DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, out LinkedList<LinkedList<bool>> newDeviceState, bool analogOverrideEn)
        {
            int[] indices = Enumerable.Range(0, avantiBitmask.Length).Where(i => avantiBitmask[i]).ToArray();
            SetModeMask(componentBitmask, indices);

            newDeviceState = deviceState;
            return 0;
        }

        public static void RefreshComponents(DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, bool analogOverrideEn)
        {

        }

        public static int powerCycleSensor(int sensorNumber, DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, bool analogOverrideEn)
        {
            PowerCycleSensors(sensorNumber - 1);
            return 0;
        }

        public static int powerCycleSensors(bool[] avantiBitmask, DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, bool analogOverrideEn)
        {
            int[] inds = Enumerable.Range(0, avantiBitmask.Length).Where(i => avantiBitmask[i]).ToArray();
            PowerCycleSensors(inds);
            return 0;
        }

        public static int powerCycleAllSensors(DigitalMultiChannelWriter serialWriter, bool analogOverrideEn)
        {
            PowerCycleAllSensors();
            return 0;
        }

        public static int toggleHallSensor(int sensorNumber, DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, bool analogOverrideEn)
        {
            OnOffMode(MODE.HALL, 2000, sensorNumber - 1);
            return 0;
        }

        public static int togglePGSensor(int sensorNumber, DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, bool analogOverrideEn)
        {
            OnOffMode(MODE.PG, 2000, sensorNumber - 1);
            return 0;

        }

        public static int togglePGSensors(bool[] avantiBitmask, DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, bool analogOverrideEn)
        {
            int[] inds = Enumerable.Range(0, avantiBitmask.Length).Where(i => avantiBitmask[i]).ToArray();
            OnOffMode(MODE.HALL, 2000, inds);
            return 0;
        }

        public static int disconnectAllSensors(DigitalMultiChannelWriter serialWriter, LinkedList<LinkedList<bool>> deviceState, bool analogOverrideEn)
        {   // make sure you are also resetting the device state and connected states when and where this function is called
            ResetTesterState();
            return 0;
        }

        public static void disableAnalogOverride(DigitalMultiChannelWriter serialWriter, ref bool analogOverrideEn)
        {
            analogOverrideEn = false;
            Daq.DigitalWrite(false, PROG_F_EN);
        }

        public static int changeAnalogFrequency(int sensorNumber, int frequency, DigitalMultiChannelWriter serialWriter, ref bool analogOverrideEn)
        {
            string fen = F_EN1;
            if (sensorNumber == 1) fen = F_EN1;
            if (sensorNumber == 2) fen = F_EN2;
            if (sensorNumber == 3) fen = F_EN3;
            if (sensorNumber == 4) fen = F_EN4;

            Daq.DigitalWrite(true, PROG_F_EN);
            SetSignalFrequency(frequency, fen);
            return 0;
        }
    }
}