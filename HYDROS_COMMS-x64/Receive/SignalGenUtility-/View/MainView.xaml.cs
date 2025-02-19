using System.Windows;

namespace DelsysSigNIalGen.View
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
            ValuePlot.InitializeValuePlot(LineRenderableSeries1, LineRenderableSeries2);
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
