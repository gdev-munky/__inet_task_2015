using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;

namespace portscan
{
    class Program
    {
        internal static int MaxThreads;
        private static List<PortSpan> portSpans = new List<PortSpan>();
        internal static IPAddress _ip = IPAddress.Loopback;
        internal static int ThreadCount = 36;
        static void Main(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var s = args[i];
                var ns = (args.Length > i+1) ? args[i + 1] : null;
                ParseArg(s, ns);
            }
            Console.Title = "Scanning ...";
            Console.WriteLine("Scanning {0}...", _ip);

            IEnumerable<int> tcpOpened;
            IEnumerable<int> udpOpened;
            var stopWatch = new Stopwatch();

            stopWatch.Start();
            {
                var scanner = new PortScanner(_ip, ThreadCount);
                scanner.ReportProgress += ReportProgress;
                foreach (var span in portSpans)
                {
                    scanner.PushTaskGroup(span.FirstPort, span.LastPort, span.Udp);
                }
                scanner.Scan(out tcpOpened, out udpOpened);
            }
            stopWatch.Stop();

            Console.Title = "";
            var listTcp = tcpOpened.ToList();
            var listUdp = udpOpened.ToList();
            listTcp.Sort();
            listUdp.Sort();
            Console.WriteLine("Completed in {0} ms", stopWatch.ElapsedMilliseconds);
            Console.WriteLine("Opened TCP ports ({0}):", listTcp.Count());
            Console.WriteLine(string.Join(", ", listTcp));
            Console.WriteLine("Opened UDP ports ({0}):", listUdp.Count());
            Console.WriteLine(string.Join(", ", listUdp));
        }

        static void ParseArg(string arg, string nextArg)
        {
            ushort temp;
            switch (arg)
            {
                case "/tcp":
                case "/udp":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    try
                    {
                        var bounds = nextArg.Split(new[] {".."}, StringSplitOptions.None);
                        var s = int.Parse(bounds[0]);
                        var e = int.Parse(bounds[1]);
                        portSpans.Add(new PortSpan(s, e, arg == "/udp"));
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Argument error: failed to parse tcp port span. Format: {0} FIRST..LAST", arg);
                    }
                    return;
                case "/ip":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    if (!IPAddress.TryParse(nextArg, out _ip)) 
                        Console.WriteLine("Argument error: specified ip is not valid");
                    return;
                case "/threads":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    if (!int.TryParse(nextArg, out ThreadCount))
                        Console.WriteLine("Argument error: specified thread count is not valid");
                    return;
                case "/hostname":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    try
                    {
                        var p = Dns.GetHostEntry(nextArg);
                        _ip = p.AddressList.First();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Argument error: specified hostname is not valid");
                    }
                    return;
            }
        }

        static void ReportProgress(int a, int b)
        {
            Console.Title = string.Format("Progress : {0}/{1} - {2:P}", a, b, (float) a/b);
        }
    }

    class PortSpan
    {
        public int FirstPort { get; set; }
        public int LastPort { get; set; }
        public bool Udp { get; set; }

        public PortSpan(int f, int l, bool udp)
        {
            FirstPort = f;
            LastPort = l;
            Udp = udp;
        }
    }
}
