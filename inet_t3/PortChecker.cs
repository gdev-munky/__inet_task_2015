using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace portscan
{
    public class PortChecker
    {
        public PortChecker(IPAddress ip, List<ProtocolTester> testers, int timeout = 250)
        {
            IP = ip;
            TimeOut = timeout;
            ProtocolTesters = testers;
        }
        public List<ProtocolTester> ProtocolTesters { get; set; }
        public IPAddress IP { get; set; }
        public int TimeOut { get; set; }
        public bool IsTcpOpened(int port, out string protocol)
        {
            protocol = "";
            if (IP == null)
                throw new NullReferenceException("IP is set to null, cannot do anything");

            var endPoint = new IPEndPoint(IP, port);
            var success = false;
            foreach (var tester in ProtocolTesters.Where(t => t.Tcp))
            {
                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    sock.ReceiveTimeout = sock.SendTimeout = TimeOut;
                    sock.Connect(endPoint, TimeOut);
                    success = true;

                    var bts = new byte[4096];
                    var recvd = 0;
                    protocol = "?";
                    try
                    {
                        recvd = sock.Receive(bts);
                    }
                    catch
                    {
                    }
                    if (tester.TestTCP(sock, bts, recvd, TimeOut))
                    {
                        protocol = tester.Name;
                        sock.Close();
                        return true;
                    }
                }
                catch
                {
                }
                sock.Close();
                if (!success)
                    break;
            }

            return success;
        }

        public bool IsUdpOpened(int port, out string protocol)
        {
            protocol = "";
            if (IP == null)
                throw new NullReferenceException("IP is set to null, cannot do anything");
            var ep = new IPEndPoint(IP, port);
            
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            foreach (var t in ProtocolTesters.Where(t => t.TestUDP(socket, ep, TimeOut)))
            {
                protocol = t.Name;
                return true;
            }
            return false;
        }
    }
}
