using Ivi.Visa;
using NationalInstruments.Visa;
using System.Diagnostics;

namespace DelsysTestLib.LabEquipment
{
    public class VisaEquipmentI
    {
        public bool IsConnected { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }

        public IVisaSession ConnectVISA(String ResourceString)
        {
            using (var rmSession = new ResourceManager())
            {
                MessageBasedSession messageBasedSession = (MessageBasedSession)rmSession.Open(ResourceString);
                if (messageBasedSession != null)
                {
                    this.IsConnected = true;
                    messageBasedSession.RawIO.Write("*IDN?");
                    string readIDN = messageBasedSession.RawIO.ReadString();
                    Trace.WriteLine($"IDN: {readIDN}");
                }
                else
                { this.IsConnected = false; }
                return messageBasedSession;
            }
        }

        public string ReadIDN(MessageBasedSession session)
        {
            session.RawIO.Write("*IDN?");
            string readIDN = session.RawIO.ReadString();
            return readIDN;
        }
        public void ClearStatus(MessageBasedSession session)
        {
            session.RawIO.Write("*CLS");
        }
        public void Reset(MessageBasedSession session)
        {
            session.RawIO.Write("*RST");
        }
        public void SendCommand(MessageBasedSession session, string command)
        {
            session.RawIO.Write(command);
        }
        public string ReadCommand(MessageBasedSession session, string command)
        {
            session.RawIO.Write(command);
            string readCommand = session.RawIO.ReadString();
            return readCommand;
        }
    }
}
