namespace SmtpMime
{
    public abstract class EitherSecureStream
    {
        public bool Secure { get; protected set; }
        public abstract void Send(byte[] message);
        public abstract void Send(byte[] message, int offset, int length);
        public abstract int Read(byte[] a, int o, int l);

    }
}
