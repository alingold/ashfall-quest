using System;
using System.Collections.Generic;
using System.Linq;
namespace Ashfall.Modules.World
{
    internal sealed class WorldNearestHit
    {
        internal readonly double Distance; internal readonly string Owner;
        internal WorldNearestHit(double distance,string owner){Distance=distance;Owner=owner;}
    }
    internal static class WorldNearestPolicy
    {
        // Fixed SI tolerance, measured from global minimum, independent of enumeration order.
        internal const double TieMeters=0.00001;
        internal static bool Allows(string owner,List<WorldNearestHit> hits)
        {
            if(String.IsNullOrWhiteSpace(owner)||hits.Count==0)return false;
            double min=hits.Min(h=>h.Distance);
            return hits.Where(h=>h.Distance<=min+TieMeters).All(h=>h.Owner==owner);
        }
    }
    public sealed class WorldQueryHit
    {
        public readonly string MapPayloadHash, SourceId, ActorId;
        public readonly int Layer; public readonly float Distance;
        // SourceId is the authored collider/body identity, never replaced by semantic owner.
        public string BodyId { get { return SourceId; } }
        public WorldQueryHit(string mapPayloadHash,string sourceId,string actorId,int layer,float distance)
        { MapPayloadHash=mapPayloadHash;SourceId=sourceId;ActorId=actorId;Layer=layer;Distance=distance; }
    }
    public sealed class WorldHitRouting
    {
        readonly string hash; readonly SemanticRegistry registry;
        public WorldHitRouting(WorldBuildPlan plan) { if(plan==null)throw new ArgumentNullException("plan");hash=plan.Identity.PayloadSha256;registry=plan.Registry(); }
        public bool CanInteract(string actor,string owner,float range,WorldQueryHit[] hits)
        {
            var rows=new List<WorldNearestHit>();return !String.IsNullOrWhiteSpace(owner)&&Append(actor,range,hits,rows)&&WorldNearestPolicy.Allows(owner,rows);
        }
        internal bool Append(string actor,float range,WorldQueryHit[] hits,List<WorldNearestHit> rows)
        {
            if(String.IsNullOrWhiteSpace(actor)||range<=0||Single.IsNaN(range)||Single.IsInfinity(range)||hits==null)return false;
            foreach(var h in hits)
            {
                if(h==null||h.Distance<0||Single.IsNaN(h.Distance)||Single.IsInfinity(h.Distance))return false;
                if(h.Layer==WorldLayers.Actor&&h.ActorId==actor)continue;
                if(h.Distance>range)continue;
                SemanticRecord role=null;if(h.MapPayloadHash==hash)try{role=registry.Resolve(h.SourceId);}catch(System.IO.InvalidDataException){}
                if(role!=null&&role.kind=="decoration"&&h.Layer==WorldLayers.Decoration)continue;
                rows.Add(new WorldNearestHit(h.Distance,role!=null&&role.kind=="interaction"&&h.Layer==WorldLayers.Interaction?role.ownerId:null));
            }
            return true;
        }
        public bool Trigger(string mapPayloadHash,string source,string owner,int layer)
        { if(String.IsNullOrWhiteSpace(owner)||mapPayloadHash!=hash||layer!=WorldLayers.Interaction)return false;try{return registry.Route(source,owner,true);}catch(System.IO.InvalidDataException){return false;} }
    }
}
