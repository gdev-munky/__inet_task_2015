using System.Net;
using System.Net.Sockets;

namespace portscan
{
    public static class SocketExtensions
    {
        /// <summary>
        /// Connects the specified socket.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="endpoint">The IP endpoint.</param>
        /// <param name="timeout">The timeout.</param>
        public static void Connect(this Socket socket, EndPoint endpoint, int timeout)
        {
            var result = socket.BeginConnect(endpoint, null, null);

            var success = result.AsyncWaitHandle.WaitOne(timeout, true);
            if (success)
                socket.EndConnect(result);
            else
            {
                socket.Close();
                throw new SocketException(10060); // Connection timed out.
            }
        }
    }
}
