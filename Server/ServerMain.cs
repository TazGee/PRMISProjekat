using Domain.Enumeratori;
using Domain.Interfejsi;
using Domain.Modeli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;

namespace Server
{
    internal class ServerMain
    {
        static Dictionary<Korisnik, IPEndPoint> korisnickeSesije = new Dictionary<Korisnik, IPEndPoint>();

        static List<Korisnik> listaKorisnika = new List<Korisnik>
        {
            new Korisnik("Nikola Tasic", "taske", "123", false),
            new Korisnik("Jovana Boljanovic", "joki", "123", false)
        };

        static Dictionary<IPEndPoint, IDevice> uredjaji = new Dictionary<IPEndPoint, IDevice>();

        static int trenutnaIteracija = 0;
        static int maxNeaktivnihIteracija = 50;
        static Dictionary<Korisnik, int> poslednjaAktivnost = new Dictionary<Korisnik, int>();

        static void Main(string[] args)
        {
            int devicePort = 5000;
            int tcpHandshakePort = 4000;

            Console.ForegroundColor = ConsoleColor.Magenta;

            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket korisnickiSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            udpSocket.Bind(new IPEndPoint(IPAddress.Any, devicePort));
            korisnickiSocket.Bind(new IPEndPoint(IPAddress.Any, 17000));
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, tcpHandshakePort));

            udpSocket.Blocking = false;
            korisnickiSocket.Blocking = false;

            tcpSocket.Listen(15);

            int nextPort = 15001;

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                trenutnaIteracija++;

                Ispis(uredjaji, korisnickeSesije);

                Korisnik kor = null;

                if(tcpSocket.Poll(100 * 1000, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[4096];
                    Socket clientSocket = tcpSocket.Accept();
                    IPEndPoint clientEP = (IPEndPoint)clientSocket.RemoteEndPoint;

                    int received = clientSocket.Receive(buffer);

                    Korisnik korisnik;
                    using (MemoryStream ms = new MemoryStream(buffer, 0, received))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        korisnik = (Korisnik)bf.Deserialize(ms);
                    }

                    bool rezultat = ObradiKorisnika(korisnik, listaKorisnika);

                    if (rezultat)
                    {
                        int udpPort = ++nextPort;

                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter bw = new BinaryWriter(ms);
                            bw.Write(true);
                            bw.Write(udpPort);
                            byte[] odgovor = ms.ToArray();
                            clientSocket.Send(odgovor);
                        }

                        kor = korisnik;
                        korisnik.UDPPort = udpPort;
                        korisnickeSesije.Add(korisnik, new IPEndPoint(clientEP.Address, udpPort));
                        poslednjaAktivnost[korisnik] = trenutnaIteracija;
                    }
                    else
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter bw = new BinaryWriter(ms);
                            bw.Write(false);
                            byte[] odgovor = ms.ToArray();
                            clientSocket.Send(odgovor);
                        }
                    }
                }

                if(udpSocket.Poll(100 * 1000, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[4096];
                    int received = udpSocket.ReceiveFrom(buffer, ref posiljaocEP);
                    
                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);

                    IDevice uredjaj = null;
                    Komanda komanda = new Komanda();

                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryReader br = new BinaryReader(ms);
                        BinaryFormatter bf = new BinaryFormatter();

                        TipPodatka type = (TipPodatka)br.ReadByte();

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

                    if (uredjaj != null)
                    {
                        IPEndPoint pronasao = null;
                        foreach (IPEndPoint ip in uredjaji.Keys)
                        {
                            if (uredjaji[ip].GetID() == uredjaj.GetID())
                            {
                                pronasao = ip;
                                break;
                            }
                        }
                        if (pronasao == null)
                        {
                            uredjaji[(IPEndPoint)posiljaocEP] = uredjaj;

                            foreach (IPEndPoint ip in korisnickeSesije.Values)
                                PosaljiListuUredjaja(udpSocket, ip);
                        }
                        else
                        {
                            uredjaji[pronasao] = uredjaj;

                            IPEndPoint korisnikEP = null;

                            foreach (Korisnik k in korisnickeSesije.Keys)
                            {
                                if (k.Id == komanda.idKorisnika)
                                {
                                    korisnikEP = korisnickeSesije[k];
                                    break;
                                }
                            }

                            if (korisnikEP == null) continue;

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

                if (korisnickiSocket.Poll(100 * 1000, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[4096];
                    int received = korisnickiSocket.ReceiveFrom(buffer, ref posiljaocEP);
                    
                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);

                    Komanda komanda = new Komanda();
                    Korisnik korisnik = null;

                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryReader br = new BinaryReader(ms);
                        BinaryFormatter bf = new BinaryFormatter();
                        TipPodatka type = (TipPodatka)br.ReadByte();
                        if(type == TipPodatka.ZahtevListeUredjaja)
                        {
                            korisnik = (Korisnik)bf.Deserialize(ms);
                            IPEndPoint zaSlanje = new IPEndPoint(((IPEndPoint)posiljaocEP).Address, korisnik.UDPPort);
                            PosaljiListuUredjaja(korisnickiSocket, zaSlanje);
                        }
                        else if(type == TipPodatka.Komanda)
                        {
                            int idUredjaja = br.ReadInt32();
                            komanda = (Komanda)bf.Deserialize(ms);
                            korisnik = (Korisnik)bf.Deserialize(ms);

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

                List<Korisnik> neaktivni = new List<Korisnik>();

                foreach (var par in poslednjaAktivnost)
                {
                    if (trenutnaIteracija - par.Value > maxNeaktivnihIteracija)
                        neaktivni.Add(par.Key);
                }

                byte[] buff = new byte[1024];

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

                    korisnickeSesije.Remove(k);
                    poslednjaAktivnost.Remove(k);
                }
            }
        }

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
