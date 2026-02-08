using Domain.Enumeratori;

namespace Domain.Interfejsi
{
    public interface IDevice
    {
        TipPodatka DeviceType { get; }
        string GetProperties();
        int GetID();
    }
}
