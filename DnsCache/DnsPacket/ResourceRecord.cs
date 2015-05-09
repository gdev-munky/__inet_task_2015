using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DnsCache.DnsPacket
{
    public class ResourceRecord : IMySerializable, IEquatable<ResourceRecord>
    {
        public bool Equals(ResourceRecord other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_key, other._key) && Type == other.Type && Class == other.Class && Equals(Data, other.Data);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ResourceRecord) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_key != null ? _key.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (int) Type;
                hashCode = (hashCode*397) ^ (int) Class;
                hashCode = (hashCode*397) ^ (Data != null ? Data.GetHashCode() : 0);
                return hashCode;
            }
        }

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
            Key = RequestRecord.ReadDnsStringFromBytes(bytes, ref offset);
            Type = (DnsQueryType)BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            Class = (DnsQueryClass)BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            TTL = BEBitConverter.ToInt32(bytes, offset); offset += 4;
            var len = BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            Data = new byte[len];
            Array.Copy(bytes, offset, Data, 0, (int)len);
            FixPointersInData(bytes, offset);
            offset += len;
        }
        
        internal void FixPointersInData(byte[] packet, int dataOffset)
        {
            switch (Type)
            {
                case DnsQueryType.NS:
                case DnsQueryType.CNAME:
                case DnsQueryType.PTR:
                case DnsQueryType.MR:
                case DnsQueryType.MG:
                case DnsQueryType.MB:
                case DnsQueryType.MD:
                case DnsQueryType.MF:
                {
                    int offset = dataOffset;
                    var bts = new List<byte>();
                    RequestRecord.WriteDnsString(bts, RequestRecord.ReadDnsStringFromBytes(packet, ref offset));
                    Data = bts.ToArray();
                }
                break;
                case DnsQueryType.MINFO:
                {
                    int offset = dataOffset;
                    var bts = new List<byte>();
                    var s1 = RequestRecord.ReadDnsStringFromBytes(packet, ref offset);
                    var s2 = RequestRecord.ReadDnsStringFromBytes(packet, ref offset);
                    RequestRecord.WriteDnsString(bts, s1);
                    RequestRecord.WriteDnsString(bts, s2);
                    Data = bts.ToArray();
                }
                break;
                case DnsQueryType.SOA:
                    {
                        int offset = dataOffset;
                        var bts = new List<byte>();
                        var s1 = RequestRecord.ReadDnsStringFromBytes(packet, ref offset);
                        var s2 = RequestRecord.ReadDnsStringFromBytes(packet, ref offset);
                        RequestRecord.WriteDnsString(bts, s1);
                        RequestRecord.WriteDnsString(bts, s2);
                        bts.AddRange(Data.Skip(offset - dataOffset));
                        Data = bts.ToArray();
                    }
                    break;
                case DnsQueryType.WKS:
                    //no support
                    break;
                case DnsQueryType.MX:
                    {
                        int offset = dataOffset+2;
                        var str = RequestRecord.ReadDnsStringFromBytes(packet, ref offset);
                        var bts = new List<byte>();
                        bts.AddRange(Data.Take(2));
                        RequestRecord.WriteDnsString(bts, str);
                        Data = bts.ToArray();
                        break;
                    }
            }
        }

        public override string ToString()
        {
            var str = "";
            switch (Type)
            {
                case DnsQueryType.A:
                    str = string.Join(".", Data);
                    break;
                case DnsQueryType.AAAA:
                    str = string.Join(".", Data.Select(b => string.Format("{0:X2}", b)));
                    break;
                case DnsQueryType.NS:
                case DnsQueryType.CNAME:
                case DnsQueryType.PTR:
                case DnsQueryType.MR:
                case DnsQueryType.MG:
                case DnsQueryType.MB:
                case DnsQueryType.MD:
                case DnsQueryType.MF:
                {
                    int offset = 0;
                    str = RequestRecord.ReadDnsStringFromBytes(Data, ref offset, 0);
                    break;
                }
                case DnsQueryType.MINFO:
                {
                    int offset = 0;
                    var bts = new List<byte>();
                    var s1 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset, 0);
                    var s2 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset, 0);
                    str = string.Format("{0}, {1}", s1, s2);
                    break;
                }
                case DnsQueryType.SOA:
                {
                    int offset = 0;
                    var bts = new List<byte>();
                    var s1 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset, 0);
                    var s2 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset, 0);
                    var d1 = BEBitConverter.ToUInt32(Data, offset); offset += 4;
                    var d2 = BEBitConverter.ToInt32(Data, offset); offset += 4;
                    var d3 = BEBitConverter.ToInt32(Data, offset); offset += 4;
                    var d4 = BEBitConverter.ToInt32(Data, offset); offset += 4;
                    var d5 = BEBitConverter.ToInt32(Data, offset); offset += 4;

                    str = string.Format("{0}, {1}, serial: {2:X8}, refresh: {3}, retry: {4}, expire: {5}, min: {6}", s1,
                        s2, d1, d2, d3, d4, d5);
                    break;
                }
                case DnsQueryType.MX:
                {
                    var pref = BEBitConverter.ToInt16(Data, 0);
                    int offset = 2;
                    str = string.Format("{0} (pref: {1})", 
                        RequestRecord.ReadDnsStringFromBytes(Data, ref offset, 0),
                        pref);
                    break;
                }
                default:
                    str = Encoding.ASCII.GetString(Data);
                    break;
            }
            return Key + "\\[" + Type + "]: " + str;
        }

        public static bool operator ==(ResourceRecord a, ResourceRecord b)
        {
            if ((object) a == null)
                return (object)b == null;
            if ((object)b == null)
                return false;
            return a.Type == b.Type && a.Key == b.Key && a.Class == b.Class;
        }

        public static bool operator !=(ResourceRecord a, ResourceRecord b)
        {
            return !(a == b);
        }
    }
}
