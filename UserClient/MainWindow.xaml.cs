using Domain.Enumeratori;
using Domain.Interfejsi;
using Domain.Modeli;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;

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

        List<IDevice> listaUredjaja = new List<IDevice>();
        IDevice selectedDevice = null;

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

            // -- Definisanje tcp socketa i endpointa
            tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpServerEP = new IPEndPoint(ipAddress, port);

            // -- Povezivanje
            try
            {
                tcpSocket.Connect(tcpServerEP);

                // -- Prikazivanje grida za prijavu
                ConnectionGrid.Visibility = Visibility.Collapsed;
                AuthGrid.Visibility = Visibility.Visible;

                cts = new CancellationTokenSource();
                task = Task.Run(() => ReceiveLoop(cts.Token));

                ShowLogin(sender, e);
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Doslo je do greske prilikom povezivanja!\nGreska: {ex.SocketErrorCode}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private void ReceiveLoop(CancellationToken token)
        {
            byte[] buf = new byte[4096];
            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);

            while (!token.IsCancellationRequested)
            {
                Socket s = udpSocket;
                if (s == null) break;

                try
                {
                    int received = udpSocket.ReceiveFrom(buf, ref posiljaocEP);

                    byte[] data = new byte[received];
                    Array.Copy(buf, data, received);

                    Komanda komanda = new Komanda();

                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryReader br = new BinaryReader(ms);
                        BinaryFormatter bf = new BinaryFormatter();
                        TipPodatka type = (TipPodatka)br.ReadByte();

                        if (type == TipPodatka.Komanda)  komanda = (Komanda)bf.Deserialize(ms);
                        else if(type == TipPodatka.ListaUredjaja)
                        {
                            // TO DO
                        }
                        else throw new Exception("Server je poslao nepoznat tip podatka!");
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
            string nick = LoginNickname.Text;
            string pw = LoginPassword.Password;

            user = new Korisnik(nick, pw, true);

            byte[] buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, user);
                buffer = ms.ToArray();
            }

            tcpSocket.Send(buffer);

            byte[] odgovor = new byte[16];
            int received = tcpSocket.Receive(odgovor);

            using (MemoryStream ms = new MemoryStream(odgovor, 0, received))
            {
                BinaryReader br = new BinaryReader(ms);
                bool ok = br.ReadBoolean();

                if (!ok)
                {
                    MessageBox.Show("Neuspesan pokusaj prijave!", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                udpPort = br.ReadInt32();
            }

            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            udpServerEP = new IPEndPoint(tcpServerEP.Address, udpPort);

            cts = new CancellationTokenSource();
            task = Task.Run(() => ReceiveLoop(cts.Token));

            Dispatcher.Invoke(() =>
            {
                AuthGrid.Visibility = Visibility.Collapsed;
                DevicesGrid.Visibility = Visibility.Visible;
            });
            LogBox.AppendText($"[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesna prijava na server!");
        }

        void Registracija(object sender, RoutedEventArgs e)
        {
            try
            {
                string imeprezime = RegisterFirstLastName.Text;
                string nick = RegisterNickname.Text;
                string pw = RegisterPassword.Password;

                user = new Korisnik(imeprezime, nick, pw, false);

                byte[] buffer;
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, user);
                    buffer = ms.ToArray();
                }

                tcpSocket.Send(buffer);

                byte[] odgovor = new byte[16];
                int received = tcpSocket.Receive(odgovor);

                using (MemoryStream ms = new MemoryStream(odgovor, 0, received))
                {
                    BinaryReader br = new BinaryReader(ms);
                    bool ok = br.ReadBoolean();

                    if (!ok)
                    {
                        MessageBox.Show("Neuspesan pokusaj registracije!", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    udpPort = br.ReadInt32();
                }

                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                udpServerEP = new IPEndPoint(tcpServerEP.Address, udpPort);

                cts = new CancellationTokenSource();
                task = Task.Run(() => ReceiveLoop(cts.Token));

                Dispatcher.Invoke(() =>
                {
                    AuthGrid.Visibility = Visibility.Collapsed;
                    DevicesGrid.Visibility = Visibility.Visible;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nije moguce registrovati se!\nGreska: {ex.Message}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        Komanda GenerisiKomandu(TipKomande tip)
        {
            Komanda komanda = new Komanda();
            komanda.tipKomande = tip;
            komanda.idKorisnika = user.Id;
            komanda.rezultatKomande = RezultatKomande.Slanje;
            komanda.dodatnaPoruka = "";
            LogBox.AppendText($"[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesno generisana komanda {tip}!");
            return komanda;
        }

        void PosaljiKomandu(Komanda komanda)
        {
            byte[] buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryFormatter bf = new BinaryFormatter();

                bw.Write((byte)TipPodatka.Komanda);
                bf.Serialize(ms, komanda);

                buffer = ms.ToArray();
                udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, udpServerEP);
            }
        }

        void KapijaToggle(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.KapijaToggle);
            PosaljiKomandu(komanda);
        }

        void KlimaToggle(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.KlimaToggle);
            PosaljiKomandu(komanda);
        }

        void KlimaRezimToggle(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.KlimaRezim);
            PosaljiKomandu(komanda);
        }
        
        void KlimaUvecajTemp(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.KlimaUvecajTemp);
            PosaljiKomandu(komanda);
        }

        void KlimaSmanjiTemp(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.KlimaSmanjiTemp);
            PosaljiKomandu(komanda);
        }

        void SvetlaToggle(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.SvetlaToggle);
            PosaljiKomandu(komanda);
        }

        void SvetlaBojaToggle(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.SvetlaBoja);
            PosaljiKomandu(komanda);
        }

        void SvetlaPojacajOsv(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.SvetlaPovecajOsvetljenje);
            PosaljiKomandu(komanda);
        }

        void SvetlaSmanjiOsv(object sender, RoutedEventArgs e)
        {
            Komanda komanda = GenerisiKomandu(TipKomande.SvetlaSmanjiOsvetljenje);
            PosaljiKomandu(komanda);
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
