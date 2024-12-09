//using System.ComponentModel;
//using System.Windows;

//namespace DelsysTestLib.UI.Views
//{
//    /// <summary>
//    /// Interaction logic for ErrorMessageView.xaml
//    /// </summary>
//    public partial class ErrorMessageView : Window, INotifyPropertyChanged
//    {
//        private string _text;
//        public event PropertyChangedEventHandler PropertyChanged;
//        Window _LastWindow;

//        public ErrorMessageView(Window lastContext = null)
//        {
//            InitializeComponent();
//            this.DataContext = this;
//            _LastWindow = lastContext;
//        }

//        public void setError(string message)
//        {
//            _text = message;
//            textBlock.Text = _text;
//        }

//        protected void OnPropertyChanged(string propertyName)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }

//        public string Error
//        {
//            get { return _text; }
//            set
//            {
//                _text = value;
//                OnPropertyChanged("Error");
//            }
//        }

//        private void Button_Click(object sender, RoutedEventArgs e)
//        {
//            if (_LastWindow != null)
//            {
//                _LastWindow.Show();
//            }
//            this.Close();
//        }

//    }
//}
