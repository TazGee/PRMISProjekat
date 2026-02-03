using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    [Serializable]
    public class Kapija : IDevice
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public bool Otvorena { get; set; } = false;

        public Kapija(string name)
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Name = name;
        }

        public long GetID()
        {
            return Id;
        }

        public string GetProperties()
        {
            if(Otvorena)    return $"[Kapija]: {Name} | Otvorena";
            else            return $"[Kapija]: {Name} | Zatvorena";
        }
    }
}
