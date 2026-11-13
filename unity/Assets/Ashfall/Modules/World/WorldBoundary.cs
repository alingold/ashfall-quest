using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Ashfall.Core;

namespace Ashfall.Modules.World
{
    public sealed class ContentIdentity
    {
        public readonly int Schema;
        public readonly string MapId, Version, PayloadSha256, SourceRevision, SourceSha256, Disposition;
        internal ContentIdentity(MapDocument d, string hash) { Schema=d.schema; MapId=d.id; Version=d.version; PayloadSha256=hash; SourceRevision=d.sourceRevision; SourceSha256=d.sourceSha256; Disposition=d.approval; }
    }
    // Coordinates are already in map-local Unity SI, independently authored, never inferred from mesh hulls.
    [DataContract] public sealed class AuthoredBox
    {
        [DataMember] public float[] center, size;
    }
    [DataContract] public sealed class SemanticRecord
    {
        [DataMember] public string sourceId, ownerId, kind, evidence;
        [DataMember] public AuthoredBox[] boxes;
    }
    public sealed class WorldLayers
    {
        public const int Structure=8, Interaction=9, Actor=10, Decoration=11;
        public static void Validate(Func<int,string> name)
        {
            if(name==null || name(8)!="WorldStructure" || name(9)!="WorldInteraction" || name(10)!="PlayerActor" || name(11)!="WorldDecoration") throw new InvalidDataException("Unbound central collision layers 8/9/10/11");
        }
    }
    public sealed class ApprovedWorld
    {
        readonly byte[] payload;
        public ContentIdentity Identity { get; private set; }
        private ApprovedWorld(byte[] bytes, ContentIdentity identity) { payload=(byte[])bytes.Clone(); Identity=identity; }
        public static ApprovedWorld Load(byte[] bytes,string hash,IWorldApproval approval)
        {
            var frozen=WorldBoundary.Copy(bytes,hash);
            var d=WorldBoundary.Document(frozen);
            var id=new ContentIdentity(d,WorldLoader.Hash(frozen));
            WorldBoundary.Approve(id,approval);
            WorldLoader.Load(frozen,id.PayloadSha256,false,new LegacyApproval());
            return new ApprovedWorld(frozen,id);
        }
        sealed class LegacyApproval : IContentApproval { public bool IsApproved(string m,string v,string h) { return true; } }
        public LoadedMap Snapshot() { return WorldLoader.Load(payload,Identity.PayloadSha256,true,null); }
        public void Revalidate(IWorldApproval approval) { WorldBoundary.Approve(Identity,approval); }
        public WorldBuildPlan PrepareBuild(IWorldApproval approval,IWorldBuildPolicy policy,Func<int,string> layers)
        {
            WorldBoundary.Approve(Identity,approval); WorldLayers.Validate(layers);
            var plan=WorldBoundary.Prepare(payload,Identity,policy); WorldBoundary.Approve(Identity,approval); return plan;
        }
    }
    public sealed class QuarantinedWorld
    {
        readonly byte[] payload;
        public ContentIdentity Identity { get; private set; }
        private QuarantinedWorld(byte[] bytes,ContentIdentity id) { payload=bytes; Identity=id; }
        public static QuarantinedWorld LoadPilot(byte[] bytes,string hash)
        {
            var frozen=WorldBoundary.Copy(bytes,hash); WorldLoader.Load(frozen,hash,true,null);
            return new QuarantinedWorld(frozen,new ContentIdentity(WorldBoundary.Document(frozen),hash));
        }
        public LoadedMap Snapshot() { return WorldLoader.Load(payload,Identity.PayloadSha256,true,null); }
        public WorldBuildPlan PrepareQuarantinedPilot(IWorldBuildPolicy policy,Func<int,string> layers)
        { WorldLayers.Validate(layers); return WorldBoundary.Prepare(payload,Identity,policy); }
    }
    public sealed class WorldBuildPlan
    {
        readonly byte[] payload;
        public ContentIdentity Identity { get; private set; }
        internal WorldBuildPlan(byte[] bytes,ContentIdentity id) { payload=(byte[])bytes.Clone(); Identity=id; }
        public LoadedMap Snapshot() { return WorldLoader.Load(payload,Identity.PayloadSha256,true,null); }
        public SemanticRegistry Registry() { return new SemanticRegistry(WorldBoundary.Document(payload), Snapshot()); }
        public LoadedSpawn[] SafeSpawns(string mode,string team,float height,ISpawnQuery query,IWorldBuildPolicy policy)
        {
            if(policy==null)throw new ArgumentNullException("policy");
            return WorldLoader.SafeSpawns(Snapshot(),mode,team,height,query).Where(s=>policy.AllowsSpawn(Identity,mode,team,s.Position)).ToArray();
        }
    }
    internal static class WorldBoundary
    {
        internal static byte[] Copy(byte[] bytes,string hash) { if(bytes==null || bytes.Length>16*1024*1024)throw new InvalidDataException("Map payload limit"); var b=(byte[])bytes.Clone(); if(!String.Equals(WorldLoader.Hash(b),hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Payload hash mismatch"); return b; }
        internal static MapDocument Document(byte[] bytes) { StrictWorldJson.Validate(bytes); using(var m=new MemoryStream(bytes)){var d=(MapDocument)new DataContractJsonSerializer(typeof(MapDocument)).ReadObject(m);if(d==null||d.schema!=1)throw new InvalidDataException("Unsupported map schema");return d;} }
        internal static void Approve(ContentIdentity id,IWorldApproval approval) { if(id.Disposition!="accepted" || approval==null || !approval.IsApproved(id))throw new InvalidDataException("Approval denied or revoked: "+id.MapId); }
        internal static WorldBuildPlan Prepare(byte[] bytes,ContentIdentity id,IWorldBuildPolicy policy)
        {
            if(policy==null)throw new ArgumentNullException("policy");
            var map=WorldLoader.Load(bytes,id.PayloadSha256,true,null);
            new SemanticRegistry(Document(bytes),map);
            foreach(var surface in map.Surfaces)if(!policy.ApproveMaterial(id,surface))throw new InvalidDataException("Material identity denied: "+surface.Id);
            // Callbacks receive disposable copies; no callback-owned data is built.
            return new WorldBuildPlan(bytes,id);
        }
    }
    public sealed class SemanticRegistry
    {
        readonly Dictionary<string,SemanticRecord> rows=new Dictionary<string,SemanticRecord>(StringComparer.Ordinal);
        internal SemanticRegistry(MapDocument d,LoadedMap map)
        {
            if(d.semantics==null)throw new InvalidDataException("Unclassified source IDs: "+String.Join(",",map.Surfaces.Select(s=>s.Id).Concat(map.Pads.Select(p=>p.Id))));
            if(d.semanticsVersion!="world-semantics/1")throw new InvalidDataException("Unsupported semantic registry version");
            foreach(var r in d.semantics)
            {
                if(r==null || String.IsNullOrWhiteSpace(r.sourceId) || String.IsNullOrWhiteSpace(r.ownerId) || String.IsNullOrWhiteSpace(r.evidence) || rows.ContainsKey(r.sourceId) || !(r.kind=="structure"||r.kind=="interaction"||r.kind=="hazard"||r.kind=="decoration"))throw new InvalidDataException("Invalid semantic classification: "+(r==null?"null":r.sourceId));
                if(r.kind=="interaction"||r.kind=="hazard")
                {
                    if(r.boxes==null||r.boxes.Length==0||r.boxes.Length>32)throw new InvalidDataException("Authored boxes required: "+r.sourceId);
                    foreach(var box in r.boxes)if(box==null||box.center==null||box.size==null||box.center.Length!=3||box.size.Length!=3||box.center.Any(x=>Single.IsNaN(x)||Single.IsInfinity(x))||box.size.Any(x=>Single.IsNaN(x)||Single.IsInfinity(x)||x<=0))throw new InvalidDataException("Invalid authored box: "+r.sourceId);
                }
                else if(r.boxes!=null && r.boxes.Length!=0)throw new InvalidDataException("Unexpected volume: "+r.sourceId);
                rows.Add(r.sourceId,r);
            }
            foreach(var s in map.Surfaces)
            {
                SemanticRecord r; if(!rows.TryGetValue(s.Id,out r))throw new InvalidDataException("Unclassified source ID: "+s.Id);
                if(s.SemanticOwner!=r.ownerId || (r.kind=="structure"?"structure":r.kind=="decoration"?"decoration":"trigger")!=s.ColliderRole)throw new InvalidDataException("Source role/owner mismatch: "+s.Id);
            }
            foreach(var p in map.Pads) { SemanticRecord r; if(!rows.TryGetValue(p.Id,out r)||r.kind!="interaction"||r.ownerId!=p.SemanticOwner)throw new InvalidDataException("Unclassified pad: "+p.Id); }
            if(rows.Count!=map.Surfaces.Length+map.Pads.Length)throw new InvalidDataException("Unknown semantic source IDs: "+String.Join(",",rows.Keys.Except(map.Surfaces.Select(s=>s.Id).Concat(map.Pads.Select(p=>p.Id)))));
        }
        // Fresh records returned: callers cannot mutate the registry.
        public SemanticRecord Resolve(string sourceId)
        {
            SemanticRecord r;if(sourceId==null||!rows.TryGetValue(sourceId,out r))throw new InvalidDataException("Unknown collider source: "+sourceId);
            return new SemanticRecord {sourceId=r.sourceId,ownerId=r.ownerId,kind=r.kind,evidence=r.evidence,boxes=r.boxes==null?null:r.boxes.Select(b=>new AuthoredBox {center=(float[])b.center.Clone(),size=(float[])b.size.Clone()}).ToArray()};
        }
        public bool Route(string sourceId,string expectedOwner,bool trigger)
        { var r=Resolve(sourceId);return r.ownerId==expectedOwner && (trigger?(r.kind=="interaction"||r.kind=="hazard"):r.kind=="interaction"); }
    }
}
