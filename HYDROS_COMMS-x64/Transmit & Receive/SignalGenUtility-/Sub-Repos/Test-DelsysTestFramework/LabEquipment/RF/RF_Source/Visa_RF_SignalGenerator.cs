using DelsysTestLib.LabEquipment;
using NationalInstruments.Visa;
using System.Diagnostics;

namespace DelsysTestFramework.LabEquipment.RF.RF_Source;

public class Visa_RF_SignalGenerator : VisaEquipmentI // Signal Generator
{
    public static Visa_RF_SignalGenerator Instance { get; } = new Visa_RF_SignalGenerator();
    private MessageBasedSession _rf;


    public Visa_RF_SignalGenerator()
    {
        Name = "RF Source";
        Address = SearchForRFSourceVisaDevices();
    }

    public string SearchForRFSourceVisaDevices()
    {
        var rm = new ResourceManager();
        IEnumerable<string> resources = rm.Find("?*");

        foreach (var resource in resources)
        {
            Trace.WriteLine(resource);
            if (resource.Contains("19::INSTR"))
            {
                Address = resource;

                if (_rf == null)
                {
                    ConnectRF();
                }
                return resource;
            }
        }
        Address = "";
        return "";
    }

    public bool ConnectRF()
    {
        if (IsConnected)
            return true;
        else
        {
            try
            {
                if (Address == "")
                    SearchForRFSourceVisaDevices();
                else
                {
                    _rf = (MessageBasedSession)ConnectVISA(Address);
                    _rf.TimeoutMilliseconds = 5000;
                    _rf.RawIO.Write("*RST");
                    _rf.Clear();

                    _rf.RawIO.Write("*RCL 1,01");
                    _rf.RawIO.Write("FREQ 2402 MHz");
                    _rf.RawIO.Write("POW -50dBm");
                    _rf.RawIO.Write("OUTP OFF");
                }
            }
            catch (System.Exception e)
            {
                Trace.WriteLine(e.Message);
                return false;
            }
            if (_rf == null)
                return false;
            else
                return true;
        }
    }

    public void ConfigureRXSensitivityTest()
    {
        if (!IsConnected)
            ConnectRF();

        _rf.RawIO.Write("FREQ 2402 MHz");
        _rf.RawIO.Write("POW -80dBm");
        _rf.RawIO.Write("OUTP ON"); // turn on output
        Thread.Sleep(500);
    }

    public void SetPowerLevel(double powerLevel)
    {
        if (!IsConnected)
            ConnectRF();

        _rf.RawIO.Write("POW " + powerLevel + "dBm");
    }

    public void SetFrequency(double frequency)
    {
        if (!IsConnected)
            ConnectRF();

        _rf.RawIO.Write("FREQ " + frequency + "MHz");
    }

    public void SetOn()
    {
        if (!IsConnected)
            ConnectRF();

        _rf.RawIO.Write("OUTP ON");
    }
    public void SetOff()
    {
        if (!IsConnected)
            ConnectRF();

        _rf.RawIO.Write("OUTP OFF");
    }
    public void Reset()
    {
        if (!IsConnected)
            ConnectRF();

        _rf.RawIO.Write("OUTP OFF"); // turn off output
        _rf.RawIO.Write("*RST");
    }

    public void Write(string v)
    {
        try
        {
            _rf.RawIO.Write(v);
        }
        catch (Exception exp)
        {
            Trace.WriteLine(exp.Message);
        }
    }
    public string Read()
    {
        try
        {
            return _rf.RawIO.ReadString();
        }
        catch (Exception exp)
        {
            Trace.WriteLine(exp.Message);
            return "";
        }
    }
    public string Query(string command)
    {
        try
        {
            _rf.RawIO.Write(command);
            return _rf.RawIO.ReadString();
        }
        catch (Exception exp)
        {
            Trace.WriteLine(exp.Message);
            return "";
        }
    }
    public void SendEndEnabled(bool selection)
    {
        _rf.SendEndEnabled = selection;
    }
    public void Write(byte[] data)
    {
        _rf.RawIO.Write(data);
    }
}
