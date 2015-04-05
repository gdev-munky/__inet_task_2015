using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace inet_t1
{
    public static class SocketExtensions
    {
        public static byte[] ReceiveAll(this Socket socket)
        {
            var result = new List<byte>();
            var sockList = new List<Socket> {socket};
            var emptyList = new List<Socket>();
            Socket.Select(sockList, emptyList, emptyList, 500);
            while (sockList.Any())
            {
                var data = new byte[0xffff];
                var recvd = socket.Receive(data);
                if (recvd == 0)
                    break;
                result.AddRange(data.Take(recvd));
            }
            return result.ToArray();
        }

        public static SocketError TrySend(this Socket socket, byte[] data, int offset = 0, int length = -1, SocketFlags flags = SocketFlags.None)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            if (socket == null)
                throw new ArgumentNullException("socket");
            if (length < 0)
                length = data.Length;
            SocketError err;
            var btsSend = socket.Send(data, offset, length, flags, out err);
            return err;
        }
    }
}
