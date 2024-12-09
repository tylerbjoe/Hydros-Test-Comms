using System;
using System.Collections.Generic;
using System.Linq;


// Platform target must be set to x86 in properties for DAQmx dll

namespace DelsysNICommon
{

    public class MM74HC595MTC : Part
    {

        public static readonly string Description =
            "8-Bit SIPO Shift Register with Output Latches";

        public const int DATA_WIDTH = 8;

        public List<bool> InnerState;
        public DigitalPin Clk { get; }
        public DigitalPin SerIn { get; }
        public DigitalPin Clear { get; }
        public DigitalPin Hiz { get; }
        public DigitalPin LatchEn { get; }
        public DigitalPin SerOut { get; }
        public List<DigitalPin> OutputPins { get; }

        private bool prevClk = false;
        private bool prevLatch = false;

        public MM74HC595MTC()
        {

            OutputPins = Enumerable.Range(0, DATA_WIDTH).Select(i => new DigitalPin(this, Pin.IN_OUT.OUTPUT)).ToList();
            InnerState = Enumerable.Repeat(false, DATA_WIDTH).ToList();
            State = Enumerable.Repeat(false, DATA_WIDTH).ToList();

            Clk = new DigitalPin(this, Pin.IN_OUT.INPUT);
            SerIn = new DigitalPin(this, Pin.IN_OUT.INPUT);
            Clear = new DigitalPin(this, Pin.IN_OUT.INPUT);
            Hiz = new DigitalPin(this, Pin.IN_OUT.INPUT);
            LatchEn = new DigitalPin(this, Pin.IN_OUT.INPUT);
            SerOut = new DigitalPin(this, Pin.IN_OUT.INPUT);

            Pins = new List<Pin> { Clk, SerIn, Clear, Hiz, LatchEn, SerOut };
            Pins.AddRange(OutputPins);

        }



        internal void UpdateOutput()
        {
            if ((bool)Hiz.CurrentValue)
            {
                OutputPins.ForEach(p => p.SetValue(false));
            }

            if (!(bool)Hiz.CurrentValue)
            {
                for (int i = 0; i < InnerState.Count; i++)
                {
                    OutputPins[i].SetValue(State[i]);
                }
            }

            if (!(bool)Clear.CurrentValue)
            {
                InnerState = Enumerable.Repeat(false, DATA_WIDTH).ToList();
                State = Enumerable.Repeat(false, DATA_WIDTH).ToList();
                OutputPins.ForEach(p => p.SetValue(false));
            }

        }

        internal void UpdateEdge()
        {

            bool posEdgeClk = (bool)Clk.CurrentValue && !prevClk;
            prevClk = (bool)Clk.CurrentValue;

            if (posEdgeClk)
            {
                InnerState.Insert(0, (bool)SerIn.CurrentValue);
                SerOut.SetValue(InnerState.Last());
                InnerState.RemoveAt(InnerState.Count - 1);
            }




            bool posEdgeLatch = (bool)LatchEn.CurrentValue && !prevLatch;
            prevLatch = (bool)LatchEn.CurrentValue;
            if (posEdgeLatch)
            {
                for (int i = 0; i < InnerState.Count; i++)
                {
                    State[i] = InnerState[i];
                }
            }

        }



        internal override void ChangeHandler(object sender, ref List<Part> queue)
        {
            UpdateEdge();
            UpdateOutput();

            List<Part> parts = GetConnectedParts();
            queue.AddRange(parts);
        }


        public List<bool> GetOutputs()
        {
            return OutputPins.Select(p => (bool)p.CurrentValue).ToList();
        }


    }


    public class MAX4558 : Part
    {

        public static readonly string Description =
            "Low-voltage, CMOS analog IC configured as an 8-to-1 multiplexer";


        public const int DATA_WIDTH = 8;

        public DigitalPin A { get; }
        public DigitalPin B { get; }
        public DigitalPin C { get; }

        public AnalogPin X { get; }
        public List<AnalogPin> InputPins { get; }

        public int SelectedIndex { get; private set; }

        private AnalogPin selectedPin;

        public MAX4558()
        {
            InputPins = Enumerable.Range(0, DATA_WIDTH).Select(i => new AnalogPin(this, Pin.IN_OUT.OUTPUT)).ToList();

            selectedPin = InputPins[0];

            A = new DigitalPin(this, Pin.IN_OUT.INPUT);
            B = new DigitalPin(this, Pin.IN_OUT.INPUT);
            C = new DigitalPin(this, Pin.IN_OUT.INPUT);
            X = new AnalogPin(this, Pin.IN_OUT.OUTPUT);

            SelectedIndex = 0;

            Pins = new List<Pin> { A, B, C, X };
            Pins.AddRange(InputPins);
        }


