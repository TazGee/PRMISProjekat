using Domain.Enumeratori;

namespace Domain.Interfejsi
{
    public interface IDevice
    {
        TipUredjaja DeviceType { get; }
        string GetProperties();
        long GetID();
    }
}
