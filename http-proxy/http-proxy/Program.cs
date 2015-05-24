using System;

namespace HttpProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            var port = 4502;
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out port))
                    port = 4502;
            }

            Console.WriteLine("Launching at port {0} ...", port);
            var proxy = new ProxyServer();
            proxy.Start(port);
            Console.WriteLine("Listening! Press enter to exit");
            Console.ReadLine();
            Console.WriteLine("Stopping ...");
            proxy.Stop();
        }
    }
}
