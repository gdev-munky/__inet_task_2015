using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DnsCache.DnsPacket
{
    public class ResourceRecord : IMySerializable
    {
        private string _key = ".";
        public string Key
        {
            get { return _key; }
            set { _key = value.EndsWith(".") ? value : value + "."; }
        }
        public DnsQueryType Type { get; set; }
        public DnsQueryClass Class { get; set; }
        public int TTL { get; set; }
        public byte[] Data { get; set; }

        public byte[] GetBytes()
        {
            var len = (ushort)Data.Length;
            var bts = new List<byte>();

            RequestRecord.WriteDnsString(bts, Key);
            bts.AddRange(BEBitConverter.GetBytes((ushort)Type));
            bts.AddRange(BEBitConverter.GetBytes((ushort)Class));
            bts.AddRange(BEBitConverter.GetBytes(TTL));
            bts.AddRange(BEBitConverter.GetBytes(len));
            bts.AddRange(Data.Take(len));

            return bts.ToArray();
        }

        public void FromBytes(byte[] bytes, ref int offset)
        {
            Key = RequestRecord.ReadDnsStringFromBytes(bytes, ref offset, 0);
            Type = (DnsQueryType)BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            Class = (DnsQueryClass)BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            TTL = BEBitConverter.ToInt32(bytes, offset); offset += 4;
            var len = BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            Data = new byte[len];
            Array.Copy(bytes, offset, Data, 0, (int)len);
            offset += len;
        }

        public override string ToString()
        {
            var str = "";
            switch (Type)
            {
                case DnsQueryType.A:
                    str = string.Join(".", Data);
                    break;
                default:
                    str = Encoding.ASCII.GetString(Data);
                    break;
            }
            return "[" + Type + "]: " + str;
        }
    }
}
