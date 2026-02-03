using Domain.Modeli;
using System;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
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

        Korisnik user;

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

            // -- Definisanje tcp socketa i endpointa i povezivanje
            tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpServerEP = new IPEndPoint(ipAddress, port);

            // -- Povezivanje
            try
            {
                tcpSocket.Connect(tcpServerEP);
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Doslo je do greske prilikom povezivanja!\nGreska: {ex.SocketErrorCode}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // -- Dobijanje UDP porta od servera i cuvanje u buffer
            byte[] buffer = new byte[4];
            int bytesReceived = tcpSocket.Receive(buffer);
            if (bytesReceived != 4)
            {
                MessageBox.Show("Server nije poslao ispravan port!", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // -- Konvertovanje porta u int
            udpPort = BitConverter.ToInt32(buffer, 0);
            MessageBox.Show($"Server je prosledio port! Port: {udpPort}", "Uspesno", MessageBoxButton.OK, MessageBoxImage.Information);

            // -- Povezivanje na server preko UDP protokola
            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            udpServerEP = new IPEndPoint(ipAddress, udpPort);

            // -- Prikazivanje grida za prijavu
            ConnectionGrid.Visibility = Visibility.Collapsed;
            AuthGrid.Visibility = Visibility.Visible;

            cts = new CancellationTokenSource();
            task = Task.Run(() => ReceiveLoop(cts.Token));

            ShowLogin(sender, e);
        }

        private void ReceiveLoop(CancellationToken token)
        {
            byte[] buf = new byte[4096];

            while (!token.IsCancellationRequested)
            {
                Socket s = udpSocket;
                if (s == null) break;

                try
                {
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    int n = s.ReceiveFrom(buf, ref ep);
                    string text = Encoding.UTF8.GetString(buf, 0, n);

                    if (text == "uspesno")
                    {
                        Dispatcher.Invoke(() => {AuthGrid.Visibility = Visibility.Collapsed;});
                        user.Prijavljen = true;
                        MessageBox.Show("Prijava uspesna!", "Uspesno", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if(text == "neuspesno")
                    {
                        MessageBox.Show("Neuspesan pokusaj prijave!", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch
                {
                    Dispatcher.Invoke(() => Disconnect());
                    break;
                }
            }
        }

        private void Disconnect()
        {
            try
            {
                if (cts != null) cts.Cancel();

                if (udpSocket != null)
                {
                    try { udpSocket.Shutdown(SocketShutdown.Both); } catch { }
                    try { udpSocket.Close(); } catch { }
                }
                udpSocket = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nije moguce prekinuti konekciju!\nGreska: {ex.Message}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void Prijava(object sender, RoutedEventArgs e)
        {
            try
            {
                string nick = LoginNickname.Text;
                string pw = LoginPassword.Password;

                user = new Korisnik(nick, pw, true);

                byte[] buffer = new byte[1024];
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, user);
                    buffer = ms.ToArray();
                    udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, udpServerEP);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nije moguce prijaviti se!\nGreska: {ex.Message}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void Registracija(object sender, RoutedEventArgs e)
        {

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
