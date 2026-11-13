using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ashfall.Modules.Inventory
{
    public sealed class ContentRecord
    {
        public readonly string Kind, SourceId, Version, Sha256;
        public ContentRecord(string kind, string sourceId, string version, string sha256)
        {
            CheckText(kind); CheckText(sourceId); CheckText(version);
            if (sha256 == null || sha256.Length != 64) throw new ArgumentException("SHA256 required");
            foreach (char c in sha256) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) throw new ArgumentException("Lowercase hex required");
            Kind = kind; SourceId = sourceId; Version = version; Sha256 = sha256;
        }
        private static void CheckText(string text)
        {
            if (String.IsNullOrEmpty(text)) throw new ArgumentException("Empty identity");
            // Reject malformed UTF16; do not trim, case-fold, or invent replacement IDs.
            new UTF8Encoding(false, true).GetBytes(text);
        }
    }
    public sealed class ContentIndex : IContentIndex
    {
        private readonly Dictionary<string, ContentRecord> records = new Dictionary<string, ContentRecord>(StringComparer.Ordinal);
        private readonly string contentHash;
        private static string Field(string value) { return Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture) + ":" + value; }
        private static string Key(string kind, string id) { return Field(kind) + Field(id); }
        public ContentIndex(IEnumerable<ContentRecord> source, IExportApproval approval)
        {
            if (source == null || approval == null) throw new ArgumentNullException();
            var ordered = new List<ContentRecord>();
            foreach (ContentRecord r in source)
            {
                if (r == null) throw new ArgumentException("Null record");
                if (!approval.IsApproved(r.Kind, r.SourceId, r.Version, r.Sha256)) throw new InvalidOperationException("Unapproved export: " + r.SourceId);
                string key = Key(r.Kind, r.SourceId);
                if (records.ContainsKey(key)) throw new ArgumentException("Duplicate source identity: " + r.SourceId);
                records.Add(key, r); ordered.Add(r);
            }
            ordered.Sort(delegate(ContentRecord a, ContentRecord b) { int c = StringComparer.Ordinal.Compare(a.Kind, b.Kind); return c != 0 ? c : StringComparer.Ordinal.Compare(a.SourceId, b.SourceId); });
            var text = new StringBuilder("ASHFALL-CONTENT/1\n");
            foreach (ContentRecord r in ordered) text.Append(Field(r.Kind)).Append(Field(r.SourceId)).Append(Field(r.Version)).Append(Field(r.Sha256)).Append('\n');
            using (SHA256 sha = SHA256.Create()) contentHash = BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        public string ContentHash { get { return contentHash; } }
        public bool TryResolve(string kind, string sourceId, out ContentRecord record)
        { if (kind == null || sourceId == null) { record = null; return false; } return records.TryGetValue(Key(kind, sourceId), out record); }
    }
}
