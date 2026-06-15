namespace VaxDrive.VaxAgent.Network.Protocols;

public interface IProtocolParser
{
    DeviceRecord? Parse(byte[] frame);
}
