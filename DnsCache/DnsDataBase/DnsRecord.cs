using System;
using System.Net;
using System.Text;
using DnsCache.DnsPacket;

namespace DnsCache.DnsDataBase
{
    public class DnsRecord
    {
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

        public DnsRecord(ResourceRecord resource)
        {
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
