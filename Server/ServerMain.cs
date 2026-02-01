using Domain.Interfejsi;
using System;
using System.Collections;
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
            Console.WriteLine("Server started...");

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Dictionary<IPEndPoint, IDevice> uredjaji = new Dictionary<IPEndPoint, IDevice>();
            socket.Bind(new IPEndPoint(IPAddress.Any, 5000));
            socket.Blocking = false;

            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, 4000));
            tcpSocket.Listen(15);

            int nextPort = 5001;

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = new byte[1024];

            while (true)
            {
                if (tcpSocket.Poll(1000 * 1000, SelectMode.SelectRead))
                {
                    Socket clientSocket = tcpSocket.Accept();
                    IPEndPoint clientEP = (IPEndPoint)clientSocket.RemoteEndPoint;
                    Console.WriteLine($"Korisnik {clientEP} povezan preko TCP-a");

                    int udpPort = ++nextPort;

                    byte[] bytes = BitConverter.GetBytes(udpPort);
                    clientSocket.Send(bytes);
                    clientSocket.Close();

                    Console.WriteLine($"Dodeljen UDP port {udpPort} korisniku {clientEP}");
                }

                if (socket.Poll(1000 * 1000, SelectMode.SelectRead))
                {
                    int received = socket.ReceiveFrom(buffer, ref posiljaocEP);

                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);

                    IDevice uredjaj;
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        uredjaj = (IDevice)bf.Deserialize(ms);
                    }

                    uredjaji[(IPEndPoint)posiljaocEP] = uredjaj;

                    Console.WriteLine($"Povezao se novi uredjaj: {uredjaj.GetProperties()}");
                    Console.WriteLine($"Primljeno od {posiljaocEP}");
                }
            }
        }
    }
}
