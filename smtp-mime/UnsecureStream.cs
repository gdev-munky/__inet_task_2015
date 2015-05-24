using System.Net.Sockets;

namespace SmtpMime
{
    public class UnsecureStream : EitherSecureStream
    {
        private readonly NetworkStream _stream;

        public UnsecureStream(TcpClient c)
        {
            _stream = c.GetStream();
            Secure = false;
        }

        public override void Send(byte[] message)
        {
            Send(message, 0, message.Length);
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
