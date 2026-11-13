using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ashfall.Core;
using Ashfall.Modules.Audio;
using Ashfall.Modules.Persistence;

namespace Ashfall.Integration.AudioSettings
{
    // Dedicated store instance/main-thread router required. No real Godot save is opened.
    // Persistence remains responsible for atomic commits, backup and stale-writer checks.
    public sealed class AudioSettingsBinding : IAudioSettingsBinding
    {
        private readonly AtomicSettingsStore store;
        private readonly AudioRouter router;
        private static readonly string[] Buses={"SFX","Music","Voice","Ambience"};
        private static readonly Regex Member=new Regex(@"\G[ \r\n\t]*""([A-Za-z][A-Za-z0-9_]*)""[ \r\n\t]*:[ \r\n\t]*(-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)[ \r\n\t]*");
        public AudioSettingsBinding(AtomicSettingsStore store,AudioRouter router)
        {if(store==null||router==null)throw new ArgumentNullException();this.store=store;this.router=router;}
        private static float Clamp(float v){return Math.Max(0,Math.Min(1,v));}
        private static float Number(string token)
        {
            float v;
            if(!Regex.IsMatch(token,@"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z") || !float.TryParse(token,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||float.IsNaN(v)||float.IsInfinity(v))throw new FormatException("Nonfinite/invalid audio number");
            return v;
        }
        // Source audio_volumes is a flat numeric dictionary. Future structured/escaped
        // keys fail explicitly; they are never discarded or rewritten by this adapter.
        private static Dictionary<string,string> Parse(string text)
        {
            if(text.Length>65536)throw new FormatException("Audio settings size");
            text=text.Trim(' ','\r','\n','\t');
            if(text.Length<2||text[0]!='{'||text[text.Length-1]!='}')throw new FormatException("audio_volumes object required");
            var result=new Dictionary<string,string>(StringComparer.Ordinal);int at=1;
            if(text.Substring(1,text.Length-2).Trim(' ','\r','\n','\t').Length==0)return result;
            while(at<text.Length-1)
            {
                var m=Member.Match(text,at);if(!m.Success||m.Index!=at)throw new FormatException("Unsupported audio volume member");
                string key=m.Groups[1].Value,token=m.Groups[2].Value;
                if(result.ContainsKey(key))throw new FormatException("Duplicate audio bus");result.Add(key,token);at+=m.Length;
                if(at==text.Length-1)return result;
                if(text[at++]!=','||at>=text.Length-1)throw new FormatException("Audio dictionary delimiter");
            }
            throw new FormatException("Trailing audio dictionary comma");
        }
        private Dictionary<string,string> Read(out float master)
        {
            string text;master=store.TryRead("master_volume",out text)?Clamp(Number(text)):1;
            var data=store.TryRead("audio_volumes",out text)?Parse(text):new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string bus in Buses)if(!data.ContainsKey(bus))data[bus]=(bus=="SFX"||bus=="Voice")?"1":"0";
            bool current=store.TryRead("audio_defaults_revision",out text)&&CurrentRevision(text);
            if(!current){data["Music"]="0";data["Ambience"]="0";}
            return data;
        }
        // Exact decimal comparison to 993, equivalent to truncation toward zero for
        // this positive threshold. Exponent saturation avoids numeric overflow while
        // accepting arbitrarily large valid JSON revisions without rewriting them.
        private static bool CurrentRevision(string token)
        {
            if(token.Length>65536 || !Regex.IsMatch(token,@"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z"))throw new FormatException("Invalid audio revision");
            if(token[0]=='-')return false;
            int e=token.IndexOfAny(new[]{'e','E'});string coefficient=e<0?token:token.Substring(0,e);
            int dot=coefficient.IndexOf('.'),fraction=dot<0?0:coefficient.Length-dot-1;
            string digits=coefficient.Replace(".","").TrimStart('0');if(digits.Length==0)return false;
            int exponent=0;
            if(e>=0){int at=e+1;bool negative=token[at]=='-';if(token[at]=='+'||negative)at++;
                for(;at<token.Length;at++)exponent=Math.Min(1000000,exponent*10+token[at]-'0');
                if(negative)exponent=-exponent;}
            int integerDigits=digits.Length-fraction+exponent;
            if(integerDigits!=3)return integerDigits>3;
            string prefix=(digits+"000").Substring(0,3);
            return String.CompareOrdinal(prefix,"993")>=0;
        }
        private sealed class Snapshot
        {
            internal string Volumes,Master,Revision,Id;
            internal bool HasVolumes,HasMaster,HasRevision,Committed;
            internal float[] Gains;
        }
        private Snapshot pending;
        private bool busy;
        public AudioPublicationReceipt LastReceipt {get;private set;}
        private AudioPublicationReceipt Receipt(AudioPublicationState state,bool committed,string id,string bus,string reason)
        {return LastReceipt=new AudioPublicationReceipt(state,committed,id,bus,reason);}
        private AudioPublicationReceipt Blocked(string reason)
        {return Receipt(pending==null?AudioPublicationState.Rejected:AudioPublicationState.LivePending,pending!=null&&pending.Committed,pending==null?"":pending.Id,"",reason);}
        private Snapshot Capture(Dictionary<string,string> values,float master,bool committed)
        {
            var s=new Snapshot{Committed=committed,Gains=new float[5]};
            s.HasVolumes=store.TryRead("audio_volumes",out s.Volumes);s.HasMaster=store.TryRead("master_volume",out s.Master);s.HasRevision=store.TryRead("audio_defaults_revision",out s.Revision);
            // Length-delimited raw tokens distinguish absent and empty values.
            string identity="";
            foreach(string value in new[]{s.Volumes,s.Master,s.Revision})identity+=value==null?"-1:":value.Length.ToString(CultureInfo.InvariantCulture)+":"+value;
            using(var sha=System.Security.Cryptography.SHA256.Create())s.Id=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","");
            s.Gains[0]=Math.Max(master,.0001f);
            for(int i=0;i<Buses.Length;i++){float value=Clamp(Number(values[Buses[i]]));s.Gains[i+1]=value<=0?0:Math.Max(value,.001f);}
            return s;
        }
        private bool Matches(Snapshot s)
        {
            string value;
            return store.TryRead("audio_volumes",out value)==s.HasVolumes && value==s.Volumes
                && store.TryRead("master_volume",out value)==s.HasMaster && value==s.Master
                && store.TryRead("audio_defaults_revision",out value)==s.HasRevision && value==s.Revision;
        }
        private AudioPublicationReceipt ApplyPending()
        {
            var s=pending;
            for(int i=0;i<s.Gains.Length;i++)
            {
                string bus=i==0?"Master":Buses[i-1];
                try{router.SetBus(bus,s.Gains[i]);}
                catch(Exception ex){return Receipt(AudioPublicationState.LivePending,s.Committed,s.Id,bus,"Live publication pending: "+ex.Message);}
            }
            pending=null;return Receipt(AudioPublicationState.Synchronized,s.Committed,s.Id,"","");
        }
        // Legacy results cannot express partial success: reject with an explicit durable
        // status and expose the typed receipt. A repeated Save is never a retry API.
        private static RuleResult Legacy(AudioPublicationReceipt r)
        {return r.Accepted?RuleResult.Accept():RuleResult.Reject((r.Committed?"Committed; ":"")+r.Reason);}
        public RuleResult Load(){return Legacy(LoadPublication());}
        public RuleResult SaveBus(string bus,float gain){return Legacy(PublishBus(bus,gain));}
        public AudioPublicationReceipt LoadPublication()
        {
            if(busy||pending!=null)return Blocked("Pending publication must be retried before Load");
            busy=true;
            try
            {
                var result=store.Load(false);if(!result.Accepted)return Blocked(result.Reason);
                float master;var values=Read(out master);pending=Capture(values,master,false);return ApplyPending();
            }
            catch(Exception ex){return Blocked(ex.Message);}
            finally{busy=false;}
        }
        public AudioPublicationReceipt PublishBus(string bus,float linearGain)
        {
            if(busy||pending!=null)return Blocked("Pending publication must be retried before another edit");
            if(!AudioCatalog.ValidBus(bus)||float.IsNaN(linearGain)||float.IsInfinity(linearGain))return Blocked("Invalid bus/gain");
            busy=true;
            try
            {
                var loaded=store.Load(false);if(!loaded.Accepted)return Blocked(loaded.Reason);
                float master;var data=Read(out master);linearGain=Clamp(linearGain);
                if(bus=="Master")master=linearGain;else data[bus]=linearGain.ToString("R",CultureInfo.InvariantCulture);
                // Validate every known gain BEFORE staging or committing anything.
                foreach(string known in Buses)Number(data[known]);
                var json=new StringBuilder("{");bool first=true;
                foreach(string key in new SortedSet<string>(data.Keys,StringComparer.Ordinal)){if(!first)json.Append(',');first=false;json.Append('"').Append(key).Append("\":").Append(data[key]);}
                store.Write("audio_volumes",json.Append('}').ToString());
                store.Write("master_volume",master.ToString("R",CultureInfo.InvariantCulture));
                string revision;
                if(!store.TryRead("audio_defaults_revision",out revision)||!CurrentRevision(revision))store.Write("audio_defaults_revision","993");
                // Allocate and validate immutable publication state before the durable boundary.
                var snapshot=Capture(data,master,true);
                var result=store.Flush();
                if(!result.Accepted){store.Load(false);return Blocked(result.Reason);}
                pending=snapshot;return ApplyPending();
            }
            catch(Exception ex){return Blocked(ex.Message);}
            finally{busy=false;}
        }
        public AudioPublicationReceipt RetryPublication()
        {
            if(busy)return Blocked("Publication already in progress");
            if(pending==null)return LastReceipt??Blocked("No publication to retry");
            busy=true;
            try
            {
                var result=store.Load(false);
                if(!result.Accepted)return Receipt(AudioPublicationState.Conflict,pending.Committed,pending.Id,"","Pending snapshot could not be verified: "+result.Reason);
                if(!Matches(pending))return Receipt(AudioPublicationState.Conflict,pending.Committed,pending.Id,"","Audio settings changed externally; reopen binding and explicitly Load current settings");
                return ApplyPending();
            }
            catch(Exception ex){return Receipt(AudioPublicationState.Conflict,pending.Committed,pending.Id,"",ex.Message);}
            finally{busy=false;}
        }
    }
}
