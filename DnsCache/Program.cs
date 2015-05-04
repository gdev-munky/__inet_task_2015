using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DnsCache.DnsDataBase;

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
            var dns = new DnsCacheServer {ShouldRun = true};

            var ip = IPAddress.Any;
            if (!IPAddress.TryParse(args[0], out ip))
            {
                try
                {
                    var entries = Dns.GetHostEntry(args[0]).AddressList;
                    Console.WriteLine(string.Join("; ", entries.Cast<object>()));
                    if (entries.Length < 0)
                    {
                        Console.WriteLine("Failed to resolve '{0}'", args[0]);
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
            dns.ParentServer = new IPEndPoint(ip, 53);
            ushort port = 53;
            if (args.Length == 2)
            {
                if (!ushort.TryParse(args[1], out port))
                {
                    ReportUsage();
                    return;
                }
            }

            dns.Port = port;

            var t = new Thread(() => dns.Listen());
            t.Start();
            Console.Title = "DNS Cache Server at port " + port;
            Console.WriteLine("Listening... (write 'exit' to exit)");
            var s = (Console.ReadLine()??"").ToLowerInvariant();
            while (s != "exit")
            {
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
            Console.WriteLine("Usage: {0} <parent_dns_hostname_or_ip> <[listen_port]>");
        }

    }
}
