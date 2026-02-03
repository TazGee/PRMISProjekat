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

            udpSocket.Bind(new IPEndPoint(IPAddress.Any, 5000));
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, 4000));

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
                            BinaryFormatter bf = new BinaryFormatter();
                            uredjaj = (IDevice)bf.Deserialize(ms);
                        }

                        bool pronasao = false;
                        foreach (IDevice u in uredjaji.Values)
                        {
                            if(u.GetID() == uredjaj.GetID())
                            {
                                // TODO: Uredjaj komunicira
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

                        Korisnik korisnik;
                        using (MemoryStream ms = new MemoryStream(data))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            korisnik = (Korisnik)bf.Deserialize(ms);
                        }

                        if(korisnik.Login)
                        {
                            bool pronasao = false;
                            foreach(Korisnik k in listaKorisnika)
                            {
                                if(k.Nickname == korisnik.Nickname && k.Password == korisnik.Password && !k.Prijavljen)
                                {
                                    pronasao = true;
                                    k.Prijavljen = true;
                                    break;
                                }
                            }

                            string poruka;
                            if (pronasao)
                            {
                                poruka = "uspesno";
                            }
                            else
                            {
                                poruka = "neuspesno";
                            }

                            Console.WriteLine($"Status prijave korisnika: {poruka}");
                            byte[] binarnaPoruka = Encoding.UTF8.GetBytes(poruka);
                            int brBajta = s.SendTo(binarnaPoruka, 0, binarnaPoruka.Length, SocketFlags.None, posiljaocEP);
                        }
                        else
                        {
                            // TODO: Registracija
                        }
                    }
                }
                if(sc != null)
                {
                    korisnickiSocketi.Add(sc);
                }
            }
        }
    }
}
