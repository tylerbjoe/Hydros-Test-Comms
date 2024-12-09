using DelsysTestLib.LabEquipment;
using Ivi.Visa;
using NationalInstruments.Visa;
using System.Diagnostics;

namespace DelsysTestFramework.LabEquipment.RF.RF_Control;

public class Visa_RF_Spectrum : VisaEquipmentI
{
    public static Visa_RF_Spectrum Instance { get; } = new Visa_RF_Spectrum();
    private MessageBasedSession _rf;

    bool specAnalyzerControlRevB = false;

    public Visa_RF_Spectrum()
    {
        Name = "RF Spectrum Analyzer";
        Address = SearchForRFSpectrumVisaDevices();
    }

    private string SearchForRFSpectrumVisaDevices()
    {
        var rm = new ResourceManager();
        IEnumerable<string> resources = rm.Find("?*");

        foreach (var resource in resources)
        {
            Trace.WriteLine(resource);
            if (resource.Contains("0x0957::0x2118"))
            {
                specAnalyzerControlRevB = false;
                Address = resource;
                if (ConnectRF())
                {
                    return resource;
                }
                break;
            }
            else if (resource.Contains("0x0957::0xFFEF"))
            {
                Address = resource;
                specAnalyzerControlRevB = true;
                if (_rf == null)
                {
                    if (ConnectRF())
                    {
                        return resource;
                    }
                }
                break;
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
                    SearchForRFSpectrumVisaDevices();
                else
                {
                    _rf = (MessageBasedSession)ConnectVISA(Address);
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

    public void ConfigureSA()
    {
        SendEndEnabled(true);
        _rf.RawIO.Write("*RST");
        Task.Delay(1000).Wait();

        //var configString =      $"SENS:BAND:VID 1 MHz\n" +
        //                        $"SENS:BAND:RES 1 MHz\n" +
        //                        "SENS:FREQ:SPAN 0 Hz\n" +
        //                        $"DISP:WIND:TRAC:Y:RLEV 10 dBm\n" +
        //                        "TRAC:MODE MAXH\n" +
        //                        $"CALC:MARK:PEAK:THR -10 dBm\n" +
        //                        "CALC:MARK:MODE POS\n";

        //WriteToSpectrumAnalyzer(configString);
        //Trace.WriteLine(configString);

        int param = (int)(double.Parse(Query("SENS:BAND:VID?")) / 1e6);
        if (param != 1)
            WriteToSpectrumAnalyzer("SENS:BAND:VID 1 MHz\n");

        param = (int)(double.Parse(Query("SENS:BAND:RES?")) / 1e6);
        if (param != 1)
            WriteToSpectrumAnalyzer("SENS:BAND:RES 1 MHz\n");

        param = (int)(double.Parse(Query("SENS:FREQ:SPAN?")));
        if (param != 0)
            WriteToSpectrumAnalyzer("SENS:FREQ:SPAN 0 Hz\n");

        param = (int)(double.Parse(Query("DISP:WIND:TRAC:Y:RLEV?")));
        if (param != 10)
            WriteToSpectrumAnalyzer("DISP:WIND:TRAC:Y:RLEV 10 dBm\n");

        param = (int)(double.Parse(Query("CALC:MARK:PEAK:THR?")));
        if (param != -10)
            WriteToSpectrumAnalyzer("CALC:MARK:PEAK:THR -10 dBm\n");

        string param2 = Query("CALC:MARK:MODE?");
        if (param2 != "POS")
            WriteToSpectrumAnalyzer("CALC:MARK:MODE POS\n");

        Task.Delay(2500).Wait();

        // Comment below is from the PCBTesterApp:
        // Reading the first peak usually results in an error.
        // We do a 'dummy' read here to try and eliminate that.

        // clear any existing peaks
        // set the test frequency to 2440 MHz
        WriteToSpectrumAnalyzer("TRAC:MODE WRIT\n" +
                                "TRAC:MODE MAXH\n" +
                                "SENS:FREQ:CENT 2440 MHz\n" +
                                "CALC:MARK:X 2440\n");
    }

    private void WriteToSpectrumAnalyzer(string s)
    {
        if (specAnalyzerControlRevB)
        {
            _rf.RawIO.Write(s);
        }
        else
        {
            _rf.Clear();
            Thread.Sleep(1000);
            foreach (string line in s.Split('\n'))
            {
                if (string.IsNullOrEmpty(line))
                    continue;
                _rf.RawIO.Write(line);
                _rf.Clear();
                Thread.Sleep(1000);
            }
        }
    }

    public int GetBandwidthVid()
    {
        _rf.RawIO.Write("SENS:BAND:VID?\n");
        Thread.Sleep(500);
        return (int)(double.Parse(_rf.RawIO.ReadString()) / 1e6);
    }

    public int GetBandwidthRes()
    {
        _rf.RawIO.Write("SENS:BAND:RES?\n");
        Thread.Sleep(500);
        return (int)(double.Parse(_rf.RawIO.ReadString()) / 1e6);
    }

    public int GetFrequencySpan()
    {
        _rf.RawIO.Write("SENS:FREQ:SPAN?\n");
        Thread.Sleep(500);
        return (int)(double.Parse(_rf.RawIO.ReadString()) / 1e6);
    }

    public int GetReferenceLevel()
    {
        _rf.RawIO.Write("DISP:WIND:TRAC:Y:RLEV?\n");
        Thread.Sleep(500);
        return (int)(double.Parse(_rf.RawIO.ReadString()));
    }

    public int GetPeakThreshold()
    {
        _rf.RawIO.Write("CALC:MARK:PEAK:THR?\n");
        Thread.Sleep(500);
        return (int)(double.Parse(_rf.RawIO.ReadString()));
    }

    public int GetMarkerX()
    {
        _rf.RawIO.Write("CALC:MARK:X?\n");
        Thread.Sleep(500);
        return (int)(double.Parse(_rf.RawIO.ReadString()) / 1e6);
    }

    public string GetMarkMode()
    {
        _rf.RawIO.Write("CALC:MARK:MODE?\n");
        Thread.Sleep(500);
        return _rf.RawIO.ReadString();
    }

    public void ResetSpectrumAnalyzer()
    {
        if (GetBandwidthVid() != 1)
            WriteToSpectrumAnalyzer("SENS:BAND:VID 1 MHz\n");
        if (GetBandwidthRes() != 1)
            WriteToSpectrumAnalyzer("SENS:BAND:RES 1 MHz\n");
        if (GetFrequencySpan() != 0)
            WriteToSpectrumAnalyzer("SENS:FREQ:SPAN 0 Hz\n");
        if (GetReferenceLevel() != 10)
            WriteToSpectrumAnalyzer("DISP:WIND:TRAC:Y:RLEV 10 dBm\n");
        if (GetPeakThreshold() != -10)
            WriteToSpectrumAnalyzer("CALC:MARK:PEAK:THR -10 dBm\n");
        if (GetMarkMode() != "POS")
            WriteToSpectrumAnalyzer("CALC:MARK:MODE POS\n");

        WriteToSpectrumAnalyzer("TRAC:MODE WRIT\n" +
                                "TRAC:MODE MAXH\n" +
                                "SENS:FREQ:CENT 2440 MHz\n" +
                                "CALC:MARK:X 2440\n");
    }

    public void SetupSpectrumAnalyzerForReading(int freq, int attenuation, int reference)
    {
        ConfigureSA();

        WriteToSpectrumAnalyzer("SENS:FREQ:CENT " + freq + " MHz\n");
        WriteToSpectrumAnalyzer("SENS:POW:ATT " + attenuation + "\n");
        WriteToSpectrumAnalyzer("DISP:WIND:TRAC:Y:RLEV " + reference + " dbm\n");
        WriteToSpectrumAnalyzer("CALC:MARK:X " + freq + "\n");
        Task.Delay(500).Wait();
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
                result = double.Parse(Query("CALC:MARK:Y? \n"));
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Timeout"))
                {
                    Thread.Sleep(100);
                    Trace.WriteLine("Communication error with N9320, retrying...");
                    retry = true;
                }
            }
        } while (retry);

        return result;
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
