﻿using System;
using System.Collections.Generic;
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
            return "[" + Type + "]: " + str;
        }
    }
}
