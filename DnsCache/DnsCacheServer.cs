using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DnsCache.DnsDataBase;
using DnsCache.DnsPacket;

namespace DnsCache
{
    public class DnsCacheServer
    {
        public DomainTreeNode DomainRoot;
        public IPEndPoint ParentServer;
        public bool IsRunning { get; private set; }
        public bool ShouldRun { get; set; }
        private Socket UdpSocket { get; set; }
        private Socket TcpSocket { get; set; }
        public ushort Port { get; set; }

        internal List<PrecacheTask> Tasks = new List<PrecacheTask>();

        public void Listen()
        {
            DomainRoot = new DomainTreeNode();
            UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                UdpSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
            }
            catch
            {
                Console.WriteLine("Failed to bind to UDP port "+ Port);
                return;
            }

            if (Equals(ParentServer, UdpSocket.LocalEndPoint))
            {
                Console.WriteLine("Forwarder address == My address; Failed to initialize");
                return;
            }
            IsRunning = true;
            var tickThread = new Thread(Tick);
            tickThread.Start();
            UdpSocket.ReceiveTimeout = 1000;
            while (ShouldRun)
            {
                var buffer = new byte[4096];
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                int len = 0;
                try
                {
                    len = UdpSocket.ReceiveFrom(buffer, ref sender);
                    if (len < 12)
                        continue;
                }
                catch
                {
                    if (!ShouldRun)
                        break;
                    continue;
                }
                var p = new Packet();
                var offset = 0;
                try
                {
                    p.FromBytes(buffer, ref offset);
                }
                catch (Exception e)
                {
                    continue;
#if DEBUG
                    throw;
#endif
                }
                if (offset > len)
                    Console.WriteLine(sender + ": sent unknown shit");
                if (p.Flags.IsQuery())
                    HandleQuery((IPEndPoint) sender, p);
                else 
                    HandleResponse((IPEndPoint)sender, p);
                
            }

            IsRunning = false;
            tickThread.Join();
            UdpSocket.Close();
        }

        public void Tick()
        {
            while (ShouldRun && IsRunning)
            {
                lock (DomainRoot)
                    DomainRoot.Tick();
                lock (Tasks)
                    Tasks.RemoveAll(task =>
                    {
                        if (!task.TimedOut || task.NoFrowarding)
                            return false;
                        task.Packet.Flags |= DnsPacketFlags.RefusedError;
                        UdpSocket.SendTo(task.Packet.GetBytes(), task.Client);
                        return true;
                        
                    });
                Thread.Sleep(1000);
            }
        }

