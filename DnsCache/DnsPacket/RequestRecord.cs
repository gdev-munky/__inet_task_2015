using System;
using System.Collections.Generic;
using System.Text;

namespace DnsCache.DnsPacket
{
    public class RequestRecord : IMySerializable, IEquatable<RequestRecord>
    {
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RequestRecord) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_key != null ? _key.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (int) Type;
                hashCode = (hashCode*397) ^ (int) Class;
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
        public byte[] GetBytes()
        {
            var bts = new List<byte>();
            
            WriteDnsString(bts, Key);
            bts.AddRange(BEBitConverter.GetBytes((ushort)Type));
            bts.AddRange(BEBitConverter.GetBytes((ushort)Class));

            return bts.ToArray();
        }

        public void FromBytes(byte[] bytes, ref int offset)
        {
            Key = ReadDnsStringFromBytes(bytes, ref offset, 0);
            Type = (DnsQueryType)BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            Class = (DnsQueryClass)BEBitConverter.ToUInt16(bytes, offset); offset += 2;
        }

        internal static string ReadDnsStringFromBytes(byte[] bytes, ref int offset, int originalOffset)
        {
            var str = "";
            var len = bytes[offset++];
            while (len != 0)
            {
                if ((len & 0xc0) > 0) // read from ptr
                {
                    var addr = originalOffset + bytes[offset] + (len & ~0xc0)*256;
                    ++offset;
                    if (addr > offset)
                        throw new Exception("?");
                    var result = ReadDnsStringFromBytes(bytes, ref addr, originalOffset);
                    return string.IsNullOrWhiteSpace(str) ? result : str + "." + result;
                }
                var label = Encoding.ASCII.GetString(bytes, offset, len);
                offset += len;
                str = string.IsNullOrWhiteSpace(str) ? label : str + "." + label;
                len = bytes[offset++];
            }
            return str;
        }

        internal static string ReadDnsStringFromBytes(byte[] bytes, ref int offset)
        {
            return ReadDnsStringFromBytes(bytes, ref offset, 0) + ".";
        }

        internal static void WriteDnsString(List<byte> bts, string key)
        {
            var startp = 0;
            for (var i = 0; i < key.Length; ++i)
            {
                if (key[i] != '.')
                    continue;
                var labelLen = i - startp;
                var label = Encoding.ASCII.GetBytes(key.Substring(startp, labelLen));
                bts.Add((byte)label.Length);
                bts.AddRange(label);
                startp = i + 1;
            }
            bts.Add(0);
        }

        public override string ToString()
        {
            return Type + ": " + Key;
        }

        public bool Equals(RequestRecord other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_key, other._key) && Type == other.Type && Class == other.Class;
        }

        public static bool operator ==(RequestRecord a, RequestRecord b)
        {
            if ((object) a == null)
                return (object) b == null;
            if ((object) b == null)
                return false;
            return a.Equals(b);
        }

        public static bool operator !=(RequestRecord a, RequestRecord b)
        {
            return !(a == b);
        }
    }
}