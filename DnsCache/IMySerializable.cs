namespace DnsCache
{
    public interface IMySerializable
    {
        byte[] GetBytes();
        void FromBytes(byte[] bytes, ref int offset);
    }
}