using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace portscan
{
    public static class Testers
    {
        public static void AddBytesBE(this List<byte> bts, ushort data)
        {
            bts.AddRange(BitConverter.GetBytes(data).Reverse());
        }
        public static byte[] FormDNSName(string hostname)
        {
            var bts = new List<byte>();
            var parts = hostname.Split('.');
            foreach (var part in parts)
            {
                bts.Add((byte)part.Length);//Assuming it is < 64
                bts.AddRange(Encoding.ASCII.GetBytes(part));
            }
            bts.Add(0);
            return bts.ToArray();
        }


        public static ProtocolTester CreateDNSTester()
        {
            return new ProtocolTester
            {
                Name = "DNS",
                Tcp = true,
                Udp = true,
                UdpTester = TestUDP_DNS,
                TcpTester = TestTCP_DNS
            };
        }
        public static bool TestUDP_DNS(Socket sock, IPEndPoint ep, int timeout)
        {
            const ushort dnsPacketId = 666;
            const ushort dnsFlags = 2 << 11;
            const ushort dnsRequestCount = 1;
            const ushort dnsAnswerCount = 0;
            const ushort dnsAccessRightsCount = 0;
            const ushort dnsAdditionalInfoCount = 0;
            var bts = new List<byte>();
            bts.AddBytesBE(dnsPacketId);
            bts.AddBytesBE(dnsFlags);
            bts.AddBytesBE(dnsRequestCount);
            bts.AddBytesBE(dnsAnswerCount);
            bts.AddBytesBE(dnsAccessRightsCount);
            bts.AddBytesBE(dnsAdditionalInfoCount);

            const ushort dnsRequest0Type = 1;
            const ushort dnsRequest0Class = 1;
            bts.AddRange(FormDNSName("google.com"));
            bts.AddBytesBE(dnsRequest0Type);
            bts.AddBytesBE(dnsRequest0Class);

            try
            {
                sock.SendTimeout = sock.ReceiveTimeout = timeout;
                sock.SendTo(bts.ToArray(), ep);

                var recvBTS = new byte[8192];
                EndPoint recvEP = new IPEndPoint(ep.Address, ep.Port);
                var recvLen = sock.ReceiveFrom(recvBTS, ref recvEP);
                if (recvLen <= 0)
                    return false;
                //Console.WriteLine("[Debug, DNS] Received response via UDP (from {0}), len={1}", recvEP, recvLen);
                return (recvBTS[0] == bts[0]) && (recvBTS[1] == bts[1]);
            }
            catch
            {
                return false;
            }
            return false;
        }
        public static bool TestTCP_DNS(Socket sock, byte[] bytes, int recvd, int timeout)
        {
            var port = ((IPEndPoint) sock.RemoteEndPoint).Port;
            const ushort dnsPacketId = 666;
            const ushort dnsFlags = 2 << 11;
            const ushort dnsRequestCount = 1;
            const ushort dnsAnswerCount = 0;
            const ushort dnsAccessRightsCount = 0;
            const ushort dnsAdditionalInfoCount = 0;
            var bts = new List<byte>();
            bts.AddBytesBE(dnsPacketId);
            bts.AddBytesBE(dnsFlags);
            bts.AddBytesBE(dnsRequestCount);
            bts.AddBytesBE(dnsAnswerCount);
            bts.AddBytesBE(dnsAccessRightsCount);
            bts.AddBytesBE(dnsAdditionalInfoCount);

            const ushort dnsRequest0Type = 1;
            const ushort dnsRequest0Class = 1;
            bts.AddRange(FormDNSName("google.com"));
            bts.AddBytesBE(dnsRequest0Type);
            bts.AddBytesBE(dnsRequest0Class);

            try
            {
                sock.SendTimeout = sock.ReceiveTimeout = timeout;
                sock.Send(bts.ToArray());

                var recvBTS = new byte[8192];
                var recvLen = sock.Receive(recvBTS);
                if (recvLen <= 0)
                    return false;
                //Console.WriteLine("[Debug, DNS] Received response via TCP (from {0}), len={1}", sock.RemoteEndPoint, recvLen);
                //Console.WriteLine(Encoding.ASCII.GetString(recvBTS, 0 , recvLen));
                var ep = sock.RemoteEndPoint;
                if (!sock.Connected)
                try
                {
                    Console.WriteLine("Reconnecting to port({0}) after checking for DNS via TCP", port);
                    sock.Close();
                    sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    sock.Connect(ep);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to reconnect to {0} after checking for DNS via TCP - {1}", ep, ex);
                }
                return (recvBTS[0] == bts[0]) && (recvBTS[1] == bts[1]);
            }
            catch
            {
                return false;
            }
            return false;
        }

        public static ProtocolTester CreateNTPTester()
        {
            return new ProtocolTester
            {
                Name = "NTP",
                Tcp = false,
                Udp = true,
                UdpTester = TestUDP_NTP
            };
        }
        public static bool TestUDP_NTP(Socket sock, IPEndPoint ep, int timeout)
        {
            //Console.WriteLine("Sending NTP request to {0}, timeout is {1} ms", ep, timeout);
            var bts = new byte[48];
            bts[0] = 0x08;
            try
            {
                /*var debug = (ep.Port == 123);
                if (debug)
                    Console.WriteLine("Sending NTP request to {0}, timeout is {1} ms", ep, timeout);*/
                
                sock.SendTimeout = sock.ReceiveTimeout = timeout;
                sock.SendTo(bts.ToArray(), ep);
                /*if (debug)
                    Console.WriteLine("Sent!");*/

                var recvBTS = new byte[8192];
                EndPoint recvEP = new IPEndPoint(ep.Address, ep.Port);
                var recvLen = sock.ReceiveFrom(recvBTS, ref recvEP);
                /*if (debug)
                    Console.WriteLine("Got response, length = {0}", recvLen);*/
                if (recvLen <= 0)
                    return false;
                //Console.WriteLine("[Debug, NTP] Received response via UDP (from {0}), len={1}", recvEP, recvLen);
                var fb = recvBTS[0];
                return ((fb & 0x07) == 4);

            }
            catch
            {
                return false;
            }
        }

        public static ProtocolTester CreateSMTPTester()
        {
            return new ProtocolTester
            {
                Name = "SMTP",
                Tcp = true,
                Udp = false,
                TcpTester = TestTCP_SMTP
            };
        }
        public static bool TestTCP_SMTP(Socket sock, byte[] bytes, int recvd, int timeout)
        {
            if (recvd <= 0)
                return false;
            var greeting = Encoding.ASCII.GetString(bytes, 0, recvd);
            return (greeting.Contains("SMTP"));
        }

        public static ProtocolTester CreatePOP3Tester()
        {
            return new ProtocolTester
            {
                Name = "POP3",
                Tcp = true,
                Udp = false,
                TcpTester = TestTCP_POP3
            };
        }
        public static bool TestTCP_POP3(Socket sock, byte[] bytes, int recvd, int timeout)
        {
            if (recvd <= 0)
                return false;
            var greeting = Encoding.ASCII.GetString(bytes, 0, recvd);
            return greeting.Contains("POP3");
        }

        public static ProtocolTester CreateHTTPTester()
        {
            return new ProtocolTester
            {
                Name = "HTTP",
                Tcp = true,
                Udp = false,
                TcpTester = TestTCP_HTTP
            };
        }
        public static bool TestTCP_HTTP(Socket sock, byte[] bytes, int recvd, int timeout)
        {
            sock.SendTimeout = sock.ReceiveTimeout = timeout;
            try
            {
                sock.Send(Encoding.ASCII.GetBytes("GET / HTTP/1.1" + Environment.NewLine + Environment.NewLine));
                //Console.WriteLine("[Debug, HTTP] : send ok");
                recvd = sock.Receive(bytes);
                //Console.WriteLine("[Debug, HTTP] : at {0} - recv ok", sock.RemoteEndPoint);
            }
            catch(SocketException e)
            {
                //Console.WriteLine("[Debug, HTTP] : at {0} - socker error : {1}", sock.RemoteEndPoint, e.SocketErrorCode);
                return false;
            }
            var greeting = Encoding.ASCII.GetString(bytes, 0, recvd);
            //Console.WriteLine("[Debug, HTTP] : at {0} - {1}{2}", sock.RemoteEndPoint, Environment.NewLine, greeting);
            return (greeting.Contains("HTTP"));
        }
    }
}
