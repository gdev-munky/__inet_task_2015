using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace inet_t3
{
    class Program
    {
        private static int _portStart = 0x0000;
        private static int _portEnd = 0xffff;
        private static IPAddress _ip = IPAddress.Loopback;
        static void Main(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var s = args[i];
                var ns = (args.Length > i+1) ? args[i + 1] : null;
                ParseArg(s, ns);
            }
            Console.Title = "Preparing ...";
            Console.WriteLine("Forming tasks ...");
            var portChecker = new PortChecker(_ip);
            var tasks = new List<ScanTask>();
            for (var port = _portStart; port <= _portEnd; ++port)
            {
                var t = new ScanTask
                {
                    MyPort = port,
                    Checker = portChecker,
                    ResetEvent = new ManualResetEvent(false),
                    UseUDP = false
                };
                ThreadPool.QueueUserWorkItem(a => ((ScanTask)a).Run(), t); 
                tasks.Add(t);

                t = new ScanTask
                {
                    MyPort = port,
                    Checker = portChecker,
                    ResetEvent = new ManualResetEvent(false),
                    UseUDP = true
                };
                ThreadPool.QueueUserWorkItem(a => ((ScanTask)a).Run(), t); 
                tasks.Add(t);
            }
            Console.WriteLine("Scanning ...");
            var resetEvents = tasks.Select(task => (WaitHandle) task.ResetEvent).ToList();
            var offset = 0;
            while (resetEvents.Count > offset)
            {
                Console.Title = string.Format("Waiting threads {0}/{1} - {2:P}", 
                    offset, resetEvents.Count,
                    (float)offset / resetEvents.Count);
                WaitHandle.WaitAll(resetEvents.Where((handle, i) => i >= offset && i < offset+64).ToArray());
                offset += 64;
            }

            var tcpOpened = new List<int>();
            var udpOpened = new List<int>();
            foreach (var scanTask in tasks.Where(scanTask => scanTask.Result))
            {
                if (scanTask.UseUDP)
                    udpOpened.Add(scanTask.MyPort);
                else
                    tcpOpened.Add(scanTask.MyPort);
            }

            Console.Title = "Complete";
            Console.WriteLine("Opened TCP ports in range [{0}..{1}]:", _portStart, _portEnd);
            Console.WriteLine(string.Join(", ", tcpOpened));
            Console.WriteLine("Opened UDP ports in range [{0}..{1}]:", _portStart, _portEnd);
            Console.WriteLine(string.Join(", ", udpOpened));
        }

        static void ParseArg(string arg, string nextArg)
        {
            ushort temp;
            switch (arg)
            {
                case "/s":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    if (!ushort.TryParse(nextArg, out temp))
                        Console.WriteLine("Argument error: specified start port is not valid");
                    _portStart = temp;
                    return;
                case "/e":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    if (!ushort.TryParse(nextArg, out temp))
                        Console.WriteLine("Argument error: specified end port is not valid");
                    _portEnd = temp;
                    return;
                case "/ip":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    if (!IPAddress.TryParse(nextArg, out _ip)) 
                        Console.WriteLine("Argument error: specified ip is not valid");
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
    }

    class ScanTask
    {
        public int MyPort { get; set; }
        public PortChecker Checker { get; set; }
        public ManualResetEvent ResetEvent { get; set; }
        public bool UseUDP { get; set; }
        public bool Result { get; set; }

        public void Run()
        {
            Result = UseUDP ? Checker.IsUdpOpened(MyPort) : Checker.IsTcpOpened(MyPort);
            ResetEvent.Set();
        }
    }
}
