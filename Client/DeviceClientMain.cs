using Domain.Interfejsi;
using Domain.Modeli;
using System;
using System.Net;
using System.Net.Sockets;

namespace DeviceClient
{
    internal class DeviceClientMain
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Client started...");

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

            if (unos == 1) { uredjaj = new Kapija(false); }
            else if (unos == 2) { uredjaj = new Klima(); }
            else if (unos == 3) { uredjaj = new Svetla(); }
            else { Console.WriteLine("Neuspesno dodeljivanje tipa, probajte opet..."); Console.ReadKey(); }

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
            udpSocket.Connect(udpServerEP);



            udpSocket.Close();
            Console.ReadKey();
        }
    }
}
