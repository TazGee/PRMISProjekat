using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    public class Svetla : IDevice
    {
        public void PrintProperties()
        {
            Console.WriteLine("Test svetla");
        }
    }
}
