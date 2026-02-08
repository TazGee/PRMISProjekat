using Domain.Enumeratori;
using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    [Serializable]
    public class Klima : IDevice
    {
        public TipPodatka DeviceType => TipPodatka.Klima;
        public int Id { get; set; }
        public string Name { get; set; }
        public int Temperatura { get; set; } = 20;
        public RezimiKlime RezimRada { get; set; } = RezimiKlime.Grejanje;
        public bool Upaljena { get; set; } = false;

        public Klima(string name)
        {
            Id = (int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Name = name;
        }

        public Klima() { }

        public int GetID()
        {
            return Id;
        }

        public string GetProperties()
        {
            if(Upaljena) return $"[Klima]: {Name} | Upaljena | {RezimRada.ToString()} | {Temperatura}˘C";
            else return $"[Klima]: {Name} | Ugasena | {RezimRada.ToString()} | {Temperatura}˘C";
        }
        public override string ToString()
        {
            return $"{Name} ({Id})";
        }
    }
}
