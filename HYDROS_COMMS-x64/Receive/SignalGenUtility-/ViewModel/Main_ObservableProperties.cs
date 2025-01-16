using CommunityToolkit.Mvvm.ComponentModel;
using DelsysSigNIalGen.Model;
using Plotter.ViewModel.Plots;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DelsysSigNIalGen.ViewModel;

partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private List<string> _AI_Names;

    [ObservableProperty]
    LiveLinePlotViewModel _PlotViewModel;
    [ObservableProperty]
    DefaultLinePlotViewModel _DefaultLinePlotViewModel;

    [ObservableProperty]
    private string _DataFilePath = "C:\\Users\\TJoe\\Documents\\1_8_pooltest\\test0.bin";

    [ObservableProperty]
    private string _LogFilePath = "C:\\Users\\TJoe\\Documents\\1_8_pooltest\\testingLog.txt";

    [ObservableProperty]
    public int _secretCarrierFrequency = 100_000;

    [ObservableProperty]
    HardwareModel _hw = HardwareModel.Instance;

    [ObservableProperty]
    private bool _wavFileSelected = false;

    [ObservableProperty]
    private bool _ZoomExtents = true;

    [ObservableProperty]
    private bool _IsContinuous = true;

    [ObservableProperty]
    private int _SamplesInMemory = 30_000_000;

    [ObservableProperty]
    private int _signalFrom = 0;
    partial void OnSignalFromChanged(int oldValue, int newValue)
    {
        switch (newValue)
        {
            case 0:
                IsFromFile = true;
                IsCalculatedShape = false;
                break;
            case 1:
                IsFromFile = false;
                IsCalculatedShape = true;
                break;
        }
    }

    [ObservableProperty]
    private string _WavFileLocation;
    [ObservableProperty]
    private double _SweepStartFreq,
                   _SweepEndFreq,
                   _SweepStep,
                   _SweepAmplitude,
                   _amplitude,
                   _frequency,
                   _dcOffset = 0.0;

    [ObservableProperty]
    private int _AISampleRate,
                   _AIPullRate,
                        _AOSampleRate;
    partial void OnAISampleRateChanged(int value)
    {
        AIPullRate = (int)(value * 0.1);
        SamplesInMemory = (int)(value * 30);
        if (value > _ff.DAQ.Part.MAX_SAMPLE_RATE)
        {
            System.Windows.MessageBox.Show($"The sample rate is too high for the {_ff.DAQ.DaqModel}. The maximum sample rate is {_ff.DAQ.Part.MAX_SAMPLE_RATE}", "", MessageBoxButton.OK);
        }
    }

    [ObservableProperty]
    private bool _isFromFile = true;
    [ObservableProperty]
    private bool _isCalculatedShape = false;

    [ObservableProperty]
    private int _signalShape;
    [ObservableProperty]
    private string _analogInChannel;
    [ObservableProperty]
    private string _AnalogInRange;

    [ObservableProperty]
    private bool _isAnalogInRunning = false;
    partial void OnIsAnalogInRunningChanged(bool oldValue, bool newValue)
    {
        IsAnalogInNotRunning = !newValue;
    }

    [ObservableProperty]
    private bool _isAnalogInNotRunning = true;
}
