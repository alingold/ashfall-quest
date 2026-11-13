using System;
using System.IO;
using System.Text;
using Ashfall.Modules.Persistence;
using Ashfall.Modules.SaveMigration;
using Ashfall.Integration.MigrationRuntime;
class RuntimeTests
{
    static string root; static int count;
    static string P(string n){return Path.Combine(root,n);}
    static void Check(bool b,string n){if(!b)throw new Exception(n);count++;Console.WriteLine("PASS "+n);}
    static void Put(string n,string s){File.WriteAllText(P(n),s,new UTF8Encoding(false));}
    static SettingsMigrationRuntime R(string n){return new SettingsMigrationRuntime(P(n),SourceProfile.Quest);}
    static string Campaign="{\"version\":1,\"chapter\":3,\"kills\":7,\"play_time\":2.5,\"unknown\":{\"x\":12345678901234567890.2300}}";
    sealed class FaultFiles:IAtomicSettingsFiles
    {
        readonly PhysicalSettingsFiles real=new PhysicalSettingsFiles();
        internal string Mode;
        public IDisposable AcquireFamily(string p){if(Mode=="lock")throw new IOException("lock failure");return real.AcquireFamily(p);}
        public bool Exists(string p){return real.Exists(p);}
        public byte[] Read(string p,int n){if(Mode=="read")throw new IOException("read failure");return real.Read(p,n);}
        public void CreateDurable(string p,byte[] b)
        {
            if(Mode=="create")throw new IOException("durable failure");
            real.CreateDurable(p,Mode=="verify"?new byte[]{123}:b);
            if(Mode=="race")File.WriteAllText(p.Substring(0,p.IndexOf(".pending.",StringComparison.Ordinal)),"external");
        }
        public void Commit(string p,string d,string b,bool r){if(Mode=="commit")throw new IOException("atomic failure");real.Commit(p,d,b,r);}
    }
    static void Main(string[] args)
    {
        root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
        if(args[0]=="cold")
        {
            var p=SemanticMigration.ReadCommitted(P("cold-save"),"campaign",false);
            Check(p.State==MigrationState.Ready&&p.SemanticJson.Contains("\"chapter\":5")&&p.SemanticJson.Contains("12345678901234567890.2300"),"cold actual public migration reload mapping/precision");
            var s=new AtomicSettingsStore(P("cold-save"));string v;
            Check(s.Load(false).Accepted&&s.TryRead("semantic.originals.base64",out v)&&v.Contains(Convert.ToBase64String(File.ReadAllBytes(P("cold-original")))),"cold exact original archive");
            Check(R("cold-save").Commit(R("other").PreviewCampaign(P("absent"),P("cold-original"),false)).State==CommitState.Refused,"cold cross-instance refused");
            Console.WriteLine("TOTAL "+count);return;
        }
        Put("source",Campaign);byte[] original=File.ReadAllBytes(P("source"));
        foreach(string path in new[]{"relative","C:relative","\\root-relative"})
        {bool rejected=false;try{new SettingsMigrationRuntime(path,SourceProfile.Quest);}catch(ArgumentException){rejected=true;}Check(rejected,"explicit absolute path "+path);}
        var runtime=R("cold-save");var preview=runtime.PreviewCampaign(P("expanded"),P("source"),false);
        Check(preview.State==MigrationState.Ready&&preview.Kind==MigrationKind.Campaign,"ready typed campaign");
        Check(preview.SemanticJson.Contains("\"chapter\":5")&&preview.SemanticJson.Contains("\"mission_id\":\"extraction\""),"legacy semantic ID mapping");
        Check(preview.SemanticJson.Contains("12345678901234567890.2300"),"unknown precision preserved");
        byte[] copy=preview.ReadOriginal(P("source"));copy[0]=0;
        Check(preview.ReadOriginal(P("source"))[0]==123,"original clone immutable");
        var paths=preview.SourcePaths;paths[0]="changed";Check(preview.SourcePaths[0]!="changed","source list clone immutable");
        Check(!File.Exists(P("cold-save"))&&!File.Exists(P("cold-save.lock")),"preview no destination effects");
        Put("source","{\"version\":2}");Put("cold-original",Campaign);
        Check(runtime.Commit(preview).State==CommitState.Committed,"commit immutable preview after source mutation");
        Check(File.ReadAllText(P("source"))=="{\"version\":2}","commit never writes source");
        var saved=File.ReadAllBytes(P("cold-save"));
        Check(runtime.Commit(preview).State==CommitState.Conflict,"repeat cannot overwrite");
        Check(Convert.ToBase64String(saved)==Convert.ToBase64String(File.ReadAllBytes(P("cold-save"))),"conflict preserves destination bytes");
        Check(runtime.Commit(null).State==CommitState.Refused,"null refused");
        Put("lobby","{\"walk\":99,\"mystery\":[1,2]}");
        var quest=R("q");var qp=quest.PreviewLobby(P("lobby"),null);
        var skin=new SettingsMigrationRuntime(P("skin"),SourceProfile.SkinLibrary);var sp=skin.PreviewLobby(P("lobby"),null);
        Check(qp.SemanticJson.Contains("\"walk\":5")&&sp.SemanticJson.Contains("\"walk\":3"),"profile source bounds retained");
        Check(skin.Commit(qp).State==CommitState.Refused&&!File.Exists(P("skin.lock")),"profile switch refuses before IO");
        Check(R("foreign").Commit(qp).State==CommitState.Refused&&!File.Exists(P("foreign.lock")),"same profile foreign runtime refuses before IO");
        Check(quest.Commit(qp).State==CommitState.Committed&&skin.Commit(sp).State==CommitState.Committed,"both profiles explicit commit");
        var store=new AtomicSettingsStore(P("q"));string value;
        Check(store.Load(false).Accepted&&store.TryRead("semantic.source-profile",out value)&&value=="quest","profile persisted");
        Check(store.TryRead("semantic.originals.base64",out value)&&value.Contains(Convert.ToBase64String(File.ReadAllBytes(P("lobby")))),"unknown lobby values archived");
        string[] invalid={"{", "{\"walk\":1,\"walk\":2}","{\"version\":2}","{\"schema\":1}","[]"};
        for(int i=0;i<invalid.Length;i++)
        {
            Put("bad"+i,invalid[i]);var r=R("bad-save"+i);var p=r.PreviewLobby(P("bad"+i),null);
            Check(p.State!=MigrationState.Ready&&r.Commit(p).State==CommitState.Refused&&!File.Exists(P("bad-save"+i+".lock")),"malformed/schema refused "+i);
        }
        var missing=R("missing");Check(missing.Commit(missing.PreviewLobby(P("absent"),null)).State==CommitState.Refused,"missing refused");
        var budget=new SettingsMigrationRuntime(P("budget"),SourceProfile.Quest,new PhysicalSettingsFiles(),5);
        Check(budget.PreviewLobby(P("lobby"),null).State==MigrationState.Blocked&&!File.Exists(P("budget")),"source overbudget rejects bounded read");
        Put("huge",new string(' ',AtomicSettingsStore.MaxDocumentBytes+1));
        Check(R("huge-save").PreviewLobby(P("huge"),null).State==MigrationState.Blocked,"per-source maximum before allocation");
        File.WriteAllBytes(P("utf8"),new byte[]{123,34,120,34,58,34,255,34,125});var utf=R("utf-save");
        Check(utf.Commit(utf.PreviewLobby(P("utf8"),null)).State==CommitState.Refused&&!File.Exists(P("utf-save.lock")),"malformed UTF8 no destination access");
        Put("large","{\"unknown\":\""+new string('x',3200000)+"\"}");var large=R("large-save");var lp=large.PreviewLobby(P("large"),null);
        Check(lp.State==MigrationState.Ready&&large.Commit(lp).State==CommitState.Refused&&!File.Exists(P("large-save")),"archive output overbudget preserves absent destination");
        Put("recover","{");Put("recover.pending", "{\"version\":1,\"campaign_id\":\"ashfall-expanded-v1\",\"mission_id\":\"relay\",\"chapter\":0,\"kills\":0,\"play_time\":0}");
        var cr=R("campaign-recovery");
        Check(cr.PreviewCampaign(P("recover"),null,false).State==MigrationState.Invalid,"source recovery requires explicit opt in");
        Check(cr.PreviewCampaign(P("recover"),null,true).SelectedSource==P("recover.pending"),"accepted source pending priority");
        Put("recover.backup.pending","{\"version\":2}");
        Check(cr.PreviewCampaign(P("recover"),null,true).State==MigrationState.Blocked,"future family member blocks valid source recovery");
        Put("conflicting", "{\"version\":1,\"campaign_id\":\"ashfall-expanded-v1\",\"mission_id\":\"relay\",\"chapter\":0,\"kills\":0,\"play_time\":0}");
        Put("conflicting.pending", "{\"version\":1,\"campaign_id\":\"ashfall-expanded-v1\",\"mission_id\":\"turbine\",\"chapter\":1,\"kills\":1,\"play_time\":1}");
        Check(cr.PreviewCampaign(P("conflicting"),null,true).SelectedSource==P("conflicting"),"conflicting valid family primary wins");
        var locked=R("locked");var lockPlan=locked.PreviewLobby(P("lobby"),null);
        using(new PhysicalSettingsFiles().AcquireFamily(P("locked")))Check(locked.Commit(lockPlan).State==CommitState.IoFailure&&!File.Exists(P("locked")),"actual exclusive family contention");
        Check(locked.Commit(lockPlan).State==CommitState.Committed,"actual family contention retry");
        var alias=R("lobby.backup");Check(alias.Commit(alias.PreviewLobby(P("lobby"),null)).State==CommitState.Refused&&!File.Exists(P("lobby.backup.lock")),"source family alias refused");
        foreach(string mode in new[]{"lock","create","commit","verify","race"})
        {
            var f=new FaultFiles{Mode=mode};var r=new SettingsMigrationRuntime(P(mode),SourceProfile.Quest,f,10000);var p=r.PreviewLobby(P("lobby"),null);
            var result=r.Commit(p);
            Check(result.State==(mode=="verify"?CommitState.Refused:mode=="race"?CommitState.Conflict:CommitState.IoFailure),"typed failure "+mode);
            Check(mode=="race"?File.ReadAllText(P(mode))=="external":!File.Exists(P(mode)),"failure preserves destination "+mode);
            if(mode!="race"){f.Mode="";Check(r.Commit(p).State==CommitState.Committed,"retry immutable plan "+mode);}
        }
        Put("occupied","existing bytes");var occupied=R("occupied");Check(occupied.Commit(occupied.PreviewLobby(P("lobby"),null)).State==CommitState.Conflict&&File.ReadAllText(P("occupied"))=="existing bytes","corrupt occupied destination preserved");
        var readFailure=new SettingsMigrationRuntime(P("occupied"),SourceProfile.Quest,new FaultFiles{Mode="read"},10000);
        Check(readFailure.Commit(readFailure.PreviewLobby(P("lobby"),null)).State==CommitState.IoFailure&&File.ReadAllText(P("occupied"))=="existing bytes","actual file-read failure preserves bytes");
        Put("orphan.backup","{\"schema\":99,\"values\":{}}");var orphan=R("orphan");Check(orphan.Commit(orphan.PreviewLobby(P("lobby"),null)).State==CommitState.Conflict&&!File.Exists(P("orphan")),"future/orphan backup protected");
        Put("rates","{\"matadors\":40,\"1911\":-4}");var tune=R("tuning");var tp=tune.PreviewTuning(P("rates"),MigrationKind.WeaponTiming);
        Check(tp.SemanticJson.Contains("\"matadors\":48")&&tp.SemanticJson.Contains("\"1911\":0.2")&&tune.Commit(tp).State==CommitState.Committed,"accepted tuning bounds and multiplier");
        var a=new AtomicSettingsStore(P("backup"));Check(a.Load(false).Accepted,"backup actual initial load");a.Write("v","one");Check(a.Flush().Accepted,"backup first write");a.Write("v","two");Check(a.Flush().Accepted,"backup atomic replacement");
        Put("backup","broken");var b=new AtomicSettingsStore(P("backup"));Check(b.Load(true).Accepted&&b.TryRead("v",out value)&&value=="one","actual backup opt-in recovery");
        Check(Convert.ToBase64String(original)==Convert.ToBase64String(File.ReadAllBytes(P("cold-original"))),"fixture originals exact");
        Console.WriteLine("TOTAL "+count);
    }
}
