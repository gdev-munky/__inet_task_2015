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
            var pos = path.LastIndexOf(".", StringComparison.Ordinal);
            if (pos < 0)
                return this;
            var label = path.Substring(pos + 1);
            var subDomain = SubDomains.FirstOrDefault(node => node.Label == label);
            if (subDomain == null)
                return null;
            return subDomain.Resolve(string.Join(".", path.Substring(0, pos)));
        }
        public string AccumulateLabels()
        {
            var str = Label;
            if (Parent == null)
                return str;
            return str + "." + Parent.AccumulateLabels();
        }

        public IEnumerable<DnsRecord> GetAllRecords(bool recursive = false, params DnsQueryType[] types)
        {
            foreach (var rec in Cache.Where(record => types.Contains(record.Type)))
                yield return rec;
            
            if (!recursive) 
                yield break;
            foreach (var rec in SubDomains.SelectMany(sub => sub.GetAllRecords(true, types)))
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

        public void Tick()
        {
            Cache.RemoveAll(record => record.IsOutdated);
            foreach (var subdomain in SubDomains)
                subdomain.Tick();
        }

        public void AddNewData(string path, ResourceRecord record)
        {
            while (path.EndsWith("."))
                path = path.Remove(path.Length - 1);
            var pos = path.LastIndexOf(".", StringComparison.Ordinal);
            if (pos < 0)
            {
                Cache.Add(new DnsRecord(record));
                return;
            }
            var label = path.Substring(pos + 1);
            var subDomain = SubDomains.FirstOrDefault(node => node.Label == label);
            if (subDomain == null)
            {
                subDomain = new DomainTreeNode(label, this);
                SubDomains.Add(subDomain);
            }
            subDomain.AddNewData(string.Join(".", path.Substring(0, pos)), record);
        }
    }
}
