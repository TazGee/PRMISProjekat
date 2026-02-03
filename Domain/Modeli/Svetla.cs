using Domain.Enumeratori;
using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    [Serializable]
    public class Svetla : IDevice
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int ProcenatOsvetljenja { get; set; } = 100;
        public NijanseSvetla NijansaSvetla { get; set; } = NijanseSvetla.Bela;
        public bool Upaljena { get; set; } = false;
        
        public Svetla(string name)
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Name = name;
        }

        public Svetla() { }

        public long GetID()
        {
            return Id;
        }

        public string GetProperties()
        {
            if(Upaljena) return $"[Svetla]: {Name} | Upaljena | {ProcenatOsvetljenja}% | {NijansaSvetla.ToString()}";
            else return $"[Svetla]: {Name} | Ugasena | {ProcenatOsvetljenja}% | {NijansaSvetla.ToString()}";
        }
    }
}
