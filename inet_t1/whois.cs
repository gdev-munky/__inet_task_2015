using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace inet_t1
{
    public class WhoIs
    {
        public bool DebugOutput { get; set; }
        private string[] _db;
        public WhoIs(bool rebuild = false)
        {
            if (rebuild) BuildDB();
            _db = LoadDB();
        }

        public string GetCountryByIP(IPAddress ip)
        {
            var whois = QueryWhoisByIP(ip);
            if (whois == "#")
                return "unknown";
            return ExtractCountry(QueryWhoIs(ip, whois));
        }
        public void GetInfoByIP(IPAddress ip, out string sAS, out string sCountry, out string sNote, out string sWhois)
        {
            var whois = QueryWhoisByIP(ip);
            if (whois == "#")
            {
                sWhois = 
                sAS = 
                sCountry = "(no info)";
                sNote = "(?) Probably it is a local IP";
                return;
            }
            sWhois = whois;
            var whoisAnswer = QueryWhoIs(ip, whois);
            sAS = ExtractAS(whoisAnswer);
            sCountry = ExtractCountry(whoisAnswer);
            sNote = "";
        }

        public string QueryWhoIs(IPAddress ip, string whoisServer)
        {
            var ipstr = ip.ToString();
            if (!ipstr.EndsWith("\r\n"))
                ipstr += "\r\n";
            var provider = QueryWhoisByIP(ip);
            if (provider == "whois.arin.net")
                ipstr = "n " + ipstr;
            var result = Query(whoisServer, ipstr);
            if (DebugOutput)
                Debug_SaveWhoisAnswer(ip, provider, result);
            return result;
        }

        static string Query(string provider, string query)
        {
            var mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = 1000
            };
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
                int id;
                if (!int.TryParse(idStr, out id)) continue;
                if (id < 0 || id > 255) continue;

                if (!line.Contains("whois."))
                    continue;
                var tmp = line.Substring(line.IndexOf("whois."), 17);
                result[id] = tmp.Trim();
            }

            using (var f = new StreamWriter("whois_db.txt"))
                f.Write(string.Join(Environment.NewLine, result));
        }

        static string[] LoadDB()
        {
            var result = new string[256];
            using (var f = new StreamReader("whois_db.txt"))
                for (var i = 0; i < result.Length; i++)
                    result[i] = f.ReadLine();
            return result;
        }

        string QueryWhoisByIP(IPAddress ip)
        {
            return _db[ip.GetAddressBytes()[0]];
        }

        static string ExtractCountry(string answer)
        {
            var r = new Regex(@"(country:\s*)\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var matches = r.Matches(answer);
            foreach (var s in from Match match in matches select match.Value)
            {
                return s.Substring(s.IndexOf(':')+1).Trim();
            }
            return "(no info)";
        }
        static string ExtractAS(string answer)
        {
            var r = new Regex(@"(origin(as)?:\s*)AS\d+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var matches = r.Matches(answer);
            foreach (var s in from Match match in matches select match.Value)
            {
                return s.Substring(s.IndexOf(':')+1).Trim();
            }
            return "(no info)";
        }

        static void Debug_SaveWhoisAnswer(IPAddress ip, string whoisServer, string answer)
        {
            var date = DateTime.Now;
            using (var f = new StreamWriter("whois_" + ip + "_" + whoisServer + ".txt"))
            {
                f.WriteLine("Written at {0:F}", date);
                f.WriteLine("WhoIs server: " + whoisServer);
                f.WriteLine("Queried IP: " + ip);
                f.WriteLine("=======================================");
                f.WriteLine(answer);
                f.Write("=======================================");
            }
        }

        public void UpdateDataBase()
        {
            BuildDB();
            _db = LoadDB();
        }
    }
}
