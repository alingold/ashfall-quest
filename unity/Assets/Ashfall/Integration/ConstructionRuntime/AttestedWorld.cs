using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionGeometry;
namespace Ashfall.Integration.ConstructionRuntime
{
    // Authoritative triangle fallback: exact World-baked SI vertices/winding, two-sided
    // closed-segment LOS, .045 contact expansion, .4 objective margin. No global Physics state.
    public sealed class AttestedWorld : IConstructionWorld
    {
        readonly ApprovedWorld map; readonly IWorldApproval approval; readonly IGeometryAttestation attestation;
        readonly SourceTriangle[] triangles; readonly IRuntimeSnapshot occupants; readonly bool archive;
        readonly FloorBurialPolicy policy; PlacedPanel[] panels=new PlacedPanel[0];
        sealed class FrozenOccupants:IConstructionOccupants
        {
            public ProtectedBox[] Players;public Float3[] Objectives;
            public bool TrySnapshot(out ProtectedBox[] p,out Float3[] o){p=Players;o=Objectives;return p!=null&&o!=null;}
        }
        readonly FrozenOccupants frozen=new FrozenOccupants();
        string instance;
        internal void Attach(string session,long epoch,string hostInstance)
        {if(instance!=null||String.IsNullOrEmpty(hostInstance))throw new ArgumentException("exclusive_host_instance");instance=BodyIdentity.ConstructedScope(session,epoch,hostInstance);}
        internal bool Capture()
        {
            long generation=occupants.Generation;ProtectedBox[] p;Float3[] o;
            if(!Ready||!occupants.TrySnapshot(out p,out o)||p==null||o==null)return false;
            frozen.Players=(ProtectedBox[])p.Clone();frozen.Objectives=(Float3[])o.Clone();
            return occupants.IsCurrent(generation);
        }
        public long PreparedGeneration {get{return occupants.Generation;}}
        public bool PreparedCurrent(long generation){return Ready&&occupants.IsCurrent(generation);}
        public bool PrepareSnapshot(PlacedPanel[] current){if(!Capture())return false;Bind(current);return true;}
        public ContentIdentity Identity {get{return map.Identity;}}
        public AttestedWorld(ApprovedWorld world,IWorldApproval approvals,IWorldBuildPolicy materials,Func<int,string> layers,
            IGeometryAttestation roles,IRuntimeSnapshot snapshot,bool isArchive,FloorBurialPolicy floorPolicy)
        {
            if(world==null||roles==null||snapshot==null)throw new ArgumentException("binding_dependencies");
            map=world;approval=approvals;attestation=roles;occupants=snapshot;archive=isArchive;policy=floorPolicy;
            var plan=map.PrepareBuild(approval,materials,layers);var registry=plan.Registry();var list=new List<SourceTriangle>();
            var bodyOwners=new Dictionary<string,string>(StringComparer.Ordinal);
            var geometrySnapshot=plan.Snapshot();
            foreach(var pad in geometrySnapshot.Pads) RejectVolume(registry.Resolve(pad.Id));
            foreach(var surface in geometrySnapshot.Surfaces)
            {
                var semantic=registry.Resolve(surface.Id);RejectVolume(semantic);string body;SurfaceRole role;
                if(!roles.Resolve(map.Identity,surface.Id,semantic.ownerId,out body,out role)||String.IsNullOrEmpty(body)||body==semantic.ownerId)
                    throw new ArgumentException("unattested_body:"+surface.Id);
                body=BodyIdentity.Map(map.Identity,body);
                if(body==semantic.ownerId)throw new ArgumentException("body_semantic_alias:"+surface.Id);
                string owner;if(bodyOwners.TryGetValue(body,out owner)&&owner!=semantic.ownerId)throw new ArgumentException("body_owner_conflict");
                bodyOwners[body]=semantic.ownerId;
                bool structural=role==SurfaceRole.Structure||role==SurfaceRole.TerrainSurface||role==SurfaceRole.ConstructionTerrain;
                if((semantic.kind=="structure"&&!structural)||(semantic.kind=="decoration"&&role!=SurfaceRole.Decoration)||
                    (semantic.kind!="structure"&&semantic.kind!="decoration"))throw new ArgumentException("unsupported_role:"+surface.Id);
                for(int i=0;i<surface.Indices.Length;i+=3)list.Add(new SourceTriangle("map-source/"+BodyIdentity.Part(surface.Id)+"/tri/"+(i/3),semantic.ownerId,body,map.Identity.PayloadSha256,role,
                    role==SurfaceRole.Decoration?11:8,surface.Vertices[surface.Indices[i]],surface.Vertices[surface.Indices[i+1]],surface.Vertices[surface.Indices[i+2]]));
            }
            // Reserve room for all 256 live construction bodies before any mutation.
            if(list.Count>100000-256*12)throw new ArgumentException("runtime_triangle_budget");
            triangles=list.ToArray(); Query();
        }
        static void RejectVolume(SemanticRecord row)
        {if(row.kind=="interaction"||row.kind=="hazard"||(row.boxes!=null&&row.boxes.Length>0))throw new ArgumentException("unsupported_authored_volume:"+row.sourceId);}
        public bool Ready {get{try{map.Revalidate(approval);return attestation.Current(map.Identity);}catch(System.IO.InvalidDataException){return false;}}}
        // Caller owns this preallocated immutable array; shared-lane commit is assignment only.
        internal void PublishPreparedPanels(PlacedPanel[] frozenPanels){panels=frozenPanels;}
        internal void Bind(PlacedPanel[] current){panels=(PlacedPanel[])current.Clone();}
        StructuralWorld Query()
        {
            if(!Ready)throw new InvalidOperationException("map_attestation_revoked");
            var all=new List<SourceTriangle>(triangles);
            foreach(var p in panels)all.AddRange(PanelTriangles(new PlacedPanel(BodyIdentity.Part(instance)+BodyIdentity.Part(p.Id),p.Owner,p.Shape,p.Health),Identity.PayloadSha256));
            return new StructuralWorld(new CollisionAllocation(8,9,10,11),all,frozen,archive,policy);
        }
        public bool HasStaticContact(Panel p,float tolerance){return Query().HasStaticContact(p,tolerance);}
        public bool IsBuried(Panel p){return Query().IsBuried(p);}
        public string Clearance(Panel p,string actor){return Query().Clearance(p,actor);}
        public bool HasLineOfSight(string actor,Float3 from,Float3 to){return Query().HasLineOfSight(actor,from,to);}
        // Construction bodies participate in nearest-ground and LOS, never static support.
        public static SourceTriangle[] PanelTriangles(PlacedPanel p,string hash)
        {
            var h=Geometry.HalfSize(p.Shape);var c=p.Shape.Center;var v=new Float3[8];
            for(int i=0;i<8;i++)v[i]=new Float3(c.X+((i&1)==0?-h.X:h.X),c.Y+((i&2)==0?-h.Y:h.Y),c.Z+((i&4)==0?-h.Z:h.Z));
            int[] ix={0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7,0,2,1,1,2,3,4,5,6,5,7,6};
            var result=new SourceTriangle[12];for(int i=0;i<12;i++)result[i]=new SourceTriangle("construction/"+p.Id+"/"+i,p.Owner,BodyIdentity.Constructed(p.Id),hash,SurfaceRole.Construction,8,v[ix[i*3]],v[ix[i*3+1]],v[ix[i*3+2]]);return result;
        }
    }
}

