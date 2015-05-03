using System;
using System.Collections.Generic;
using System.Net;
using DnsCache.DnsPacket;

namespace DnsCache
{
    internal class PrecacheTask
    {
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
        }
        public bool TimedOut { get { return DateTime.Now > TimeSent.AddSeconds(3); } }

        public Packet AppendNewData(IEnumerable<ResourceRecord> records)
        {
            Packet.Answers.AddRange(records);
            return Packet;
        }
    }
}
