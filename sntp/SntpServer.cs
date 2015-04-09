using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SNTP
{
    public class SntpServer
    {
        public int Port { get; set; }
        public TimeSpan TimeDelta { get; set; }
        private Socket ListenSocket { get; set; }
        public bool Running { get; private set; }
        public bool ShouldRun { get; set; }

        public SntpServer ()
        {
            Port = 123;
            TimeDelta = new TimeSpan(0, 0, 0, 0, 0);
            ShouldRun = true;
        }

        public void Run()
        {
            if (!ShouldRun)
                return;
            try
            {
                ListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                ListenSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                //ListenSocket.Listen(64);
                Running = true;

                while (ShouldRun)
                {
                    var buffer = new byte[512];
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    try
                    {
                        var recvd = ListenSocket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref ep);
                        //Console.WriteLine("[DBG]: Received udp packet from {0}", ep);
                        var request = SntpPacket.Read(buffer, 0, recvd);
                        if (request == null)
                            continue;
                        Console.WriteLine("[DBG]: That was NTP request from {0}", ep);

                        var response = SntpPacket.Response(request, TimeDelta);
                        var thr = new Thread(o =>
                        {
                            var t = (Tuple<SntpPacket, EndPoint>) o;
                            ListenSocket.SendTo(t.Item1.GetBytes(), t.Item2);
                            Console.WriteLine("[DBG]: Response was sent to {0}", t.Item2);
                        });
                        thr.Start(new Tuple<SntpPacket, EndPoint>(response, ep));
                    }
                    catch (SocketException e)
                    {
                    }
                }

                Running = false;
                ListenSocket.Close();
            }
            catch (Exception)
            {
                Running = false;
                throw;
            }
        }
    }
}
