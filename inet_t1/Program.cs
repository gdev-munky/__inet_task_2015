using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace inet_t1
{
    class Program
    {
        private static WhoIs whois;
        static void Main(string[] args)
        {
            const string whoisDb = "whois_db.txt";
            var rebuild = false;
            if (!File.Exists(whoisDb))
                rebuild = true;
            else if ((DateTime.Now - File.GetLastWriteTime(whoisDb)).Days >= 7)
                rebuild = true;

            Console.WriteLine("Loading whois db from {0}...", rebuild ? "web" : "disk");
            whois = new WhoIs(rebuild);
            foreach (var s in args)
            {
                try
                {
                    IPAddress ip;
                    if (!IPAddress.TryParse(s, out ip))
                    {
                        var entry = Dns.GetHostEntry(s);
                        ip = entry.AddressList.First();
                    }
                    TraceRt(ip);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("Done!");
        }

        static void TraceRt(IPAddress ip, int ttl = 30, int packets = 1)
        {
            Console.WriteLine("Tracing route to ip : {0} ...", ip);
            for (var step = 0; step < ttl; ++step)
            {
                using (var pinger = new Ping())
                {
                    var time = DateTime.Now;
                    var reply = pinger.Send(ip, 5000, new byte[] {}, new PingOptions(step + 1, false));
                    var dtime = (DateTime.Now - time).TotalMilliseconds;
                    if (reply == null)
                    {
                        Console.WriteLine("[{0,2}] : no reply", step);
                        continue;
                    }
                    if (reply.Status != IPStatus.Success && reply.Status != IPStatus.TtlExpired )
                    {
                        Console.WriteLine("[{0,2}] : error={1}", step, reply.Status);
                        continue;
                    }
                    Console.Write("[{0,2}] : ip={1,15}; time={2} ms; country: ", step, reply.Address, Math.Round(dtime));
                    Console.WriteLine(whois.CountryByIP(reply.Address));

                    if (Equals(ip, reply.Address))
                        return;
                }
            }
        }
    }
}
