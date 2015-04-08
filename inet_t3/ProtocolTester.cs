using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace portscan
{
    public delegate bool DTestUDPProtocol(Socket socket, IPEndPoint ep, int timeout);
    public delegate bool DTestTCPProtocol(Socket socket, byte[] bytes, int recvd, int timeout);
    public class ProtocolTester
    {
        public bool Udp { get; set; }
        public bool Tcp { get; set; }
        public string Name { get; set; }

        public DTestTCPProtocol TcpTester { get; set; }
        public DTestUDPProtocol UdpTester { get; set; }

        public bool TestTCP(Socket s, byte[] bytes, int recvd, int timeout)
        {
            return TcpTester != null && TcpTester(s, bytes, recvd, timeout);
        }
        public bool TestUDP(Socket s, IPEndPoint ep, int timeout)
        {
            return UdpTester != null && UdpTester(s, ep, timeout);
        }
    }
}
