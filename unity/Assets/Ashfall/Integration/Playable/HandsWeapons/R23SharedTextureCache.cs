using System;using System.Collections.Generic;
namespace Ashfall.Integration.Playable.HandsWeapons {
 // Main-thread asset leases. A loader retains each file/color-domain once;
 // materials/meshes remain private to that loader. No cached asset survives its
 // last actual loader owner. The generic core permits GPU-free lifecycle tests.
 internal sealed class R23SharedTextureCache<T> where T:class {
  struct Key:IEquatable<Key>{internal readonly string File;internal readonly bool Linear;internal Key(string file,bool linear){File=file;Linear=linear;}public bool Equals(Key other){return Linear==other.Linear&&String.Equals(File,other.File,StringComparison.Ordinal);}public override bool Equals(object value){return value is Key&&Equals((Key)value);}public override int GetHashCode(){return StringComparer.Ordinal.GetHashCode(File)*397^(Linear?1:0);}}
  sealed class Entry{internal T Value;internal int Owners;}
  readonly Dictionary<Key,Entry> entries=new Dictionary<Key,Entry>();readonly Action<T> destroy;
  internal R23SharedTextureCache(Action<T> release){if(release==null)throw new ArgumentNullException("release");destroy=release;}
  internal int Count{get{return entries.Count;}}
  internal bool Contains(string file,bool linear){return entries.ContainsKey(new Key(file,linear));}
  internal Owner OpenOwner(){return new Owner(this);}
  internal sealed class Owner:IDisposable {
   readonly R23SharedTextureCache<T> cache;readonly Dictionary<Key,Entry> retained=new Dictionary<Key,Entry>();bool disposed;
   internal Owner(R23SharedTextureCache<T> owner){cache=owner;}
   internal T Acquire(string file,bool linear,Func<T> create){
    if(disposed)throw new ObjectDisposedException("R23 texture owner");if(String.IsNullOrEmpty(file)||create==null)throw new ArgumentException("Actual file/color-domain and texture factory required");
    var key=new Key(file,linear);Entry entry;if(retained.TryGetValue(key,out entry))return entry.Value;
    if(!cache.entries.TryGetValue(key,out entry)){var value=create();if(value==null)throw new InvalidOperationException("Texture factory returned no resource");entry=new Entry{Value=value};cache.entries.Add(key,entry);}
    entry.Owners++;retained.Add(key,entry);return entry.Value;
   }
   public void Dispose(){if(disposed)return;disposed=true;foreach(var pair in retained){var entry=pair.Value;if(--entry.Owners==0){cache.entries.Remove(pair.Key);cache.destroy(entry.Value);}}retained.Clear();}
  }
 }
}
