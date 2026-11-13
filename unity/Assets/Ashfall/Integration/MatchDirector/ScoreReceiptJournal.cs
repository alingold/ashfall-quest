using System;
using System.IO;
using System.Text;
using System.Globalization;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.MatchDirector
{
    // Append-only logical receipts over a caller-loaded, exclusively owned settings family.
    // Flush is required even for a matching retry: a previous failed flush may be in memory.
    public sealed partial class ScoreReceiptJournal
    {
        private readonly ISettingsStore store;
        public ScoreReceiptJournal(ISettingsStore store)
        { if(store==null)throw new ArgumentNullException("store");this.store=store; }
        public static string Key(HealthTransition e)
        {return "md5:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(e.SessionId))+":"+e.AuthorityEpoch.ToString(CultureInfo.InvariantCulture)+":"+e.Sequence.ToString(CultureInfo.InvariantCulture);}
        public RuleResult Retain(HealthTransition e,string killer,long life)
        {return Retain(e,killer,life,-1);}
        public RuleResult Retain(HealthTransition e,string killer,long life,int killerTeam)
        {
            if(e==null||e.After==null)return RuleResult.Reject("receipt-event");
            string payload;
            using(var m=new MemoryStream())
            {using(var w=new BinaryWriter(m,Encoding.UTF8,true))
             {w.Write(1);w.Write(e.After.Id);w.Write(e.After.Revision);w.Write(e.BeforeRevision);w.Write(e.Kind);w.Write(life);w.Write(killer??"");w.Write(e.After.Team);w.Write((int)e.After.Life);w.Write(killerTeam);}
             payload=Convert.ToBase64String(m.ToArray());}
            string old,key=Key(e);
            if(store.TryRead(key,out old)&&old!=payload)return RuleResult.Reject("receipt-conflict");
            string indexKey=Index(e.SessionId,e.AuthorityEpoch),index;
            if(!store.TryRead(indexKey,out index))index="";
            var keys=new System.Collections.Generic.List<string>(index.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries));
            if(!keys.Contains(key))
            {if(keys.Count>=8192)return RuleResult.Reject("receipt-capacity");keys.Add(key);store.Write(indexKey,String.Join("\n",keys.ToArray()));}
            store.Write(key,payload);return store.Flush();
        }
        private static string Index(string session,long epoch)
        {return "md5-index:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(session))+":"+epoch.ToString(CultureInfo.InvariantCulture);}
        public string[] Pending(string session,long epoch)
        {
            string index;if(!store.TryRead(Index(session,epoch),out index))return new string[0];
            var pending=new System.Collections.Generic.List<string>();
            foreach(string key in index.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries))
            {string payload;bool ack;if(!TryRead(key,out payload,out ack))throw new InvalidDataException("receipt-index");if(!ack)pending.Add(key);}
            return pending.ToArray();
        }
        public RuleResult Acknowledge(HealthTransition e)
        {string value;if(!store.TryRead(Key(e),out value))return RuleResult.Reject("receipt-missing");store.Write(Key(e)+":ack","1");return store.Flush();}
        public bool TryRead(string key,out string payload,out bool acknowledged)
        {string a;acknowledged=store.TryRead(key+":ack",out a)&&a=="1";return store.TryRead(key,out payload);}
    }
}
