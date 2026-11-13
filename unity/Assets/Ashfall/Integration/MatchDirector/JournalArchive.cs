using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
namespace Ashfall.Integration.MatchDirector
{
    public sealed partial class ScoreReceiptJournal
    {
        private sealed class FrozenStore : ISettingsStore
        {
            internal readonly Dictionary<string,string> Values=new Dictionary<string,string>(StringComparer.Ordinal);
            public bool TryRead(string key,out string value){return Values.TryGetValue(key,out value);}
            public void Write(string key,string value){throw new InvalidOperationException("quarantined-journal");}
            public RuleResult Flush(){return RuleResult.Reject("quarantined-journal");}
        }
        private static void Value(BinaryWriter w,string s){RecoveryWire.Blob(w,new UTF8Encoding(false,true).GetBytes(s));}
        private static string Value(BinaryReader r){return new UTF8Encoding(false,true).GetString(RecoveryWire.Blob(r));}
        internal byte[] CaptureArchive(string session,long epoch)
        {
            string indexKey=Index(session,epoch),index;bool exists=store.TryRead(indexKey,out index);
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m))
            {
                w.Write(1);w.Write(exists);if(exists){Value(w,index);var seen=new HashSet<string>(StringComparer.Ordinal);
                foreach(string key in index.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries))
                {RecoveryWire.Require(seen.Count<8192&&seen.Add(key)&&key.StartsWith("md5:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(session))+":"+epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)+":",StringComparison.Ordinal));string payload,ack;RecoveryWire.Require(store.TryRead(key,out payload));Value(w,payload);bool a=store.TryRead(key+":ack",out ack);w.Write(a);if(a)Value(w,ack);}}
                w.Flush();RecoveryWire.Require(m.Length<=RecoveryWire.Limit);return m.ToArray();
            }
        }
        internal static ScoreReceiptJournal RestoreArchive(byte[] bytes,string session,long epoch)
        {
            var store=new FrozenStore();using(var r=RecoveryWire.Reader(bytes))
            {RecoveryWire.Require(r.ReadInt32()==1);if(RecoveryWire.Bool(r)){string index=Value(r);store.Values.Add(Index(session,epoch),index);foreach(string key in index.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)){store.Values.Add(key,Value(r));if(RecoveryWire.Bool(r))store.Values.Add(key+":ack",Value(r));}}RecoveryWire.End(r);}
            var journal=new ScoreReceiptJournal(store);RecoveryWire.Require(RecoveryWire.Hash(bytes)==RecoveryWire.Hash(journal.CaptureArchive(session,epoch)));return journal;
        }
    }
}
