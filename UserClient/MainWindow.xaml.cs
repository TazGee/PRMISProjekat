using Domain.Enumeratori;
using Domain.Interfejsi;
using Domain.Modeli;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace UserClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // -- Socketi koji ce se koristiti
        Socket tcpSocket;
        Socket udpSocket = null;

        // -- Za pokretanje threada za recieve podataka
        CancellationTokenSource cts;
        Task task;

        // -- EndPointi za TCP i UDP
        IPEndPoint tcpServerEP;
        IPEndPoint udpServerEP;

        // -- Konkretan korisnik (objekat) koji ce se koristiti na ovoj instanci
        Korisnik user;

        // -- UDP port koji se dobije od servera
        int udpPort;

        // -- Lista uredjaja koje server salje i trenutno izabran uredjaj
        public ObservableCollection<IDevice> ListaUredjaja { get; } = new ObservableCollection<IDevice>();
        IDevice selectedDevice = null;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        // -- Povezivanje na server
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

                udpServerEP = new IPEndPoint(ipAddress, 17000);

                ShowLogin(sender, e);
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Doslo je do greske prilikom povezivanja!\nGreska: {ex.SocketErrorCode}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        // -- Loop koji sluzi za primanje podataka od servera
        private void ReceiveLoop(CancellationToken token)
        {
            // -- Cuvanje EndPoint-a servera nakon primanja podataka
            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);

            // -- Petlja koja traje dok se ne zatrazi kraj
            while (!token.IsCancellationRequested)
            {
                // -- Ako UDP soket ne postoji
                if (udpSocket == null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Gasenje recieve loop-a (socket = null)!");
                    });
                    break;
                }

                // -- Primanje podataka od servera
                try
                {
                    // -- Buffer za primanje podataka
                    byte[] buf = new byte[10000];
                    int received = udpSocket.ReceiveFrom(buf, ref posiljaocEP);

                    Dispatcher.Invoke(() => {LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Server salje podatke...");});

                    // -- Kopiranje buffera u novi niz bajta
                    byte[] data = new byte[received];
                    Array.Copy(buf, data, received);

                    // -- Komanda u koju kasnije deserijalizujemo pristigle komande
                    Komanda komanda = new Komanda();
                    
                    // -- Koriscenje/deserijalizacija primljenih podataka
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryReader br = new BinaryReader(ms);
                        BinaryFormatter bf = new BinaryFormatter();
                        TipPodatka type = (TipPodatka)br.ReadByte();

                        if (type == TipPodatka.Komanda) // Ako je komanda
                        {
                            komanda = (Komanda)bf.Deserialize(ms);
                            Dispatcher.Invoke(() => { LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Server salje odgovor na komandu: {komanda.rezultatKomande} | {komanda.dodatnaPoruka}"); });
                        }
                        else if (type == TipPodatka.ListaUredjaja) // Ako je lista uredjaja
                        {
                            Dispatcher.Invoke(() => {LogBox.AppendText($"\n[{DateTime.Now:HH:mm:ss}] - Server salje listu uredjaja za azuriranje...");});

                            // -- Uzimanje broja uredjaja i pravljenje nove liste uredjaja
                            int count = br.ReadInt32();
                            var novaLista = new List<IDevice>();

                            // -- Petlja koja se izvrsava onoliko puta koliko server salje uredjaja
                            for (int i = 0; i < count; i++)
                            {
                                // -- Ocitavanje tipa uredjaja i pravljenje objekta za deserijalizaciju
                                TipPodatka tip = (TipPodatka)br.ReadByte();
                                IDevice uredjaj;

                                // -- Deserijalizacija podataka
                                if (tip == TipPodatka.Kapija) uredjaj = (Kapija)bf.Deserialize(ms);
                                else if (tip == TipPodatka.Klima) uredjaj = (Klima)bf.Deserialize(ms);
                                else uredjaj = (Svetla)bf.Deserialize(ms);

                                // -- Dodavanje uredjaja na listu
                                novaLista.Add(uredjaj);
                            }

                            // -- Azuriranje liste nakon prijema podataka
                            Dispatcher.Invoke(() =>
                            {
                                int? selectedId = selectedDevice?.GetID();

                                foreach (var novi in novaLista)
                                {
                                    var postojeci = ListaUredjaja.FirstOrDefault(x => x.GetID() == novi.GetID());

                                    if (postojeci == null)
                                    {
                                        ListaUredjaja.Add(novi);
                                    }
                                    else
                                    {
                                        UpdateDeviceState(postojeci, novi);
                                    }
                                }

                                for (int i = ListaUredjaja.Count - 1; i >= 0; i--)
                                {
                                    if (!novaLista.Any(x => x.GetID() == ListaUredjaja[i].GetID()))
                                        ListaUredjaja.RemoveAt(i);
                                }

                                if (selectedId != null)
                                {
                                    selectedDevice = ListaUredjaja.FirstOrDefault(x => x.GetID() == selectedId);
                                    DevicesList.SelectedItem = selectedDevice;
                                    OsveziPrikazSelektovanog();
                                }
                            });
                        }
                        else if (type == TipPodatka.Timeout) // Ako je podatak timeout
                        {
                            // -- Prekid konekcije sa serverom
                            Disconnect();

                            // -- Prikazivanje stranice za povezivanje
                            Dispatcher.Invoke(() =>
                            {
                                ConnectionGrid.Visibility = Visibility.Visible;
                                DevicesGrid.Visibility = Visibility.Collapsed;
                                AuthGrid.Visibility = Visibility.Collapsed;
                            });
                            MessageBox.Show("Server je ugasio vasu sesiju zbog neaktivnosti!", "Timeout", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else throw new Exception("Server je poslao nepoznat tip podatka");
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => { LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Desio se exception: {ex.Message}!"); });
                    Dispatcher.Invoke(() => Disconnect());
                    break;
                }
            }
            Dispatcher.Invoke(() => { LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Zavrsio se recieve loop!");});
        }

        // -- Azuriranje statusa uredjaja
        static void UpdateDeviceState(IDevice target, IDevice source)
        {
            if (target is Kapija t1 && source is Kapija s1)
            {
                t1.Name = s1.Name;
                t1.Otvorena = s1.Otvorena;
            }
            else if (target is Klima t2 && source is Klima s2)
            {
                t2.Name = s2.Name;
                t2.Upaljena = s2.Upaljena;
                t2.RezimRada = s2.RezimRada;
                t2.Temperatura = s2.Temperatura;
            }
            else if (target is Svetla t3 && source is Svetla s3)
            {
                t3.Name = s3.Name;
                t3.Upaljena = s3.Upaljena;
                t3.NijansaSvetla = s3.NijansaSvetla;
                t3.ProcenatOsvetljenja = s3.ProcenatOsvetljenja;
            }
        }

        // -- Osvezavanje prikaza izabranog uredjaja
        void OsveziPrikazSelektovanog()
        {
            // -- Ako je izabrani null preskace se sve
            if (selectedDevice == null) return;

            // -- Ako nije null azurira se WPF
            if (selectedDevice is Kapija k)
            {
                KapijaControl.Visibility = Visibility.Visible;
                KlimaControl.Visibility = Visibility.Collapsed;
                SvetlaControl.Visibility = Visibility.Collapsed;

                KapijaName.Text = $"Naziv: {k.Name}";
                KapijaStatus.Text = k.Otvorena ? "Status: Otvorena" : "Status: Zatvorena";
            }
            else if (selectedDevice is Klima kl)
            {
                KapijaControl.Visibility = Visibility.Collapsed;
                KlimaControl.Visibility = Visibility.Visible;
                SvetlaControl.Visibility = Visibility.Collapsed;

                KlimaName.Text = $"Naziv: {kl.Name}";
                KlimaStatus.Text = kl.Upaljena ? "Status: Upaljena" : "Status: Ugasena";
                KlimaRezim.Text = $"Rezim: {kl.RezimRada}";
                KlimaTemperatura.Text = $"Temperatura: {kl.Temperatura}C";
            }
            else if (selectedDevice is Svetla s)
            {
                KapijaControl.Visibility = Visibility.Collapsed;
                KlimaControl.Visibility = Visibility.Collapsed;
                SvetlaControl.Visibility = Visibility.Visible;

                SvetlaName.Text = $"Naziv: {s.Name}";
                SvetlaStatus.Text = s.Upaljena ? "Status: Upaljena" : "Status: Ugasena";
                SvetlaBoja.Text = $"Boja: {s.NijansaSvetla}";
                SvetlaSvetlina.Text = $"Svetlina: {s.ProcenatOsvetljenja}%";
            }
        }

        // -- Poziva se kada se promeni izabrani uredjaj
        void DevicesListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedDevice = DevicesList.SelectedItem as IDevice;
            OsveziPrikazSelektovanog();
        }

        // -- Prekid koneckije sa serverom
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
                if (tcpSocket != null)
                {
                    try { tcpSocket.Shutdown(SocketShutdown.Both); } catch { }
                    try { tcpSocket.Close(); } catch { }
                }
                tcpSocket = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nije moguce prekinuti konekciju!\nGreska: {ex.Message}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -- Funkcija za zahtevanje liste uredjaja od servera
        void ZahtevajUredjaje()
        {
            byte[] buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryFormatter bf = new BinaryFormatter();

                bw.Write((byte)TipPodatka.ZahtevListeUredjaja);
                bf.Serialize(ms, user);

                buffer = ms.ToArray();
                udpSocket.SendTo(buffer, udpServerEP);
            }
            LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesno poslat zahtev za dobijanje liste uredjaja! [{udpServerEP}]");
        }

        // -- Funkcija koju poziva dugme za prijavu
        void Prijava(object sender, RoutedEventArgs e)
        {
            try
            {
                string nick = LoginNickname.Text;
                string pw = LoginPassword.Password;

                user = new Korisnik(nick, pw, true);

                // -- Slanje poruke (korisnika) serveru 
                byte[] buffer;
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, user);
                    buffer = ms.ToArray();
                }
                tcpSocket.Send(buffer);

                // -- Ocekivanje odgovora od servera
                byte[] odgovor = new byte[16];
                int received = tcpSocket.Receive(odgovor);

                // -- Razumevanje podataka koje je server vratio i obrada
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

                // -- Cuvanje dobijenog porta u korisniku
                user.UDPPort = udpPort;

                // -- Kreiranje UDP soketa
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSocket.Bind(new IPEndPoint(IPAddress.Any, udpPort));

                LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesno kreiran soket sa portom {udpPort}!");

                // -- Skripvanje autentikacije i prikaz dela za kontrolu uredjaja
                Dispatcher.Invoke(() =>
                {
                    AuthGrid.Visibility = Visibility.Collapsed;
                    DevicesGrid.Visibility = Visibility.Visible;
                });
                LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesna prijava na server!");

                ZapocniRecieve();
                ZahtevajUredjaje();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exception! {ex.Message}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -- Registracija korisnika, slica/ista kao prijava
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

                user.UDPPort = udpPort;

                LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesno kreiran soket sa portom {udpPort}!");

                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSocket.Bind(new IPEndPoint(IPAddress.Any, udpPort));

                Dispatcher.Invoke(() =>
                {
                    AuthGrid.Visibility = Visibility.Collapsed;
                    DevicesGrid.Visibility = Visibility.Visible;
                });
                LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesna registracija na server!");

                ZapocniRecieve();
                ZahtevajUredjaje();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nije moguce registrovati se!\nGreska: {ex.Message}", "Greska", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // -- Funkcija koja zapocinje loop za dobijanje podataka od servera
        void ZapocniRecieve()
        {
            cts = new CancellationTokenSource();
            task = Task.Run(() => ReceiveLoop(cts.Token));
        }

        // -- Generisanje komande
        Komanda GenerisiKomandu(TipKomande tip)
        {
            Komanda komanda = new Komanda();
            komanda.tipKomande = tip;
            komanda.idKorisnika = user.Id;
            komanda.rezultatKomande = RezultatKomande.Slanje;
            komanda.dodatnaPoruka = "";
            LogBox.AppendText($"\n[{DateTime.Now.ToString("HH:mm:ss")}] - Uspesno generisana komanda {tip}!");
            return komanda;
        }

        // -- Slanje komande
        void PosaljiKomandu(Komanda komanda)
        {
            byte[] buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryFormatter bf = new BinaryFormatter();

                bw.Write((byte)TipPodatka.Komanda);
                bw.Write(selectedDevice.GetID());
                bf.Serialize(ms, komanda);
                bf.Serialize(ms, user);

                buffer = ms.ToArray();
                udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, udpServerEP);
            }
        }

        // -- Odavde na dole su funckije koje pozivaju dugmici za kontrolisanje uredjaja
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

        // -- Prikazivanje login-a ili register-a
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
