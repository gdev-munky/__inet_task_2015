using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DnsCache.DnsDataBase;
using DnsCache.DnsPacket;

namespace DnsCache
{
    internal class PrecacheTask
    {
        public bool NoFrowarding { get; set; }
        public ushort ClientId { get; set; }
        public ushort ParentId { get; set; }
        public Packet Packet { get; set; }
        public DateTime TimeSent { get; private set; }
        public IPEndPoint Client { get; set; }

        public PrecacheTask(ushort cid, ushort pid, Packet p)
        {
            ClientId = cid;
            ParentId = pid;
            Packet = p;
            TimeSent = DateTime.Now;
            NoFrowarding = false;
        }
        public PrecacheTask(ushort pid, Packet p)
        {
            ParentId = pid;
            Packet = p;
            TimeSent = DateTime.Now;
            NoFrowarding = true;
        }
        public bool TimedOut { get { return DateTime.Now > TimeSent.AddSeconds(3); } }

        public Packet AppendNewData(IEnumerable<ResourceRecord> records)
        {
            var resourceRecords = records as ResourceRecord[] ?? records.ToArray();
            Packet.Answers.AddRange(resourceRecords);
            return Packet;
        }
    }
}
