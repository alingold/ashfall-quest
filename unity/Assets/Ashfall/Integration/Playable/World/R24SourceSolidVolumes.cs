#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace Ashfall.Integration.Playable.World
{
    // Static player collision for individually proven closed convex source
    // pieces. Original rendering and triangle collision remain map-owned.
    // There is deliberately no hull around a whole concave hourglass assembly.
    public sealed class R24SourceSolidVolumes
    {
        [Serializable]sealed class Definition{public string id="";public int material=0,vertices=0,triangles=0;public int[] references=null;}
        [Serializable]sealed class Data{public int version=0;public string mapId="",sourceSha256="",geometrySha256="";public Definition[] definitions=null;}
        sealed class Part{public Definition Definition;public readonly List<Vector3> Triangles=new List<Vector3>();public int Read;}
        struct Reference{public Part Part;public int Triangle;}
        readonly Data data;readonly List<Part> parts=new List<Part>();readonly Dictionary<int,List<Reference>> byChunk=new Dictionary<int,List<Reference>>();
        readonly List<MeshCollider> colliders=new List<MeshCollider>();bool verified,built;
        public int Count{get{return colliders.Count;}}
#if UNITY_EDITOR
        public MeshCollider[] ReadActualColliders(){return colliders.ToArray();}
#endif
        public R24SourceSolidVolumes(string json,string map,string source)
        {
            data=JsonUtility.FromJson<Data>(json);
            if(data==null||data.version!=1||map!="archive:afterlife-gods-alamo-hank"||data.mapId!=map||data.sourceSha256!=source||source!="c8a4f3ab47fe79acf3821c9e60d37fefaf567b756fd73bd99f60b7bb25c6063f"||data.geometrySha256.Length!=64||data.definitions==null||data.definitions.Length!=66)throw new InvalidDataException("Actual Afterlife solid-piece source binding");
            var ids=new HashSet<string>(StringComparer.Ordinal);var selected=new HashSet<long>();int total=0;
            foreach(var row in data.definitions){
                if(row==null||String.IsNullOrEmpty(row.id)||!ids.Add(row.id)||(row.material!=22&&row.material!=24)||row.vertices<4||row.vertices>255||row.triangles<4||row.triangles>128||row.references==null||row.references.Length!=row.triangles*2)throw new InvalidDataException("Bounded closed source solid definition");
                var part=new Part{Definition=row};parts.Add(part);
                for(int i=0;i<row.references.Length;i+=2){int chunk=row.references[i],triangle=row.references[i+1];long key=((long)chunk<<32)|(uint)triangle;if(chunk<0||chunk>=310||triangle<0||!selected.Add(key))throw new InvalidDataException("Disjoint original source solid triangles");List<Reference> list;if(!byChunk.TryGetValue(chunk,out list)){list=new List<Reference>();byChunk.Add(chunk,list);}list.Add(new Reference{Part=part,Triangle=triangle});total++;}
            }
            if(total!=728)throw new InvalidDataException("Actual source solid triangle count");
        }
        public void VerifyGeometry(Stream stream)
        {
            long position=stream.Position;
            try{stream.Position=0;string actual;using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(actual!=data.geometrySha256)throw new InvalidDataException("Source solid geometry checksum");verified=true;}
            finally{stream.Position=position;}
        }
        public void ReadChunk(int chunk,int material,int collisionFlags,bool visible,int owner,Vector3[] positions,int[] indices)
        {
            List<Reference> selected;if(!byChunk.TryGetValue(chunk,out selected))return;
            if(!verified||built||collisionFlags!=1||!visible||owner>=0)throw new InvalidDataException("Source solid must remain visible static world geometry");
            foreach(var item in selected){var part=item.Part;if(part.Definition.material!=material||item.Triangle>=indices.Length/3)throw new InvalidDataException("Source solid triangle/material range");for(int j=0;j<3;j++)part.Triangles.Add(positions[indices[item.Triangle*3+j]]);part.Read++;}
            byChunk.Remove(chunk);
        }
        public void Build(Transform parent,List<UnityEngine.Object> owned)
        {
            if(!verified||built||parent==null||owned==null||byChunk.Count!=0)throw new InvalidDataException("Complete original source solid coverage required");
            foreach(var part in parts){
                if(part.Read!=part.Definition.triangles)throw new InvalidDataException("Missing original solid triangles");
                var vertices=new List<Vector3>();var indices=new int[part.Triangles.Count];var lookup=new Dictionary<Vector3,int>();
                for(int i=0;i<indices.Length;i++){var point=part.Triangles[i];int index;if(!lookup.TryGetValue(point,out index)){index=vertices.Count;lookup.Add(point,index);vertices.Add(point);}indices[i]=index;}
                if(vertices.Count!=part.Definition.vertices)throw new InvalidDataException("Source solid point identity changed");
                Vector3 origin=vertices[0];for(int i=0;i<vertices.Count;i++)vertices[i]-=origin;
                var mesh=new Mesh{name=part.Definition.id+" exact closed source surface"};owned.Add(mesh);mesh.SetVertices(vertices);mesh.triangles=indices;mesh.RecalculateBounds();
                var node=new GameObject(part.Definition.id);node.layer=R23PlayableMap.PlayerOnlyLayer;node.transform.SetParent(parent,false);node.transform.localPosition=origin;
                var collider=node.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;colliders.Add(collider);
                // Retain readable source geometry through native convex cooking.
                part.Triangles.Clear();
            }
            built=true;
        }
    }
}
#endif
