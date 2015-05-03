using System;

namespace DnsCache.DnsPacket
{
    [Flags]
    public enum DnsPacketFlags : ushort
    {
        None = 0,
        Response = 0x8000,
        Inverse = 0x0800,
        ServerStatus = 0x1000,

        FormatError = 0x0001,
        ServerFaliure = 0x0002,
        NameError = 0x0003,
        NotSupportedError = 0x0004,
        RefusedError = 0x0005,
        ReservedError06 = 0x0006,
        ReservedError07 = 0x0007,
        ReservedError08 = 0x0008,
        ReservedError09 = 0x0009,
        ReservedError0A = 0x000a,
        ReservedError0B = 0x000b,
        ReservedError0C = 0x000c,
        ReservedError0D = 0x000d,
        ReservedError0E = 0x000e,
        ReservedError0F = 0x000f,

        AnswerIsAuthoritative = 0x0400,
        Truncated = 0x0200,
        RecursionIsDesired = 0x0100,
        RecursionIsAllowed = 0x0080,
    }
}