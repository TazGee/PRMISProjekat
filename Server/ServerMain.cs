using Domain.Enumeratori;
using Domain.Interfejsi;
using Domain.Modeli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

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

                foreach (Socket s in soketi)
                {
                    if (s == tcpSocket)
                    {
                        Socket clientSocket = tcpSocket.Accept();
                        IPEndPoint clientEP = (IPEndPoint)clientSocket.RemoteEndPoint;
                        Console.WriteLine($"Korisnik {clientEP} povezan preko TCP-a");

                        int udpPort = ++nextPort;
                        byte[] bytes = BitConverter.GetBytes(udpPort);

                        sc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        sc.Bind(new IPEndPoint(IPAddress.Any, udpPort));

                        clientSocket.Send(bytes);
                        clientSocket.Close();

                        Console.WriteLine($"Dodeljen UDP port {udpPort} korisniku {clientEP}");
                    }
                    else if (s == udpSocket)
                    {
                        int received = udpSocket.ReceiveFrom(buffer, ref posiljaocEP);

                        byte[] data = new byte[received];
                        Array.Copy(buffer, data, received);

                        IDevice uredjaj;

                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            BinaryReader br = new BinaryReader(ms);
                            BinaryFormatter bf = new BinaryFormatter();

                            TipUredjaja type = (TipUredjaja)br.ReadByte();

                            if (type == TipUredjaja.Kapija) uredjaj = (Kapija)bf.Deserialize(ms);
                            else if (type == TipUredjaja.Klima) uredjaj = (Klima)bf.Deserialize(ms);
                            else if (type == TipUredjaja.Svetla) uredjaj = (Svetla)bf.Deserialize(ms);
                            else throw new Exception("Nepoznat tip uređaja");
                        }

                        bool pronasao = false;
                        foreach (IDevice u in uredjaji.Values)
                        {
                            if(u.GetID() == uredjaj.GetID())
                            {
                                pronasao = true;

                                

                                break;
                            }
                        }
                        if(!pronasao)
                        {
                            uredjaji[(IPEndPoint)posiljaocEP] = uredjaj;

                            Console.WriteLine($"Povezao se novi uredjaj: {uredjaj.GetProperties()}");
                            Console.WriteLine($"Primljeno od {posiljaocEP}");
                        }
                    }
                    else
                    {
                        int received = s.ReceiveFrom(buffer, ref posiljaocEP);

                        byte[] data = new byte[received];
                        Array.Copy(buffer, data, received);

                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            BinaryReader br = new BinaryReader(ms);

                            TipUdpPoruke tipPoruke = (TipUdpPoruke)br.ReadByte();

                            if(tipPoruke == TipUdpPoruke.Korisnik)
                            {
                                Korisnik korisnik = (Korisnik)bf.Deserialize(ms);
                                byte[] binarnaPoruka = Encoding.UTF8.GetBytes(ObradiKorisnika(korisnik, listaKorisnika));
                                int brBajta = s.SendTo(binarnaPoruka, 0, binarnaPoruka.Length, SocketFlags.None, posiljaocEP);
                            }
                            else
                            {

                            }
                        }
                    }
                }
                if(sc != null)
                {
                    korisnickiSocketi.Add(sc);
                }
            }
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
