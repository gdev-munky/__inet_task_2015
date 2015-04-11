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
        private static readonly List<PortSpan> PortSpans = new List<PortSpan>();
        internal static IPAddress IP = IPAddress.Loopback;
        internal static int ThreadCount = 1024;
        static void Main(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var s = args[i];
                var ns = (args.Length > i+1) ? args[i + 1] : null;
                if (!ParseArg(s, ns))
                    return;
            }
            Console.Title = "Scanning ...";
            Console.WriteLine("Scanning {0}...", IP);

            IEnumerable<ProtocolPort> tcpOpened;
            IEnumerable<ProtocolPort> udpOpened;
            var stopWatch = new Stopwatch();

            stopWatch.Start();
            {
                var scanner = new PortScanner(IP, ThreadCount);
                scanner.ProtocolTesters.AddRange(new[]
                {
                    Testers.CreateHTTPTester(),
                    Testers.CreateNTPTester(),
                    Testers.CreateDnsTester(),
                    Testers.CreateSMTPTester(),
                    Testers.CreatePOP3Tester()
                });
                scanner.ReportProgress += ReportProgress;
                foreach (var span in PortSpans)
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

            var dnsUdpPort = listUdp.FirstOrDefault(port => port.Name == "DNS");
            if (dnsUdpPort != null)
            {
                var dnsTcpPort = listTcp.FirstOrDefault(p => p.Port == dnsUdpPort.Port);
                if (dnsTcpPort != null)
                    dnsTcpPort.Name = dnsUdpPort.Name;
            }
            Console.WriteLine("Completed in {0} ms", stopWatch.ElapsedMilliseconds);
            Console.WriteLine("Opened TCP ports ({0}):", listTcp.Count());
            Console.WriteLine(string.Join(", ", listTcp));
            Console.WriteLine("Opened UDP ports ({0}):", listUdp.Count());
            Console.WriteLine(string.Join(", ", listUdp));
        }

        static bool ParseArg(string arg, string nextArg)
        {
            switch (arg)
            {
                case "/?":
                case "/help":
                //case "help":
                //case "?":
                    PrintHelp();
                    return false;
                case "/tcp":
                case "/udp":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return true;
                    try
                    {
                        var bounds = nextArg.Split(new[] {".."}, StringSplitOptions.None);
                        var s = int.Parse(bounds[0]);
                        var e = int.Parse(bounds[1]);
                        PortSpans.Add(new PortSpan(s, e, arg == "/udp"));
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Argument error: failed to parse tcp port span. Format: {0} FIRST..LAST", arg);
                    }
                    return true;
                case "/ip":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return false;
                    if (!IPAddress.TryParse(nextArg, out IP)) 
                        Console.WriteLine("Argument error: specified ip is not valid");
                    return true;
                case "/threads":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return false;
                    if (!int.TryParse(nextArg, out ThreadCount))
                        Console.WriteLine("Argument error: specified thread count is not valid");
                    return true;
                case "/hostname":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return false;
                    try
                    {
                        var p = Dns.GetHostEntry(nextArg);
                        IP = p.AddressList.First();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Argument error: specified hostname is not valid");
                        return false;
                    }
                    return true;
            }
            return true;
        }

        static void PrintHelp()
        {
            Console.WriteLine("portscan help: =================");
            Console.WriteLine(" /ip IP - sets IP to scan (def: 127.0.0.1)");
            Console.WriteLine(" /hostname H - sets host to scan");
            Console.WriteLine(" /tcp A..B - adds a TCP port span [A, B]");
            Console.WriteLine(" /udp A..B - adds a UDP port span [A, B]");
            Console.WriteLine(" /threads N - sets thread count to N (def: 1024)");
            Console.WriteLine("================================");
            Console.WriteLine(" You may specify either ip or hostname. Also, multiple port spans are allowed");
            Console.WriteLine(" You should not specify more then 1k threads for big port spans");
            Console.WriteLine("================================");
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
