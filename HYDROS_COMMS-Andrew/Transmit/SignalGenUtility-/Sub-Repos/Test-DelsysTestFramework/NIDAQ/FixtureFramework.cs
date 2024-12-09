using DelsysNICommon;
using DelsysTestFramework.NIDAQ;
using System.Diagnostics;

namespace DelsysTestLib.NIDAQ
{
    public sealed class FixtureFramework
    {
        public static FixtureFramework Instance { get; } = new FixtureFramework();
        public NIDAQController DAQ { get; } = new NIDAQController();

        public AIRead AIRead { get; }

        public string MODEL { get; set; }

        public FixtureFramework()
        {
            AIRead = new AIRead();
        }

        #region NI Card Controls
        public void InitNICard(int DeviceType)
        {
            bool daqFound = DAQ.Initialize(DeviceType);

            if (!daqFound)
            {
                Trace.WriteLine("Connection to NI was not found");
                return;
            }
            else
            {
                Trace.WriteLine($"Connected to NI {DAQ.DaqModel}");
                MODEL = DAQ.DaqModel;
            }
            MODEL = DAQ.DaqModel;

        }
        public void InitNICard(string DeviceName)
        {
            bool daqFound = DAQ.Initialize(DeviceName);

            if (!daqFound)
            {
                Trace.WriteLine("Connection to NI was not found");
                return;
            }
            else
            {
                Trace.WriteLine($"Connected to NI {DAQ.DaqModel}");
                MODEL = DAQ.DaqModel;
            }
            MODEL = DAQ.DaqModel;

        }

        public string[] GetNIConnected()
        {
            return DAQ.GetConnectedNIDevices();
        }
        #endregion

        #region Analog Output Controls
        public void StartTestSignal(string pin, double voltage)
        {
            DAQ.AnalogWriteVoltage(voltage, pin);
            Task.Delay(100);
        }
        public void StopTestSignal(string pin)
        {
            DAQ.AnalogWriteVoltage(0, pin);
            Task.Delay(100);
        }
        public void StartGeneratedSignal(string pin, double frequency, double pk_pk, WaveformType waveformType, double dc_offset = 0.0)
        {
            DAQ.StartGenerateFunction(pin, waveformType, frequency, pk_pk / 2.0, 2_000_000, 2_000_000, dc_offset);
            Task.Delay(100);
        }
        public void StartGeneratedSignalFromFloatArray(string pin, float[] data, int outFreq, bool regeneration)
        {
            DAQ.StartFunctionFromFloatArray(pin, data, outFreq, regeneration); // 44.1kHz is the most common .wav file sample rate
            Task.Delay(100);
        }

        public void StartSweep(string pin, double amp, double startFreq, double stopFreq, double step)
        {
            DAQ.StartSineSweep(pin, amp, startFreq, stopFreq, step, 2_000_000);
        }

        public void StopGeneratedSignal(string pin)
        {
            try
            {
                DAQ.StopGenerateFunction(pin);
                DAQ.AnalogWriteVoltage(0, pin);
                Task.Delay(100);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }

        }
        #endregion

        #region Digital Input Controls
        public Dictionary<string, bool> GetDIOStates(params string[] pinsToGetStatesOf)
        {
            var states = new Dictionary<string, bool>();
            for (int i = 0; i < pinsToGetStatesOf.Length; i++)
            {
                states.Add(pinsToGetStatesOf[i], GetDIOPinState(pinsToGetStatesOf[i]));
            }

            return states;
        }
        public bool GetDIOPinState(string pin)
        {
            int pinIndex = int.Parse(pin.Split('.')[1]);
            var read = DAQ.DigitalRead(pin);

            return (read & (1U << pinIndex)) != 0;
        }

        #endregion

        #region Digital Output Controls
        public void SetDO(bool set, params string[] pins)
        {
            DAQ.DigitalWrite(set, pins);
            Trace.WriteLine($"Set {set} to {string.Join(", ", pins)}");
            Thread.Sleep(100);
        }

        public bool ReadDO(params string[] read)
        {
            Thread.Sleep(100);
            var x = DAQ.DigitalRead(read);
            return x > 0;
        }
        #endregion
    }
}