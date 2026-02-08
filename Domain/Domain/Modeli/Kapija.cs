using Domain.Enumeratori;
using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    [Serializable]
    public class Kapija : IDevice
    {
        public TipPodatka DeviceType => TipPodatka.Kapija;
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Otvorena { get; set; } = false;

        public Kapija(string name)
        {
            Id = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Name = name;
        }

        public int GetID()
        {
            return Id;
        }

        public string GetProperties()
        {
            if(Otvorena)    return $"[Kapija]: {Name} | Otvorena";
            else            return $"[Kapija]: {Name} | Zatvorena";
        }
        public override string ToString()
        {
            return $"{Name} ({Id})";
        }
    }
}
