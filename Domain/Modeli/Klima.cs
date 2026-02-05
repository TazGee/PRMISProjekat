using Domain.Enumeratori;
using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    [Serializable]
    public class Klima : IDevice
    {
        public TipUredjaja DeviceType => TipUredjaja.Klima;
        public long Id { get; set; }
        public string Name { get; set; }
        public int Temperatura { get; set; } = 20;
        public RezimiKlime RezimRada { get; set; } = RezimiKlime.Grejanje;
        public bool Upaljena { get; set; } = false;

        public Klima(string name)
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Name = name;
        }

        public Klima() { }

        public long GetID()
        {
            return Id;
        }

        public string GetProperties()
        {
            if(Upaljena) return $"[Klima]: {Name} | Upaljena | {RezimRada.ToString()} | {Temperatura}˘C";
            else return $"[Klima]: {Name} | Ugasena | {RezimRada.ToString()} | {Temperatura}˘C";
        }
    }
}
