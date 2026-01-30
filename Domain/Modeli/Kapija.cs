using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    public class Kapija : IDevice
    {
        public bool Otvorena {  get; set; }

        public Kapija(bool otv)
        {
            Otvorena = otv;
        }

        public bool OtvoriKapiju()
        {
            if (!Otvorena)
            {
                Otvorena = true;
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ZatvoriKapiju()
        {
            if (Otvorena)
            {
                Otvorena = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void PrintProperties()
        {
            Console.WriteLine("Test kapija");
        }
    }
}
