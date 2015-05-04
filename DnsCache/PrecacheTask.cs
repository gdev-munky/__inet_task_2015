﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DnsCache.DnsDataBase;
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

        public Packet AppendNewData(IEnumerable<ResourceRecord> records, DnsResourceRecordType target = DnsResourceRecordType.Cache)
        {
            var resourceRecords = records as ResourceRecord[] ?? records.ToArray();
            if (target.HasFlag(DnsResourceRecordType.Cache))
                Packet.Answers.AddRange(resourceRecords);
            if (target.HasFlag(DnsResourceRecordType.Authority))
                Packet.AuthorityRecords.AddRange(resourceRecords);
            if (target.HasFlag(DnsResourceRecordType.AdditionalInfo))
                Packet.AdditionalRecords.AddRange(resourceRecords);
            return Packet;
        }
    }
}