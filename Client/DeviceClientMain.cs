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
            IDevice uredjaj; // Objekat uredjaja vezan za program

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Client started...");

            EndPoint posiljaocEP = new IPEndPoint(IPAddress.Any, 0); // EndPoint posiljaoca

            // -- Unosenje tipa uredjaja
            int unos = 0;
            do
            {
                Console.Clear();
                Console.WriteLine("Izaberite tip uredjaja: \n[1] - Kapija\n[2] - Klima\n[3] - Svetla");
                Console.Write("Vas unos: ");
                unos = int.Parse(Console.ReadLine());
            }
            while (unos < 1 || unos > 3) ;

            // -- Unos naziva uredjaja
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

            // -- Unosenje ip-a i porta za 
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

            // -- Kreiranje udp soketa i endpointa za slanje poruka na server
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpServerEP = new IPEndPoint(ipAddress, port);

            // -- Neblokirajuci mod soketa
            udpSocket.Blocking = false;

            // -- Slanje uredjaja serveru
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

            // -- Loop za primanje podataka
            while (true)
            {
                if (udpSocket.Poll(500 * 1000, SelectMode.SelectRead)) // Polling model zbog blokirajuceg rezima
                {
                    // -- Kreiranje buffera i primanje podataka na njega
                    byte[] buffer = new byte[2048];
                    int received = udpSocket.ReceiveFrom(buffer, ref posiljaocEP);

                    // -- Kopiranje buffera u novi niz bajta
                    byte[] data = new byte[received];
                    Array.Copy(buffer, data, received);

                    // -- Kreiranje prazne komande za deserijalizaciju
                    Komanda komanda;

                    // -- Deserijalizacija komandi primljenih od servera i ispis teksta
                    using (MemoryStream ms = new MemoryStream(data))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        komanda = (Komanda)bf.Deserialize(ms);
                        Console.WriteLine($"Primio komandu od servera: {komanda.rezultatKomande} | {komanda.dodatnaPoruka}");
                    }
                    Komanda povratna = ObradiKomandu(komanda, uredjaj);
                    Console.WriteLine($"Obradio komandu: {povratna.rezultatKomande} | {povratna.dodatnaPoruka}");

                    // -- Ako je komanda procitana i ispravnog je tipa i id-a korisnika
                    if (povratna.idKorisnika != -1 && povratna.tipKomande != TipKomande.Nepoznata)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            BinaryWriter bw = new BinaryWriter(ms);
                            BinaryFormatter bf = new BinaryFormatter();

                            // -- Upisivanje tipa podatka i komande u BinaryWriter
                            bw.Write((byte)TipPodatka.Komanda);
                            bf.Serialize(ms, povratna);

                            // -- Upisivanje tipa uredjaja u BinaryWriter
                            if (uredjaj is Kapija) bw.Write((byte)TipPodatka.Kapija);
                            else if (uredjaj is Klima) bw.Write((byte)TipPodatka.Klima);
                            else bw.Write((byte)TipPodatka.Svetla);
                            bf.Serialize(ms, uredjaj);

                            // -- Prebacivanje ms u niz i slanje serveru
                            buffer = ms.ToArray();
                            udpSocket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, udpServerEP);
                        }
                    }
                    else Console.WriteLine("Komanda nije dobro obradjena!");
                }
            }
        }

        // -- Obradjivanje komande
        static Komanda ObradiKomandu(Komanda komanda, IDevice uredjaj)
        {
            if(uredjaj is Kapija kapija) // Ako je kapija
            {
                if(komanda.tipKomande == TipKomande.KapijaToggle) // Komanda za menjanje stanja
                {
                    kapija.Otvorena = !kapija.Otvorena;
                    if(kapija.Otvorena) komanda.dodatnaPoruka = $"Kapija {kapija.Name} uspesno otvorena!";
                    else komanda.dodatnaPoruka = $"Kapija {kapija.Name} uspesno zatvorena!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
            }
            else if(uredjaj is Klima klima) // Ako je klima
            {
                if (komanda.tipKomande == TipKomande.KlimaToggle) // Komanda za menjanje stanja
                {
                    klima.Upaljena = !klima.Upaljena;
                    if(klima.Upaljena) komanda.dodatnaPoruka = $"Klima {klima.Name} uspesno upaljena!";
                    else komanda.dodatnaPoruka = $"Klima {klima.Name} uspesno ugasena!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.KlimaRezim) // Promena rezima
                {
                    if (klima.RezimRada == RezimiKlime.Ventilacija) klima.RezimRada = RezimiKlime.Grejanje;
                    else if (klima.RezimRada == RezimiKlime.Grejanje) klima.RezimRada = RezimiKlime.Hladjenje;
                    else klima.RezimRada = RezimiKlime.Ventilacija;

                    komanda.dodatnaPoruka = $"Rezim klime {klima.Name} uspesno promenjen na {klima.RezimRada}!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.KlimaUvecajTemp) // Uvecavanje temperature
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
                else // Smanjivanje temperature
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
            else if(uredjaj is Svetla svetla) // Ako je tip svetla
            {
                if (komanda.tipKomande == TipKomande.SvetlaToggle)  // Komanda za menjanje stanja
                {
                    svetla.Upaljena = !svetla.Upaljena;
                    if(svetla.Upaljena) komanda.dodatnaPoruka = $"Svetla {svetla.Name} uspesno upaljena!";
                    else komanda.dodatnaPoruka = $"Svetla {svetla.Name} uspesno ugasena!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.SvetlaBoja) // Menjanje boje svetla
                {
                    if (svetla.NijansaSvetla == NijanseSvetla.Bela) svetla.NijansaSvetla = NijanseSvetla.Crvena;
                    else if (svetla.NijansaSvetla == NijanseSvetla.Crvena) svetla.NijansaSvetla = NijanseSvetla.Zelena;
                    else if (svetla.NijansaSvetla == NijanseSvetla.Zelena) svetla.NijansaSvetla = NijanseSvetla.Plava;
                    else svetla.NijansaSvetla = NijanseSvetla.Bela;

                    komanda.dodatnaPoruka = $"Boja svetla {svetla.Name} uspesno promenjena na {svetla.NijansaSvetla}!";
                    komanda.rezultatKomande = RezultatKomande.Uspesna;
                    return komanda;
                }
                else if (komanda.tipKomande == TipKomande.SvetlaPovecajOsvetljenje) // Pojacavanje osvetljenosti
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
                else // Smanjivanje osvetljenosti
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
