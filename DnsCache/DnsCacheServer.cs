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
                }
                catch
                {
                    if (!ShouldRun)
                        break;
                    continue;
                }
                var p = new Packet();
                var offset = 0;
                p.FromBytes(buffer, ref offset);
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
                        if (!task.TimedOut)
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
                                      ": sent response, but we don`t have a task to wait one. (or timeout has occured)");
                    return;
                }
                Tasks.Remove(task);
            }
            lock (DomainRoot)
            {
                foreach (var rec in p.Answers)
                {
                    DomainRoot.AddNewData(rec.Key, rec);
                    Console.WriteLine("+ " + rec);
                }
            }
            task.AppendNewData(p.Answers);
            if (task.Packet.Answers.Count < 1)
            {
                task.Packet.Flags |= DnsPacketFlags.NameError;
                if (!p.Flags.IsSuccessfull())
                    task.Packet.Flags = p.Flags;
            }
            
            UdpSocket.SendTo(task.Packet.GetBytes(), task.Client);
            Console.WriteLine(task.Client + ": precaching complete from " + sender);
        }

        internal void HandleQuery(IPEndPoint sender, Packet p)
        {
            var unknown = new List<Tuple<string, DnsQueryType>>();
            var answer = new Packet
            {
                Id = p.Id,
                Flags = DnsPacketFlags.Response
            };
            answer.Queries.AddRange(p.Queries);
            var inv = p.Flags.HasFlag(DnsPacketFlags.Inverse);
            if (inv)
            {
                answer.Flags |= DnsPacketFlags.Inverse;
                Console.WriteLine(sender + ": sent inversed request");
                var rp = new Packet
                {
                    Id = p.Id
                };
                rp.Queries.AddRange(p.Queries);
                rp.Flags |= DnsPacketFlags.NotSupportedError;
                UdpSocket.SendTo(rp.GetBytes(), sender);
                return;
            }
            foreach (var q in p.Queries)
            {
                if (q.Class != DnsQueryClass.IN)
                {
                    Console.WriteLine(sender + ": requests some non-internet things (" + q.Key + ")");
                    continue;
                }
                Console.WriteLine(sender + ": wants - " + q);
                DomainTreeNode node;
                lock (DomainRoot)
                    node = DomainRoot.Resolve(q.Key);
                if (node == null)
                {
                    unknown.Add(new Tuple<string, DnsQueryType>(q.Key, q.Type));
                    continue;
                }
                var resources = node.GetAllRecords(false, q.Type).ToArray();
                if (resources.Length < 1)
                {
                    unknown.Add(new Tuple<string, DnsQueryType>(q.Key, q.Type));
                    continue;
                }
                p.Answers.AddRange(
                    resources.Select(
                        record =>
                            record.GetResourceRecord(q.Key, (int) (record.ExpirationTime - DateTime.Now).TotalSeconds)));
            }
            if (unknown.Count < 1)
            {
                UdpSocket.SendTo(p.GetBytes(), sender);
                return;
            }
            var newRequest = DomainTreeNode.FormRequest(false, unknown.ToArray());
            lock (Tasks)
            {
                foreach (var t in Tasks)
                {
                    if (!t.Packet.Queries.Except(newRequest.Queries).Any())
                        return;
                }
                Tasks.Add(new PrecacheTask(p.Id, newRequest.Id, answer) { Client = sender });
            }
            UdpSocket.SendTo(newRequest.GetBytes(), ParentServer);
        }
    }
}
