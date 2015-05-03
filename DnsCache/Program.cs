using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using DnsCache.DnsDataBase;

namespace DnsCache
{
    class Program
    {
        internal static Random Rnd;
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: {0} <parent_dns_hostname_or_ip>");
                return;
            }
            Rnd = new Random();
            var dns = new DnsCacheServer {ShouldRun = true};

            var ip = IPAddress.Any;
            if (!IPAddress.TryParse(args[0], out ip))
            {
                try
                {
                    var entries = Dns.GetHostEntry(args[0]);
                    if (entries.AddressList.Length < 0)
                        return;
                    ip = entries.AddressList.First();
                }
                catch (Exception)
                {
                    Console.WriteLine("Usage: {0} <parent_dns_hostname_or_ip>");
                    return;
                }
            }
            dns.ParentServer = new IPEndPoint(ip, 53);
            var t = new Thread(() => dns.Listen());
            t.Start();
            Console.WriteLine("Listening...");
            var s = (Console.ReadLine()??"").ToLowerInvariant();
            while (s != "exit")
            {
                Console.WriteLine("Write exit to exit");
                s = (Console.ReadLine() ?? "").ToLowerInvariant();
            }
            dns.ShouldRun = false;
            t.Abort();
            t.Join();

            var f = new StreamWriter("log.txt");
            foreach (var domain in dns.DomainRoot.SubDomains)
            {
                printDomain(domain, f);
            }
            f.Close();
        }

        static void printDomain(DomainTreeNode domain, StreamWriter f, string offset = "")
        {
            f.WriteLine(offset + "#DOMAIN: " + domain.AccumulateLabels());
            offset += "\t";
            foreach (var r in domain.Cache)
            {
                f.WriteLine(offset + r);
            }
            foreach (var d in domain.SubDomains)
            {
                printDomain(d,f, offset);
            }
        }

    }
}
