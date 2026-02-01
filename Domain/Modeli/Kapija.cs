using Domain.Interfejsi;

namespace Domain.Modeli
{
    public class Kapija : IDevice
    {
        public string Name { get; set; }
        public bool Otvorena { get; set; } = false;

        public Kapija(string name)
        {
            Name = name;
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

        public string GetProperties()
        {
            if(Otvorena)    return $"[Kapija]: {Name} | Otvorena";
            else            return $"[Kapija]: {Name} | Zatvorena";
        }
    }
}
