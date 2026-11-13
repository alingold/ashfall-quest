using System;
using System.IO;
using System.Text;
using Ashfall.Modules.Persistence;
internal static class PersistenceR2Tests
{
    static string root;
    static int count;
    static string NewPath() { string d=Path.Combine(root,"r2-case-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return Path.Combine(d,"settings.json"); }
    static void Put(string p,string text) { File.WriteAllBytes(p,new UTF8Encoding(false,true).GetBytes(text)); }
    static string Doc(string schema) { return "{\"schema\":"+schema+",\"values\":{}}"; }
    static void Check(bool value,string reason) { if(!value) throw new Exception(reason); count++; Console.WriteLine("PASS "+reason); }
    sealed class HookFiles : IAtomicSettingsFiles
    {
        readonly PhysicalSettingsFiles disk=new PhysicalSettingsFiles();
        public Action Hook;
        public IDisposable AcquireFamily(string p) { return disk.AcquireFamily(p); }
        public bool Exists(string p) {return disk.Exists(p);}
        public byte[] Read(string p,int max) {return disk.Read(p,max);}
        public void CreateDurable(string p,byte[] b) {disk.CreateDurable(p,b);if(p.Contains(".pending.")&&Hook!=null){Action h=Hook;Hook=null;h();}}
        public void Commit(string p,string d,string b,bool r) {disk.Commit(p,d,b,r);}
    }
    static int Main(string[] args)
    {
        try
        {
            root=args[0];
            foreach(string schema in new[]{"1e100","1.00000000000000000000000000001","1e99999999999999999999999","0","-1","true","\"1\"","null","{}"})
            {
                string p=NewPath(); Put(p,Doc("1")); var s=new AtomicSettingsStore(p); Check(s.Load(false).Accepted,"late setup "+schema);
                Put(p+".backup",Doc(schema)); string before=File.ReadAllText(p+".backup");
                Check(!s.Flush().Accepted && File.ReadAllText(p+".backup")==before,"late unsupported backup preserved "+schema);
                Check(!s.Load(true).Accepted && s.State==SettingsState.NewerSchema,"unsupported backup blocks recovery "+schema);
                Put(p,Doc(schema));Put(p+".backup",Doc("1"));
                Check(!s.Load(true).Accepted && s.State==SettingsState.NewerSchema,"unsupported primary blocks recovery "+schema);
            }
            foreach(string schema in new[]{"1","1.0","1e0","1E+0000","0.1e1","1000e-3","1.00000000000000000000000000000000000000"})
            {string p=NewPath();Put(p,Doc(schema));var s=new AtomicSettingsStore(p);Check(s.Load(false).Accepted&&s.Flush().Accepted,"exact schema one "+schema);}
            foreach(string token in new[]{"\\ud800","\\udc00","\\ud800a","a\\udc00"})
            {
                foreach(bool key in new[]{true,false})
                {
                    string p=NewPath();Put(p,"{\"schema\":1,\"values\":{"+(key?"\""+token+"\":\"ok\"":"\"x\":\""+token+"\"")+"}}");
                    var s=new AtomicSettingsStore(p);Check(!s.Load(false).Accepted&&!s.Flush().Accepted,"unpaired loaded "+(key?"key ":"value ")+token);
                }
                string q=NewPath();var a=new AtomicSettingsStore(q);a.Load(false);
                string bad=JsonTokens.String("\"ok\"");
                // Direct .NET strings test public input separately from escaped JSON parsing.
                bad=token.Replace("\\ud800","\ud800").Replace("\\udc00","\udc00");
                bool keyRejected=false,valueRejected=false;
                try{a.Write(bad,"ok");}catch(FormatException){keyRejected=true;}
                try{a.Write("x",bad);}catch(FormatException){valueRejected=true;}
                Check(keyRejected&&valueRejected,"Write rejects unpaired inputs "+token);
            }
            {
                string p=NewPath();var fs=new HookFiles();var a=new AtomicSettingsStore(p,fs);a.Load(false);a.Write("x","first");a.Flush();a.Write("x","second");
                var b=new AtomicSettingsStore(p);Check(b.Load(false).Accepted,"second owner snapshot");
                fs.Hook=delegate {
                    Check(!b.Load(false).Accepted && b.State==SettingsState.IoError,"contending Load rejected during commit");
                    Check(!b.Flush().Accepted,"contending Flush rejected during commit");
                    bool denied=false;try{using(new PhysicalSettingsFiles().AcquireFamily(p)) { }}catch(IOException){denied=true;}
                    Check(denied,"independent physical owner denied during commit");
                };
                Check(a.Flush().Accepted,"owner holds lock through physical replacement");
                Check(b.Load(false).Accepted,"lock released after commit");
                a.Write("x","third");Check(a.Flush().Accepted,"next owner commit");b.Write("x","stale");Check(!b.Flush().Accepted,"serialized stale snapshot rejected");
            }
            {
                string p=NewPath(),src=p+".source";Put(src,"{\"x\":1}");var s=new AtomicSettingsStore(p);s.Load(false);
                using(new PhysicalSettingsFiles().AcquireFamily(p))
                {Check(!s.ImportGodotSettings(src).Accepted,"import respects family lock");Check(!s.Flush().Accepted,"flush respects family lock");}
                Check(s.ImportGodotSettings(src).Accepted&&s.Flush().Accepted,"lock contention retry");
            }
            {
                string p=NewPath();using(var file=new FileStream(p,FileMode.CreateNew))file.SetLength((long)AtomicSettingsStore.MaxDocumentBytes+1);
                bool rejected=false;try{new PhysicalSettingsFiles().Read(p,AtomicSettingsStore.MaxDocumentBytes);}catch(IOException){rejected=true;}
                Check(rejected,"physical byte limit rejects before buffer allocation");var s=new AtomicSettingsStore(p);Check(!s.Load(true).Accepted&&!s.Flush().Accepted,"oversize primary cannot recover overwrite");
                string q=NewPath();Put(q,Doc("1"));s=new AtomicSettingsStore(q);s.Load(false);File.Copy(p,q+".backup");Check(!s.Flush().Accepted,"oversize late backup blocks commit");
                string r=NewPath();s=new AtomicSettingsStore(r);s.Load(false);Check(!s.ImportGodotSettings(p).Accepted,"oversize import blocked");
                byte[] exact=new byte[1024];File.WriteAllBytes(r+".bounded",exact);Check(new PhysicalSettingsFiles().Read(r+".bounded",1024).Length==1024,"physical exact byte boundary accepted");
            }
            Console.WriteLine("RESULT "+count+" assertions passed");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
