using Domain.Enumeratori;
using Domain.Interfejsi;
using Domain.Modeli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

namespace Server
{
    internal class ServerMain
    {
        // -- Recnik koji za svakog korisnika cuva IPEndPoint na koji treba da odgovara
        static Dictionary<Korisnik, IPEndPoint> korisnickeSesije = new Dictionary<Korisnik, IPEndPoint>();

        // -- Lista korisnika iz "baze podataka"
        static List<Korisnik> listaKorisnika = new List<Korisnik>
        {
            new Korisnik("Nikola Tasic", "taske", "123", false),
            new Korisnik("Jovana Boljanovic", "joki", "123", false)
        };

        // -- Recnik koji za cuva uredjaje i IPEndPointe
        static Dictionary<IPEndPoint, IDevice> uredjaji = new Dictionary<IPEndPoint, IDevice>();

        // -- Promenljive i recnik neophodni za realizaciju sistema neaktivnosti
        static int trenutnaIteracija = 0;
        static int maxNeaktivnihIteracija = 50;
        static Dictionary<Korisnik, int> poslednjaAktivnost = new Dictionary<Korisnik, int>();

        static void Main(string[] args)
        {
            // -- Neki od portova
            int devicePort = 5000;
            int tcpHandshakePort = 4000;

            Console.ForegroundColor = ConsoleColor.Magenta;

            // -- Soketi koji se koriste za primanje i slanje podataka
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket korisnickiSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // -- Bindovanje soketa na ip i port
            udpSocket.Bind(new IPEndPoint(IPAddress.Any, devicePort));
            korisnickiSocket.Bind(new IPEndPoint(IPAddress.Any, 17000));
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, tcpHandshakePort));

            // -- Stavljanje UDP soketa u neblokirajuci rezim
            udpSocket.Blocking = false;
            korisnickiSocket.Blocking = false;

            // -- TCP soket listen - broj komandi koje slusa
            tcpSocket.Listen(15);

            // -- Sledeci port od kog se generise 
            int nextPort = 5001;

