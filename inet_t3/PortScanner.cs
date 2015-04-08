using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace portscan
{
    public delegate void DReportProgress(int done, int total);

    public class PortScanner
    {
        public event DReportProgress ReportProgress;
        public int ThreadCount { get; private set; }
        public IPAddress ScanTarget { get; private set; }
        public int TaskCount { get; private set; }
        public List<ProtocolTester> ProtocolTesters { get; set; }

        private readonly ScannerThread[] _threads;

        public PortScanner(IPAddress epoint, int threadsCount = 36)
        {
            ThreadCount = threadsCount;
            ScanTarget = epoint;
            _threads = new ScannerThread[ThreadCount];
            ProtocolTesters = new List<ProtocolTester>();
            var checker = new PortChecker(epoint, ProtocolTesters);
            for (var i = 0; i < ThreadCount; ++i)
                _threads[i] = new ScannerThread(checker);
        }

        public void PushTaskGroup(int portS, int portE, bool udp)
        {
            var portsToProcess = (portE - portS + 1);
            for (var i = 0; i < portsToProcess; ++i)
            {
                var task = new ScannerTask
                {
                    Port = i + portS,
                    Udp = udp
                };
                _threads[i % ThreadCount].Tasks.Add(task);
            }
            TaskCount += portsToProcess;
        }

        public void Scan(out IEnumerable<ProtocolPort> openTcpPorts, out IEnumerable<ProtocolPort> openUdpPorts, int progressReportDelay = 1000)
        {
            foreach (var thr in _threads)
                thr.BeginProcess();
            var finished = false;
            while (!finished)
            {
                finished = true;
                var tasksComplete = 0;
                foreach (var t in _threads)
                {
                    if (!t.Finished)
                        finished = false;
                    tasksComplete += t.Processed;
                }
                ReportProgress(tasksComplete, TaskCount);
                if (!finished)
                    Thread.Sleep(progressReportDelay);
            }
            var tcp = new List<ProtocolPort>();
            var udp = new List<ProtocolPort>();
            foreach (var thr in _threads)
            {
                thr.EndProcess();
                var openPorts = thr.Tasks.Where(task => task.IsOpen).ToArray();
                tcp.AddRange(openPorts.Where(task => !task.Udp).Select(t => new ProtocolPort(t.Port, t.ProtocolName)));
                udp.AddRange(openPorts.Where(task => task.Udp).Select(t => new ProtocolPort(t.Port, t.ProtocolName)));
            }
            openTcpPorts = tcp;
            openUdpPorts = udp;
        }
    }

    

    public class ScannerTask
    {
        public int Port { get; set; }
        public bool Udp { get; set; }
        public bool IsOpen { get; set; }
        public string ProtocolName { get; set; }
    }

    public class ProtocolPort : IComparable<ProtocolPort>
    {
        public int Port { get; set; }
        public string Name { get; set; }

        public ProtocolPort(int p, string name)
        {
            Port = p;
            Name = name;
        }

        public int CompareTo(ProtocolPort other)
        {
            return Port.CompareTo(other.Port);
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Port, Name);
        }
    }
}
