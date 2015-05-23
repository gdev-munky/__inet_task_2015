using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HttpProxy
{
    public abstract class HttpPacket
    {
        public HttpPacket()
        {
            Content = new byte[0];
            HttpVersion = "";
        }

        public string HttpVersion { get; protected set; }
        public abstract string GetHeader(string key, string defaultValue = null);
        public abstract IEnumerable<string> GetHeaders();
        public byte[] Content { get; protected set; }

        public abstract bool LoadFromBytes(byte[] bts);
        public abstract byte[] GetBytes();

        public static HttpRequestPacket ReadRequest(byte[] data)
        {
            var p = new HttpRequestPacket();
            return p.LoadFromBytes(data) ? p : null;
        }
        public static HttpResponsePacket ReadResponse(byte[] data)
        {
            var p = new HttpResponsePacket();
            return p.LoadFromBytes(data) ? p : null;
        }
    }

    public class HttpRequestPacket : HttpPacket
    {
        public string Method { get; protected set; }
        public string UriString { get; protected set; }
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();

        public HttpRequestPacket()
        {
            Method = "";
            UriString = "";
        }

        public override string GetHeader(string key, string defaultValue = null)
        {
            string value;
            return _headers.TryGetValue(key, out value) ? value : defaultValue;
        }

        public override IEnumerable<string> GetHeaders()
        {
            return _headers.Keys;
        }

        public override bool LoadFromBytes(byte[] bts)
        {
            var ms = new MemoryStream(bts);
            var sr = new StreamReader(ms);

            var s = sr.ReadLine();
            if (s == null) return false;
            
            var words = s.Split(' ');
            if (words.Length < 3) return false;

            Method = words[0].ToUpperInvariant();
            UriString = words[1].ToLowerInvariant();
            var httpv = words[2].ToUpperInvariant();
            if (httpv.StartsWith("HTTP"))
            {
                var id = httpv.IndexOf("/", StringComparison.Ordinal);
                if (id < 0 || id >= httpv.Length - 2)
                    return false;
                HttpVersion = httpv.Substring(id + 1);
            }

            while (true)
            {
                s = sr.ReadLine();
                if (string.IsNullOrEmpty(s))
                    break;

                var id = s.IndexOf(":", StringComparison.Ordinal);
                if (id < 0) continue;
                _headers[s.Substring(0, id)] = s.Substring(id + 1).Trim();
            }
            var bytesLeft = (int) (ms.Length - ms.Position);
            Content = new byte[bytesLeft];
            ms.Read(Content, 0, bytesLeft);
            return true;
        }

        public override byte[] GetBytes()
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);

            sw.Write("{0} {1} HTTP/{2}\r\n", Method, UriString, HttpVersion);
            foreach (var header in _headers)
                sw.Write("{0}: {1}\r\n", header.Key, header.Value);
            sw.Write("\r\n");
            sw.Flush();
            ms.Write(Content, 0, Content.Length);
            ms.Flush();

            return ms.ToArray();
        }
    }
    public class HttpResponsePacket : HttpPacket
    {
        public override IEnumerable<string> GetHeaders()
        {
            return _headers.Keys;
        }
        public string ResultCode { get; protected set; }
        public string ResultCodeDesc { get; protected set; }
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();

        public HttpResponsePacket()
        {
            ResultCode = "";
            ResultCodeDesc = "";
        }

        public override string GetHeader(string key, string defaultValue = null)
        {
            string value;
            return _headers.TryGetValue(key, out value) ? value : defaultValue;
        }

        public override bool LoadFromBytes(byte[] bts)
        {
            var ms = new MemoryStream(bts);
            var sr = new StreamReader(ms);

            var s = sr.ReadLine();
            if (s == null) return false;
            
            var words = s.Split(' ');
            if (words.Length < 3) return false;

            ResultCodeDesc = string.Join(" ", words.Skip(2));
            ResultCode = words[1].ToLowerInvariant();
            var httpv = words[0].ToUpperInvariant();
            if (httpv.StartsWith("HTTP"))
            {
                var id = httpv.IndexOf("/", StringComparison.Ordinal);
                if (id < 0 || id >= httpv.Length - 2)
                    return false;
                HttpVersion = httpv.Substring(id + 1);
            }

            while (true)
            {
                s = sr.ReadLine();
                if (string.IsNullOrEmpty(s))
                    break;

                var id = s.IndexOf(":", StringComparison.Ordinal);
                if (id < 0) continue;
                _headers[s.Substring(0, id)] = s.Substring(id + 1).Trim();
            }
            var bytesLeft = (int) (ms.Length - ms.Position);
            Content = new byte[bytesLeft];
            ms.Read(Content, 0, bytesLeft);
            return true;
        }

        public override byte[] GetBytes()
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);

            sw.Write("HTTP/{0} {1} {2}\r\n", HttpVersion, ResultCode, ResultCodeDesc);
            foreach (var header in _headers)
                sw.Write("{0}: {1}\r\n", header.Key, header.Value);
            sw.Write("\r\n");
            sw.Flush();
            ms.Write(Content, 0, Content.Length);
            ms.Flush();
            return ms.ToArray();
        }
    }
}
