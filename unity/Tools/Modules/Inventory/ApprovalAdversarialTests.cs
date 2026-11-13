using System;
using System.Globalization;
using System.Threading;
using Ashfall.Modules.Inventory;
sealed class Exact : IExportApproval {
 public readonly ContentRecord R;
 public Exact(ContentRecord r) { R=r; }
 public bool IsApproved(string k,string i,string v,string h) { return k==R.Kind && i==R.SourceId && v==R.Version && h==R.Sha256; }
}
sealed class All : IExportApproval { public bool IsApproved(string k,string i,string v,string h) { return true; } }
static class Adversarial {
 static int count;
 static void Check(bool b,string s) { if(!b) throw new Exception(s); count++; Console.WriteLine("PASS "+s); }
 static ContentRecord R(string k,string i,string v,char h) { return new ContentRecord(k,i,v,new string(h,64)); }
 public static int Main() {
  ContentRecord good=R("map","ID","v1",'a'); var exact=new Exact(good);
  foreach(ContentRecord bad in new[]{R("Map","ID","v1",'a'),R("map","id","v1",'a'),R("map","ID","v2",'a'),R("map","ID","v1",'b')}) {
   bool rejected=false;try {new ContentIndex(new[]{bad},exact);}catch(InvalidOperationException){rejected=true;}
   Check(rejected,"exact approval rejects changed field");
  }
  var all=new All(); var rows=new[]{R("kind","\uE000","v\n:1",'a'),R("kind","\uD800\uDC00","v\n:1",'b'),R("kind","I","v\n:1",'c'),R("kind","\u0131","v\n:1",'d')};
  string baseline=new ContentIndex(rows,all).ContentHash;
  Thread.CurrentThread.CurrentCulture=new CultureInfo("tr-TR");Array.Reverse(rows);
  Check(baseline==new ContentIndex(rows,all).ContentHash,"ordinal ordering independent of Turkish culture/input order");
  Check(baseline=="cb5bea0102f3e814daa9dfa30c7cbc38818e71fd1b13d7120fdb518972e08e26","independent encoding oracle golden");
  Console.WriteLine("ORDINAL GOLDEN "+baseline);
  ContentRecord found;
  var index=new ContentIndex(new[]{R("map","\uFFFD","v1",'a')},all);
  bool alias=index.TryResolve("map","\uD800",out found);
  Check(!alias,"malformed high-surrogate lookup does not alias replacement ID");
  Check(!index.TryResolve("map","MAP",out found),"unregistered lookup rejected");
  var kindIndex=new ContentIndex(new[]{R("\uFFFD","x","v1",'a')},all);
  Check(!kindIndex.TryResolve("\uDC00","x",out found),"malformed kind lookup does not alias replacement kind");
  foreach(string invalid in new[]{"\uD800","\uDC00"}) {
   foreach(int position in new[]{0,1,2}) {
    bool rejected=false;try {R(position==0?invalid:"map",position==1?invalid:"id",position==2?invalid:"v1",'a');}catch(ArgumentException){rejected=true;}
    Check(rejected,"malformed UTF16 constructor field rejected");
   }
  }
  Console.WriteLine("PASS "+count+" retained reviewer regression checks");return 0;
 }
}
