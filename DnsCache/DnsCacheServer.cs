using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        public bool AntiPoison { get; set; }
        public bool UpdateRecordsOnTTL { get; set; }

        internal List<PrecacheTask> Tasks = new List<PrecacheTask>();

        public IEnumerable<DnsRecord> GetLocalData(DnsQueryType type, string key)
        {
            DomainTreeNode node;
            lock (DomainRoot)
                node = DomainRoot.Resolve(key);
            if (node == null)
                yield break;
            var resources = node.GetAllRecordsByType(type).ToList();
            if (type != DnsQueryType.CNAME)
            {
                DnsRecord cnameRecord;
                var cname = GetKnownCanonicalName(key, out cnameRecord);
                if (cnameRecord != null && cname != key)
                {
                    resources.Add(cnameRecord);
                    resources.AddRange(GetLocalData(type, cname).Where(r => !resources.Contains(r)));
                }
            }
            resources.Sort((a, b) => a.ExpirationTime < b.ExpirationTime ? 1 : (a.ExpirationTime == b.ExpirationTime ? 0 : -1));
            foreach (var record in resources)
                yield return record;
        }

        public string GetKnownCanonicalName(string key)
        {
            var results = GetLocalData(DnsQueryType.CNAME, key).ToArray();
            return results.Any() ? results.First().GetDataAsDomainName() : key;
        }
        public string GetKnownCanonicalName(string key, out DnsRecord cnameRecord)
        {
            cnameRecord = GetLocalData(DnsQueryType.CNAME, key).FirstOrDefault();
            return cnameRecord != null ? cnameRecord.GetDataAsDomainName() : key;
        }
        public ResourceRecord[] GetLocalResourceData(DnsQueryType type, string key)
        {
            return GetLocalData(type, key).Select(r => r.GetResourceRecord(r.SecondsLeft)).ToArray();
        }

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
                {
                    var list =
                        DomainRoot.Tick()
                            .Select(
                                record =>
                                    new RequestRecord {Class = DnsQueryClass.IN, Key = record.Key, Type = record.Type})
                            .ToList();
                    if (UpdateRecordsOnTTL)
                    {
                        lock (Tasks)
                            foreach (var t in Tasks)
                                foreach (var q in t.MyRequests)
                                    list.Remove(q);

                        if (list.Any())
                            SendDnsRequestTo(ParentServer, list.ToArray());
                    }
                }
                lock (Tasks)
                    Tasks.RemoveAll(task =>
                    {
                        if (!task.TimedOut || task.NoFrowarding)
                            return false;
                        var p = new Packet
                        {
                            Flags = DnsPacketFlags.RefusedError
                        };
                        p.Queries.AddRange(task.ClientRequests);
                        UdpSocket.SendTo(p.GetBytes(), task.Client);
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
                    Console.WriteLine("[{0} #{1:X4}]: either dns-poison attempt or just too slow answer",
                        sender, p.Id);
                    foreach (var a in p.Answers)
                        Console.WriteLine("\t" + a);

                    Console.WriteLine("Tasks ids:");
                    foreach (var a in Tasks)
                        Console.WriteLine("\t#{0:X4}->#{1:X4}", a.ParentId, a.ClientId);
                    
                    if (!AntiPoison)
                    {
                        foreach (var rec in p.Answers)
                        {
                            var added = false;
                            foreach (var key in p.Queries.Select(record => record.Key))
                                if (DomainRoot.AddNewData(key, rec))
                                    added = true;
                            Console.ForegroundColor = added ? ConsoleColor.Red : ConsoleColor.Gray;
                            Console.WriteLine("[+]: Wierd answer added: {0}; ttl: {1} s", rec, rec.TTL);
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        foreach (var rec in p.AuthorityRecords)
                        {
                            var added = false;
                            foreach (var key in p.Queries.Select(record => record.Key))
                                if (DomainRoot.AddNewData(key, rec))
                                    added = true;
                            Console.ForegroundColor = added ? ConsoleColor.Red : ConsoleColor.Gray;
                            Console.WriteLine("[+]: Wierd authority added: {0}; ttl: {1} s", rec, rec.TTL);
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        foreach (var rec in p.AdditionalRecords)
                        {
                            var added = false;
                            foreach (var key in p.Queries.Select(record => record.Key))
                                if (DomainRoot.AddNewData(key, rec))
                                    added = true;
                            Console.ForegroundColor = added ? ConsoleColor.Red : ConsoleColor.Gray;
                            Console.WriteLine("[+]: Wierd info added: {0}; ttl: {1} s", rec, rec.TTL);
                            Console.ForegroundColor = ConsoleColor.White;
                        }
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
                        if (DomainRoot.AddNewData(key, rec))
                            added = true;
                    if (DomainRoot.AddNewData(rec.Key, rec))
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
                        if (DomainRoot.AddNewData(key, rec))
                            added = true;
                    Console.ForegroundColor = added ? ConsoleColor.DarkGreen : ConsoleColor.Gray;
                    Console.WriteLine("[+]: Additional info for [{0} #{3:X4}]: added: {1}; ttl: {2} s", task.Client, rec,
                        rec.TTL, task.ClientId);
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            if (task.NoFrowarding)
                return;
            var rp = new Packet
            {
                Id = task.ClientId,
                Flags = DnsPacketFlags.Response | DnsPacketFlags.AnswerIsAuthoritative
            };
            rp.Queries.AddRange(task.ClientRequests);
            if (!p.Flags.IsSuccessfull())
                rp.Flags = p.Flags;
            else
                foreach (var q in task.ClientRequests)
                {
                    var addedSomething = false;
                    var allData = GetLocalResourceData(q.Type, q.Key);
                    if (allData.Length > 0)
                        addedSomething = true;
                    var toAdd = allData.Where(a => !rp.Answers.Contains(a)).ToArray();
                    rp.Answers.AddRange(toAdd);
                    if (q.Type != DnsQueryType.ANY && q.Type != DnsQueryType.NS)
                    {
                        var authInfo =
                            GetLocalResourceData(DnsQueryType.NS, q.Key)
                                .Where(a => !rp.AuthorityRecords.Contains(a))
                                .ToArray();
                        if (authInfo.Length > 0)
                            addedSomething = true;
                        rp.AuthorityRecords.AddRange(authInfo);
                        var otherInfo = authInfo.SelectMany(record =>
                            GetLocalResourceData(DnsQueryType.ANY, record.Key))
                            .Where(a => !rp.AdditionalRecords.Contains(a) && !rp.Answers.Contains(a))
                            .ToArray();
                        if (otherInfo.Length > 0)
                            addedSomething = true;
                        rp.AdditionalRecords.AddRange(otherInfo);
                    }
                    if (!addedSomething)
                    {
                        rp.Flags |= DnsPacketFlags.NameError;
                    }
                }
            Console.WriteLine("[ ]: Answering client [{0} #{1:X4}] (was delayed)", task.Client, task.ClientId);
            UdpSocket.SendTo(rp.GetBytes(), task.Client);
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
           var knownAnswers = new List<ResourceRecord>();
           var knownAuth = new List<ResourceRecord>();
           var knownInfo = new List<ResourceRecord>();
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

                var data = GetLocalResourceData(q.Type, q.Key);
                knownAnswers.AddRange(data);
                if (data.Length < 1)
                {
                    unknown.Add(q);
                    continue;
                }
                if (q.Type != DnsQueryType.ANY && q.Type != DnsQueryType.NS)
                {
                    var authData = GetLocalResourceData(DnsQueryType.NS, q.Key);
                    knownAuth.AddRange(authData);
                    var additionalData = authData.SelectMany(record =>
                        GetLocalResourceData(DnsQueryType.ANY, record.Key))
                        .Where(a => !knownAnswers.Contains(a) && !knownAuth.Contains(a) && !knownInfo.Contains(a))
                        .ToArray();
                    knownInfo.AddRange(additionalData);
                }
            }
            if (unknown.Count < 1)
            {
                Console.WriteLine("[ ]: Answering client [{0} #{1:X4}] (immediately)", sender, p.Id);
                SendDnsResponseTo(sender, p.Id, p.Queries, knownAnswers, knownAuth, knownInfo);
                return;
            } 
            lock (Tasks)
                foreach (var t in Tasks)
                    if (!t.MyRequests.Except(unknown).Any())
                        return;
            
            SendDnsRequestTo(ParentServer, p, sender, unknown, knownAuth, knownInfo);
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
                Tasks.Add(new PrecacheTask(p.Id, requests));
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
        internal void SendDnsRequestTo(EndPoint target, Packet clientPacket, IPEndPoint client, 
            IEnumerable<RequestRecord> requests, 
            IEnumerable<ResourceRecord> knownAuthority = null,
            IEnumerable<ResourceRecord> knownInfo = null)
        {
            var requestsArray = requests.ToArray();
            var pr = DomainTreeNode.FormRequest(false, requestsArray);
            
            pr.Flags |= DnsPacketFlags.RecursionIsDesired;
            lock (Tasks)
            {
                Tasks.Add(new PrecacheTask(clientPacket.Id, pr.Id, clientPacket.Queries.ToArray(), requestsArray)
                {
                    Client = client
                });
            }
            UdpSocket.SendTo(pr.GetBytes(), target);
        }

        internal void SendDnsResponseTo(EndPoint target, ushort id, IEnumerable<RequestRecord> requests,
            IEnumerable<ResourceRecord> answers, IEnumerable<ResourceRecord> auth, IEnumerable<ResourceRecord> inf,
            DnsPacketFlags flags = DnsPacketFlags.Response | DnsPacketFlags.AnswerIsAuthoritative)
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
