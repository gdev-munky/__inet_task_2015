using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace SmtpMime
{
    public delegate bool AskDelegate(string q, out string a);
    public class SmtpClient
    {
        public bool SupportsAuth { get; private set; }
        public bool BinaryMime { get; private set; }
        public string RecepientEmail { get; set; }
        public string SenderEmail { get; set; }
        private TcpClient _server;
        private EitherSecureStream _stream;
        public event Action<string> ServerMessage;
        public event Action<string> ClientMessage;
        public AskDelegate AskFunc;
        public bool Connected { get; private set; }
        public bool Helloed { get; private set; }
        public bool SupportsSsl { get; private set; }
        public SmtpClient()
        {
            CurrentEncoding = Encoding.UTF8;
        }
        public bool Connect(string hostname, bool secure = false)
        {
            SupportsAuth =
            Helloed = 
            Connected = 
            SupportsSsl = false;
            _server = null;
            _stream = null;
            try
            {
                _server = new TcpClient(hostname, secure ? 465 : 25);
                _stream = secure ? new SecureStream(_server, hostname) : (EitherSecureStream)new UnsecureStream(_server);
                
                Connected = true;
            }
            catch (Exception) { }
            return Connected;
        }
        public Encoding CurrentEncoding { get; set; }
        private string Read()
        {
            var bts = _stream.ReadAll();
            var s = CurrentEncoding.GetString(bts);
            while (s.EndsWith("/r/n"))
                s = s.Substring(0, s.Length - 2);
            while (s.EndsWith("/n"))
                s = s.Substring(0, s.Length - 1);

            OnServerMessage(s);

            return s;
        }
        private void Write(string s, params object[] args)
        {
            var str = s.Format(args)+"\r\n";
            var bts = CurrentEncoding.GetBytes(str);
            _stream.Send(bts);
            OnClientMessage(s);
        }
        public SmtpAnswer Request(string command, params string[] args)
        {
            Write(command + " " + string.Join(" ", args));
            var s = Read();
            var a = new SmtpAnswer();
            var lines = s.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
                a.Add(line);
            return a;
        }
        public SmtpAnswer RequestSingle(string command)
        {
            Write(command);
            var s = Read();
            var a = new SmtpAnswer();
            var lines = s.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
                a.Add(line);
            return a;
        }
        protected virtual void OnServerMessage(string arg1)
        {
            var handler = ServerMessage;
            if (handler != null) handler(arg1);
        }
        protected virtual void OnClientMessage(string obj)
        {
            var handler = ClientMessage;
            if (handler != null) handler(obj);
        }
        protected bool Ask(string q, out string a)
        {
            a = null;
            if (AskFunc == null)
                return false;
            return AskFunc(q, out a);
        }

        public bool ActHello()
        {
            if (Helloed)
                return true;
            Read();
            var a = Request("EHLO kek.ru");
            if (a.GetCodes().Any(c => c < 200 || c > 300))
                return false;
            BinaryMime = a.GetMessages().Any(s => s.Contains("BINARYMIME"));
            SupportsSsl = a.GetMessages().Any(s => s.Contains("STARTTLS"));
            SupportsAuth = a.GetMessages().Any(s => s.Contains("AUTH") && s.Contains("LOGIN"));
            Helloed = true;
            return true;
        }
        public bool ActLogin()
        {
            if (!Helloed)
                if (!ActHello())
                    return false;
            if (!SupportsAuth)
                return true;
            var a = Request("AUTH LOGIN");
            var q = a.GetMessages().FirstOrDefault();
            if (a.GetCodes().Any(c => c != 334) || q == null)
                return false;
            var message = Ext.FromBase64(q);
            var value = "";
            if (!Ask(message, out value))
                return false;
            a = Request(value.ToBase64());
            q = a.GetMessages().FirstOrDefault();
            if (a.GetCodes().Any(c => c != 334) || q == null)
                return false;
            message = Ext.FromBase64(q);
            if (!Ask(message, out value))
                return false;
            a = Request(value.ToBase64());
            if (a.GetCodes().Any(c => c < 200 || c > 300))
                return false;
            return true;
        }
        public bool ActFormMessage(IEnumerable<FileInfo> filesToSend)
        {
            if (!Helloed)
                if (!ActHello())
                    return false;

            var a = Request("MAIL FROM:", "<" + SenderEmail + ">");
            if (a.GetCodes().Any(c => c < 200 || c > 300))
                return false;

            a = Request("RCPT TO:", "<" + RecepientEmail + ">");
            if (a.GetCodes().Any(c => c < 200 || c > 300))
                return false;

            a = Request("DATA");
            if (a.GetCodes().Any(c => c != 354))
                return false;
            Write("from: " + SenderEmail);
            Write("to: " + RecepientEmail);
            Write("subject: test");
            Write("MIME-Version: 1.0");
            var boundary = MimeType.CreateFormDataBoundary();
            Write("Content-Type: multipart/mixed; boundary=" + boundary);
            Write("");
            OnClientMessage("... file data begin ...");
            foreach (var file in filesToSend)
            {
                if (BinaryMime)
                    file.WriteMultipartFormData_Bin(_stream, boundary, MimeMapping.GetMimeMapping(file.Name));
                else
                    file.WriteMultipartFormData_Base64(_stream, boundary, MimeMapping.GetMimeMapping(file.Name));
            }
            _stream.Send(MimeType.GenerateEndBoundary(boundary));
            OnClientMessage("... file data end ...");

            Write("Sample Text");
            a = RequestSingle(".");
            if (a.GetCodes().Any(c => c != 250))
                return false;

            return true;
        }
    }

    internal static class Ext
    {
        public static int BufferSize = 4096;
        public static byte[] ReadAll(this EitherSecureStream stream)
        {
            var buffer = new byte[BufferSize];
            while (true)
            {
                int len;
                try { len = stream.Read(buffer, 0, BufferSize); }
                catch { break; }
                if (len < 1) break;
                return buffer.Take(len).ToArray();
            }
            return new byte[0];
        }
        public static string FromBase64(string s)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }
        public static string ToBase64(this string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        }
        public static string Format(this string s, params object[] args)
        {
            return string.Format(s, args);
        }
    }
}
