using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

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
            var ttl = 30;
            var outputWhoisServer = false;
            for (int id = 0; id < args.Length; id++)
            {
                var s = args[id];
                if (s == "-debug")
                {
                    whois.DebugOutput = !whois.DebugOutput;
                    continue;
                }
                if (s == "-outputserver")
                {
                    outputWhoisServer = !outputWhoisServer;
                    continue;
                }
                if (s == "-upd")
                {
                    whois.UpdateDataBase();
                    continue;
                }
                if (s == "-ttl")
                {
                    try { ttl = int.Parse(args[id + 1]); }
                    catch {Console.WriteLine("Failed to set TTL (arg id = {0}).", id);} 
                    continue;
                }
                try
                {
                    TraceRt(s, ttl, outputWhoisServer);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("Done!");
        }

        static void TraceRt(string host, int maxttl = 30, bool outputWhoisServer = false)
        {
            Console.Write("Tracing route to " + host);
            IPAddress ip;
            if (!IPAddress.TryParse(host, out ip))
            {
                try
                {
                    var entry = Dns.GetHostEntry(host);
                    ip = entry.AddressList.First();
                }
                catch (Exception)
                {
                    Console.WriteLine(" (wtf?)");
                    Console.WriteLine("Failed to resolve hostname '{0}'.", host);
                    return;
                }
            }
            Console.WriteLine(" (ip: {0})", ip);
            for (var step = 0; step < maxttl; ++step)
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
                    if (reply.Status != IPStatus.Success && reply.Status != IPStatus.TtlExpired)
                    {
                        Console.WriteLine("[{0,2}] : error={1}", step, reply.Status);
                        continue;
                    }
                    Console.Write("[{0,2}] : ip={1,15}; latency={2} ms; ", step, reply.Address, Math.Round(dtime));

                    string sAS, sNote, sCountry, sServer;
                    whois.GetInfoByIP(reply.Address, out sAS, out sCountry, out sNote, out sServer);
                    if (sServer.StartsWith("("))
                        Console.Write("whois: {0}", sServer);
                    
                    else
                    {
                        if (outputWhoisServer)
                            Console.Write("whois: {2}; country: {0}; AS: {1}", sCountry, sAS, sServer);
                        else
                            Console.Write("country: {0}; AS: {1}", sCountry, sAS);
                    }
                    

                    if (!string.IsNullOrEmpty(sNote))
                        Console.WriteLine("; note: " + sNote);
                    else 
                        Console.WriteLine();

                    if (Equals(ip, reply.Address))
                        return;
                }
            }
        }
    }
}
