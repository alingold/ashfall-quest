using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ashfall.Modules.Persistence;
using Ashfall.Modules.SaveMigration;
class MigrationTests
{
    static int count;
    static string root;
    static string P(string n){return Path.Combine(root,n);}
    static void Check(bool b,string name){if(!b)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static void Put(string n,string text){File.WriteAllText(P(n),text,new UTF8Encoding(false));}
    static string Expanded(int chapter,string mission){return "{\"version\":1,\"campaign_id\":\"ashfall-expanded-v1\",\"mission_id\":\""+mission+"\",\"chapter\":"+chapter+",\"kills\":7,\"play_time\":2.5,\"alien\":{\"precise\":123456789012345678901.2300}}";}
    static MigrationPlan C(string n,bool recover){return new SemanticMigration(new PhysicalMigrationSources()).Campaign(P(n),P(n+"-legacy"),recover);}
    static string Value(string json,string key){return RawJson.Object(json)[key];}
    sealed class Maps:IMapAvailability{public bool IsPlayable(string id){return id=="archive:Exact-ID";}}
    sealed class FailingFiles:IAtomicSettingsFiles
    {
        readonly PhysicalSettingsFiles real=new PhysicalSettingsFiles();
        public bool FailCommit;
        public IDisposable AcquireFamily(string p){return real.AcquireFamily(p);}
        public bool Exists(string p){return real.Exists(p);}
        public byte[] Read(string p,int n){return real.Read(p,n);}
        public void CreateDurable(string p,byte[] b){real.CreateDurable(p,b);}
        public void Commit(string p,string d,string b,bool replace){if(FailCommit)throw new IOException("Injected pre-commit failure");real.Commit(p,d,b,replace);}
    }
    static void Main(string[] args)
    {
        root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
        Check(C("absent",true).State==MigrationState.Missing,"missing family");
        Put("legacy-legacy","{\"version\":1,\"chapter\":3,\"kills\":9,\"play_time\":10}");
        var legacy=C("legacy",true);Check(Value(legacy.SemanticJson,"mission_id")=="\"extraction\""&&Value(legacy.SemanticJson,"chapter")=="5","legacy chapter maps by mission ID");
        Put("all",Expanded(3,"dam_crown"));Put("all.pending",Expanded(4,"spillway"));Put("all.backup",Expanded(5,"extraction"));
        Check(C("all",true).SelectedSource==P("all"),"primary wins conflicting known family");
        Put("all","{bad");Check(C("all",true).SelectedSource==P("all.pending"),"corrupt primary recovers pending before backup");
        Check(C("all",false).State==MigrationState.Invalid,"recovery opt in");
        Put("all.pending","{bad");Check(C("all",true).SelectedSource==P("all.backup"),"backup recovery");
        Put("only.backup.pending",Expanded(0,"relay"));Check(C("only",true).State==MigrationState.Invalid,"backup.pending never candidate");
        Put("future",Expanded(0,"relay"));Put("future.backup.pending","{\"version\":2}");Check(C("future",true).State==MigrationState.Blocked,"future backup.pending blocks good primary");
        Put("missingmission","{\"version\":1,\"campaign_id\":\"ashfall-expanded-v1\",\"chapter\":0,\"kills\":0,\"play_time\":0}");
        Put("missingmission.backup",Expanded(0,"relay"));Check(C("missingmission",true).State==MigrationState.Blocked,"missing mission blocks recovery");
        Put("mismatch",Expanded(1,"relay"));Put("mismatch.backup",Expanded(0,"relay"));Check(C("mismatch",true).SelectedSource==P("mismatch.backup"),"known chapter mismatch permits valid recovery");
        Put("badstats",Expanded(0,"relay").Replace("\"kills\":7","\"kills\":-1"));Check(C("badstats",true).State==MigrationState.Invalid,"negative kills rejected");
        Put("fraction",Expanded(0,"relay").Replace("\"kills\":7","\"kills\":1.5"));Check(C("fraction",true).State==MigrationState.Invalid,"fractional kills rejected");
        Put("rounded",Expanded(0,"relay").Replace("\"version\":1","\"version\":1.000000000000000000000000001"));Check(C("rounded",true).State==MigrationState.Blocked,"version cannot round to one");
        Put("invalid-new","{");Put("invalid-new-legacy","{\"version\":1,\"chapter\":0,\"kills\":0,\"play_time\":0}");Check(C("invalid-new",true).State==MigrationState.Invalid,"invalid expanded cannot fall back to legacy");
        var m=new SemanticMigration(new PhysicalMigrationSources());
        Check(m.Campaign(P("x"),P("x"),true).State==MigrationState.Blocked,"equal paths blocked");
        Put("payload",Expanded(4,"spillway")); var plan=C("payload",true);
        Check(Value(plan.SemanticJson,"alien")=="{\"precise\":123456789012345678901.2300}","unknown token precision retained");
        byte[] original=File.ReadAllBytes(P("payload"));
        var store=new AtomicSettingsStore(P("unity-save"));Check(SemanticMigration.Commit(plan,store).Accepted,"semantic versioned campaign commit through accepted store");
        var reload=new AtomicSettingsStore(P("unity-save"));Check(reload.Load(false).Accepted,"actual store reload"); string v;
        Check(reload.TryRead("semantic.originals.base64",out v)&&RawJson.String(Value(v,P("payload")))==Convert.ToBase64String(original),"original bytes archive loaded");
        Check(reload.TryRead("semantic.data",out v)&&v==plan.SemanticJson,"semantic data roundtrip");
        Check(!SemanticMigration.Commit(legacy,reload).Accepted,"existing Unity destination wins");
        Check(Convert.ToBase64String(File.ReadAllBytes(P("payload")))==Convert.ToBase64String(original),"source unchanged");
        Check(!SemanticMigration.Commit(plan,new AtomicSettingsStore(P("payload"))).Accepted,"source alias rejected");
        var importer=new AtomicSettingsStore(P("importer"));Check(importer.Load(false).Accepted&&!importer.ImportGodotSettings(P("payload")).Accepted,"accepted unversioned importer rejects campaign");
        Put("lobby","{\"walk\":99,\"teams\":-2,\"friendly_fire\":1,\"map\":\"archive:Exact-ID\",\"mode\":\"respawn\",\"mystery\":[1,2]}");
        var lobby=m.Lobby(P("lobby"),SourceProfile.Quest,new Maps());
        Check(Value(lobby.SemanticJson,"walk")=="5"&&Value(lobby.SemanticJson,"teams")=="0","lobby numeric clamps");
        Check(Value(lobby.SemanticJson,"friendly_fire")=="false","lobby booleans strict");
        Check(Value(lobby.SemanticJson,"map")=="\"archive:Exact-ID\"","exact map identity availability");
        Check(Value(m.Lobby(P("lobby"),SourceProfile.Quest,null).SemanticJson,"map")=="\"range\"","unknown map availability defaults safely");
        Put("defaults","{}");
        Check(Value(m.Lobby(P("defaults"),SourceProfile.Quest,null).SemanticJson,"walk")=="2.5"&&Value(m.Lobby(P("defaults"),SourceProfile.SkinLibrary,null).SemanticJson,"walk")=="1.0","distinct source defaults");
        Put("versioned-lobby","{\"version\":2}");Check(m.Lobby(P("versioned-lobby"),SourceProfile.Quest,null).State==MigrationState.Blocked,"versioned lobby blocked");
        Put("duplicate","{\"walk\":1,\"walk\":2}");Check(m.Lobby(P("duplicate"),SourceProfile.Quest,null).State==MigrationState.Invalid,"duplicate keys rejected");
        Put("explosive","{\"rocket_speed\":999,\"frag_damage\":999,\"frag_radius\":-1,\"future_key\":9}");
        var tuning=m.Tuning(P("explosive"),false);
        Check(Value(tuning.SemanticJson,"rocket_speed")=="240"&&Value(tuning.SemanticJson,"frag_damage")=="60"&&Value(tuning.SemanticJson,"frag_radius")=="0.05","source explosive clamp semantics");
        Put("rates","{\"matadors\":40,\"1911\":-4,\"m60\":\"20\"}");
        var rates=m.Tuning(P("rates"),true);
        Check(Value(rates.SemanticJson,"matadors")=="48"&&Value(rates.SemanticJson,"1911")=="0.2"&&Value(rates.SemanticJson,"m60")=="7.2","timing overrides clamp before matador multiplier");
        Check(SemanticMigration.Commit(lobby,new AtomicSettingsStore(P("unity-lobby"))).Accepted&&SemanticMigration.Commit(tuning,new AtomicSettingsStore(P("unity-tuning"))).Accepted,"separate lobby and tuning schemas persisted");
        var entries=new[]{new PersonalOverride("walk_speed_multiplier","2","3"),new PersonalOverride("fling_power","1","2")};
        string personal=PersonalSettingsPolicy.ForSave("{\"walk_speed_multiplier\":3,\"fling_power\":2.5,\"unknown\":1e100}",entries);
        Check(Value(personal,"walk_speed_multiplier")=="2"&&Value(personal,"fling_power")=="2.5"&&Value(personal,"unknown")=="1e100","save personal prior but keep deliberate edit and unknown value");
        Check(!SemanticMigration.Commit(legacy,new AtomicSettingsStore(P("legacy"))).Accepted,"missing expanded source destination alias rejected");
        Check(Value(PersonalSettingsPolicy.ForSourceSave("{\"walk_speed_multiplier\":3}",entries,"{}",false,SourceProfile.SkinLibrary),"walk_speed_multiplier")=="3","skin library has no offline map save policy");
        Check(Value(PersonalSettingsPolicy.ForSourceSave("{\"walk_speed_multiplier\":4}",entries,"{\"walk_speed_multiplier\":2}",true,SourceProfile.Quest),"walk_speed_multiplier")=="2","live cached personal overrides effective and deliberate edits");
        var fail=new FailingFiles{FailCommit=true};
        Check(!SemanticMigration.Commit(plan,new AtomicSettingsStore(P("failed"),fail)).Accepted&&!File.Exists(P("failed")),"atomic first commit failure leaves absent destination");
        var atomic=new AtomicSettingsStore(P("atomic"));Check(atomic.Load(false).Accepted,"atomic load");atomic.Write("n","old");Check(atomic.Flush().Accepted,"atomic initial commit");
        var faulty=new AtomicSettingsStore(P("atomic"),fail);Check(faulty.Load(false).Accepted,"fault injection load");faulty.Write("n","new");Check(!faulty.Flush().Accepted,"replacement failure rejected");
        var before=new AtomicSettingsStore(P("atomic"));Check(before.Load(false).Accepted&&before.TryRead("n",out v)&&v=="old","failed replace preserves previous complete state");
        atomic.Write("n","next");Check(atomic.Flush().Accepted,"real atomic replace maintains backup");
        Put("atomic","corrupt");var recover=new AtomicSettingsStore(P("atomic"));Check(recover.Load(true).Accepted&&recover.TryRead("n",out v)&&v=="old","actual accepted backup recovery");
        recover.Write("n","restored");Check(recover.Flush().Accepted,"atomic recovery commit");
        Check(File.ReadAllText(P("atomic.backup")).Length>0&&Directory.GetFiles(root,"atomic.corrupt.*").Length==1,"recovery retains backup and corrupt bytes");
        var stale=new AtomicSettingsStore(P("atomic"));Check(stale.Load(false).Accepted,"stale reader loaded");recover.Write("n","fresh");Check(recover.Flush().Accepted,"competing writer flush");stale.Write("n","lost");Check(!stale.Flush().Accepted,"stale writer cannot clobber");
        Check(SemanticMigration.ReadCommitted(P("unity-save"),"campaign",false).State==MigrationState.Ready,"validated semantic campaign reload");
        Check(SemanticMigration.ReadCommitted(P("unity-save"),"lobby",false).State==MigrationState.Blocked,"semantic kind cannot be confused");
        Check(SemanticMigration.ReadCommitted(P("unity-lobby"),"lobby",false).State==MigrationState.Ready,"validated lobby reload");
        Check(SemanticMigration.ReadCommitted(P("unity-tuning"),"explosive-tuning",false).State==MigrationState.Ready,"validated tuning reload");
        var newer=new AtomicSettingsStore(P("unity-save"));Check(newer.Load(false).Accepted,"semantic future fixture load");newer.Write("semantic.schema","2");Check(newer.Flush().Accepted,"semantic future fixture flush");
        Check(SemanticMigration.ReadCommitted(P("unity-save"),"campaign",true).State==MigrationState.Blocked,"future semantic schema never recovers older backup");
        Put("unknown-mission",Expanded(0,"future_mission"));Check(C("unknown-mission",true).State==MigrationState.Blocked,"unknown mission identity protected");
        Put("huge-time",Expanded(0,"relay").Replace("2.5","31536001"));Check(C("huge-time",true).State==MigrationState.Invalid,"play time range gate");
        var badLobby=new AtomicSettingsStore(P("unity-lobby"));Check(badLobby.Load(false).Accepted,"tampered lobby fixture loaded");
        badLobby.Write("semantic.data",lobby.SemanticJson.Replace("\"walk\":5","\"walk\":99"));Check(badLobby.Flush().Accepted,"tampered lobby fixture flushed");
        Check(SemanticMigration.ReadCommitted(P("unity-lobby"),"lobby",false).State==MigrationState.Invalid,"semantic lobby ranges revalidated on reload");
        var backFuture=new AtomicSettingsStore(P("future-dest"));Check(backFuture.Load(false).Accepted,"future destination setup");backFuture.Write("semantic.schema","2");Check(backFuture.Flush().Accepted,"future semantic first version");backFuture.Write("semantic.schema","1");Check(backFuture.Flush().Accepted,"future semantic now backup");
        Check(SemanticMigration.ReadCommitted(P("future-dest"),"campaign",true).State==MigrationState.Blocked,"future semantic backup blocks even good primary");
        Console.WriteLine("TOTAL "+count+" assertions passed");
    }
}
