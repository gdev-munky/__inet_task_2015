using System.Collections.Generic;
using System.Threading;

namespace portscan
{
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
                string protocol;
                task.IsOpen = task.Udp
                    ? Checker.IsUdpOpened(task.Port, out protocol)
                    : Checker.IsTcpOpened(task.Port, out protocol);
                task.ProtocolName = protocol;
                Processed++;
            }
            Finished = true;
        }
    }
}
