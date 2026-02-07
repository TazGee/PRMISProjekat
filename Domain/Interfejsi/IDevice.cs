using Domain.Enumeratori;

namespace Domain.Interfejsi
{
    public interface IDevice
    {
        TipPodatka DeviceType { get; }
        string GetProperties();
        long GetID();
    }
}
