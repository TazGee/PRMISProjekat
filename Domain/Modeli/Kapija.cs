using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    public class Kapija : IDevice
    {
        public void PrintProperties()
        {
            Console.WriteLine("Test kapija");
        }
    }
}
