using Domain.Enumeratori;
using Domain.Interfejsi;

namespace Domain.Modeli
{
    public class Svetla : IDevice
    {
        public string Name { get; set; }
        public int ProcenatOsvetljenja { get; set; } = 100;
        public NijanseSvetla NijansaSvetla { get; set; } = NijanseSvetla.Bela;
        public bool Upaljena { get; set; } = false;
        
        public Svetla(string name)
        {
            Name = name;
        }

        public Svetla() { }

        public string GetProperties()
        {
            if(Upaljena) return $"[Svetla]: {Name} | Upaljena | {ProcenatOsvetljenja}% | {NijansaSvetla.ToString()}";
            else return $"[Svetla]: {Name} | Ugasena | {ProcenatOsvetljenja}% | {NijansaSvetla.ToString()}";
        }
    }
}
