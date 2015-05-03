namespace DnsCache.DnsPacket
{
    public enum DnsQueryClass : ushort
    {
        /// <summary>
        /// Internet
        /// </summary>
        IN = 1,
        /// <summary>
        /// CSNET class (Obsolete - used only for examples in some obsolete RFCs)
        /// </summary>
        CS = 2,
        /// <summary>
        /// CHAOS
        /// </summary>
        CH = 3,
        /// <summary>
        /// Hesiod [Dyer 87]
        /// </summary>
        HS = 4
    }
}