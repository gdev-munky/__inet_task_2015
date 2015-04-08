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
                bts.AddRange(Encoding.ASCII.GetBytes(hostname));
            }
            bts.Add(0);
            return bts.ToArray();
        }


        public static ProtocolTester CreateDNSTester()
        {
            return new ProtocolTester
            {
                Name = "DNS",
                Tcp = false,
                Udp = true,
                UdpTester = TestUDP_DNS
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
            
            var bts = new byte[48];
            bts[0] = 0x08;
            try
            {
                sock.SendTimeout = sock.ReceiveTimeout = timeout;
                sock.SendTo(bts.ToArray(), ep);

                var recvBTS = new byte[8192];
                EndPoint recvEP = new IPEndPoint(ep.Address, ep.Port);
                var recvLen = sock.ReceiveFrom(recvBTS, ref recvEP);
                if (recvLen <= 0)
                    return false;
                //Console.WriteLine("[Debug, NTP] Received response via UDP (from {0}), len={1}", recvEP, recvLen);
                return recvLen == 48;
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
            return (greeting.Contains("POP3"));
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
                sock.Send(Encoding.ASCII.GetBytes("GET /wiki/HTTP HTTP/1.0" + Environment.NewLine + Environment.NewLine));
                //Console.WriteLine("[Debug, HTTP] : send ok");
                recvd = sock.Receive(bytes);
                //Console.WriteLine("[Debug, HTTP] : recv ok");
            }
            catch(SocketException e)
            {
                //Console.WriteLine("[Debug, HTTP] : " + e.SocketErrorCode);
                return false;
            }
            var greeting = Encoding.ASCII.GetString(bytes, 0, recvd);
            //Console.WriteLine("[Debug, HTTP] : {0}", greeting);
            return (greeting.Contains("HTTP"));
        }
    }
}
