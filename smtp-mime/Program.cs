using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace SmtpMime
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && Directory.Exists(args[0]))
                Environment.CurrentDirectory = Path.GetFullPath(args[0]);

            var strSmtpServer = Ask("Input SMTP server address:");
            if (strSmtpServer == null)
                return;
            var client = new SmtpClient
            {
                AskFunc = Ask
            };
            client.SenderEmail = Ask("Input sender email:");
            if (client.SenderEmail == null)
                return;
            client.RecepientEmail = Ask("Input recipient email:");
            if (client.RecepientEmail == null)
                return;
            client.ClientMessage += EchoFuncClient;
            client.ServerMessage += EchoFuncServer;
            if (!client.Connect(strSmtpServer, 465))
            {
                Ask("Failed to connect.");
                return;
            }
            LOGIN:
            if (!client.ActLogin())
            {
                WriteColoredLine(ConsoleColor.Red, "Failed to auth");
                if (Ask("Try agian? (Enter YES to try again, anything else to exit):").ToUpperInvariant() == "YES")
                    goto LOGIN;
                return;
            }
            else
            {
                if (!client.ActFormMessage(EnumerateImagesInDirectory(Environment.CurrentDirectory)))
                {
                    WriteColoredLine(ConsoleColor.Red, "Failed to send");
                }
            }
            
            Ask("Press enter to exit");
        }

        static string Ask(string message, params object[] args)
        {
            Console.WriteLine(message, args);
            Console.Write(">");
            var s = Console.ReadLine();
            return string.IsNullOrEmpty(s) ? null : s;
        }
        static bool Ask(string message, out string answer)
        {
            Console.WriteLine(message);
            Console.Write(">");
            answer = Console.ReadLine();
            return !string.IsNullOrEmpty(answer);
        }
        static void WriteColoredLine(ConsoleColor color, string message)
        {
            var oldc = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldc;
        }
        static void EchoFuncServer(string a)
        {
            WriteColoredLine(ConsoleColor.DarkYellow, a);
        }
        static void EchoFuncClient(string a)
        {
            WriteColoredLine(ConsoleColor.DarkGreen, a);
        }

        static IEnumerable<FileInfo> EnumerateImagesInDirectory(string dir)
        {
            return Directory.GetFiles(dir).Select(f => 
                new FileInfo(f)).Where(f => 
                    MimeType.IsMimeTypeAnImage(MimeMapping.GetMimeMapping(f.Name)));
        }
    }
}