        internal void HandleResponse(IPEndPoint sender, Packet p)
        {
            PrecacheTask task;
            lock (Tasks)
            {
                task = Tasks.FirstOrDefault(t => t.ParentId == p.Id);
                if (task == null)
                {
                    Console.WriteLine(sender +
                                      ": either dns-poison attempt or just too slow answer");
                    foreach (var a in p.Answers)
                    {
                        Console.WriteLine("\t" + a);
                    }
                    return;
                }
                Tasks.Remove(task);
            }


            lock (DomainRoot)
            {
                Console.WriteLine("[ ]: Got response for [{0} #{1:X4}]", task.Client, task.ClientId);
                foreach (var rec in p.Answers)
                {
                    var added = false;
                    foreach (var key in p.Queries.Select(record => record.Key))
                        if (DomainRoot.AddNewData(key, rec))
                            added = true;
                    if (DomainRoot.AddNewData(rec.Key, rec))
                        added = true;
                    Console.ForegroundColor = added ? ConsoleColor.Green : ConsoleColor.Gray;
                    Console.WriteLine("[+]: Answer for [{0} #{3:X4}]: added: {1}; ttl: {2} s", task.Client, rec, rec.TTL,
                        task.ClientId);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                foreach (var rec in p.AuthorityRecords)
                {
                    var added = false;
                    foreach (var key in p.Queries.Select(record => record.Key))
                        if (DomainRoot.AddNewData(key, rec, DnsResourceRecordType.Authority))
                            added = true;
                    if (DomainRoot.AddNewData(rec.Key, rec, DnsResourceRecordType.Authority))
                        added = true;
                    Console.ForegroundColor = added ? ConsoleColor.Cyan : ConsoleColor.Gray;
                    Console.WriteLine("[+]: Authority for [{0} #{3:X4}]: added: {1}; ttl: {2} s", task.Client, rec,
                        rec.TTL, task.ClientId);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                foreach (var rec in p.AdditionalRecords)
                {
                    var added = false;
                    foreach (var key in p.Queries.Select(record => record.Key))
                        if (DomainRoot.AddNewData(key, rec, DnsResourceRecordType.AdditionalInfo))
                            added = true;
                    if (DomainRoot.AddNewData(rec.Key, rec, DnsResourceRecordType.AdditionalInfo))
                        added = true;
                    Console.ForegroundColor = added ? ConsoleColor.DarkGreen : ConsoleColor.Gray;
                    Console.WriteLine("[+]: Additional info for [{0} #{3:X4}]: added: {1}; ttl: {2} s", task.Client, rec,
                        rec.TTL, task.ClientId);
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            if (task.NoFrowarding)
                return;
            task.AppendNewData(p.Answers);
            task.AppendNewData(p.AuthorityRecords, DnsResourceRecordType.Authority);
            task.AppendNewData(p.AdditionalRecords, DnsResourceRecordType.AdditionalInfo);
            if (task.Packet.Answers.Count < 1)
            {
                task.Packet.Flags |= DnsPacketFlags.NameError;
                if (!p.Flags.IsSuccessfull())
                    task.Packet.Flags = p.Flags;
            }
            Console.WriteLine("[ ]: Answering client [{0} #{1:X4}] (was delayed)", task.Client, task.ClientId);
            UdpSocket.SendTo(task.Packet.GetBytes(), task.Client);
        }

        internal void HandleQuery(IPEndPoint sender, Packet p)
        {
            var unknown = new List<RequestRecord>();
            var inv = p.Flags.HasFlag(DnsPacketFlags.Inverse);
            if (inv)
            {
                var answer = new Packet
                {
                    Id = p.Id,
                    Flags = DnsPacketFlags.Response |  DnsPacketFlags.Inverse
                };
                answer.Queries.AddRange(p.Queries);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(sender + ": sent inversed request");
                Console.ForegroundColor = ConsoleColor.White;
                var rp = new Packet
                {
                    Id = p.Id
                };
                rp.Queries.AddRange(p.Queries);
                rp.Flags |= DnsPacketFlags.NotSupportedError;
                UdpSocket.SendTo(rp.GetBytes(), sender);
                return;
            }
            IEnumerable<ResourceRecord> knownAnswers = null;
            IEnumerable<ResourceRecord> knownAuth = null;
            IEnumerable<ResourceRecord> knownInfo = null;
            foreach (var q in p.Queries)
            {
                if (q.Class != DnsQueryClass.IN)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(sender + ": requests some non-internet things (" + q.Key + ")");
                    Console.ForegroundColor = ConsoleColor.White;
                    continue;
                }
                Console.WriteLine("[?]: Request [{0} #{1:X4}]: {2}", sender, p.Id, q);
                DomainTreeNode node;
                lock (DomainRoot)
                    node = DomainRoot.Resolve(q.Key);
                if (node == null)
                {
                    unknown.Add(q);
                    continue;
                }
                var resources = node.GetAllRecords(false, q.Type).ToArray();
                knownAnswers = resources.Select(
                    record =>
                        record.GetResourceRecord(record.SecondsLeft));
                knownAuth = node.Authority.Select(
                    record =>
                        record.GetResourceRecord(record.SecondsLeft));
                knownInfo = node.AdditionalInfo.Select(
                    record =>
                        record.GetResourceRecord(record.SecondsLeft));
                if (resources.Length < 1)
                    unknown.Add(q);
            }
            if (unknown.Count < 1)
            {
                Console.WriteLine("[ ]: Answering client [{0} #{1:X4}] (immediately)", sender, p.Id);
                SendDnsResponseTo(sender, p.Id, p.Queries, knownAnswers, knownAuth, knownInfo);
                return;
            } 
            lock (Tasks)
                foreach (var t in Tasks)
                    if (!t.Packet.Queries.Except(unknown).Any())
                        return;
            
            SendDnsRequestTo(ParentServer, p.Id, sender, unknown, knownAuth, knownInfo);
        }

        /// <summary>
        /// Without forwarding
        /// </summary>
        /// <param name="target"></param>
        /// <param name="requests"></param>
        internal void SendDnsRequestTo(EndPoint target, params RequestRecord[] requests)
        {
            var p = DomainTreeNode.FormRequest(false, requests);
            p.Flags |= DnsPacketFlags.RecursionIsDesired;
            lock (Tasks)
            {
                Tasks.Add(new PrecacheTask(p.Id, p));
            }
            UdpSocket.SendTo(p.GetBytes(), target);
        }
        /// <summary>
        /// With forwaridng
        /// </summary>
        /// <param name="target"></param>
        /// <param name="clientId"></param>
        /// <param name="client"></param>
        /// <param name="requests"></param>
        internal void SendDnsRequestTo(EndPoint target, ushort clientId, IPEndPoint client, 
            IEnumerable<RequestRecord> requests, 
            IEnumerable<ResourceRecord> knownAuthority = null,
            IEnumerable<ResourceRecord> knownInfo = null)
        {
            var p = DomainTreeNode.FormRequest(false, requests.ToArray());
            p.Flags |= DnsPacketFlags.RecursionIsDesired;
            if (knownAuthority != null)
                p.AuthorityRecords = knownAuthority.ToList();
            if (knownInfo != null)
                p.AdditionalRecords = knownInfo.ToList();
            lock (Tasks)
            {
                Tasks.Add(new PrecacheTask(clientId, p.Id, p)
                {
                    Client = client
                });
            }
            UdpSocket.SendTo(p.GetBytes(), target);
        }
        internal void SendDnsResponseTo(EndPoint target, ushort id, IEnumerable<RequestRecord> requests, IEnumerable<ResourceRecord> answers, IEnumerable<ResourceRecord> auth, IEnumerable<ResourceRecord> inf, DnsPacketFlags flags = DnsPacketFlags.Response)
        {
            var p = new Packet
            {
                Id = id,
                Flags = flags,
                Queries = requests.ToList()
            };
            if (answers != null)
                p.Answers = answers.ToList();
            if (auth != null)
                p.AuthorityRecords = auth.ToList();
            if (inf != null)
                p.AdditionalRecords = inf.ToList();
            UdpSocket.SendTo(p.GetBytes(), target);
        }
    }
}
