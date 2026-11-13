using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Ashfall.Modules.Persistence;
class ReviewTests
{
    static string root;
    static int passes, failures;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    static string NewPath() { string d=System.IO.Path.Combine(root,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return System.IO.Path.Combine(d,"settings.json"); }
    static void Put(string p,string t) { File.WriteAllBytes(p,Utf8.GetBytes(t)); }
    static void Require(bool b,string s) { if(!b) throw new Exception(s); }
    static string Read(AtomicSettingsStore s,string k) { string v; Require(s.TryRead(k,out v),"missing "+k);return v; }
    static string Save(string v) { return "{\"schema\":1,\"values\":{\"x\":\""+v+"\"}}"; }
    static void Test(string name,Action a) { try { a();passes++;Console.WriteLine("PASS "+name); } catch(Exception e) { failures++; Console.WriteLine("FAIL "+name+": "+e.Message); } }
    static string Hash(byte[] b) { using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(b)).Replace("-",""); }
    sealed class HookFiles:IAtomicSettingsFiles
    {
        readonly PhysicalSettingsFiles fs=new PhysicalSettingsFiles();
        public Action Hook;
        public bool Exists(string p) {return fs.Exists(p);}
        public IDisposable AcquireFamily(string p) { return fs.AcquireFamily(p); }
        public byte[] Read(string p, int maxBytes) {return fs.Read(p, maxBytes);}
        public void CreateDurable(string p,byte[] b) {fs.CreateDurable(p,b);if(p.Contains(".pending.") && Hook!=null) {Action a=Hook;Hook=null;a();}}
        public void Commit(string p,string d,string b,bool r) {fs.Commit(p,d,b,r);}
    }
    static void Main(string[] args)
    {
        root=args[0];
        Test("real replacement failure leaves primary and retry succeeds",delegate {
            string p=NewPath();Put(p,Save("old"));var s=new AtomicSettingsStore(p);Require(s.Load(false).Accepted,"load");s.Write("x","new");
            using(var locked=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.Read)) Require(!s.Flush().Accepted,"replacement should fail while delete sharing denied");
            Require(File.ReadAllText(p)==Save("old"),"old changed");Require(s.Flush().Accepted,"retry");Require(File.ReadAllText(p+".backup")==Save("old"),"backup");
        });
        Test("real recovery quarantine and retained backup",delegate {
            string p=NewPath();Put(p,"{bad");Put(p+".backup",Save("good"));byte[] bad=File.ReadAllBytes(p),backup=File.ReadAllBytes(p+".backup");
            var s=new AtomicSettingsStore(p);Require(!s.Load(false).Accepted,"explicit recovery required");Require(s.Load(true).Accepted,"recover");Require(File.ReadAllText(p)=="{bad","load mutated");Require(s.Flush().Accepted,"flush");
            Require(Hash(File.ReadAllBytes(p+".corrupt."+Hash(bad)))==Hash(bad),"quarantine");Require(Hash(File.ReadAllBytes(p+".backup"))==Hash(backup),"backup changed");
        });
        Test("orphan backup physical opt-in",delegate {
            string p=NewPath();Put(p+".backup",Save("orphan"));var s=new AtomicSettingsStore(p);Require(!s.Load(false).Accepted,"orphan accepted");Require(s.Load(true).Accepted && s.Flush().Accepted,"recover");Require(Read(s,"x")=="orphan","value");
        });
        Test("original bytes and raw unknown numbers survive physical import reload",delegate {
            string p=NewPath(),src=p+".source";Put(src,"\ufeff{ \"a\":{\"n\":900719925474099312345678901234567890,\"e\":1.23000e+099},\"b\":\"snow \\u2603\" }");byte[] original=File.ReadAllBytes(src);
            var s=new AtomicSettingsStore(p);Require(s.Load(false).Accepted && s.ImportGodotSettings(src).Accepted,"import");Require(Read(s,"a")=="{\"n\":900719925474099312345678901234567890,\"e\":1.23000e+099}","raw number changed");Require(s.Flush().Accepted,"flush");
            string side=p+".godot."+Hash(original)+".json";var metadata=JsonTokens.Object(File.ReadAllText(side));Require(Hash(Convert.FromBase64String(JsonTokens.String(metadata["originalBase64"])))==Hash(original),"sidecar original mismatch");Require(Hash(File.ReadAllBytes(src))==Hash(original),"source changed");
            var again=new AtomicSettingsStore(p);Require(again.Load(false).Accepted && Read(again,"a")==Read(s,"a"),"reload raw");
        });
        Test("sidecar conflict fails without mutation",delegate {
            string p=NewPath(),src=p+".source";Put(src,"{\"x\":3}");string side=p+".godot."+Hash(File.ReadAllBytes(src))+".json";Put(side,"conflict");var s=new AtomicSettingsStore(p);s.Load(false);Require(!s.ImportGodotSettings(src).Accepted,"conflict accepted");Require(!File.Exists(p) && File.ReadAllText(side)=="conflict","mutated");string v;Require(!s.TryRead("x",out v),"partial import");
        });
        foreach(string schema in new[]{"2","1e100","1.00000000000000000000000000001"})
        {
            string captured=schema;
            Test("future primary blocks recovery schema="+schema,delegate {
                string p=NewPath();Put(p,"{\"schema\":"+captured+",\"values\":{}}");Put(p+".backup",Save("old"));string original=File.ReadAllText(p);var s=new AtomicSettingsStore(p);
                bool accepted=s.Load(true).Accepted;SettingsState state=s.State;bool flushed=s.Flush().Accepted;
                Require(!accepted && state==SettingsState.NewerSchema && !flushed && File.ReadAllText(p)==original,"load="+accepted+" state="+state+" flush="+flushed+" primary="+File.ReadAllText(p));
            });
            Test("future backup blocks replacement schema="+schema,delegate {
                string p=NewPath();Put(p,Save("primary"));Put(p+".backup","{\"schema\":"+captured+",\"values\":{}}");var s=new AtomicSettingsStore(p);bool accepted=s.Load(false).Accepted;if(accepted)s.Write("x","new");bool flushed=s.Flush().Accepted;
                Require(!accepted && s.State==SettingsState.NewerSchema && !flushed,"load="+accepted+" state="+s.State+" flush="+flushed+" backup="+(File.Exists(p+".backup")?File.ReadAllText(p+".backup"):"<absent>"));
            });
        }
        foreach(string bad in new[]{"{\"x\":\"\\ud800\"}","{\"x\":\"\\udc00\"}","{\"x\":\"\\ud800a\"}","{\"\\ud800\":1}"})
        {
            string captured=bad;Test("invalid Unicode import "+bad,delegate { string p=NewPath(),src=p+".source";Put(src,captured);var s=new AtomicSettingsStore(p);s.Load(false);Require(!s.ImportGodotSettings(src).Accepted,"unpaired surrogate accepted"); });
        }
        Test("valid Unicode pair import",delegate {string p=NewPath(),src=p+".source";Put(src,"{\"x\":\"\\ud83d\\ude00\"}");var s=new AtomicSettingsStore(p);s.Load(false);Require(s.ImportGodotSettings(src).Accepted && Read(s,"x")=="\ud83d\ude00" && s.Flush().Accepted,"valid pair");});
        Test("invalid UTF8 rejected",delegate {string p=NewPath(),src=p+".source";File.WriteAllBytes(src,new byte[]{123,34,120,34,58,34,0xed,0xa0,0x80,34,125});var s=new AtomicSettingsStore(p);s.Load(false);Require(!s.ImportGodotSettings(src).Accepted,"invalid bytes accepted");});
        Test("duplicate decoded and nested keys rejected",delegate {foreach(string raw in new[]{"{\"a\":1,\"\\u0061\":2}","{\"x\":{\"a\":1,\"a\":2}}"}) {string p=NewPath(),src=p+".source";Put(src,raw);var s=new AtomicSettingsStore(p);s.Load(false);Require(!s.ImportGodotSettings(src).Accepted,"duplicate accepted");}});
        Test("nesting64 accepted nesting65 rejected",delegate {foreach(int depth in new[]{63,64}) {string p=NewPath(),src=p+".source";Put(src,"{\"x\":"+new string('[',depth)+"0"+new string(']',depth)+"}");var s=new AtomicSettingsStore(p);s.Load(false);Require(s.ImportGodotSettings(src).Accepted==(depth==63),"depth "+depth);}});
        Test("size limit accepted at 4194304 chars and rejected above",delegate {foreach(int n in new[]{4194304,4194305}) {string p=NewPath(),src=p+".source";Put(src,"{\"x\":\""+new string('a',n-8)+"\"}");var s=new AtomicSettingsStore(p);s.Load(false);Require(s.ImportGodotSettings(src).Accepted==(n==4194304),"size "+n);}});
        Test("stale primary before flush rejected",delegate {string p=NewPath();Put(p,Save("old"));var s=new AtomicSettingsStore(p);s.Load(false);s.Write("x","ours");Put(p,Save("other"));Require(!s.Flush().Accepted && File.ReadAllText(p)==Save("other"),"stale overwrite");});
        Test("late backup before flush rejected",delegate {string p=NewPath();Put(p,Save("old"));var s=new AtomicSettingsStore(p);s.Load(false);Put(p+".backup","{\"schema\":2}");Require(!s.Flush().Accepted,"late accepted");});
        Test("RACE external primary after precheck cannot be overwritten",delegate {
            string p=NewPath();Put(p,Save("old"));var fs=new HookFiles();var s=new AtomicSettingsStore(p,fs);s.Load(false);s.Write("x","ours");fs.Hook=delegate {Put(p,"{\"schema\":2}");};bool accepted=s.Flush().Accepted;
            Require(!accepted && File.ReadAllText(p)=="{\"schema\":2}","accepted="+accepted+" primary="+File.ReadAllText(p)+" backup="+(File.Exists(p+".backup")?File.ReadAllText(p+".backup"):"<absent>"));
        });
        Test("RACE future backup after precheck cannot be destroyed",delegate {
            string p=NewPath();Put(p,Save("old"));var fs=new HookFiles();var s=new AtomicSettingsStore(p,fs);s.Load(false);s.Write("x","ours");fs.Hook=delegate {Put(p+".backup","{\"schema\":2}");};bool accepted=s.Flush().Accepted;
            Require(!accepted && File.ReadAllText(p+".backup")=="{\"schema\":2}","accepted="+accepted+" backup="+(File.Exists(p+".backup")?File.ReadAllText(p+".backup"):"<absent>"));
        });
        Test("quarantine conflict prevents replacement",delegate {
            string p=NewPath();Put(p,"bad");Put(p+".backup",Save("good"));string quarantine=p+".corrupt."+Hash(File.ReadAllBytes(p));Put(quarantine,"conflict");var s=new AtomicSettingsStore(p);Require(s.Load(true).Accepted,"load");Require(!s.Flush().Accepted && File.ReadAllText(p)=="bad" && File.ReadAllText(quarantine)=="conflict" && File.ReadAllText(p+".backup")==Save("good"),"quarantine conflict mutated family");
        });
        Test("unreadable physical backup prevents load and flush",delegate {
            string p=NewPath();Put(p,Save("old"));Put(p+".backup",Save("backup"));var s=new AtomicSettingsStore(p);
            using(var locked=new FileStream(p+".backup",FileMode.Open,FileAccess.Read,FileShare.None)) Require(!s.Load(true).Accepted && s.State==SettingsState.IoError && !s.Flush().Accepted,"backup io ignored");
            Require(File.ReadAllText(p)==Save("old"),"primary changed");
        });
        Console.WriteLine("RESULT passes="+passes+" failures="+failures+" (failures are review counterexamples)");
        Environment.ExitCode=failures==0?0:1;
    }
}
