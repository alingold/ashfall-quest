using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.TeamLaunch;
using UnityEngine;

namespace Ashfall.Integration.Playable.World
{
    public sealed partial class R23PlayableMap
    {
        struct SpawnSurface
        {
            internal int Material;
            internal sbyte[] Facing;
        }

        struct Boundary
        {
            internal float Distance;
            internal int Facing;
        }

        readonly Dictionary<Collider,SpawnSurface> spawnSurfaces=new Dictionary<Collider,SpawnSurface>();
        readonly RaycastHit[] spawnHits=new RaycastHit[128];
        Boundary[] aboveSpawn,belowSpawn;

        void ApplyReviewedSpawnDefaults()
        {
            spawnSurfaces.Clear();
            aboveSpawn=new Boundary[manifest.materials.Length];
            belowSpawn=new Boundary[manifest.materials.Length];
            Ashfall.Core.Float3 point;
            if(R26MapPodPolicy.ReviewedDefault(MapId,SourceSha256,true,out point))localWarmupSpawn=new Vector3(point.X,point.Y,point.Z);
            if(R26MapPodPolicy.ReviewedDefault(MapId,SourceSha256,false,out point))localSpawn=localGameSpawn=new Vector3(point.X,point.Y,point.Z);
        }

        void RegisterSpawnShell(MeshCollider collider,int material,Vector3[] vertices,int[] indices)
        {
            if(manifest.version!=1||!MapId.StartsWith("archive:",StringComparison.Ordinal))return;
            // The archive converter's outward vertex normals follow (c-a) cross (b-a).
            // PhysX receives both windings for walking and climbing on thin sheets.
            var up=collider.transform.InverseTransformDirection(Vector3.up);
            var sides=new sbyte[indices.Length/3];
            for(int i=0;i<sides.Length;i++){
                Vector3 a=vertices[indices[3*i]],b=vertices[indices[3*i+1]],c=vertices[indices[3*i+2]];
                var normal=Vector3.Cross(c-a,b-a);
                float vertical=Vector3.Dot(normal,up);
                if(vertical*vertical>.0225f*normal.sqrMagnitude)sides[i]=(sbyte)(vertical>0?1:-1);
            }
            spawnSurfaces.Add(collider,new SpawnSurface{Material=material,Facing=sides});
        }

        bool ReadBoundaries(Vector3 point,Vector3 direction,Boundary[] boundary)
        {
            Array.Clear(boundary,0,boundary.Length);
            int count=Physics.RaycastNonAlloc(point,direction,spawnHits,Mathf.Infinity,1<<gameObject.layer,QueryTriggerInteraction.Ignore);
            if(count==spawnHits.Length)return false;
            for(int i=0;i<count;i++){
                var hit=spawnHits[i];SpawnSurface surface;
                if(hit.triangleIndex<0||!spawnSurfaces.TryGetValue(hit.collider,out surface))continue;
                int facing=surface.Facing[hit.triangleIndex%surface.Facing.Length];
                int m=surface.Material;float d=Mathf.Max(.00001f,hit.distance);
                if(boundary[m].Distance==0||d<boundary[m].Distance-.015f)boundary[m]=new Boundary{Distance=d,Facing=facing};
                else if(Mathf.Abs(d-boundary[m].Distance)<=.015f&&boundary[m].Facing!=facing)boundary[m].Facing=0;
            }
            return true;
        }

        public bool SpawnClearsShell(Vector3 feet)
        {
            if(spawnSurfaces.Count==0)return true;
            var at=feet+Vector3.up*.2f;
            bool backfaces=Physics.queriesHitBackfaces;
            try{
                Physics.queriesHitBackfaces=true;
                if(!ReadBoundaries(at,Vector3.up,aboveSpawn)||!ReadBoundaries(at,Vector3.down,belowSpawn))return false;
            }finally{Physics.queriesHitBackfaces=backfaces;}
            float floor=float.PositiveInfinity;
            for(int i=0;i<belowSpawn.Length;i++)if(belowSpawn[i].Distance>0)floor=Mathf.Min(floor,belowSpawn[i].Distance);
            bool hasTop=false;
            for(int i=0;i<belowSpawn.Length;i++){
                var down=belowSpawn[i];
                if(down.Distance>0&&down.Distance<=floor+.015f&&down.Facing>=0)hasTop=true;
                // Closed slab: outward top above the body and outward underside below it.
                // The sg terrain sheet has a surface only on its upper side.
                if(aboveSpawn[i].Facing>0&&(down.Facing<0||manifest.materials[i].name=="sg"))return false;
            }
            return floor>.65f||hasTop;
        }
    }
}
