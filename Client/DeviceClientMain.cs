using Domain.Interfejsi;
using Domain.Modeli;
using Domain.Enumeratori;
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
            IDevice uredjaj;

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Client started...");

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0);

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
            Console.WriteLine("Povezani ste na server!");

            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpServerEP = new IPEndPoint(ipAddress, port);

            udpSocket.Blocking = false;

            byte[] initBuffer;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                BinaryFormatter bf = new BinaryFormatter();
                bw.Write((byte)uredjaj.DeviceType);
                bf.Serialize(ms, uredjaj);
                initBuffer = ms.ToArray();
                udpSocket.SendTo(initBuffer, udpServerEP);
            }

            while (true)
            {
                if (udpSocket.Poll(500 * 1000, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[2048];
                    int received = udpSocket.ReceiveFrom(buffer, ref posiljaocEP);

                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);

                    Komanda komanda;

                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        komanda = (Komanda)bf.Deserialize(ms);
                        Console.WriteLine($"Primio komandu od servera: {komanda.rezultatKomande} | {komanda.dodatnaPoruka}");
                    }
                    Komanda povratna = ObradiKomandu(komanda, uredjaj);
                    Console.WriteLine($"Obradio komandu: {povratna.rezultatKomande} | {povratna.dodatnaPoruka}");

                    if (povratna.idKorisnika != -1 && povratna.tipKomande != TipKomande.Nepoznata)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter bw = new BinaryWriter(ms);
                            BinaryFormatter bf = new BinaryFormatter();

                            bw.Write((byte)TipPodatka.Komanda);
                            bf.Serialize(ms, povratna);

                            if (uredjaj is Kapija) bw.Write((byte)TipPodatka.Kapija);
                            else if (uredjaj is Klima) bw.Write((byte)TipPodatka.Klima);
                            else bw.Write((byte)TipPodatka.Svetla);
                            bf.Serialize(ms, uredjaj);

                            buffer = ms.ToArray();
                            udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, udpServerEP);
                        }
                    }
                    else Console.WriteLine("Komanda nije dobro obradjena!");
                }
            }
        }

        static Komanda ObradiKomandu(Komanda komanda, IDevice uredjaj)
        {
            if(uredjaj is Kapija kapija)
            {
                if(komanda.tipKomande == TipKomande.KapijaToggle)
                {
                    kapija.Otvorena = !kapija.Otvorena;
                    if(kapija.Otvorena) komanda.dodatnaPoruka = $"Kapija {kapija.Name} uspesno otvorena!";
                    else komanda.dodatnaPoruka = $"Kapija {kapija.Name} uspesno zatvorena!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
            }
            else if(uredjaj is Klima klima)
            {
                if (komanda.tipKomande == TipKomande.KlimaToggle)
                {
                    klima.Upaljena = !klima.Upaljena;
                    if(klima.Upaljena) komanda.dodatnaPoruka = $"Klima {klima.Name} uspesno upaljena!";
                    else komanda.dodatnaPoruka = $"Klima {klima.Name} uspesno ugasena!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.KlimaRezim)
                {
                    if (klima.RezimRada == RezimiKlime.Ventilacija) klima.RezimRada = RezimiKlime.Grejanje;
                    else if (klima.RezimRada == RezimiKlime.Grejanje) klima.RezimRada = RezimiKlime.Hladjenje;
                    else klima.RezimRada = RezimiKlime.Ventilacija;

                    komanda.dodatnaPoruka = $"Rezim klime {klima.Name} uspesno promenjen na {klima.RezimRada}!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.KlimaUvecajTemp)
                {
                    if(klima.Temperatura >= 30)
                    {
                        komanda.dodatnaPoruka = $"Temperatura klime {klima.Name} nemoze da se promeni, vec je maksimalna!";
                        komanda.rezultatKomande = RezultatKomande.Neuspesna;
                        return komanda;
                    }
                    else
                    {
                        klima.Temperatura++;
                        komanda.dodatnaPoruka = $"Temperatura klime {klima.Name} uspesno uvecana na {klima.Temperatura}%!";
                        komanda.rezultatKomande = RezultatKomande.Uspesna;
                        return komanda;
                    }
                }
                else
                {
                    if (klima.Temperatura <= 18)
                    {
                        komanda.dodatnaPoruka = $"Temperatura klime {klima.Name} nemoze da se promeni, vec je minimalna!";
                        komanda.rezultatKomande = RezultatKomande.Neuspesna;
                        return komanda;
                    }
                    else
                    {
                        klima.Temperatura--;
                        komanda.dodatnaPoruka = $"Temperatura klime {klima.Name} uspesno umanjena na {klima.Temperatura}%!";
                        komanda.rezultatKomande = RezultatKomande.Uspesna;
                        return komanda;
                    }
                }
            }
            else if(uredjaj is Svetla svetla)
            {
                if (komanda.tipKomande == TipKomande.SvetlaToggle)
                {
                    svetla.Upaljena = !svetla.Upaljena;
                    if(svetla.Upaljena) komanda.dodatnaPoruka = $"Svetla {svetla.Name} uspesno upaljena!";
                    else komanda.dodatnaPoruka = $"Svetla {svetla.Name} uspesno ugasena!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.SvetlaBoja)
                {
                    if (svetla.NijansaSvetla == NijanseSvetla.Bela) svetla.NijansaSvetla = NijanseSvetla.Crvena;
                    else if (svetla.NijansaSvetla == NijanseSvetla.Crvena) svetla.NijansaSvetla = NijanseSvetla.Zelena;
                    else if (svetla.NijansaSvetla == NijanseSvetla.Zelena) svetla.NijansaSvetla = NijanseSvetla.Plava;
                    else svetla.NijansaSvetla = NijanseSvetla.Bela;

                    komanda.dodatnaPoruka = $"Boja svetla {svetla.Name} uspesno promenjena na {svetla.NijansaSvetla}!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.SvetlaPovecajOsvetljenje)
                {
                    if (svetla.ProcenatOsvetljenja >= 100)
                    {
                        komanda.dodatnaPoruka = $"Osvetljenje svetla {svetla.Name} nemoze da se promeni, vec je maksimalno!";
                        komanda.rezultatKomande = RezultatKomande.Neuspesna;
                        return komanda;
                    }
                    else
                    {
                        svetla.ProcenatOsvetljenja += 10;
                        komanda.dodatnaPoruka = $"Osvetljenje svetla {svetla.Name} uspesno uvecano na {svetla.ProcenatOsvetljenja}%!";
                        komanda.rezultatKomande = RezultatKomande.Uspesna;
                        return komanda;
                    }
                }
                else
                {
                    if (svetla.ProcenatOsvetljenja <= 0)
                    {
                        komanda.dodatnaPoruka = $"Osvetljenje svetla {svetla.Name} nemoze da se promeni, vec je minimalno!";
                        komanda.rezultatKomande = RezultatKomande.Neuspesna;
                        return komanda;
                    }
                    else
                    {
                        svetla.ProcenatOsvetljenja -= 10;
                        komanda.dodatnaPoruka = $"Osvetljenje svetla {svetla.Name} uspesno umanjeno na {svetla.ProcenatOsvetljenja}%!";
                        komanda.rezultatKomande = RezultatKomande.Uspesna;
                        return komanda;
                    }
                }
            }
            Komanda k;
            k.tipKomande = TipKomande.Nepoznata;
            k.rezultatKomande = RezultatKomande.Neuspesna;
            k.idKorisnika = -1;
            k.dodatnaPoruka = "";
            return k;
        }
    }
}
