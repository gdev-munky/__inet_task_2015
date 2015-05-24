using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace HttpProxy
{
    public class ProxyServer
    {
        private TcpListener _tcpl;
        private bool _shouldRun;
        private List<Connection> connections = new List<Connection>();

        public ProxyServer()
        {
            BufferSize = 8192;
        }

        public int BufferSize { get; set; }
        public int Port { get; private set; }
        public IPAddress ListenIP { get; private set; }
        public bool Runs { get; private set; }

        public void Start(int port = 4502)
        {
            Start(IPAddress.Any, port);
        }

        public void Start(IPAddress listenIp, int port = 4502)
        {
            Port = port;
            ListenIP = IPAddress.Any;
            _tcpl = new TcpListener(ListenIP, Port);

            _shouldRun = true;
            _tcpl.Start();

            new Task(ListenLoop).Start();
        }

        public void Stop()
        {
            Runs = false;
            _shouldRun = false;
            lock(connections) foreach (var c in connections)
            {
                c.ShouldRun = false;
            }
            _tcpl.Stop();
        }

        private void ListenLoop()
        {
            Runs = true;
            while (_shouldRun)
            {
                TcpClient client = null;
                try
                {
                    client = _tcpl.AcceptTcpClient();
                }
                catch (SocketException) { }
                if (client == null) continue;

                Console.WriteLine("Accepted client from {0}", client.Client.RemoteEndPoint);

                new Task(() => { HandleClient(client); }).Start();
            }
            Runs = false;
        }

        private void HandleClient(TcpClient client)
        {
            var con = new Connection(client);
            lock (connections)
            {
                connections.RemoveAll(c => !c.IsRunning);
                connections.Add(con);
            }
            con.Loop();
        }

    }
}
