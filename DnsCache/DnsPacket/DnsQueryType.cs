namespace DnsCache.DnsPacket
{
    public enum DnsQueryType : ushort
    {
        /// <summary>
        /// Host address
        /// </summary>
        A = 1,

        /// <summary>
        /// Authoritative name server
        /// </summary>
        NS = 2,

        /// <summary>
        /// Mail destination (Obsolete - use MX)
        /// </summary>
        MD = 3,

        /// <summary>
        /// Mail forwarder (Obsolete - use MX)
        /// </summary>
        MF = 4,

        /// <summary>
        /// Canonical name for an alias
        /// </summary>
        CNAME = 5,

        /// <summary>
        /// Marks the start of a zone of authority
        /// </summary>
        SOA = 6,

        /// <summary>
        /// Mailbox domain name (EXPERIMENTAL)
        /// </summary>
        MB = 7,

        /// <summary>
        /// Mail group member (EXPERIMENTAL)
        /// </summary>
        MG = 8,

        /// <summary>
        /// Mail rename domain name (EXPERIMENTAL)
        /// </summary>
        MR = 9,

        /// <summary>
        /// Null RR (EXPERIMENTAL)
        /// </summary>
        NULL = 10,

        /// <summary>
        /// Well known service description
        /// </summary>
        WKS = 11,

        /// <summary>
        /// Domain name pointer
        /// </summary>
        PTR = 12,

        /// <summary>
        /// Host information
        /// </summary>
        HINFO = 13,

        /// <summary>
        /// Mailbox or mail list information
        /// </summary>
        MINFO = 14,

        /// <summary>
        /// Mail exchange
        /// </summary>
        MX = 15,

        /// <summary>
        /// Text strings
        /// </summary>
        TXT = 16,

        /// <summary>
        /// IPv6 address
        /// </summary>
        AAAA = 28,

        /// <summary>
        /// Request for a transfer of an entire zone
        /// </summary>
        AXFR = 252,

        /// <summary>
        /// Request for mailbox-related records (MB, MG or MR)
        /// </summary>
        MAILB = 253,

        /// <summary>
        /// Request for mail agent RRs (Obsolete - see MX)
        /// </summary>
        MAILA = 254,

        /// <summary>
        /// Request for all records
        /// </summary>
        ANY = 255
    }
}