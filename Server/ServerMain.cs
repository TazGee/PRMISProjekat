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
        static void Main(string[] args)
        {
            int devicePort = 5000;
            int tcpHandshakePort = 4000;

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Server started...");

            List<Korisnik> listaKorisnika = new List<Korisnik>
            {
                new Korisnik("Nikola Tasic", "taske", "123", false),
                new Korisnik("Jovana Boljanovic", "joki", "123", false)
            };

            Dictionary<Korisnik, IPEndPoint> korisnickeSesije = new Dictionary<Korisnik, IPEndPoint>();

            List<Socket> korisnickiSocketi = new List<Socket>();

            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Dictionary<IPEndPoint, IDevice> uredjaji = new Dictionary<IPEndPoint, IDevice>();

            udpSocket.Bind(new IPEndPoint(IPAddress.Any, devicePort));
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, tcpHandshakePort));

            udpSocket.Blocking = false;

            tcpSocket.Listen(15);

            int nextPort = 5001;

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[1024];
            
            while (true)
            {
                List<Socket> soketi = new List<Socket> { tcpSocket, udpSocket };
                soketi.AddRange(korisnickiSocketi);

                Socket.Select(soketi, null, null, 1_000_000);

                Socket sc = null;
                Korisnik kor = null;

                foreach (Socket s in soketi)
                {
                    if (s == tcpSocket)
                    {
                        Socket clientSocket = tcpSocket.Accept();
                        IPEndPoint clientEP = (IPEndPoint)clientSocket.RemoteEndPoint;

                        int received = clientSocket.Receive(buffer);

                        Korisnik korisnik;
                        using (MemoryStream ms = new MemoryStream(buffer, 0, received))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            korisnik = (Korisnik)bf.Deserialize(ms);
                        }

                        string rezultat = ObradiKorisnika(korisnik, listaKorisnika);

                        if (rezultat == "uspesno" || rezultat == "regUspesno")
                        {
                            int udpPort = ++nextPort;

                            using (MemoryStream ms = new MemoryStream())
                            {
                                BinaryWriter bw = new BinaryWriter(ms);
                                bw.Write((byte)TipPodatka.Poruka);    
                                bw.Write(udpPort);    
                                byte[] odgovor = ms.ToArray();
                                clientSocket.Send(odgovor);
                            }

                            kor = korisnik;
                            sc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            sc.Bind(new IPEndPoint(IPAddress.Any, udpPort));
                            korisnickeSesije.Add(korisnik, (IPEndPoint)clientSocket.RemoteEndPoint);

                            //PosaljiListuUredjaja(sc, uredjaji);

                            Console.WriteLine($"Korisnik {korisnik.Nickname} prijavljen, UDP port {udpPort}");
                        }
                        else
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                BinaryWriter bw = new BinaryWriter(ms);
                                bw.Write((byte)TipPodatka.Poruka);
                                byte[] odgovor = ms.ToArray();
                                clientSocket.Send(odgovor);
                            }
                        }

                        //clientSocket.Close();
                    }
                    else if (s == udpSocket)
                    {
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

                        if(uredjaj != null)
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

                                Console.WriteLine($"Povezao se novi uredjaj: {uredjaj.GetProperties()}");
                                Console.WriteLine($"Primljeno od {posiljaocEP}");
                            }
                            else
                            {
                                uredjaji[pronasao] = uredjaj;
                                Console.WriteLine($"{komanda.dodatnaPoruka}");

                                IPEndPoint korisnikEP = null;

                                foreach(Korisnik k in korisnickeSesije.Keys)
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
                                }
                            }
                        }
                    }
                    else
                    {
                        int received = udpSocket.ReceiveFrom(buffer, ref posiljaocEP);

                        byte[] data = new byte[received];
                        Array.Copy(buffer, data, received);

                        Komanda komanda;
                        komanda.idKorisnika = -1;

                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            komanda = (Komanda)bf.Deserialize(ms);
                        }

                        if (komanda.idKorisnika != -1)
                        {
                            Korisnik k = null;
                            foreach (Korisnik korisnik in korisnickeSesije.Keys)
                            {
                                if(komanda.idKorisnika == k.Id)
                                {
                                    k = korisnik;
                                    break;
                                }
                            }

                            if(k != null)
                            {
                                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] - Korisnik {k.Nickname} je uradio komandu: {komanda.dodatnaPoruka}");

                                using (MemoryStream ms = new MemoryStream())
                                {
                                    BinaryWriter bw = new BinaryWriter(ms);
                                    BinaryFormatter bf = new BinaryFormatter();

                                    bw.Write((byte)TipPodatka.Komanda);
                                    bf.Serialize(ms, komanda);

                                    buffer = ms.ToArray();
                                    udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, korisnickeSesije[k]);
                                }
                            }
                        }
                    }
                }
                if(sc != null && kor != null)
                {
                    korisnickiSocketi.Add(sc);
                    Console.WriteLine($"Novi korisnik {kor.Nickname} dodat na dictionary sa socketima...");
                }
            }
        }

        static void PosaljiListuUredjaja(Socket korisnickiSocket, Dictionary<IPEndPoint, IDevice> uredjaji)
        {
            byte[] buffer;
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryFormatter bf = new BinaryFormatter();

                bw.Write((int)uredjaji.Count);

                foreach (var u in uredjaji.Values)
                {
                    bw.Write((byte)u.DeviceType);
                    bf.Serialize(ms, u);
                }

                buffer = ms.ToArray();
                korisnickiSocket.Send(buffer);
            }
        }

        static void AzurirajUredjaj()
        {
            
        }

        static string ObradiKorisnika(Korisnik korisnik, List<Korisnik> listaKorisnika)
        {
            if (korisnik.Login)
            {
                bool pronasao = false;
                foreach (Korisnik k in listaKorisnika)
                {
                    if (k.Nickname == korisnik.Nickname && k.Password == korisnik.Password && !k.Prijavljen)
                    {
                        pronasao = true;
                        k.Prijavljen = true;
                        break;
                    }
                }

                if (pronasao)
                {
                    Console.WriteLine($"Status prijave korisnika: uspesno | {korisnik.Nickname}");
                    return "uspesno";
                }
                else
                {
                    Console.WriteLine($"Status prijave korisnika: neuspesno | {korisnik.Nickname}");
                    return "neuspesno";
                }
            }
            else
            {
                bool pronasao = false;
                foreach (Korisnik k in listaKorisnika)
                {
                    if (k.Nickname == korisnik.Nickname)
                    {
                        pronasao = true;
                        break;
                    }
                }

                if (pronasao)
                {
                    Console.WriteLine($"Status registracije korisnika: regNeuspesno | {korisnik.Nickname}");
                    return "regNeuspesno";
                }
                else
                {
                    Console.WriteLine($"Status registracije korisnika: regUspesno | {korisnik.Nickname}");
                    listaKorisnika.Add(korisnik);
                    korisnik.Prijavljen = true;
                    return "regUspesno";
                }
            }
        }
    }
}
