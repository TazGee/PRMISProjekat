using Domain.Interfejsi;
using System;

namespace Domain.Modeli
{
    public class Klima : IDevice
    {
        public void PrintProperties()
        {
            Console.WriteLine("Test klima");
        }
    }
}
