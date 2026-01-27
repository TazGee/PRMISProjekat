using System.Windows;

namespace UserClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        void ShowRegister(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            LoginTitle.Visibility = Visibility.Collapsed;
            LoginButtons.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
            RegisterTitle.Visibility = Visibility.Visible;
            RegisterButtons.Visibility = Visibility.Visible;

            LoginNickname.Clear();
            LoginPassword.Clear();
        }

        void ShowLogin(object sender, RoutedEventArgs e)
        {
            RegisterPanel.Visibility = Visibility.Collapsed;
            RegisterTitle.Visibility = Visibility.Collapsed;
            RegisterButtons.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
            LoginTitle.Visibility = Visibility.Visible;
            LoginButtons.Visibility = Visibility.Visible;

            RegisterNickname.Clear();
            RegisterFirstLastName.Clear();
            RegisterPassword.Clear();
        }

        void ConnectToServer(object sender, RoutedEventArgs e)
        {
            ConnectionGrid.Visibility = Visibility.Collapsed;
            AuthGrid.Visibility = Visibility.Visible;

            ShowLogin(sender, e);
        }
    }
}
