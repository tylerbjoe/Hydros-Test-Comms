using DelsysTestLib.LabEquipment;
using Ivi.Visa;
using NationalInstruments.Visa;
using System.Diagnostics;

namespace DelsysTestFramework.LabEquipment.RF
{
    public class Visa_RF : VisaEquipmentI
    {
        public static Visa_RF Instance { get; } = new Visa_RF();
        private MessageBasedSession _rf;
        public int SET_FREQUENCY { get; set; }

        public Visa_RF()
        {
            Name = "RF Spec";
        }

        public bool ConnectRFSpec()
        {
            if (IsConnected)
                return true;
            else
            {
                try
                {
                    _rf = (MessageBasedSession)ConnectVISA(Address);
                }
                catch (System.Exception e)
                {
                    Trace.WriteLine(e.Message);
                    return false;
                }
                return true;
            }
        }

        public void SetFrequency(int frequency)
        {
            if (_rf == null)
            {
                this.ConnectRFSpec();
                return;
            }

            _rf.Clear();
            Thread.Sleep(1000);
            _rf.RawIO.Write("TRAC:MODE WRIT");
            Thread.Sleep(100);
            _rf.RawIO.Write("TRAC:MODE MAXH");
            Thread.Sleep(100);
            _rf.RawIO.Write("SENS:FREQ:CENT " + frequency + " MHz");
            Thread.Sleep(100);
            _rf.RawIO.Write("CALC:MARK:X " + frequency);

            SET_FREQUENCY = frequency;

            if (GetSetFrequency() != frequency) // if the frequency is not set correctly, try again
                SetFrequency(frequency);
        }

        public int GetSetFrequency()
        {
            _rf.RawIO.Write("SENS:FREQ:CENT?");
            Thread.Sleep(100);
            string response = _rf.RawIO.ReadString();
            SET_FREQUENCY = (int)(double.Parse(response) / 1e6);

            Trace.WriteLine($" RF: Set Frequency: {SET_FREQUENCY}");

            return SET_FREQUENCY;
        }

        public double GetSpectrumAnalyzerReading()
        {
            bool retry = false;
            double result = -999;
            do
            {
                try
                {
                    retry = false;
                    _rf.RawIO.Write("CALC:MARK:Y?");
                    Thread.Sleep(100);
                    var responseString = _rf.RawIO.ReadString();
                    result = double.Parse(responseString);
                }
                catch (VisaException ve)
                {
                    Trace.WriteLine($"RF: ran into exception {ve.Message}");
                    Thread.Sleep(100);
                    Trace.WriteLine("Communication error with N9320, retrying...");
                    retry = true;
                }
            } while (retry);

            return result;
        }
    }
}
