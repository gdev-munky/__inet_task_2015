using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace portscan
{
    public class PortChecker
    {
        public PortChecker(IPAddress ip, int timeout = 150)
        {
            IP = ip;
            TimeOut = timeout;
        }
        public IPAddress IP { get; set; }
        public int TimeOut { get; set; }
        public bool IsTcpOpened(int port)
        {
            if (IP == null)
                throw new NullReferenceException("IP is set to null, cannot do anything");

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var endPoint = new IPEndPoint(IP, port);
            var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var success = false;
            try
            {
                sock.Connect(endPoint, TimeOut);
                success = true;
            }
            catch {}
            sock.Close();
            stopWatch.Stop();
            return success;
        }

        public bool IsUdpOpened(int port)
        {
            if (IP == null)
                throw new NullReferenceException("IP is set to null, cannot do anything");
            EndPoint ep = new IPEndPoint(IP, port);
            EndPoint recvEP = new IPEndPoint(IP, port);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var sendbytes = Encoding.ASCII.GetBytes("Just scanning you");
            socket.ReceiveTimeout = 
            socket.SendTimeout = 400;
            var i = socket.SendTo(sendbytes, ep);
            var buffer = new byte[10240];
            try
            {
                var recvd = socket.ReceiveFrom(buffer, ref recvEP);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                    return false;
                //if (ex.SocketErrorCode == SocketError.ConnectionRefused)
                return false;
            }
            
            return true;
        }
    }
}
