using System.Net.Security;
using System.Net.Sockets;

namespace SmtpMime
{
    public class SecureStream : EitherSecureStream
    {
        private readonly SslStream _stream;

        public SecureStream(TcpClient c, string hostname)
        {
            _stream = new SslStream(c.GetStream());
            _stream.AuthenticateAsClient(hostname);
            Secure = true;
        }

        public override void Send(byte[] message)
        {
            _stream.Write(message);
        }

        public override void Send(byte[] message, int offset, int length)
        {
            _stream.Write(message, offset, length);
        }

        public override int Read(byte[] a, int o, int l)
        {
            return _stream.Read(a, o, l);
        }
    }
}
