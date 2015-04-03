using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace inet_t1
{
    public class WhoIs
    {
        private string[] db;
        public WhoIs(bool rebuild = false)
        {
            if (rebuild) BuildDB();
            db = LoadDB();
        }

        public string CountryByIP(IPAddress ip)
        {
            var whois = QueryWhoisByIP(ip);
            if (whois == "#")
                return "unknown";
            return ExtractCountry(QueryWhoIs(ip, whois));
        }

        public string QueryWhoIs(IPAddress ip, string whoisServer)
        {
            var ipstr = ip.ToString();
            if (!ipstr.EndsWith("\r\n"))
                ipstr += "\r\n";
            var provider = QueryWhoisByIP(ip);
            if (provider == "whois.arin.net")
                ipstr = "n " + ipstr;
            return Query(whoisServer, ipstr);
        }

        static string Query(string provider, string query)
        {
            var mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            mainSocket.ReceiveTimeout = 1000;
            mainSocket.Connect(provider, 43);

            recvAll(mainSocket);

            Thread.Sleep(500);
            mainSocket.Send(Encoding.UTF8.GetBytes(query));
            Thread.Sleep(500);

            var data = recvAll(mainSocket);
            mainSocket.Close();
            return Encoding.UTF8.GetString(data);
        }

        static byte[] recvAll(Socket socket)
        {
            var result = new List<byte>();
            var sockList = new List<Socket> { socket };
            var emptyList = new List<Socket>();
            Socket.Select(sockList, emptyList, emptyList, 500);
            while (sockList.Any())
            {
                var data = new byte[0xffff];
                var recvd = socket.Receive(data);
                if (recvd == 0)
                    break;
                for (var i = 0; i < recvd; ++i)
                {
                    result.Add(data[i]);
                }
            }
            return result.ToArray();
        }

        static void BuildDB()
        {
            var wc = new WebClient();
            var dbStr = wc.DownloadString(
                "http://www.internetassignednumbersauthority.org/assignments/ipv4-address-space/ipv4-address-space.txt");

            var db = dbStr.Split('\n');
            var result = new string[256];

            for (var id = 0; id < 256; ++id)
            {
                result[id] = "#";
            }

            foreach (var line in db)
            {
                if (line.Length < 80)
                    continue;
                var idStr = line.Substring(3, 3);
                int id = -1;
                if (!int.TryParse(idStr, out id)) continue;
                if (id < 0 || id > 255) continue;

                if (!line.Contains("whois."))
                    continue;
                var tmp = line.Substring(line.IndexOf("whois."), 17);
                result[id] = tmp.Trim();
            }

            var f = new StreamWriter("whois_db.txt");
            foreach (var s in result)
            {
                f.WriteLine(s);
            }
            f.Close();
        }

        static string[] LoadDB()
        {
            var result = new string[256];
            var f = new StreamReader("whois_db.txt");
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = f.ReadLine();
            }
            f.Close();
            return result;
        }

        string QueryWhoisByIP(IPAddress ip)
        {
            return db[ip.GetAddressBytes()[0]];
        }

        static string ExtractCountry(string answer)
        {
            var lines = answer.Split('\n');

            foreach (var line in lines)
            {
                if (line.StartsWith("country: "))
                {
                    return line.Substring("country: ".Length).Trim();
                }
                if (line.StartsWith("Country: "))
                {
                    return line.Substring("country: ".Length).Trim();
                }
            }
            return "unknown";
        }
    }
}
