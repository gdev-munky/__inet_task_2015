using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DnsCache.DnsDataBase;
using DnsCache.DnsPacket;

namespace DnsCache
{
    class Program
    {
        internal static Random Rnd;
        static void Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                ReportUsage();
                return;
            }
            Rnd = new Random();
            var dns = new DnsCacheServer { ShouldRun = true, AntiPoison = true};

            var ip = IPAddress.Any;
            var ipWords = args[0].Split(':');
            if (!IPAddress.TryParse(ipWords[0], out ip))
            {
                try
                {
                    var entries = Dns.GetHostEntry(ipWords[0]).AddressList;
                    Console.WriteLine(string.Join("; ", entries.Cast<object>()));
                    if (entries.Length < 0)
                    {
                        Console.WriteLine("Failed to resolve '{0}'", ipWords[0]);
                        return;
                    }
                    ip = entries.First(address => address.AddressFamily == AddressFamily.InterNetwork);
                    Console.WriteLine("Selected : " + ip);
                }
                catch
                {
                    ReportUsage();
                    return;
                }
            }
            ushort listenPort = 53;
            ushort remotePort = 53;
            if (ipWords.Length > 1)
                ushort.TryParse(ipWords[1], out listenPort);
            dns.Port = listenPort;
            if (args.Length == 2)
            {
                if (!ushort.TryParse(args[1], out remotePort))
                {
                    ReportUsage();
                    return;
                }
            }
            dns.ParentServer = new IPEndPoint(ip, remotePort);

            var t = new Thread(() => dns.Listen());
            t.Start();
            Console.Title = "DNS Cache Server at port " + listenPort;
            Console.WriteLine("Listening... (write 'exit' to exit)");
            var s = (Console.ReadLine()??"").ToLowerInvariant();
            while (s != "exit")
            {
                var words = s.Split(' ');
                switch (words[0])
                {
                    case "ap":
                    {
                        dns.AntiPoison = !dns.AntiPoison;
                        Console.WriteLine("[!]: AntiPoison set to " + dns.AntiPoison);
                        break;
                    }
                    case "?":
                    {
                        if (words.Length != 3)
                            break;
                        DnsQueryType a0;
                        if (!Enum.TryParse(words[1], true, out a0))
                            break;
                        var a1 = words[2];
                        dns.SendDnsRequestTo(dns.ParentServer, new RequestRecord { Class = DnsQueryClass.IN, Key = a1, Type = a0 });
                        Console.WriteLine("[!]: Sent!");
                        break;
                    }
                    case "?l":
                    {
                        if (words.Length != 3)
                            break;
                        DnsQueryType a0;
                        if (!Enum.TryParse(words[1], true, out a0))
                            break;
                        var a1 = words[2];
                        var node = dns.DomainRoot.Resolve(a1);
                        if (node == null)
                        {
                            Console.WriteLine("[!]: Not found! domain subtree is nt yet built");
                            break;
                        }
                        var results = node.GetAllRecords(false, a0).ToList();
                        results.Sort((a, b) => a.ExpirationTime < b.ExpirationTime ? 1 : (a.ExpirationTime == b.ExpirationTime ? 0 : -1));
                        Console.WriteLine("[i]: " + results.Count + " results found:");
                        foreach (var r in results)
                        {
                            Console.WriteLine("\t" + r);
                        }
                        break;
                    }
                    case "??":
                    {
                        if (words.Length != 4)
                            break;
                        DnsQueryType a0;
                        if (!Enum.TryParse(words[1], true, out a0))
                            break;
                        var a1 = words[2];
                        IPAddress a2;
                        if (!IPAddress.TryParse(words[3], out a2))
                            break;
                        dns.SendDnsRequestTo(new IPEndPoint(a2, 53), new RequestRecord { Class = DnsQueryClass.IN, Key = a1, Type = a0 });
                        Console.WriteLine("[!]: Sent!");
                        break;
                    }
                    case "poison":
                    {
                        if (words.Length != 4)
                            break;
                        IPAddress a0;
                        if (!IPAddress.TryParse(words[1], out a0))
                            break;
                        var a1 = words[2];
                        IPAddress a2;
                        if (!IPAddress.TryParse(words[3], out a2))
                            break;
                        var request = new RequestRecord {Class = DnsQueryClass.IN, Key = a1, Type = DnsQueryType.A};
                        var answer = new ResourceRecord
                        {
                            Class = DnsQueryClass.IN,
                            Key = a1,
                            Type = DnsQueryType.A,
                            Data = a2.GetAddressBytes(),
                            TTL = int.MaxValue
                        };
                        dns.SendDnsResponseTo(new IPEndPoint(a0, 53), (ushort) Rnd.Next(1, 65536), new[] { request }, new[] { answer }, null, new[] { answer });
                        Console.WriteLine("[!]: Sent!");
                        break;
                    }
                }
                s = (Console.ReadLine() ?? "").ToLowerInvariant();
            }
            dns.ShouldRun = false;
            t.Abort();
            t.Join();
            Console.Title = "DNS Cache Server, closing ...";

            var f = new StreamWriter("log.txt");
            foreach (var domain in dns.DomainRoot.SubDomains)
            {
                PrintDomain(domain, f);
            }
            f.Close();
        }

        static void PrintDomain(DomainTreeNode domain, StreamWriter f, string offset = "")
        {
            f.WriteLine(offset + "#DOMAIN: " + domain.AccumulateLabels());
            offset += "\t";
            foreach (var r in domain.Authority)
            {
                f.WriteLine(offset + "[auth]: " + r);
            }
            foreach (var r in domain.Cache)
            {
                f.WriteLine(offset + "[cache]: " + r);
            }
            foreach (var r in domain.AdditionalInfo)
            {
                f.WriteLine(offset + "[info]: " + r);
            }
            foreach (var d in domain.SubDomains)
            {
                PrintDomain(d,f, offset);
            }
        }

        static void ReportUsage()
        {
            Console.WriteLine("Usage: {0} <parent_dns_hostname_or_ip>:<[port]> <[listen_port]>");
        }

    }
}
