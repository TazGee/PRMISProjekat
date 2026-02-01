using Domain.Enumeratori;
using Domain.Interfejsi;

namespace Domain.Modeli
{
    public class Klima : IDevice
    {
        public string Name { get; set; }
        public int Temperatura { get; set; } = 20;
        public RezimiKlime RezimRada { get; set; } = RezimiKlime.Grejanje;
        public bool Upaljena { get; set; } = false;

        public Klima(string name)
        {
            Name = name;
        }

        public Klima() { }

        public string GetProperties()
        {
            if(Upaljena) return $"[Klima]: {Name} | Upaljena | {RezimRada.ToString()} | {Temperatura}˘C";
            else return $"[Klima]: {Name} | Ugasena | {RezimRada.ToString()} | {Temperatura}˘C";
        }
    }
}
