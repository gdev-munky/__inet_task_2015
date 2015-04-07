using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace portscan
{
    public delegate void DReportProgress(int done, int total);

    public class PortScanner
    {
        public int ThreadCount { get; private set; }
        public IPAddress ScanTarget { get; private set; }
        public int TaskCount { get; private set; }
        public event DReportProgress ReportProgress;

        private ScannerThread[] _threads;
        private PortChecker _checker;

        public PortScanner(IPAddress epoint, int threadsCount = 36)
        {
            _checker= new PortChecker(epoint);
            ThreadCount = threadsCount;
            ScanTarget = epoint;
            _threads = new ScannerThread[ThreadCount];
            for (var i = 0; i < ThreadCount; ++i)
                _threads[i] = new ScannerThread(_checker);
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

        public void Scan(out IEnumerable<int> openTcpPorts, out IEnumerable<int> openUdpPorts, int progressReportDelay = 1000)
        {
            foreach (var thr in _threads)
            {
                thr.BeginProcess();
            }
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
            var tcp = new List<int>();
            var udp = new List<int>();
            foreach (var thr in _threads)
            {
                thr.EndProcess();
                var openPorts = thr.Tasks.Where(task => task.IsOpen).ToArray();
                tcp.AddRange(openPorts.Where(task => !task.Udp).Select(t => t.Port));
                udp.AddRange(openPorts.Where(task => task.Udp).Select(t => t.Port));
            }
            openTcpPorts = tcp;
            openUdpPorts = udp;
        }
    }

    public class ScannerThread
    {
        public ScannerThread(PortChecker checker)
        {
            Checker = checker;
            Tasks = new List<ScannerTask>();
            Finished = false;
            Processed = 0;
        }
        private Thread MyThread { get; set; }
        public List<ScannerTask> Tasks { get; private set; }
        public int Processed { get; private set; }
        public PortChecker Checker { get; set; }
        public bool Finished { get; private set; }

        public void BeginProcess()
        {
            MyThread = new Thread(Process);
            MyThread.Start();
        }
        public void EndProcess()
        {
            MyThread.Join();
        }
        public void Process()
        {
            Finished = false;
            foreach (var task in Tasks)
            {
                task.IsOpen = task.Udp
                    ? Checker.IsUdpOpened(task.Port)
                    : Checker.IsTcpOpened(task.Port);
                Processed++;
            }
            Finished = true;
        }
    }

    public class ScannerTask
    {
        public int Port { get; set; }
        public bool Udp { get; set; }
        public bool IsOpen { get; set; }
    }
}
