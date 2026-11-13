using System;
using Ashfall.Core;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.World;
namespace Ashfall.Integration.LifecycleTransactions
{
    // Policy metadata is root supplied from an approved source manifest. It is not
    // collision evidence. Floor/standing/hazard results always come from ISpawnQuery.
    public sealed class SourceSpawnPolicy
    {
        public readonly string SourceId,Mode,SourceTeam;
        public readonly int Team;public readonly bool Interior;
        public SourceSpawnPolicy(string id,string mode,string sourceTeam,int team,bool interior)
        {if(String.IsNullOrEmpty(id)||String.IsNullOrEmpty(mode)||String.IsNullOrEmpty(sourceTeam)||team< -1)throw new ArgumentException("spawn policy");SourceId=id;Mode=mode;SourceTeam=sourceTeam;Team=team;Interior=interior;}
    }
    public sealed class SourceSpawnEvidence
    {
        internal readonly SourceSpawnWorld Owner;
        public readonly SpawnSite Site;public readonly ContentIdentity Content;
        internal SourceSpawnEvidence(SourceSpawnWorld owner,SpawnSite site,ContentIdentity content){Owner=owner;Site=site;Content=content;}
        public bool Validate(string actor){return Owner.Revalidate(this,actor);}
    }
    public sealed class SourceSpawnWorld : ISpawnWorld
    {
        readonly ApprovedWorld world;readonly IWorldApproval approval;readonly WorldBuildPlan plan;
        readonly IWorldBuildPolicy bounds;readonly ISpawnQuery contacts;readonly float height;
        readonly SourceSpawnPolicy[] policies;
        public SourceSpawnWorld(ApprovedWorld world,IWorldApproval approval,IWorldBuildPolicy bounds,ISpawnQuery contacts,float height,SourceSpawnPolicy[] policies,Func<int,string> layers)
        {
            if(world==null||approval==null||bounds==null||contacts==null||policies==null||policies.Length>256||Single.IsNaN(height)||Single.IsInfinity(height)||height<=0)throw new ArgumentException("source spawn binding");
            this.world=world;this.approval=approval;this.bounds=bounds;this.contacts=contacts;this.height=height;this.policies=(SourceSpawnPolicy[])policies.Clone();
            plan=world.PrepareBuild(approval,bounds,layers);
            for(int i=0;i<policies.Length;i++){if(policies[i]==null)throw new ArgumentException("null policy");for(int j=0;j<i;j++)if(policies[i].SourceId==policies[j].SourceId)throw new ArgumentException("duplicate source spawn");}
        }
        public SpawnSite[] Sites(string mapId,bool warmup)
        {
            if(mapId!=world.Identity.MapId||warmup)return new SpawnSite[0];
            world.Revalidate(approval);var sites=new System.Collections.Generic.List<SpawnSite>();
            foreach(var p in policies)foreach(var s in plan.SafeSpawns(p.Mode,p.SourceTeam,height,contacts,bounds))
                if(s.Id==p.SourceId)sites.Add(new SpawnSite(s.Id,s.Position,p.Team,p.Interior,false));
            world.Revalidate(approval);return sites.ToArray();
        }
        public bool IsSafe(string mapId,string actorId,Float3 feet)
        {
            if(String.IsNullOrEmpty(actorId)||!feet.IsFinite)return false;
            foreach(var s in Sites(mapId,false))if(Equal(s.Feet,feet))return true;
            return false;
        }
        public bool TryEvidence(string mapId,string actor,SpawnSite exact,out SourceSpawnEvidence evidence)
        {
            evidence=null;if(exact==null||String.IsNullOrEmpty(actor))return false;
            foreach(var s in Sites(mapId,false))if(Same(s,exact)){evidence=new SourceSpawnEvidence(this,s,world.Identity);return true;}
            return false;
        }
        internal bool Revalidate(SourceSpawnEvidence evidence,string actor)
        {
            if(evidence==null||evidence.Owner!=this||!ReferenceEquals(evidence.Content,world.Identity)||String.IsNullOrEmpty(actor))return false;
            foreach(var s in Sites(world.Identity.MapId,false))if(Same(s,evidence.Site))return true;return false;
        }
        static bool Equal(Float3 a,Float3 b){return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z;}
        static bool Same(SpawnSite a,SpawnSite b){return a.Id==b.Id&&a.Team==b.Team&&a.Interior==b.Interior&&a.CapsulePlatform==b.CapsulePlatform&&Equal(a.Feet,b.Feet);}
    }
    // Wraps the actual root owner's reservation. Source evidence cannot publish a
    // root by itself. A missing native/root prepare implementation fails closed.
    public sealed class SourceRootPlacement : ILifecycleRootPort
    {
        readonly SourceSpawnWorld source;readonly ILifecycleRootPort root;
        public SourceRootPlacement(SourceSpawnWorld source,ILifecycleRootPort root){if(source==null||root==null)throw new ArgumentNullException();this.source=source;this.root=root;}
        public bool Prepare(RootPlacement exact,out IPreparedRootPlacement prepared)
        {
            prepared=null;SourceSpawnEvidence evidence;
            if(exact==null||exact.MapHash==null||!source.TryEvidence(exact.MapId,exact.Canonical,exact.Site,out evidence)||evidence.Content.PayloadSha256!=exact.MapHash)return false;
            IPreparedRootPlacement inner;if(!root.Prepare(exact,out inner)||inner==null)return false;
            prepared=new Prepared(evidence,exact.Canonical,inner);return true;
        }
        sealed class Prepared : IPreparedRootPlacement
        {
            readonly SourceSpawnEvidence evidence;readonly string actor;readonly IPreparedRootPlacement inner;bool done,aborted;
            internal Prepared(SourceSpawnEvidence e,string a,IPreparedRootPlacement r){evidence=e;actor=a;inner=r;}
            bool validated;
            public bool Validate(){validated=false;validated=!done&&!aborted&&evidence.Validate(actor)&&inner.Validate();return validated;}
            public void CommitValidated(){if(done)return;if(aborted||!validated)throw new InvalidOperationException("root not validated");inner.CommitValidated();done=true;}
            public void Abort(){if(!done&&!aborted){aborted=true;inner.Abort();}}
        }
    }
}
