using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DnsCache.DnsPacket;

namespace DnsCache.DnsDataBase
{
    public class DnsRecord : IEquatable<DnsRecord>
    {
        public bool Equals(DnsRecord other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Key, other.Key) && Type == other.Type && Equals(Data, other.Data);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DnsRecord) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Key != null ? Key.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (int) Type;
                hashCode = (hashCode*397) ^ (Data != null ? Data.GetHashCode() : 0);
                return hashCode;
            }
        }

        public string Key { get; private set; }
        public DnsQueryType Type { get; set; }
        public byte[] Data { get; set; }

        public DateTime TimeObtained { get; set; }
        public DateTime ExpirationTime
        {
            get { return TimeObtained.AddSeconds(TTL); }
            set { TTL = (int) ((value - TimeObtained).TotalSeconds); }
        }
        public int TTL { get; set; }
        public bool IsOutdated { get { return DateTime.Now > ExpirationTime; } }
        public TimeSpan TimeLeft { get { return ExpirationTime - DateTime.Now; } }
        public int SecondsLeft
        {
            get { return (int) TimeLeft.TotalSeconds; }
            set { ExpirationTime = DateTime.Now.AddSeconds(value); }
        }

        public DnsRecord(ResourceRecord resource)
        {
            Key = resource.Key;
            Type = resource.Type;
            TTL = resource.TTL;
            Data = resource.Data;
            TimeObtained = DateTime.Now;
        }

        public ResourceRecord GetResourceRecord(string name, int newTtl = 60*60*24*2)
        {
            return new ResourceRecord
            {
                Class = DnsQueryClass.IN,
                Data = Data,
                Key = name,
                TTL = newTtl,
                Type = Type
            };
        }
        public ResourceRecord GetResourceRecord(int newTtl = 60*60*24*2)
        {
            return new ResourceRecord
            {
                Class = DnsQueryClass.IN,
                Data = Data,
                Key = Key,
                TTL = newTtl,
                Type = Type
            };
        }

        public string GetDataAsDomainName()
        {
            var offset = 0;
            return RequestRecord.ReadDnsStringFromBytes(Data, ref offset);
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
                        str = RequestRecord.ReadDnsStringFromBytes(Data, ref offset);
                        break;
                    }
                case DnsQueryType.MINFO:
                    {
                        int offset = 0;
                        var bts = new List<byte>();
                        var s1 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset);
                        var s2 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset);
                        str = string.Format("{0}, {1}", s1, s2);
                        break;
                    }
                case DnsQueryType.SOA:
                    {
                        int offset = 0;
                        var bts = new List<byte>();
                        var s1 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset);
                        var s2 = RequestRecord.ReadDnsStringFromBytes(Data, ref offset);
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
                            RequestRecord.ReadDnsStringFromBytes(Data, ref offset),
                            pref);
                        break;
                    }
                default:
                    str = Encoding.ASCII.GetString(Data);
                    break;
            }
            return Key + "\\[" + Type + "]: " + str;
        }

        public static bool operator ==(DnsRecord a, DnsRecord b)
        {
            if ((object)a == null)
                return (object)b == null;
            if ((object)b == null)
                return false;
            if (!(a.Type == b.Type && a.Key == b.Key && a.Data.Length == b.Data.Length))
                return false;
            return !a.Data.Where((t, i) => t != b.Data[i]).Any();
        }

        public static bool operator !=(DnsRecord a, DnsRecord b)
        {
            return !(a == b);
        }
    }
}
