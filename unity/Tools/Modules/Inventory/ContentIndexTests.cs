using System;
using Ashfall.Modules.Inventory;
namespace Ashfall.Modules.Inventory.Tests
{
    sealed class Approval : IExportApproval
    {
        public bool IsApproved(string kind, string id, string version, string sha) { return version == "reviewed-v1" && ((kind == "map" && id == "archive:Crazy Train" && sha == new string('a',64)) || (kind == "style" && id == "PJ" && (sha == new string('b',64) || sha == new string('c',64))) || (kind == "a:b" && id == "c" && sha == new string('a',64)) || (kind == "a" && id == "b:c" && sha == new string('b',64))); }
    }
    static class Tests
    {
        static int count;
        static void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
        static void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { count++; return; } catch (InvalidOperationException) { count++; return; } throw new Exception(name); }
        static ContentRecord Record(string kind, string id, string version, char hash) { return new ContentRecord(kind,id,version,new string(hash,64)); }
        public static int Main()
        {
            var a=Record("map","archive:Crazy Train","reviewed-v1",'a');
            var b=Record("style","PJ","reviewed-v1",'b');
            var approval=new Approval();
            var index=new ContentIndex(new [] {b,a},approval);
            Check(index.ContentHash=="42a9ff800e444d9995100f2e9c10d88c36959f1daa8154b8bd58f75f64a5a096","version 1 handshake golden fixture");
            Check(index.ContentHash==new ContentIndex(new [] {a,b},approval).ContentHash,"order-independent handshake");
            ContentRecord result;
            Check(index.TryResolve("style","PJ",out result) && Object.ReferenceEquals(result,b),"resolve original stable ID");
            Check(!index.TryResolve("style","pj",out result),"case-sensitive ID");
            Reject(delegate { new ContentIndex(new [] {a,a},approval); },"duplicate identity rejected");
            Reject(delegate { new ContentIndex(new [] {Record("map","x","historical-release",'a')},approval); },"historical export rejected");
            Reject(delegate { Record("map","x","reviewed-v1",'G'); },"invalid digest rejected");
            Reject(delegate { Record("map","\uD800","reviewed-v1",'a'); },"malformed UTF16 rejected");
            Check(index.ContentHash!=new ContentIndex(new [] {a,Record("style","PJ","reviewed-v1",'c')},approval).ContentHash,"changed asset breaks handshake");
            var collision=new ContentIndex(new [] {Record("a:b","c","reviewed-v1",'a'),Record("a","b:c","reviewed-v1",'b')},approval);
            Check(collision.TryResolve("a:b","c",out result) && result.Sha256==new string('a',64),"separator collision isolated");
            Console.WriteLine("PASS "+count+" ContentIndex behavior checks; golden="+index.ContentHash);
            return 0;
        }
    }
}
