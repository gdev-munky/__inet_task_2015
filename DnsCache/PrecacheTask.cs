using System;
using System.Net;
using DnsCache.DnsPacket;

namespace DnsCache
{
    internal class PrecacheTask
    {
        public bool NoFrowarding { get; set; }
        public ushort ClientId { get; set; }
        public RequestRecord[] ClientRequests { get; set; }
        public RequestRecord[] MyRequests { get; set; }
        public ushort ParentId { get; set; }
        public DateTime TimeSent { get; private set; }
        public IPEndPoint Client { get; set; }

        public PrecacheTask(ushort cid, ushort pid, RequestRecord[] creq, RequestRecord[] myreq)
        {
            ClientId = cid;
            ParentId = pid;
            TimeSent = DateTime.Now;
            NoFrowarding = false;

            ClientRequests = creq;
            MyRequests = myreq;
        }
        public PrecacheTask(ushort pid, RequestRecord[] myreq)
        {
            ParentId = pid;
            TimeSent = DateTime.Now;
            NoFrowarding = true;

            MyRequests = myreq;
        }
        public bool TimedOut { get { return DateTime.Now > TimeSent.AddSeconds(3); } }
    }
}
