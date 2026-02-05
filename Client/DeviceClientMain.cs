using Domain.Interfejsi;
using Domain.Modeli;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

namespace DeviceClient
{
    internal class DeviceClientMain
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Client started...");

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);

            IDevice uredjaj;
            int unos = 0;
            do
            {
                Console.Clear();
                Console.WriteLine("Izaberite tip uredjaja: \n[1] - Kapija\n[2] - Klima\n[3] - Svetla");
                Console.Write("Vas unos: ");
                unos = int.Parse(Console.ReadLine());
            }
            while (unos < 1 || unos > 3) ;

            string naziv;
            if (unos == 1) 
            {
                Console.Write("Unesite naziv kapije: ");
                naziv = Console.ReadLine();
                uredjaj = new Kapija(naziv); 
            }
            else if (unos == 2) 
            {
                Console.Write("Unesite naziv klime: ");
                naziv = Console.ReadLine();
                uredjaj = new Klima(naziv); 
            }
            else if (unos == 3) 
            {
                Console.Write("Unesite naziv svetala: ");
                naziv = Console.ReadLine();
                uredjaj = new Svetla(naziv); 
            }
            else { Console.WriteLine("Neuspesno dodeljivanje tipa, probajte opet..."); Console.ReadKey(); return; }

            Console.Clear();
            Console.WriteLine("Uspesno ste izabrali tip uredjaja!");

            string ip, porttxt;
            int port;
            IPAddress ipAddress;
            do
            {
                Console.Write("Unesite IP adresu kucnog servera: ");
                ip = Console.ReadLine();
                Console.Write("Unesite port kucnog servera: ");
                porttxt = Console.ReadLine();
            }
            while (!IPAddress.TryParse((ip ?? "").Trim(), out ipAddress) || !int.TryParse(porttxt, out port) || port < 1 || port > 65535);

            Console.Clear();
            Console.WriteLine("Povezivanje na server...!");

            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpServerEP = new IPEndPoint(ipAddress, port);

            byte[] buffer = new byte[1024];
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, uredjaj);
                buffer = ms.ToArray();
                udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, udpServerEP);
            }

            while(true)
            {
                int received = udpSocket.ReceiveFrom(buffer, ref posiljaocEP);

                byte[] data = new byte[received];
                Array.Copy(buffer, data, received);

                IDevice serverUredjaj;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    serverUredjaj = (IDevice)bf.Deserialize(ms);
                }

                uredjaj = serverUredjaj;

                if(uredjaj is Kapija)
                    Console.WriteLine($"Server je izmenio {((Kapija)uredjaj).Name} | Otvorena: {((Kapija)uredjaj).Otvorena.ToString()}");
                else if(uredjaj is Klima)
                    Console.WriteLine($"Server je izmenio {((Klima)uredjaj).Name} | Upaljena: {((Klima)uredjaj).Upaljena.ToString()} | {((Klima)uredjaj).RezimRada.ToString()} | {((Klima)uredjaj).Temperatura}C");
                else
                    Console.WriteLine($"Server je izmenio {((Svetla)uredjaj).Name} | Upaljena: {((Svetla)uredjaj).Upaljena.ToString()} | {((Svetla)uredjaj).NijansaSvetla.ToString()} | {((Svetla)uredjaj).ProcenatOsvetljenja}% osvetljenja");
            
                // 
            }

            udpSocket.Close();
            Console.ReadKey();
        }
    }
}
