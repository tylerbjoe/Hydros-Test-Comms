using System.Windows;
using SciChart.Charting.Visuals.RenderableSeries;
using DelsysSigNIalGen;

namespace DelsysSigNIalGen.View
{
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();

            // Initialize the ValuePlot
            ValuePlot.InitializeValuePlot(LineRenderableSeries1, LineRenderableSeries2);

            // Subscribe to SPO2 value updates
            ValuePlot.OnSpo2Updated += UpdateSPO2TextBlock;
            ValuePlot.OnHRUpdated += UpdateHRTextBlock;
        }

        // Update the TextBlock when a new SPO2 value is received
        private void UpdateSPO2TextBlock(int spo2Value)
        {
            Dispatcher.Invoke(() => SPO2TextBlock.Text = spo2Value.ToString());
        }
        private void UpdateHRTextBlock(int spo2Value)
        {
            Dispatcher.Invoke(() => SPO2TextBlock.Text = spo2Value.ToString());
        }
    }
}
