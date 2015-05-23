using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HttpProxy
{
    public class Connection
    {
        public TcpClient Client { get; set; }
        public TcpClient Server { get; set; }

        public NetworkStream ClientStream { get; set; }
        public NetworkStream ServerStream { get; set; }

        public bool ShouldRun { get; set; }
        public bool IsRunning { get; private set; }

        public Connection(TcpClient client)
        {
            Client = client;
            ShouldRun = true;
            ClientStream = Client.GetStream();
        }

        public void DisconnectServer()
        {
            if (Server == null)
                return;
            Server.Close();
            Server = null;
            ServerStream = null;
        }
        public void DisconnectClient()
        {
            if (Client == null)
                return;
            Client.Close();
            Client = null;
            ClientStream = null;
        }

        public void Loop()
        {
            IsRunning = true;
            var keepAlive = true;
            while (ShouldRun)
            {
                if (ClientStream == null || !keepAlive)
                {   
                    ShouldRun = false;
                    break;
                }

                var clientMessage = ClientStream.ReadAll();
                if (clientMessage.Length < 0)
                {
                    Console.WriteLine("[ C ] : Disconnected");
                    ShouldRun = false;
                    break;
                }
                var httpRequest = HttpPacket.ReadRequest(clientMessage);
                if (httpRequest == null)
                {
                    Console.WriteLine("[ C ] : Sent non HTTP. (length = {0})", clientMessage.Length);
                    ShouldRun = false;
                    break;
                }
                var method = httpRequest.Method;
                var host = httpRequest.GetHeader("Host");
                keepAlive = httpRequest.GetHeader("Connection", "").ToLowerInvariant() == "keep-alive";
                Console.WriteLine("[ C ] : Got HTTP {1} request, {2}->{0}", host, method, Client.Client.RemoteEndPoint);

                // <debug>
                //Console.WriteLine(string.Join("; ", httpRequest.GetHeaders()));
                // </debug>

                if (Server == null && !TryConnectToServer(host))
                {
                    Console.WriteLine("[ C ] : Host {0} is unreachable", host);
                    ShouldRun = false;
                    break;
                }
                ServerStream.Write(clientMessage, 0, clientMessage.Length);
                Console.WriteLine("[ C ] : Sent message to {0}", host);

                // == Receiving server response =================================

                var serverMessage = ServerStream.ReadAll();
                if (serverMessage.Length < 0)
                {
                    Console.WriteLine("[ S ] : Disconnected");
                    ShouldRun = false;
                    break;
                }
                var httpResponse = HttpPacket.ReadResponse(serverMessage);
                if (httpResponse == null)
                {
                    Console.WriteLine("[ S ] : Sent non HTTP. (length = {0})", serverMessage.Length);
                    ShouldRun = false;
                    break;
                }

                if (httpResponse.GetHeader("Connection", "").ToLowerInvariant() == "close")
                    keepAlive = false;
                Console.WriteLine("[ S ] : Got HTTP reponse {0}->{1} ({2})", Server.Client.RemoteEndPoint, Client.Client.RemoteEndPoint, httpResponse.ResultCode);
                ClientStream.Write(serverMessage, 0, serverMessage.Length);
                Console.WriteLine("[ S ] : Sent message to {0}", Client.Client.RemoteEndPoint);

                //Console.WriteLine("Keep-Alive: " + keepAlive);
            }
            Console.WriteLine("[ ! ] Finished");
            IsRunning = false;
            DisconnectClient();
            DisconnectServer();
        }

        private bool TryConnectToServer(string host)
        {
            try
            {
                Server = new TcpClient(host, 80);
            }
            catch (Exception)
            {
                Server = null;
                return false;
            }
            ServerStream = Server.GetStream();
            return true;
        }
    }

    internal static class Ext
    {
        public static int BufferSize = 4096;
        public static byte[] ReadAll(this NetworkStream stream)
        {
            var buffer = new byte[BufferSize];
            var message = new List<byte>();
            while (true)
            {
                int len;
                try { len = stream.Read(buffer, 0, BufferSize); }
                catch { break; }
                if (len < 1) break;
                message.AddRange(buffer.Take(len));
                if (!stream.DataAvailable)
                    break;
            }
            return message.ToArray();
        }
    }
}
