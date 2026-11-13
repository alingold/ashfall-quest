using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ashfall.Modules.Persistence;

internal static class PersistenceTests
{
    private static int passed;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action run) { run(); passed++; Console.WriteLine("PASS " + name); }
    private static byte[] Bytes(string s) { return Encoding.UTF8.GetBytes(s); }
    private static string Value(AtomicSettingsStore store, string key) { string s; Check(store.TryRead(key, out s), "Missing " + key); return s; }
    private sealed class FaultFiles : IAtomicSettingsFiles
    {
        public readonly Dictionary<string, byte[]> Data = new Dictionary<string, byte[]>();
        public bool FailCommit, FailCreate, DamagePending, FailRead;
        public bool Exists(string p) { return Data.ContainsKey(p); }
        public IDisposable AcquireFamily(string p) { return new PhysicalSettingsFiles().AcquireFamily(p); }
        public byte[] Read(string p, int maxBytes = AtomicSettingsStore.MaxDocumentBytes) { if (FailRead) throw new IOException("Injected access denied"); if (Data[p].Length > maxBytes) throw new IOException("Byte limit"); return (byte[])Data[p].Clone(); }
        public void CreateDurable(string p, byte[] b)
        {
            if (FailCreate) throw new IOException("Injected disk full");
            if (Data.ContainsKey(p)) throw new IOException("Exists");
            Data.Add(p, DamagePending && p.Contains(".pending.") ? Bytes("broken") : (byte[])b.Clone());
        }
        public void Commit(string pending, string p, string backup, bool replace)
        {
            if (FailCommit) throw new IOException("Injected replacement denied");
            Check(replace == Exists(p), "Replacement mode");
            if (replace && backup != null) Data[backup] = Data[p];
            Data[p] = Data[pending]; Data.Remove(pending);
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            string p = Path.GetFullPath(Path.Combine(args[0], "virtual.json"));
            Test("missing store roundtrip, unicode, opaque nested value and prior backup", delegate {
                var fs = new FaultFiles(); var a = new AtomicSettingsStore(p, fs);
                Check(a.Load(false).Accepted && a.State == SettingsState.Missing, "Missing");
                a.Write("dominant_hand", "left"); a.Write("unknown", "{\"x\":[1,true,null]}"); a.Write("unicode", "\u2603\ud83d\ude00\n\t");
                Check(a.Flush().Accepted, "First commit"); byte[] old = fs.Read(p);
                a.Write("dominant_hand", "right"); Check(a.Flush().Accepted, "Replace");
                Check(Convert.ToBase64String(old) == Convert.ToBase64String(fs.Read(p + ".backup")), "Backup exact");
                var b = new AtomicSettingsStore(p, fs); Check(b.Load(false).Accepted, "Reload");
                Check(Value(b,"dominant_hand") == "right" && Value(b,"unicode") == "\u2603\ud83d\ude00\n\t", "Roundtrip");
                Check(Value(b,"unknown") == "{\"x\":[1,true,null]}", "Opaque value");
            });
            Test("corrupt primary requires opt-in; recovery preserves corrupt and good backup", delegate {
                var fs = new FaultFiles(); fs.Data[p] = Bytes("{bad"); fs.Data[p+".backup"] = Bytes("{\"schema\":1,\"values\":{\"snap_degrees\":\"45\"}}");
                var a = new AtomicSettingsStore(p, fs); Check(!a.Load(false).Accepted && !a.Flush().Accepted, "Corrupt blocked");
                Check(a.Load(true).Accepted && a.State == SettingsState.Recovered && Value(a,"snap_degrees") == "45", "Recovery read");
                Check(Encoding.UTF8.GetString(fs.Read(p)) == "{bad", "Read nonmutating");
                Check(a.Flush().Accepted, "Explicit recovery commit");
                Check(Encoding.UTF8.GetString(fs.Read(p+".backup")).Contains("45"), "Good backup retained");
                bool retained = false; foreach(var key in fs.Data.Keys) if(key.Contains(".corrupt.")) retained = Encoding.UTF8.GetString(fs.Read(key)) == "{bad";
                Check(retained,"Corrupt exact quarantine");
            });
            Test("newer primary or backup prevents downgrade and recovery", delegate {
                foreach (string suffix in new[] { "", ".backup" }) {
                    var fs = new FaultFiles(); fs.Data[p] = Bytes("{\"schema\":1,\"values\":{}}");
                    fs.Data[p+suffix] = Bytes("{\"schema\":2,\"values\":{}}");
                    var a = new AtomicSettingsStore(p, fs); Check(!a.Load(true).Accepted && a.State == SettingsState.NewerSchema && !a.Flush().Accepted, "Future blocked");
                }
            });
            Test("failed replacement/disk full/verification retain old primary and retry", delegate {
                foreach(int fault in new[] {0,1,2}) {
                    var fs = new FaultFiles(); var a = new AtomicSettingsStore(p, fs); a.Load(false); a.Write("x","old"); Check(a.Flush().Accepted,"Initial");
                    fs.FailCommit = fault == 0; fs.FailCreate = fault == 1; fs.DamagePending = fault == 2;
                    a.Write("x","new"); Check(!a.Flush().Accepted,"Fault rejected");
                    var b = new AtomicSettingsStore(p, fs); Check(b.Load(false).Accepted && Value(b,"x") == "old","Old intact");
                    fs.FailCommit=false;fs.FailCreate=false;fs.DamagePending=false;
                    Check(a.Flush().Accepted,"Retry"); b.Load(false);Check(Value(b,"x")=="new","Retry persisted");
                }
            });
            Test("external edits and late newer backup rejected", delegate {
                var fs = new FaultFiles(); var a = new AtomicSettingsStore(p, fs); a.Load(false); a.Write("x","ours");
                fs.Data[p]=Bytes("{\"schema\":1,\"values\":{\"x\":\"theirs\"}}"); Check(!a.Flush().Accepted,"Stale blocked");
                a.Load(false);fs.Data[p+".backup"]=Bytes("{\"schema\":99}");Check(!a.Flush().Accepted,"Late future blocked");
            });
            Test("Godot exact bytes sidecar, unknown keys/nested numbers, original untouched", delegate {
                var fs = new FaultFiles(); string source = p+".godot-source";
                byte[] original=Bytes("\ufeff{ \"dominant_hand\":\"left\",\"snap_degrees\":45,\"future\":{\"precise\":9007199254740993},\"null_key\":null }");
                fs.Data[source]=original; var a=new AtomicSettingsStore(p,fs);a.Load(false);
                Check(a.ImportGodotSettings(source).Accepted,"Import"); Check(!fs.Exists(p),"Explicit commit only");
                Check(Value(a,"dominant_hand")=="left" && Value(a,"snap_degrees")=="45","Scalar mapping");
                Check(Value(a,"future")=="{\"precise\":9007199254740993}" && Value(a,"null_key")=="null","Unknown intact");
                Check(a.Flush().Accepted,"Migrated commit"); Check(Convert.ToBase64String(fs.Read(source))==Convert.ToBase64String(original),"Source exact");
                bool sidecar=false;foreach(var key in fs.Data.Keys)if(key.EndsWith(".json")&&key.Contains(".godot."))sidecar=Encoding.UTF8.GetString(fs.Read(key)).Contains(Convert.ToBase64String(original));
                Check(sidecar,"Original bytes sidecar"); Check(!a.ImportGodotSettings(source).Accepted,"No overwrite migration");
                var b=new AtomicSettingsStore(p,fs);Check(b.Load(false).Accepted&&Value(b,"future").Contains("9007199254740993"),"Imported reload");
            });
            Test("malformed JSON, duplicate keys, invalid escapes and versioned migration fail closed", delegate {
                foreach(string bad in new[]{"[]","{\"a\":NaN}","{\"a\":01}","{\"a\":1,}","{\"a\":1,\"a\":2}","{\"a\":\"\\q\"}","{\"version\":2}"}) {
                    var fs=new FaultFiles();fs.Data[p+".source"]=Bytes(bad);var a=new AtomicSettingsStore(p,fs);a.Load(false);
                    Check(!a.ImportGodotSettings(p+".source").Accepted&&!fs.Exists(p),"Bad migration: "+bad);
                }
            });
            Test("unknown envelope extension survives writes", delegate {
                var fs=new FaultFiles();fs.Data[p]=Bytes("{\"schema\":1,\"values\":{},\"future_extension\":{\"a\":[1,2]}}");
                var a=new AtomicSettingsStore(p,fs);Check(a.Load(false).Accepted,"Extension load");a.Write("x","y");Check(a.Flush().Accepted,"Extension flush");
                Check(Encoding.UTF8.GetString(fs.Read(p)).Contains("\"future_extension\":{\"a\":[1,2]}"),"Retained");
            });
            Test("unreadable store fails explicitly without default overwrite", delegate {
                var fs=new FaultFiles();fs.Data[p]=Bytes("{\"schema\":1,\"values\":{}}");fs.FailRead=true;
                var a=new AtomicSettingsStore(p,fs);Check(!a.Load(true).Accepted&&a.State==SettingsState.IoError&&!a.Flush().Accepted,"Read error");
                bool threw=false;try{a.Write("x","default");}catch(InvalidOperationException){threw=true;}Check(threw,"Defaults cannot overwrite unreadable");
            });
            Test("orphan backup requires opt-in, then restores missing primary", delegate {
                var fs=new FaultFiles();fs.Data[p+".backup"]=Bytes("{\"schema\":1,\"values\":{\"x\":\"backup\"}}");
                var a=new AtomicSettingsStore(p,fs);Check(!a.Load(false).Accepted&&!a.Flush().Accepted,"Orphan blocked");
                Check(a.Load(true).Accepted&&a.Flush().Accepted,"Orphan recovered");Check(fs.Exists(p+".backup"),"Backup retained");
                var b=new AtomicSettingsStore(p,fs);Check(b.Load(false).Accepted&&Value(b,"x")=="backup","Restored value");
            });
            Test("invalid schema and invalid value types fail explicitly", delegate {
                foreach(string bad in new[]{"{\"schema\":0,\"values\":{}}","{\"schema\":1,\"values\":{\"x\":false}}","{\"schema\":\"1\",\"values\":{}}","{\"schema\":1}","{\"schema\":1,\"schema\":1,\"values\":{}}"}) {
                    var fs=new FaultFiles();fs.Data[p]=Bytes(bad);var a=new AtomicSettingsStore(p,fs);
                    Check(!a.Load(false).Accepted&&(a.State==SettingsState.Corrupt || a.State==SettingsState.NewerSchema)&&!a.Flush().Accepted,"Invalid save: "+bad);
                }
            });
            Test("real Windows durable create and atomic File.Replace backup", delegate {
                string dir=Path.Combine(args[0],"physical-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                var a=new AtomicSettingsStore(Path.Combine(dir,"settings.json"));Check(a.Load(false).Accepted,"Physical load");a.Write("x","first");Check(a.Flush().Accepted,"Physical create");
                a.Write("x","second");Check(a.Flush().Accepted,"Physical replace");
                var backup=new AtomicSettingsStore(a.Path+".backup");Check(backup.Load(false).Accepted&&Value(backup,"x")=="first","Physical backup");
                var b=new AtomicSettingsStore(a.Path);Check(b.Load(false).Accepted&&Value(b,"x")=="second","Physical latest");
            });
            Console.WriteLine("RESULT " + passed + " behavior groups passed"); return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