        private static int BinaryToInt(bool[] bits)
        {
            int result = 0;

            for (int i = 0; i < bits.Length; i++)
                if (bits[i]) result |= 1 << (bits.Length - 1 - i);

            return result;
        }

        internal void UpdateOutput()
        {
            bool[] selectBits = { (bool)A.CurrentValue, (bool)B.CurrentValue, (bool)C.CurrentValue };
            int selected = BinaryToInt(selectBits);

            X.DisconnectPins(selectedPin);
            X.ConnectPins(InputPins[selected]);
            selectedPin = InputPins[selected];
            SelectedIndex = selected;
        }


        internal override void ChangeHandler(object sender, ref List<Part> queue)
        {
            UpdateOutput();

            List<Part> parts = GetConnectedParts();
            queue.AddRange(parts);
        }

        public List<double> GetInputs()
        {
            return InputPins.Select(p => (double)p.CurrentValue).ToList();
        }

    }



    public class DAC8411 : Part
    {

        public static readonly string Description =
            "16-Bit digital-to-analog converter with a serial interface";

        public const int DATA_WIDTH = 24;

        public DigitalPin Clk { get; }
        public DigitalPin SerIn { get; }
        public DigitalPin Sync { get; }

        public AnalogPin Vout { get; }

        public bool WritingSequence = false;


        private bool prevClk = false;
        private bool prevSync = false;

        public DAC8411()
        {
            State = Enumerable.Repeat(false, DATA_WIDTH).ToList();

            Clk = new DigitalPin(this, Pin.IN_OUT.INPUT);
            SerIn = new DigitalPin(this, Pin.IN_OUT.INPUT);
            Sync = new DigitalPin(this, Pin.IN_OUT.INPUT);

            Pins = new List<Pin> { Clk, SerIn };
        }


        internal void UpdateOutput()
        {
            if ((bool)Sync.CurrentValue)
            {
                WritingSequence = false;
                State = Enumerable.Repeat(false, DATA_WIDTH).ToList();
            }

        }


        internal void UpdateEdge()
        {
            if (!WritingSequence) return;

            bool negEdge = !(bool)Clk.CurrentValue && prevClk;
            prevClk = (bool)Clk.CurrentValue;

            if (!negEdge) return;

            State.Insert(0, (bool)SerIn.CurrentValue);
            State.RemoveAt(State.Count - 1);
        }



        internal override void ChangeHandler(object sender, ref List<Part> queue)
        {
            WritingSequence = !(bool)Sync.CurrentValue && prevSync;

            UpdateEdge();
            UpdateOutput();

            List<Part> parts = GetConnectedParts();
            queue.AddRange(parts);
        }


    }



    public class LTC6903
    {
        public static readonly string Description =
            "Digital programmable clock source (1kHz - 16MHz) with a serial interface";
    }

    // Parallel-in-out
    public class Register : Part
    {
        private const int DATA_WIDTH = 8;

        public List<DigitalPin> InputPins { get; }
        public List<DigitalPin> OutputPins { get; }


        public Register(List<Pin> pins)
        {
            bool cond1 = pins.All(pin => pin.SignalType == Pin.SIGNAL_TYPE.DIGITAL);
            if (!cond1) throw new Exception();

            InputPins = new List<DigitalPin>(DATA_WIDTH);
            OutputPins = new List<DigitalPin>(DATA_WIDTH);
            State = new List<bool>(DATA_WIDTH);

            for (int i = 0; i < DATA_WIDTH; i++)
            {
                InputPins.Add(new DigitalPin(this, Pin.IN_OUT.INPUT));
                OutputPins.Add(new DigitalPin(this, Pin.IN_OUT.OUTPUT));
                State.Add(false);
            }
        }

        internal void UpdateOutput()
        {
            for (int i = 0; i < DATA_WIDTH; i++)
            {
                State[i] = (bool)InputPins[i].CurrentValue;
                OutputPins[i].SetValue(State[i]);
            }
        }

        internal override void ChangeHandler(object sender, ref List<Part> queue)
        {
            UpdateOutput();

            List<Part> parts = GetConnectedParts();
            queue.AddRange(parts);
        }
    }

}