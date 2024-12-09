using NationalInstruments.DAQmx;
using NationalInstruments.Restricted;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DelsysNICommon
{
    public class NIDAQ : Part
    {
        internal Dictionary<string, string> PinNameToID = new Dictionary<string, string>();
        internal Dictionary<string, Pin> PinIDToPin = new Dictionary<string, Pin>();

        public List<DigitalPin> DIOPins = new List<DigitalPin>();
        public List<AnalogPin> AIPins = new List<AnalogPin>();
        public List<AnalogPin> AOPins = new List<AnalogPin>();

        public double MAX_SAMPLE_RATE;

        public NIDAQ()
        {
        }



        internal void InitPinDict(Device daqDevice)
        {
            MAX_SAMPLE_RATE = daqDevice.AIMaximumSingleChannelRate;
            PinNameToID = new Dictionary<string, string>();
            PinIDToPin = new Dictionary<string, Pin>();

            var mask = ~(PhysicalChannelTypes.DIPort | PhysicalChannelTypes.DOPort);
            var pinChannels = PhysicalChannelTypes.All & mask;

            string[] pinIDs = daqDevice.GetPhysicalChannels(pinChannels, PhysicalChannelAccess.All);

            foreach (var pinID in pinIDs)
            {
                string pinName = "";
                Pin pin = null;

                Regex dioRx = new Regex($@"{daqDevice.DeviceID}/port(\d+)/line(\d+)");
                Regex aiRx = new Regex($@"{daqDevice.DeviceID}/ai(\d+)");
                Regex aoRx = new Regex($@"{daqDevice.DeviceID}/ao(\d+)");

                Match dioMatch = dioRx.Match(pinID);
                Match aiMatch = aiRx.Match(pinID);
                Match aoMatch = aoRx.Match(pinID);

                string portNum = dioMatch.Groups[1].Value;
                string lineNum = dioMatch.Groups[2].Value;
                string aiNum = aiMatch.Groups[1].Value;
                string aoNum = aoMatch.Groups[1].Value;

                if (dioMatch.Success)
                {
                    pinName = $"P{portNum}.{lineNum}";
                    pin = new DigitalPin(this, Pin.IN_OUT.INOUT);
                    DIOPins.Add((DigitalPin)pin);
                }
                if (aiMatch.Success)
                {
                    pinName = $"AI{aiNum}";
                    pin = new AnalogPin(this, Pin.IN_OUT.INOUT);
                    AIPins.Add((AnalogPin)pin);
                }
                if (aoMatch.Success)
                {
                    pinName = $"AO{aoNum}";
                    pin = new AnalogPin(this, Pin.IN_OUT.INOUT);
                    AOPins.Add((AnalogPin)pin);
                }

                if (pinName == "") continue; //unsupported

                PinNameToID.Add(pinName, pinID);
                PinIDToPin.Add(pinID, pin);
            }
            return;
        }



        public Pin GetPin(string pinName)
        {
            if (!PinNameToID.TryGetValue(pinName, out string id))
                throw new Exception($"Pin name {pinName} not found");

            return PinIDToPin[id];
        }

        public Pin GetPinByID(string pinID)
        {
            if (!PinIDToPin.TryGetValue(pinID, out Pin pin))
                throw new Exception($"Pin ID {pinID} not found");

            return pin;
        }

        public string GetNameByPin(Pin pin)
        {
            string id = PinIDToPin.First(pair => pair.Value == pin).Key;
            string name = PinNameToID.First(pair => pair.Value == id).Key;
            return name;
        }

        public Dictionary<string, string>.ValueCollection GetAllPins() => PinNameToID.Values;


        public void ClearConnections()
        {
            List<Pin> allPins = new List<Pin>();
            allPins.AddRange(DIOPins);
            allPins.AddRange(AIPins);
            allPins.AddRange(AOPins);

            foreach (Pin p in allPins)
            {
                p.NetList.Clear();
            }
        }


        // update pins and propagate changes
        internal void UpdatePins(string[] pinNames, List<object> newValues)
        {
            List<Pin> changedPins = pinNames.Select(p => PinIDToPin[PinNameToID[p]]).ToList();
            List<Pin> connectedPins = new List<Pin>();

            List<Part> changedParts = new List<Part>();
            DigitalPin changedPin = null;

            for (int i = 0; i < changedPins.Count; i++)
            {
                if (changedPins[i].SignalType == Pin.SIGNAL_TYPE.DIGITAL)
                    changedPin = (DigitalPin)changedPins[i];

                changedPin.SetValue((bool)newValues[i]);

                foreach (Part changedPart in changedPin.NetList.Select(p => p.ParentPart))
                {
                    if (!changedParts.Contains(changedPart))
                        changedParts.Add(changedPart);
                }
            }

            changedParts.Remove(this);

            while (!changedParts.IsEmpty())
            {
                changedParts[0].ChangeHandler(this, ref changedParts);
                changedParts.RemoveAt(0);
            }
        }


        internal override void ChangeHandler(object sender, ref List<Part> queue)
        {

        }
    }

}
