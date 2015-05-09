using System;
using System.Collections.Generic;
using System.Linq;
using DnsCache.DnsPacket;

namespace DnsCache.DnsDataBase
{
    public class DomainTreeNode
    {
        public DomainTreeNode(string label = "", DomainTreeNode parent = null)
        {
            Label = label;
            Parent = parent;
            SubDomains = new List<DomainTreeNode>();
            Cache = new List<DnsRecord>();
        }
        public string Label { get; set; }

        public DomainTreeNode Parent { get; set; }
        public List<DomainTreeNode> SubDomains { get; set; }
        public List<DnsRecord> Cache { get; set; }

        public DomainTreeNode Resolve(string path)
        {
            while (path.EndsWith("."))
                path = path.Remove(path.Length - 1);
            if (path.Length < 1)
                return this;
            var pos = path.LastIndexOf(".", StringComparison.Ordinal);
            var label = (pos < 0) ? path : path.Substring(pos + 1);
            var subDomain = SubDomains.FirstOrDefault(node => node.Label == label);
            if (subDomain == null)
                return null;
            return subDomain.Resolve((pos < 0) ? "":string.Join(".", path.Substring(0, pos)));
        }
        public string AccumulateLabels()
        {
            var str = Label;
            if (Parent == null)
                return str;
            return str + "." + Parent.AccumulateLabels();
        }

        public IEnumerable<DnsRecord> GetAllRecordsByType(DnsQueryType type)
        {
            if (type == DnsQueryType.ANY)
                foreach (var rec in Cache)
                    yield return rec;
            else
                foreach (var rec in Cache.Where(record => record.Type == type))
                    yield return rec;
        }

        public override string ToString()
        {
            return AccumulateLabels();
        }

        public static Packet FormRequest(bool inverse = false, params Tuple<string, DnsQueryType>[] requests)
        {
            return FormRequest(inverse,
                requests.Select(
                    rpair => new RequestRecord {Class = DnsQueryClass.IN, Key = rpair.Item1, Type = rpair.Item2})
                    .ToArray());
        }
        public static Packet FormRequest(bool inverse = false, params RequestRecord[] requests)
        {
            var p = new Packet
            {
                Id = (ushort)(Program.Rnd.Next() % 0xffff),
                Flags = DnsPacketFlags.RecursionIsDesired
            };
            if (inverse)
                p.Flags |= DnsPacketFlags.Inverse;
            p.Queries.AddRange(requests);
            return p;
        }

        public IEnumerable<DnsRecord> Tick()
        {
            var cacheToRemove = Cache.Where(r => r.IsOutdated).ToArray();
            foreach (var r in cacheToRemove)
            {
                Cache.Remove(r);
                yield return r;
            }
            foreach (var r in SubDomains.SelectMany(subdomain => subdomain.Tick()))
                yield return r;
            
        }

        public bool AddNewData(string path, ResourceRecord record)
        {
            while (path.EndsWith("."))
                path = path.Remove(path.Length - 1);
            var pos = path.LastIndexOf(".", StringComparison.Ordinal);

            if (path.Length < 1)
            {
                var worked = false;
                var dnsrecord = new DnsRecord(record);

                var sameRecord = Cache.FirstOrDefault(r => r.Type == record.Type && Equals(r.Data, record.Data));
                if (sameRecord != null)
                {
                    if (sameRecord.SecondsLeft < record.TTL)
                        sameRecord.SecondsLeft = record.TTL;
                }
                else
                {
                    Cache.Add(dnsrecord);
                    worked = true;
                }

                return worked;
            }

            var label = (pos < 0) ? path : path.Substring(pos + 1);
            var subDomain = SubDomains.FirstOrDefault(node => node.Label == label);
            if (subDomain == null)
            {
                subDomain = new DomainTreeNode(label, this);
                SubDomains.Add(subDomain);
            }
            return subDomain.AddNewData((pos < 0) ? "" :string.Join(".", path.Substring(0, pos)), record);
        }

        internal static bool Equals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            return !a.Where((t, i) => t != b[i]).Any();
        }
    }
}
