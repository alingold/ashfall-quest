using System;
using System.IO;
using System.Text;
using Ashfall.Modules.Persistence;
namespace Ashfall.Modules.SaveMigration
{
    // Extends the accepted file port without editing Persistence or relying on internal APIs.
    // All accepted Load/Flush scans pass through this port under its canonical family lock.
    internal sealed class SemanticFiles : IAtomicSettingsFiles
    {
        private readonly IAtomicSettingsFiles inner;
        private readonly string primary;
        internal SemanticFiles(string path,IAtomicSettingsFiles files) { primary=Path.GetFullPath(path);inner=files; }
        public IDisposable AcquireFamily(string path){return inner.AcquireFamily(path);}
        public bool Exists(string path){return inner.Exists(path);}
        public byte[] Read(string path,int maxBytes)
        {
            byte[] bytes=inner.Read(path,maxBytes);
            if(path.Equals(primary,StringComparison.OrdinalIgnoreCase)||path.Equals(primary+".backup",StringComparison.OrdinalIgnoreCase))
            {
                string schema=null;
                try
                {
                    string text=new UTF8Encoding(false,true).GetString(bytes);if(text.StartsWith("\ufeff",StringComparison.Ordinal))text=text.Substring(1);
                    var root=RawJson.Object(text);string values;
                    if(root.TryGetValue("values",out values))
                    {
                        var data=RawJson.Object(values);string token;
                        if(data.TryGetValue("semantic.schema",out token))schema=RawJson.String(token);
                    }
                }
                catch(FormatException) { /* Accepted store handles corrupt outer documents/recovery. */ }
                catch(DecoderFallbackException) { }
                if(schema!=null&&schema!="1")throw new IOException("Unsupported semantic schema in Unity save family");
            }
            return bytes;
        }
        public void CreateDurable(string path,byte[] bytes){inner.CreateDurable(path,bytes);}
        public void Commit(string pending,string destination,string backup,bool replacing){inner.Commit(pending,destination,backup,replacing);}
    }
}
