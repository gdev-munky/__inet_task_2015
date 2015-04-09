using System;
using System.Globalization;
using System.Linq;
using System.Net;

namespace SNTP
{
    class Program
    {
        private static double TimeDelta = 0.0;
        private static int Port = 23;
        static void Main(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var s = args[i];
                var ns = (args.Length > i + 1) ? args[i + 1] : null;
                ParseArg(s, ns);
            }
            var server = new SntpServer
            {
                TimeDelta = TimeSpan.FromMilliseconds(TimeDelta),
                Port = Port
            };
            Console.WriteLine("Listening ...");
            Console.Title = string.Format("SNTP server at port {0}", Port);
            server.Run();
        }
        static void ParseArg(string arg, string nextArg)
        {
            switch (arg)
            {
                case "/?":
                case "/help":
                case "help":
                case "?":
                    PrintHelp();
                    return;
                case "/port":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    if (!int.TryParse(nextArg, NumberStyles.Any, CultureInfo.InvariantCulture, out Port) || Port < 1 || Port > UInt16.MaxValue)
                    {
                        Console.WriteLine("Argument error: specified port is not valid");
                        Port = 23;
                    }
                    return;
                case "/delta":
                    if (string.IsNullOrWhiteSpace(nextArg))
                        return;
                    if (!double.TryParse(nextArg, NumberStyles.Any, CultureInfo.InvariantCulture, out TimeDelta))
                    {
                        Console.WriteLine("Argument error: specified time delta could not be parsed");
                        TimeDelta = 0.0;
                    }
                    return;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("portscan help: =================");
            Console.WriteLine(" /port N - sets port to listen (def: 23)");
            Console.WriteLine(" /delta Value - sets time delta (def: 0.0)");
            Console.WriteLine("================================");
        }
    }
}
