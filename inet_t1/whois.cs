using System;
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

        public static bool IsIpLocal(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException("IPv6 and everything, except v4 is not yet supported");

            var bytes = ip.GetAddressBytes();
            switch (bytes[0])
            {
                case 10:
                case 127:
                    return true;
                case 172:
                    return bytes[1] > 15 && bytes[1] < 32;
                case 192:
                    return bytes[1] == 168;
            }
            return false;
        }

        public WhoIsResult GetInfoByIP(IPAddress ip)
        {
            var r = new WhoIsResult {IP = ip, ServerName = QueryWhoisByIP(ip)};
            r.ProbablyLocal = IsIpLocal(ip) || r.ServerName == null;
            if (r.ProbablyLocal)
            {
                r.ServerName =
                r.AS =
                r.NetName = 
                r.CountryCode = "(no info)";
                r.Note = "It is a local IP (or DB is corrupted)";
                return r;
            }
            r.Response = QueryWhoIs(ip, r.ServerName);
            r.AS = ExtractAS(r.Response);
            r.CountryCode = ExtractCountry(r.Response);
            r.NetName = ExtractNetName(r.Response);
            r.Note = "";
            return r;
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

            mainSocket.ReceiveAll();

            var err = mainSocket.TrySend(Encoding.UTF8.GetBytes(query));
            var attempts = 10;
            while (err != SocketError.Success && attempts > 0)
            {
                --attempts;
                Thread.Sleep(500);
                err = mainSocket.TrySend(Encoding.UTF8.GetBytes(query));
            }
            if (err != SocketError.Success)
            {
                Console.WriteLine("[Failed to send query to {0}]", provider);
                try { mainSocket.Disconnect(false); } catch { }
                try { mainSocket.Close();} catch { }
                return "";
            }
            Thread.Sleep(500);


            var data = mainSocket.ReceiveAll();
            mainSocket.Disconnect(false);
            mainSocket.Close();
            return Encoding.UTF8.GetString(data);
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
            var dbValue = _db[ip.GetAddressBytes()[0]];
            return dbValue.StartsWith("whois.") ? dbValue : null;
        }

        static readonly Regex RegexCountryExtractor = new Regex(@"(country:\s*)\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        static readonly Regex RegexASExtractor = new Regex(@"((origin(as)?)|(aut-num)):\s*AS\d+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        static readonly Regex RegexNetNameExtractor = new Regex(@"(netname|tech-c):\s*\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        static string ExtractWhoIsProperty(Regex r, string answer)
        {
            return (from Match match in r.Matches(answer) select match.Value)
                .Select(s => s.Substring(s.IndexOf(':') + 1).Trim())
                .FirstOrDefault();
        }

        static string ExtractCountry(string answer)
        {
            return ExtractWhoIsProperty(RegexCountryExtractor, answer) ?? "(no info)";
        }
        static string ExtractAS(string answer)
        {
            return ExtractWhoIsProperty(RegexASExtractor, answer) ?? "(no info)";
        }
        static string ExtractNetName(string answer)
        {
            return ExtractWhoIsProperty(RegexNetNameExtractor, answer) ?? "(no info)";
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
