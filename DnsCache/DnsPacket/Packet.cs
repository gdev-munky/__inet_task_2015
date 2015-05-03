using System.Collections.Generic;
using System.Linq;

namespace DnsCache.DnsPacket
{
    public class Packet : IMySerializable
    {
        public ushort Id { get; set; }
        public DnsPacketFlags Flags { get; set; }
        public List<RequestRecord> Queries { get; set; }
        public List<ResourceRecord> Answers { get; set; }
        public List<ResourceRecord> AuthorityRecords { get; set; }
        public List<ResourceRecord> AdditionalRecords { get; set; }
        public byte[] GetBytes()
        {
            var bts = new List<byte>();
            bts.AddRange(BEBitConverter.GetBytes(Id));
            bts.AddRange(BEBitConverter.GetBytes((ushort)Flags));
            bts.AddRange(BEBitConverter.GetBytes((ushort)Queries.Count));
            bts.AddRange(BEBitConverter.GetBytes((ushort)Answers.Count));
            bts.AddRange(BEBitConverter.GetBytes((ushort)AuthorityRecords.Count));
            bts.AddRange(BEBitConverter.GetBytes((ushort)AdditionalRecords.Count));
            bts.AddRange(Queries.SelectMany(record => record.GetBytes()));
            bts.AddRange(Answers.SelectMany(record => record.GetBytes()));
            bts.AddRange(AuthorityRecords.SelectMany(record => record.GetBytes()));
            bts.AddRange(AdditionalRecords.SelectMany(record => record.GetBytes()));
            return bts.ToArray();
        }

        public void FromBytes(byte[] bytes, ref int offset)
        {
            Id = BEBitConverter.ToUInt16(bytes, offset); offset+=2;
            Flags = (DnsPacketFlags) BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            var l1 = BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            var l2 = BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            var l3 = BEBitConverter.ToUInt16(bytes, offset); offset += 2;
            var l4 = BEBitConverter.ToUInt16(bytes, offset); offset += 2;

            Queries.Clear();
            Answers.Clear();
            AuthorityRecords.Clear();
            AdditionalRecords.Clear();

            for (var i = 0; i < l1; ++i)
            {
                var p = new RequestRecord();
                p.FromBytes(bytes, ref offset);
                Queries.Add(p);
            }
            for (var i = 0; i < l2; ++i)
            {
                var p = new ResourceRecord();
                p.FromBytes(bytes, ref offset);
                Answers.Add(p);
            }
            for (var i = 0; i < l3; ++i)
            {
                var p = new ResourceRecord();
                p.FromBytes(bytes, ref offset);
                AuthorityRecords.Add(p);
            }
            for (var i = 0; i < l4; ++i)
            {
                var p = new ResourceRecord();
                p.FromBytes(bytes, ref offset);
                AdditionalRecords.Add(p);
            }
        }

        public Packet()
        {
            Queries = new List<RequestRecord>();
            Answers = new List<ResourceRecord>();
            AuthorityRecords = new List<ResourceRecord>();
            AdditionalRecords = new List<ResourceRecord>();
        }
    }

    public static class DnsPacketFlagsExtensions
    {
        public static bool IsQuery(this DnsPacketFlags flags)
        {
            return !flags.HasFlag(DnsPacketFlags.Response);
        }
        public static bool IsStandart(this DnsPacketFlags flags)
        {
            return !flags.HasFlag(DnsPacketFlags.Inverse) && !flags.HasFlag(DnsPacketFlags.ServerStatus);
        }
        public static bool IsSuccessfull(this DnsPacketFlags flags)
        {
            return (flags & DnsPacketFlags.ReservedError0F) == 0;
        }
    }
}
