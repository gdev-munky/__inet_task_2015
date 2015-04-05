using System.Net;

namespace inet_t1
{
    public class WhoIsResult
    {
        public string ServerName { get; set; }
        public IPAddress IP { get; set; }
        public string Response { get; set; }
        public string Note { get; set; }

        public string CountryCode { get; set; }
        public string AS { get; set; }
        public string NetName { get; set; }
        public bool ProbablyLocal { get; set; }
    }
}
