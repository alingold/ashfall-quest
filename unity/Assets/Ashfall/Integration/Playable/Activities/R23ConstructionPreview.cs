#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.Playable.Activities
{
    // Collider-free render resources; the existing CandidateService owns all proposals.
    public sealed class R23ConstructionPreview:IDisposable
    {
        readonly GameObject root,boltObject;readonly Mesh box,bolt;readonly Material ghost,electric;
        readonly List<GameObject> panels=new List<GameObject>();
        readonly R23BuildLightningRecipe recipe=new R23BuildLightningRecipe();
        readonly List<Vector3> vertices=new List<Vector3>(4096);readonly List<Color32> colors=new List<Color32>(4096);readonly List<int> indices=new List<int>(4096);
        int active;Vector3 generatedStart,generatedDirection;
        public int VisiblePanelCount {get{return active;}}
        public Transform Root {get{return root.transform;}}
        public bool LightningVisible {get{return boltObject.activeSelf;}}
        public Material GhostMaterial {get{return ghost;}}
        public int LightningVertexCount {get{return bolt.vertexCount;}}
        public R23ConstructionPreview(Transform owner)
        {
            var shader=Resources.Load<Shader>("R23BuildPreview");if(shader==null)shader=Shader.Find("Ashfall/R23 Source Construction Preview");
            if(shader==null)throw new InvalidOperationException("R23 source preview shader missing");
            root=new GameObject("R23 source construction preview");root.layer=2;root.transform.SetParent(owner,false);
            ghost=new Material(shader);ghost.name="R23 unshaded alpha candidate";ghost.SetColor("_Color",new Color(.8f,.8f,.8f,.35f));ghost.SetInt("_SrcBlend",5);ghost.SetInt("_DstBlend",10);ghost.SetInt("_ZWrite",0);ghost.renderQueue=3000;
            electric=new Material(shader);electric.name="R23 depth-tested electric ribbons";electric.SetColor("_Color",Color.white);electric.SetInt("_SrcBlend",1);electric.SetInt("_DstBlend",0);electric.SetInt("_ZWrite",1);electric.renderQueue=2000;
            box=Box();bolt=new Mesh();bolt.name="R23 ten irregular crossed lightning ribbons";bolt.MarkDynamic();
            boltObject=Visual("BuildLightning088",bolt,electric);boltObject.SetActive(false);
        }
        GameObject Visual(string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.layer=2;go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;return go;
        }
        public void Begin(){active=0;}
        public void Label(string text,Transform head)
        {
            // The owner removed the build tier/cost caption; keep placement visuals and charge HUD.
        }
        public void Panel(Panel shape){foreach(var box in CandidateGeometry.Boxes(shape))BoxPanel(box);}
        void BoxPanel(Panel shape)
        {
            if(active==panels.Count)panels.Add(Visual("R23 snapped candidate face "+active,box,ghost));
            var go=panels[active++];go.transform.position=U(shape.Center);go.transform.rotation=Quaternion.identity;
            go.transform.localScale=U(CandidateGeometry.HalfSize(shape))*2;go.SetActive(true);
        }
        public void Finish(Vector3 start,Vector3 direction,float candidateDistance,double milliseconds)
        {
            for(int i=active;i<panels.Count;i++)panels[i].SetActive(false);
            // Regenerate only on the simulation tick. Late tracking transports this
            // render mesh without querying placement or touching the accepted plan.
            boltObject.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            generatedStart=start;generatedDirection=direction.normalized;
            Vector3 end=start+direction.normalized*candidateDistance;
            bool show=active>0&&recipe.Build(F(start),F(end),true,milliseconds);boltObject.SetActive(show);
            if(!show){bolt.Clear();return;}
            vertices.Clear();colors.Clear();indices.Clear();
            for(int i=0;i<recipe.Vertices.Count;i++)
            {
                vertices.Add(root.transform.InverseTransformPoint(U(recipe.Vertices[i])));uint rgb=recipe.Colors[i];colors.Add(new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255));indices.Add(i);
            }
            bolt.Clear();bolt.SetVertices(vertices);bolt.SetColors(colors);bolt.SetTriangles(indices,0,true);
        }
        public void RefreshTrackedPose(Vector3 start,Vector3 direction)
        {
            if(!boltObject.activeSelf||generatedDirection.sqrMagnitude<.000001f||direction.sqrMagnitude<.000001f)return;
            var delta=Quaternion.FromToRotation(generatedDirection,direction.normalized);
            // Mesh coordinates were generated in the stationary preview-root frame.
            // Apply a fresh absolute delta, never accumulate frame-to-frame offsets.
            boltObject.transform.SetPositionAndRotation(start+delta*(root.transform.position-generatedStart),delta*root.transform.rotation);
        }
        public void Hide(){active=0;foreach(var go in panels)if(go!=null)go.SetActive(false);if(boltObject!=null)boltObject.SetActive(false);if(bolt!=null)bolt.Clear();recipe.Clear();}
        static Mesh Box()
        {
            var mesh=new Mesh();mesh.name="R23 source preview unit box";
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            mesh.colors32=new[]{new Color32(255,255,255,255),new Color32(255,255,255,255),new Color32(255,255,255,255),new Color32(255,255,255,255),new Color32(255,255,255,255),new Color32(255,255,255,255),new Color32(255,255,255,255),new Color32(255,255,255,255)};
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};mesh.RecalculateBounds();return mesh;
        }
        public void Dispose(){Hide();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(box);UnityEngine.Object.Destroy(bolt);UnityEngine.Object.Destroy(ghost);UnityEngine.Object.Destroy(electric);}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}static Vector3 U(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
    }
}
#endif
