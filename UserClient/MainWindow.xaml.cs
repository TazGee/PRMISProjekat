using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace UserClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Socket tcpSocket;
        Socket udpSocket;

        CancellationTokenSource cts;
        Task task;

        IPEndPoint tcpServerEP;
        IPEndPoint udpServerEP;

        int udpPort;

        public MainWindow()
        {
            InitializeComponent();
        }

        void ConnectToServer(object sender, RoutedEventArgs e)
        {
            // -- Provera ispravnosti IP adrese
            IPAddress ipAddress;
            if (!IPAddress.TryParse((ConnectionIP.Text ?? "").Trim(), out ipAddress))
            {
                MessageBox.Show("Neispravna IP adresa!", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // -- Provera ispravnosti porta
            int port;
            if (!int.TryParse(ConnectionPort.Text, out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Neispravan port!", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // -- Povezivanje
            try
            {
                // -- Definisanje tcp socketa i endpointa i povezivanje
                tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                tcpServerEP = new IPEndPoint(ipAddress, port);
                tcpSocket.Connect(tcpServerEP);

                // -- Dobijanje UDP porta od servera i cuvanje u buffer
                byte[] buffer = new byte[4];
                int bytesReceived = tcpSocket.Receive(buffer);
                if(bytesReceived != 4)
                {
                    MessageBox.Show("Server nije poslao ispravan port!", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // -- Konvertovanje porta u int
                udpPort = BitConverter.ToInt32(buffer, 0);

                // -- Povezivanje na server preko UDP protokola
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpServerEP = new IPEndPoint(ipAddress, udpPort);
                udpSocket.Connect(udpServerEP);
                
                MessageBox.Show($"Povezivanje uspesno!\nIP: {ipAddress.ToString()}, Port: {udpPort}", "Uspesno", MessageBoxButton.OK, MessageBoxImage.Information);

                ConnectionGrid.Visibility = Visibility.Collapsed;
                AuthGrid.Visibility = Visibility.Visible;

                ShowLogin(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Doslo je do greske prilikom povezivanja!\nGreska: {ex.Message}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
    }
}
