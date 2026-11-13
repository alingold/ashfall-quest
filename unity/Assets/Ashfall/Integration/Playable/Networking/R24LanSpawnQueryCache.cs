using System;using Ashfall.Core;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    // One synchronous host transaction only. Freeze the provider's selected
    // source sites across prepare/validate; always re-query actual safety.
    public sealed class R24LanSpawnQueryCache
    {
        sealed class Scope:IDisposable
        {
            readonly R24LanSpawnQueryCache owner;internal readonly ISpawnWorld World;internal readonly string Map,Actor;internal readonly bool Warmup;
            internal bool Computed,Computing;internal SpawnSite[] Sites;
            internal Scope(R24LanSpawnQueryCache owner,ISpawnWorld world,string map,string actor,bool warmup){this.owner=owner;World=world;Map=map;Actor=actor;Warmup=warmup;}
            public void Dispose(){if(ReferenceEquals(owner.current,this))owner.current=null;}
        }
        Scope current;
        public string Actor{get{return current==null?null:current.Actor;}}
        public IDisposable Begin(ISpawnWorld world,string map,string actor,bool warmup)
        {
            if(current!=null)throw new InvalidOperationException("Spawn query transaction reentry");
            if(String.IsNullOrEmpty(map)||String.IsNullOrEmpty(actor))throw new ArgumentException("Actual map and authenticated spawn actor required");
            current=new Scope(this,world,map,actor,warmup);return current;
        }
        public SpawnSite[] Sites(string map,bool warmup)
        {
            var scope=current;if(scope==null||scope.Map!=map||scope.Warmup!=warmup||scope.World==null)return new SpawnSite[0];
            if(scope.Computing)throw new InvalidOperationException("Source site provider reentry");
            if(!scope.Computed){
                scope.Computing=true;
                try{var sites=scope.World.Sites(map,warmup);scope.Sites=sites==null?new SpawnSite[0]:(SpawnSite[])sites.Clone();scope.Computed=true;}
                finally{scope.Computing=false;}
            }
            return(SpawnSite[])scope.Sites.Clone();
        }
        public bool IsSafe(string map,string actor,Float3 feet)
        {var scope=current;return scope!=null&&scope.World!=null&&scope.Map==map&&scope.Actor==actor&&scope.World.IsSafe(map,actor,feet);}
        public void Clear(){current=null;}
    }
}
