using CommunityToolkit.HighPerformance;
using CommunityToolkit.Mvvm.ComponentModel;
using DelsysSigNIalGen.View;
using DelsysSigNIalGen.ViewModel;
using DelsysTestLib.NIDAQ;
using DelsysTestLib.Util;
using MathNet.Numerics;
using SciChart.Charting.Model.DataSeries;
using System.Diagnostics;
using System.Windows;

namespace DelsysSigNIalGen.Model;

public sealed partial class HardwareModel : ObservableObject
{
    public static HardwareModel Instance { get; } = new HardwareModel();
    public static FixtureFramework _ff => FixtureFramework.Instance;
    public Dictionary<string, IDataSeries<double, double>> _Data;

    [ObservableProperty]
    private Dictionary<TF_PIN, HARDWARE_PIN> _HardwarePinDef;
    [ObservableProperty]
    public Dictionary<string, HARDWARE_PIN> _PhysicalHardware;

    [ObservableProperty]
    private bool _IS_NI_CONNECTED = false;

    public HardwareModel()
    {
        InitNI();

        _PhysicalHardware = new Dictionary<string, HARDWARE_PIN>()
        {
            {"AO0"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO0" } },
            {"AO1"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO1"}},
            {"AO2"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO2"}},
            {"AO3"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO3"}},
            {"AO4"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO4"}},
            {"AI0"   , new HARDWARE_PIN {PinType="AI" , PinAddress="0"}},
            {"AI1"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI1"}},
            {"AI2"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI2"}},
            {"AI3"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI3"}},
            {"AI4"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI4"}},
            {"AI5"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI5"}},
            {"AI6"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI6"}},
            {"AI7"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI7"}},
            {"AI8"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI8"}},
            {"AI9"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI9"}},
            {"AI10"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI10"}},
            {"AI11"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI11"}},
            {"AI12"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI12"}},
            {"AI13"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI13"}},
            {"AI14"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI14"}},
            {"AI15"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI15"}},
            {"AO5"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO5" } },
            {"AO6"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO6"}},
            {"AO7"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO7"}},
            {"AO8"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO8"}},
            {"AO9"   , new HARDWARE_PIN {PinType="AO" , PinAddress="AO9"}},
            {"AI16"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI16"}},
            {"AI17"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI17"}},
            {"AI18"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI18"}},
            {"AI19"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI19"}},
            {"AI20"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI20"}},
            {"AI21"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI21"}},
            {"AI22"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI22"}},
            {"AI23"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI23"}},
            {"AI24"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI24"}},
            {"AI25"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI25"}},
            {"AI26"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI26"}},
            {"AI27"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI27"}},
            {"AI28"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI28"}},
            {"AI29"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI29"}},
            {"AI30"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI30"}},
            {"AI31"   , new HARDWARE_PIN {PinType="AI" , PinAddress="AI31"}},
        };
        HardwarePinDef = new Dictionary<TF_PIN, HARDWARE_PIN>()
        {
            // AO PINS
            {TF_PIN.AI0,       _PhysicalHardware["AI0"]},
            {TF_PIN.AI1,       _PhysicalHardware["AI1"]},
            {TF_PIN.AI2,       _PhysicalHardware["AI2"]},
            {TF_PIN.AI3,       _PhysicalHardware["AI3"]},
            {TF_PIN.AI4,       _PhysicalHardware["AI4"]},
            {TF_PIN.AI5,       _PhysicalHardware["AI5"]},
            {TF_PIN.AI6,       _PhysicalHardware["AI6"]},
            {TF_PIN.AI7,       _PhysicalHardware["AI7"]},
            {TF_PIN.AI8,       _PhysicalHardware["AI8"]},
            {TF_PIN.AI9,       _PhysicalHardware["AI9"]},
            {TF_PIN.AI10,       _PhysicalHardware["AI10"]},
            {TF_PIN.AI11,       _PhysicalHardware["AI11"]},
            {TF_PIN.AI12,       _PhysicalHardware["AI12"]},
            {TF_PIN.AI13,       _PhysicalHardware["AI13"]},
            {TF_PIN.AI14,       _PhysicalHardware["AI14"]},
            {TF_PIN.AI15,       _PhysicalHardware["AI15"]},
            {TF_PIN.AO0,       _PhysicalHardware["AO0"]},
            {TF_PIN.AO1,       _PhysicalHardware["AO1"]},
            {TF_PIN.AO2,       _PhysicalHardware["AO2"]},
            {TF_PIN.AO3,       _PhysicalHardware["AO3"]},
            {TF_PIN.AO4,       _PhysicalHardware["AO4"]},
            {TF_PIN.AI16,       _PhysicalHardware["AI16"]},
            {TF_PIN.AI17,       _PhysicalHardware["AI17"]},
            {TF_PIN.AI18,       _PhysicalHardware["AI18"]},
            {TF_PIN.AI19,       _PhysicalHardware["AI19"]},
            {TF_PIN.AI20,       _PhysicalHardware["AI20"]},
            {TF_PIN.AI21,       _PhysicalHardware["AI21"]},
            {TF_PIN.AI22,       _PhysicalHardware["AI22"]},
            {TF_PIN.AI23,       _PhysicalHardware["AI23"]},
            {TF_PIN.AI24,       _PhysicalHardware["AI24"]},
            {TF_PIN.AI25,       _PhysicalHardware["AI25"]},
            {TF_PIN.AI26,       _PhysicalHardware["AI26"]},
            {TF_PIN.AI27,       _PhysicalHardware["AI27"]},
            {TF_PIN.AI28,       _PhysicalHardware["AI28"]},
            {TF_PIN.AI29,       _PhysicalHardware["AI29"]},
            {TF_PIN.AI30,       _PhysicalHardware["AI30"]},
            {TF_PIN.AI31,       _PhysicalHardware["AI31"]},
            {TF_PIN.AO5,       _PhysicalHardware["AO5"]},
            {TF_PIN.AO6,       _PhysicalHardware["AO6"]},
            {TF_PIN.AO7,       _PhysicalHardware["AO7"]},
            {TF_PIN.AO8,       _PhysicalHardware["AO8"]},
            {TF_PIN.AO9,       _PhysicalHardware["AO9"]},
        };

        _Data = new Dictionary<string, IDataSeries<double, double>>();
        for (int i = 0; i < 2; i++)
        {
            _Data.Add($"AI{i}", new XyDataSeries<double, double>
            {
                FifoCapacity = (int)(_ff.AIRead.SAMPLE_RATE * 3.5),
                AcceptsUnsortedData = true
            });
        }
    }

    public void InitNI()
    {
        if (_ff.GetNIConnected().Length > 1)
        {
            //var vm = MultipleNICardSelectViewModel.Instance;
            //vm.NICardsConnected = _ff.GetNIConnected().ToList();
            //var multiView = new MultipleNICardSelectView()
            //{
            //    DataContext = vm
            //};
            //multiView.ShowDialog();
            //Trace.WriteLine(vm.SelectedNICard);
            //_ff.InitNICard(vm.SelectedNICard);
            _ff.InitNICard("Dev1");
        }
        else if (_ff.GetNIConnected().Length == 1)
        {
            var nicards = _ff.GetNIConnected();
            _ff.InitNICard(nicards.First());
        }
        if (_ff.MODEL == null)
        {
            if (MessageBox.Show("Could not find NI card.\n Should the program try again?", "", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                Environment.Exit(0);
            }
            InitNI();
            return;
        }
        else { IS_NI_CONNECTED = true; }
    }
    public string GetPinAddress(TF_PIN pinName)
    {
        HardwarePinDef.TryGetValue(pinName, out HARDWARE_PIN? address);
        if (address == null)
        {
            Trace.WriteLine($"{pinName} does not exist");
            Application.Current.Dispatcher.Invoke(() => { MessageBox.Show($"OOPS it looks like {pinName} does not exist. \nIs the correct NI card plugged in?"); });
            throw new Exception($"{pinName} does not exist");
        }
        return address.PinAddress;
    }
}
public partial class HARDWARE_PIN : ObservableObject
{
    public string PinAddress { get; set; }
    public string PinType { get; set; }


    private bool _PinState;
    public bool PinState
    {
        get { return _PinState; }
        set
        {
            SetProperty(ref _PinState, value);
            OnPinStateChanged(value);
        }
    }

    public void OnPinStateChanged(bool newValue)
    {
        if (PinType == "DIO")
        {
            FixtureFramework.Instance.SetDO(newValue, PinAddress);
        }
        else if (PinType == "SHIFT")
        {
            //Trace.WriteLine($"{PinAddress} Changed but not shifted");
        }
    }
}
public enum TF_PIN
{
    AO0,
    AO1,
    AO2,
    AO3,
    AO4,
    AI0,
    AI1,
    AI2,
    AI3,
    AI4,
    AI5,
    AI6,
    AI7,
    AI8,
    AI9,
    AI10,
    AI11,
    AI12,
    AI13,
    AI14,
    AI15,
    AI16,
    AI17,
    AI18,
    AI19,
    AI20,
    AI21,
    AI22,
    AI23,
    AI24,
    AI25,
    AI26,
    AI27,
    AI28,
    AI29,
    AI30,
    AI31,
    AO5,
    AO6,
    AO7,
    AO8,
    AO9,
}
