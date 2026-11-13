#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.Playable.Bots
{
    // Map-owned source rescue craft. No collider, input, inventory or round authority.
    public sealed class R24ValleyExtraction:MonoBehaviour
    {
        readonly R24ValleyExtractionState state=new R24ValleyExtractionState();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        Transform craft,leftRotor,rightRotor;Light beacon;bool initialized,disposed;
        public bool Ready {get;private set;}
        public string Failure {get;private set;}
        public R24ValleyExtractionState State {get{return state;}}
        public Transform CraftRoot {get{return craft;}}
        public int BoxCount {get;private set;}
        public int TriangleCount {get;private set;}
        public int RendererCount {get;private set;}
        public Material SourceMaterial(string key){Material value;return materials.TryGetValue(key,out value)?value:null;}
        public void Initialize(R23PlayableMap map)
        {
            if(initialized||map==null)throw new InvalidOperationException("One extraction map owner");initialized=true;
            try
            {
                if(!map.IsLoaded||map.MapId!="valley"||map.ManifestSha256!=R24ValleyExtractionRecipe.MapManifestSha256)throw new InvalidOperationException("Source-bound Valley extraction material manifest differs");
                for(int i=0;i<R24ValleyExtractionRecipe.MaterialKeys.Length;i++)
                {Material material;if(!map.TryReadSourceMaterial(R24ValleyExtractionRecipe.MaterialNames[i],out material))throw new InvalidOperationException("Extraction source material missing "+R24ValleyExtractionRecipe.MaterialNames[i]);materials.Add(R24ValleyExtractionRecipe.MaterialKeys[i],material);}
                Build();Ready=true;Step(0);
            }
            catch(Exception error){Failure=error.ToString();Dispose();Debug.LogError("Source Valley extraction unavailable: "+Failure,this);}
        }
        static Vector3 G(Vector3 point){return new Vector3(point.x,point.y,-point.z);}
        static Quaternion Rotation(Vector3 sourceEuler)
        {return Quaternion.AngleAxis(-sourceEuler.y*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(-sourceEuler.x*Mathf.Rad2Deg,Vector3.right)*Quaternion.AngleAxis(sourceEuler.z*Mathf.Rad2Deg,Vector3.forward);}
        Transform Node(string label,Transform parent)
        {var go=new GameObject(label);go.layer=0;go.transform.SetParent(parent,false);return go.transform;}
        T Own<T>(T value)where T:UnityEngine.Object {owned.Add(value);return value;}
        void Build()
        {
            craft=Node("KESTREL civilian rescue VTOL (source)",transform);
            leftRotor=Node("Source left rescue rotor",craft);leftRotor.localPosition=G(new Vector3(-3.4f,1.6f,.2f));
            rightRotor=Node("Source right rescue rotor",craft);rightRotor.localPosition=G(new Vector3(3.4f,1.6f,.2f));
            var box=Own(UnitBox());var buckets=new Dictionary<string,List<CombineInstance>>();var specs=R24ValleyExtractionRecipe.Build();BoxCount=specs.Count;
            foreach(var spec in specs)
            {
                string key=((int)spec.Parent)+"/"+spec.Material;List<CombineInstance> pieces;if(!buckets.TryGetValue(key,out pieces)){pieces=new List<CombineInstance>();buckets.Add(key,pieces);}
                pieces.Add(new CombineInstance{mesh=box,transform=Matrix4x4.TRS(G(spec.Position),Rotation(spec.Rotation),spec.Size)});
            }
            foreach(var pair in buckets)
            {
                int separator=pair.Key.IndexOf('/');int parent=int.Parse(pair.Key.Substring(0,separator),System.Globalization.CultureInfo.InvariantCulture);string material=pair.Key.Substring(separator+1);
                var mesh=Own(new Mesh{name="Source rescue boxes "+pair.Key});mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(pair.Value.ToArray(),true,true,false);mesh.RecalculateBounds();
                Draw(mesh,materials[material],parent==0?craft:parent==1?leftRotor:rightRotor);
            }
            var hull=Own(Hull());var hullMaterial=Own(new Material(materials["white"]));hullMaterial.name="Source rescue white hull (cull disabled)";hullMaterial.SetFloat("_Cull",0);Draw(hull,hullMaterial,craft);
            foreach(float side in new[]{-1f,1f})
            {
                var at=Node("Source rescue searchlight "+side,craft);at.localPosition=G(new Vector3(side*.9f,-.5f,-1.8f));at.localRotation=Quaternion.Euler(75,0,0);
                var light=at.gameObject.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(.65f,.85f,1);light.intensity=5;light.range=20;light.spotAngle=56;light.innerSpotAngle=0;light.shadows=LightShadows.None;light.cullingMask=1;light.renderMode=LightRenderMode.ForcePixel;
            }
            var beaconAt=Node("Source rescue red beacon",craft);beaconAt.localPosition=G(new Vector3(0,1.5f,2.2f));beacon=beaconAt.gameObject.AddComponent<Light>();beacon.type=LightType.Point;beacon.color=new Color(1,.12f,.04f);beacon.range=5;beacon.shadows=LightShadows.None;beacon.cullingMask=1;beacon.renderMode=LightRenderMode.ForcePixel;
        }
        void Draw(Mesh mesh,Material material,Transform parent)
        {var node=Node(mesh.name,parent);node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;RendererCount++;TriangleCount+=checked((int)mesh.GetIndexCount(0)/3);}
        public void Step(float dt)
        {
            if(!Ready||disposed)return;state.Step(dt);var p=state.Position;craft.localPosition=new Vector3(p.X,p.Y,p.Z);craft.localRotation=Quaternion.AngleAxis(state.RollRadians*Mathf.Rad2Deg,Vector3.forward);leftRotor.localRotation=Quaternion.AngleAxis(state.LeftRotorRadians*Mathf.Rad2Deg,Vector3.up);rightRotor.localRotation=Quaternion.AngleAxis(state.RightRotorRadians*Mathf.Rad2Deg,Vector3.up);beacon.intensity=state.BeaconEnergy;
        }
        // Flat source BoxMesh faces, reflected exactly once. Textured source steel
        // is world-triplanar; the other eight materials have no texture UV input.
        static Mesh UnitBox()
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();var axes=new[]{Vector3.right,Vector3.up,Vector3.forward};
            for(int axis=0;axis<3;axis++)foreach(float side in new[]{-1f,1f})
            {
                var normal=axes[axis]*side;var u=axes[(axis+1)%3]*.5f;var v=axes[(axis+2)%3]*.5f;var points=new[]{normal*.5f-u-v,normal*.5f-u+v,normal*.5f+u+v,normal*.5f+u-v};int start=vertices.Count;
                for(int i=0;i<4;i++){vertices.Add(G(points[i]));normals.Add(G(normal));uv.Add(new Vector2(i>=2?1:0,i==1||i==2?1:0));}
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});else indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            var mesh=new Mesh{name="Source extraction unit BoxMesh"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }
        static Mesh Hull()
        {
            var points=new List<Vector3>();foreach(var ring in R24ValleyExtractionRecipe.HullRings)foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,1),new Vector2(1,-1)})points.Add(new Vector3(corner.x*ring.x,corner.y*ring.y,ring.z));
            var sourceIndices=new List<int>();for(int ring=0;ring<3;ring++)for(int side=0;side<4;side++){int a=ring*4+side,b=ring*4+(side+1)%4;sourceIndices.AddRange(new[]{a,a+4,b,b,a+4,b+4});}sourceIndices.AddRange(new[]{0,1,2,0,2,3,12,14,13,12,15,14});
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();
            for(int i=0;i<sourceIndices.Count;i+=3){var a=G(points[sourceIndices[i]]);var b=G(points[sourceIndices[i+1]]);var c=G(points[sourceIndices[i+2]]);var normal=Vector3.Cross(b-a,c-a).normalized;int start=vertices.Count;vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{normal,normal,normal});indices.AddRange(new[]{start,start+1,start+2});}
            var mesh=new Mesh{name="Source rescue hull28 triangles"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
        }
        public void Dispose()
        {if(disposed)return;disposed=true;Ready=false;if(craft!=null){craft.gameObject.SetActive(false);Destroy(craft.gameObject);craft=null;}foreach(var value in owned)if(value!=null)Destroy(value);owned.Clear();materials.Clear();beacon=null;leftRotor=rightRotor=null;}
        void OnDestroy(){Dispose();}
    }
}
#endif