            // -- Cuvanje EndPointa posiljaoca poruka
            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);

            // -- Glavna petlja za obradu podataka
            while (true)
            {
                // -- Uvecavanje trenutnog broja iteracije
                trenutnaIteracija++;

                // -- Ispis uredjaja i sesija
                Ispis(uredjaji, korisnickeSesije);

                // -- Multipleksiranje soketa
                if(tcpSocket.Poll(100 * 1000, SelectMode.SelectRead)) // TCP soket
                {
                    // -- Pravljenje buffera za ucitavanje podataka
                    byte[] buffer = new byte[4096];
                    Socket clientSocket = tcpSocket.Accept();
                    IPEndPoint clientEP = (IPEndPoint)clientSocket.RemoteEndPoint;

                    // -- Cuvanje koliko podataka je pristiglo na soketu
                    int received = clientSocket.Receive(buffer);

                    // -- Deserijalizacija korisnika u objekat
                    Korisnik korisnik;
                    using (MemoryStream ms = new MemoryStream(buffer, 0, received))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        korisnik = (Korisnik)bf.Deserialize(ms);
                    }

                    // -- Cuvanje rezultata obrade korisnika
                    bool rezultat = ObradiKorisnika(korisnik, listaKorisnika);

                    // -- Ako je korisnik uspesno obradjen (prijavljen)
                    if (rezultat)
                    {
                        // -- Racunanje UDP porta koji se dodeljuje korisniku
                        int udpPort = ++nextPort;

                        // -- Slanje odgovora korisniku
                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter bw = new BinaryWriter(ms);
                            bw.Write(true);
                            bw.Write(udpPort);
                            byte[] odgovor = ms.ToArray();
                            clientSocket.Send(odgovor);
                        }

                        // -- Postavljanje UDP porta korisniku, dodavanje korisnika u sesiju i cuvanje poslednje aktivnosti
                        korisnik.UDPPort = udpPort;
                        korisnickeSesije.Add(korisnik, new IPEndPoint(clientEP.Address, udpPort));
                        poslednjaAktivnost[korisnik] = trenutnaIteracija;
                    }
                    else
                    {
                        // -- Slanje odgovora da nije uspesno logovanje
                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter bw = new BinaryWriter(ms);
                            bw.Write(false);
                            byte[] odgovor = ms.ToArray();
                            clientSocket.Send(odgovor);
                        }
                    }
                }

                if(udpSocket.Poll(100 * 1000, SelectMode.SelectRead)) // UDP soket uredjaja
                {
                    // -- Pravljenje buffera i ucitavanje u njega
                    byte[] buffer = new byte[4096];
                    int received = udpSocket.ReceiveFrom(buffer, ref posiljaocEP);
                    
                    // -- Kopiranje sadrzaja buffera u novi niz bajta
                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);

                    // -- Pravljenje objekata u koji kasnije deserijalizujemo
                    IDevice uredjaj = null;
                    Komanda komanda = new Komanda();

                    // -- Deserijalizacija podataka po tipu
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryReader br = new BinaryReader(ms);
                        BinaryFormatter bf = new BinaryFormatter();

                        // -- Citanje bajta da vidimo koji je tip podatka u pitanju
                        TipPodatka type = (TipPodatka)br.ReadByte();

                        // -- Pregled koji je tip podatka u pitanju i rad po tome
                        if (type == TipPodatka.Kapija) uredjaj = (Kapija)bf.Deserialize(ms);
                        else if (type == TipPodatka.Klima) uredjaj = (Klima)bf.Deserialize(ms);
                        else if (type == TipPodatka.Svetla) uredjaj = (Svetla)bf.Deserialize(ms);
                        else if (type == TipPodatka.Komanda)
                        {
                            komanda = (Komanda)bf.Deserialize(ms);
                            type = (TipPodatka)br.ReadByte();
                            if (type == TipPodatka.Kapija) uredjaj = (Kapija)bf.Deserialize(ms);
                            else if (type == TipPodatka.Klima) uredjaj = (Klima)bf.Deserialize(ms);
                            else if (type == TipPodatka.Svetla) uredjaj = (Svetla)bf.Deserialize(ms);
                        }
                        else throw new Exception("Nepoznat tip uređaja");
                    }

                    // -- Ako je uredjaj deserijalizovan
                    if (uredjaj != null)
                    {
                        // -- Trazi se i cuva se EndPoint tog uredjaja
                        IPEndPoint pronasao = null;
                        foreach (IPEndPoint ip in uredjaji.Keys)
                        {
                            if (uredjaji[ip].GetID() == uredjaj.GetID())
                            {
                                pronasao = ip;
                                break;
                            }
                        }

                        // -- Ako nije pronadjen takav EndPoint onda se dodaje na listu uredjaja
                        if (pronasao == null)
                        {
                            uredjaji[(IPEndPoint)posiljaocEP] = uredjaj;

                            foreach (IPEndPoint ip in korisnickeSesije.Values)
                                PosaljiListuUredjaja(udpSocket, ip);
                        }
                        else // Ako je pronadjen onda se replace-uje
                        {
                            uredjaji[pronasao] = uredjaj;

                            // -- Pretraga EndPointa za korisnika kom treba da se prosledi komanda
                            IPEndPoint korisnikEP = null;
                            foreach (Korisnik k in korisnickeSesije.Keys)
                            {
                                if (k.Id == komanda.idKorisnika)
                                {
                                    korisnikEP = korisnickeSesije[k];
                                    break;
                                }
                            }

                            // -- Ako nije pronadjen skipuje se iteracija
                            if (korisnikEP == null) continue;

                            // -- Prosledjivanje komande korisniku i prosledjivanje svih uredjaja svim korisnicima
                            using (MemoryStream ms = new MemoryStream())
                            {
                                BinaryWriter bw = new BinaryWriter(ms);
                                BinaryFormatter bf = new BinaryFormatter();

                                bw.Write((byte)TipPodatka.Komanda);
                                bf.Serialize(ms, komanda);

                                buffer = ms.ToArray();
                                udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, korisnikEP);

                                if (komanda.rezultatKomande == RezultatKomande.Uspesna)
                                {
                                    foreach(IPEndPoint ip in korisnickeSesije.Values)
                                        PosaljiListuUredjaja(udpSocket, ip);
                                }
                            }
                        }
                    }
                }

                if (korisnickiSocket.Poll(100 * 1000, SelectMode.SelectRead)) // Korisnicki UDP soket
                {
                    // -- Pravljenje buffera i ucitavanje u njega
                    byte[] buffer = new byte[4096];
                    int received = korisnickiSocket.ReceiveFrom(buffer, ref posiljaocEP);

                    // -- Kopiranje buffera u novi niz bajta
                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);

                    // -- Objekti u koje se kasnije deserijalizuje
                    Komanda komanda = new Komanda();
                    Korisnik korisnik = null;

                    // -- Citanje podataka i prosledjivanje
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryReader br = new BinaryReader(ms);
                        BinaryFormatter bf = new BinaryFormatter();
                        TipPodatka type = (TipPodatka)br.ReadByte();
                        if(type == TipPodatka.ZahtevListeUredjaja) // Ako je zahtev za listu uredjaja
                        {
                            // -- Slanje liste uredjaja na EndPoint korisnika koji je trazio uredjaje
                            korisnik = (Korisnik)bf.Deserialize(ms);
                            IPEndPoint zaSlanje = new IPEndPoint(((IPEndPoint)posiljaocEP).Address, korisnik.UDPPort);
                            PosaljiListuUredjaja(korisnickiSocket, zaSlanje);
                        }
                        else if(type == TipPodatka.Komanda) // Ako korisnik salje komandu
                        {
                            // -- Ako korisnik salje komandu deserijalizuju se komanda i korisnik koji je salje
                            int idUredjaja = br.ReadInt32();
                            komanda = (Komanda)bf.Deserialize(ms);
                            korisnik = (Korisnik)bf.Deserialize(ms);

                            // -- Slanje komande uredjaju
                            using (MemoryStream ms1 = new MemoryStream())
                            {
                                BinaryFormatter bf1 = new BinaryFormatter();
                                bf1.Serialize(ms1, komanda);
                                IPEndPoint zaSlanje = null;
                                foreach (IPEndPoint ep in uredjaji.Keys)
                                {
                                    if (uredjaji[ep].GetID() == idUredjaja)
                                    {
                                        zaSlanje = ep;
                                        break;
                                    }
                                }
                                
                                if (zaSlanje == null) { return; }

                                buffer = ms1.ToArray();
                                udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, zaSlanje);
                            }
                        }
                    }

                    // -- Ako je nesto deserijalizovano u objekat korisnik ponistava mu se neaktivnost
                    if (korisnik != null)
                    {
                        long id = korisnik.Id;
                        korisnik = null;
                        foreach (Korisnik k in poslednjaAktivnost.Keys)
                        {
                            if(k.Id == id)
                            {
                                korisnik = k;
                            }
                        }
                        if(korisnik != null) poslednjaAktivnost[korisnik] = trenutnaIteracija;
                    }
                }

                // -- Lista onih koji su presli granicu neaktivnosti
                List<Korisnik> neaktivni = new List<Korisnik>();

                // -- Dodavanje neaktivnih na listu
                foreach (var par in poslednjaAktivnost)
                {
                    if (trenutnaIteracija - par.Value > maxNeaktivnihIteracija)
                        neaktivni.Add(par.Key);
                }

                // -- Kreiranje buffera za slanje
                byte[] buff = new byte[1024];

                // -- Slanje timeout komande svakom korisniku koji je neaktivan i odjavljivanje korisnika
                foreach (var k in neaktivni)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        BinaryWriter bw = new BinaryWriter(ms);
                        bw.Write((byte)TipPodatka.Timeout);
                        buff = ms.ToArray();
                        foreach(Korisnik kr in korisnickeSesije.Keys)
                        {
                            if(kr.Nickname == k.Nickname)
                            {
                                udpSocket.SendTo(buff, 0, buff.Length, SocketFlags.None, korisnickeSesije[kr]);
                            }
                        }
                        foreach (Korisnik kr in listaKorisnika)
                        {
                            if (kr.Nickname == k.Nickname)
                            {
                                kr.Prijavljen = false;
                            }
                        }
                    }

                    // -- Brisanje sadrzaja sa sesija i poslednje aktivnosti gde je korisnik k kljuc
                    korisnickeSesije.Remove(k);
                    poslednjaAktivnost.Remove(k);
                }
            }
        }

        // -- Ispis svega na konzoli
        static void Ispis(Dictionary<IPEndPoint, IDevice> listaUredjaja, Dictionary<Korisnik, IPEndPoint> listaSesija)
        {
            Console.Clear();
            Console.WriteLine("##= LISTA UREDJAJA =##");
            foreach (IPEndPoint ip in listaUredjaja.Keys)
            {
                Console.WriteLine($"[{ip.Address}:{ip.Port}]\t{listaUredjaja[ip].GetProperties()}");
            }
            Console.WriteLine("\n##= LISTA KORISNICKIH SESIJA =##");
            foreach (Korisnik k in listaSesija.Keys)
            {
                Console.WriteLine($"[ID-{k.Id}]\t{k.GetProperties()}");
            }
            Console.WriteLine($"\n##= TRENUTNA ITERACIJA: {trenutnaIteracija} =##");
        }

        // -- Slanje liste uredjaja
        static void PosaljiListuUredjaja(Socket socket, IPEndPoint korisnikEP)
        {
            byte[] buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryFormatter bf = new BinaryFormatter();

                bw.Write((byte)TipPodatka.ListaUredjaja);
                bw.Write((int)uredjaji.Values.Count);

                foreach (var u in uredjaji.Values)
                {
                    bw.Write((byte)u.DeviceType);
                    bf.Serialize(ms, u);
                }
                buffer = ms.ToArray();
                socket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, korisnikEP);
            }
        }

        // -- Obradjivanje korisnika
        static bool ObradiKorisnika(Korisnik korisnik, List<Korisnik> listaKorisnika)
        {
            if (korisnik.Login)
            {
                foreach (Korisnik k in listaKorisnika)
                {
                    if (k.Nickname == korisnik.Nickname && k.Password == korisnik.Password && !k.Prijavljen)
                    {
                        k.Prijavljen = true;
                        return true;
                    }
                }
                return false;
            }
            else
            {
                foreach (Korisnik k in listaKorisnika)
                {
                    if (k.Nickname == korisnik.Nickname)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
