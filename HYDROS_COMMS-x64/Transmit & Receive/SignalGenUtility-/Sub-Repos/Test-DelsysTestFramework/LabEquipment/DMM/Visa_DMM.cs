using DelsysTestLib.Util;
using NationalInstruments.Visa;
using System.Diagnostics;

namespace DelsysTestLib.LabEquipment.DMM
{
    public class Visa_DMM : VisaEquipmentI
    {
        public static Visa_DMM Instance { get; } = new Visa_DMM();
        private MessageBasedSession _dmm;
        public DateTime? LastCalibrationDate { get; set; }
        public DateTime? NextCalibrationDate { get; set; }
        public string DMM_Info { get; set; }
        public Visa_DMM()
        {
            Name = "DMM";
            Address = "USB0::0x05E6::0x7510::04106879::INSTR";
        }

        public bool ConnectDMM()
        {
            if (IsConnected)
                return true;
            else
            {
                try
                {
                    _dmm = (MessageBasedSession)ConnectVISA(Address);
                    Trace.WriteLine($"Connected to {_dmm.ResourceName}");
                }
                catch (System.Exception e)
                {
                    Trace.WriteLine(e.Message);
                    return false;
                }
                LastCalibrationDate = ReadCalibrationDate();
                NextCalibrationDate = LastCalibrationDate?.AddYears(1);
                ReadVersion();
                return true;
            }
        }

        public void SwitchMode(DMM_Modes mode)
        {
            if (_dmm == null)
            {
                this.ConnectDMM();
                return;
            }

            string modeString = StringHelper.Instance.ReplaceCommonEscapeSequences(DMM_Mode.GetStringFromState(mode));
            _dmm.RawIO.Write(modeString);
            Trace.WriteLine($"Switched to {mode}");

        }

        public DateTime? ReadCalibrationDate()
        {
            if (_dmm == null)
            {
                this.ConnectDMM();
                return null;
            }
            _dmm.RawIO.Write("*LANG TSP");
            Task.Delay(500).Wait();
            _dmm.RawIO.Write("print(cal.verify.date)");
            Task.Delay(500).Wait();
            var temp = _dmm.RawIO.ReadString();
            Task.Delay(500).Wait();
            _dmm.RawIO.Write("*LANG SCPI");
            temp = temp.Replace(System.Environment.NewLine, "");
            DateTime dt = DateTime.Parse(temp);
            return dt;

        }

        public double ReadMeasurement()
        {
            if (_dmm == null)
            {
                this.ConnectVISA(Address);
                return -1.0;
            }

            string modeString = StringHelper.Instance.ReplaceCommonEscapeSequences(":READ?");
            _dmm.RawIO.Write(modeString);
            string temp = _dmm.RawIO.ReadString();
            temp = temp.Replace(System.Environment.NewLine, "");
            Trace.WriteLine($"Read: {temp}");
            return Double.Parse(temp, System.Globalization.NumberStyles.Any);
        }
        public void ReadVersion()
        {
            if (_dmm == null)
            {
                return;
            }
            string informationString = StringHelper.Instance.ReplaceCommonEscapeSequences("*IDN?");
            _dmm.RawIO.Write(informationString);
            DMM_Info = _dmm.RawIO.ReadString();
            DMM_Info = DMM_Info.Replace(System.Environment.NewLine, "");
        }
    }
}
