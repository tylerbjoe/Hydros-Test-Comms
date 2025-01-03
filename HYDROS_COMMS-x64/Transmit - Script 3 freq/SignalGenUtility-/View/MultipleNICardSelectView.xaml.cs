using CommunityToolkit.Mvvm.ComponentModel;
using DelsysSigNIalGen.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DelsysSigNIalGen.View
{
    /// <summary>
    /// Interaction logic for MultipleNICardSelectView.xaml
    /// </summary>
    public partial class MultipleNICardSelectView : Window
    {
        public MultipleNICardSelectView()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public partial class MultipleNICardSelectViewModel : ObservableObject
    {
        public static MultipleNICardSelectViewModel Instance { get; } = new MultipleNICardSelectViewModel();

        [ObservableProperty]
        private List<string> _NICardsConnected;
        [ObservableProperty]
        private string _SelectedNICard;
    }
}
