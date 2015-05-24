using System;
using System.Collections.Generic;
using System.Linq;

namespace SmtpMime
{
    public class SmtpAnswer
    {
        public List<Tuple<int, string>> Answers = new List<Tuple<int, string>>();
        public IEnumerable<int> GetCodes()
        {
            return Answers.Select(o => o.Item1);
        }
        public IEnumerable<string> GetMessages()
        {
            return Answers.Select(o => o.Item2);
        }
        public IEnumerable<string> GetFullMessages()
        {
            return Answers.Select(o => o.Item1 + " " + o.Item2);
        }

        public void Add(int code, string message)
        {
            Answers.Add(new Tuple<int, string>(code, message));
        }
        public bool Add(string s)
        {
            int code; string message;
            if (!SplitMessage(s, out code, out message))
                return false;
            Add(code, message);
            return true;
        }
        internal static bool SplitMessage(string str, out int code, out string message)
        {
            code = 0;
            message = str;
            if (str.Length < 3)
                return false;
            var num = str.Substring(0, 3);
            if (!int.TryParse(num, out code))
                return false;
            message = string.Join("", str.Substring(3).Skip(1));
            return true;
        }
    }
}
