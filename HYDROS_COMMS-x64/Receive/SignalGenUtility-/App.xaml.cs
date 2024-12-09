using DelsysSigNIalGen.View;
using DelsysSigNIalGen.ViewModel;
using SciChart.Charting.Visuals;
using System.Diagnostics;
using System.Windows;

namespace DelsysSigNIalGen;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enable trace output to the default trace listener
        Trace.AutoFlush = true;
        Trace.Listeners.Add(new DefaultTraceListener());
        Trace.Listeners.Add(new ConsoleTraceListener());

        // Set this code once in App.xaml.cs or application startup
        SciChartSurface.SetRuntimeLicenseKey("FLmgri4acQkt80XogfN84dUSoFavqn9gvcyJWgDbnzvvUFT0GA8cmXq9wcNq9+8lgk2SMdlX1fc5sjLQ+k6F7d83OWamPPclLn1SQoBxne+uXtmamFj4oBsUscvwOWXme0nSA728FmSLCFWidm0U1A13MqHwSfjMtseolbOo4XQhVF2na5AxCho6y3+93FDUlLIVU1YZtZv8aahPDiqnbC+D+gpYPG6UTH4bvwth1bTu5S+EzWJCnOf2OCb9t3OiMXDB6wXWouJ7DDyeBbmR6WU7CPfI85MtaocKblfHzJc4Tit/TQ9auhmFPWd67/asAhM3RX4oH+qpiICDvop4HJZJe1qr3oFqHw8z6JLemy27xUxJSSKcbDLEw8goyBPd0Y92eWorxzBCrMDzWli0MTNyqsfhlnjCNvRi7m/QZjIn3Ew8kGxk77KSMYDIOrpvcuFaN2Cn7Dgd84X4YVvAwjS5xCCyBQ==");

        try
        {
            var view = new MainView()
            {
                DataContext = MainViewModel.Instance,
            };
            view.Show();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
        }

    }

}
