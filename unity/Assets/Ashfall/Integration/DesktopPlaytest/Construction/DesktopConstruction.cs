#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Integration.ConstructionGeometry;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Ashfall.Integration.DesktopPlaytest.Construction
{
    public sealed class DesktopConstruction : MonoBehaviour, IDesktopPlaytestModule, IDisposable
    {
        DesktopPlaytestContext context;
        LocalConstruction local;
        ArenaWorld world;
        GameObject owned, preview;
        Material builtMaterial, previewMaterial;
        readonly HashSet<string> mirrored=new HashSet<string>(StringComparer.Ordinal);
        bool previewEnabled;
        public int Balance {get{return local==null?0:local.Balance;}}
        public int PanelCount {get{return local==null?0:local.Snapshot().Length;}}
        public void Initialize(DesktopPlaytestContext value)
        {
            if(context!=null)throw new InvalidOperationException("already_initialized");
            context=value;
            if(context==null)throw new ArgumentNullException("value");
            context.ResetRequested+=ResetLocal;
            ResetLocal();
        }
        void ResetLocal()
        {
            ClearOwned();
            try
            {
                if(context.View==null || context.Controller==null || context.PlayerRoot==null)throw new InvalidOperationException("pawn_missing");
                owned=new GameObject("DesktopConstruction-Owned");
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)shader=Shader.Find("Standard");
                if(shader==null)throw new InvalidOperationException("shader_missing");
                builtMaterial=new Material(shader);builtMaterial.color=new Color(.22f,.55f,.7f,1);
                previewMaterial=new Material(shader);previewMaterial.color=new Color(.3f,.9f,.4f,1);
                world=new ArenaWorld(context,owned.transform);
                world.Refresh();local=new LocalConstruction(world);
                context.ConstructionStatus="Build: B preview, E place (aim for wall/floor); 999 local test charges";
            }
            catch(Exception e){ClearOwned();context.ConstructionStatus="Build disabled: "+e.Message;}
        }
        void Update()
        {
            if(local==null || context.Paused || !Application.isFocused){HidePreview();return;}
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.bKey.wasPressedThisFrame)previewEnabled=!previewEnabled;
            if(!previewEnabled){HidePreview();context.ConstructionStatus="Build: B preview, E place | charges "+Balance;return;}
            Evaluate(keyboard.eKey.wasPressedThisFrame);
        }
        void HidePreview(){if(preview!=null){preview.SetActive(false);Destroy(preview);preview=null;}}
        public bool Evaluate(bool place)
        {
            if(local==null || context.Paused)return false;
            HidePreview();
            try
            {
                Physics.SyncTransforms();world.Refresh();
                var view=context.View.transform;
                var pose=new BuilderState(F(view.position),F(view.forward),F(view.position),true,true);
                PlacedPanel[] panels;int cost;
                var result=local.TryPlace(pose,Math.Max(0,(long)Time.frameCount),place,out panels,out cost);
                if(!result.Accepted){context.ConstructionStatus="Build refused: "+result.Reason+" | charges "+Balance;return false;}
                if(place)
                {
                    foreach(var p in panels)if(mirrored.Add(p.Id))CreatePanel(p,owned.transform,builtMaterial,true);
                    context.ConstructionStatus="Placed | charges "+Balance+" | B preview, E place";
                }
                else
                {
                    preview=new GameObject("Preview");preview.transform.SetParent(owned.transform,false);
                    foreach(var p in panels)if(!mirrored.Contains(p.Id))CreatePanel(p,preview.transform,previewMaterial,false);
                    context.ConstructionStatus="B hide, E place | cost "+cost+" | charges "+Balance;
                }
                return true;
            }
            catch(Exception e){context.ConstructionStatus="Build disabled: "+e.Message;local=null;HidePreview();return false;}
        }
        static void CreatePanel(PlacedPanel panel,Transform parent,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=panel.Id;
            go.transform.SetParent(parent,false);go.transform.position=V(panel.Shape.Center);
            go.transform.localScale=V(CandidateGeometry.HalfSize(panel.Shape))*2;
            go.GetComponent<Renderer>().sharedMaterial=material;
            go.layer=8;
            if(!solid){go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());}
        }
        void ClearOwned()
        {
            local=null;world=null;previewEnabled=false;mirrored.Clear();HidePreview();
            if(owned!=null){owned.SetActive(false);Destroy(owned);owned=null;}
            if(builtMaterial!=null)Destroy(builtMaterial);
            if(previewMaterial!=null)Destroy(previewMaterial);
            builtMaterial=null;previewMaterial=null;
        }
        public void Dispose(){if(context!=null)context.ResetRequested-=ResetLocal;ClearOwned();context=null;}
        void OnDisable(){Dispose();}
        void OnDestroy(){Dispose();}
        internal static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
        internal static Vector3 V(Float3 v){return new Vector3(v.X,v.Y,v.Z);}

        // Synthetic sandbox geometry only. Actual live BoxCollider corners
        // become triangles for the existing source burial/support/LOS implementation.
        sealed class ArenaWorld : IConstructionWorld,IConstructionOccupants
        {
            readonly DesktopPlaytestContext context;readonly Transform owned;
            StructuralWorld geometry;
            public ArenaWorld(DesktopPlaytestContext context,Transform owned){this.context=context;this.owned=owned;}
            static string Path(Transform t)
            {return t.parent==null?t.name+":"+t.GetSiblingIndex():Path(t.parent)+"/"+t.name+":"+t.GetSiblingIndex();}
            public void Refresh()
            {
                var triangles=new List<SourceTriangle>();int mapBodies=0;
                foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider>())
                {
                    if(!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy || collider.transform.IsChildOf(context.PlayerRoot))continue;
                    if(context.Targets.Contains(collider))continue;
                    if(collider.gameObject.layer!=8)throw new InvalidOperationException("unclassified_collider:"+Path(collider.transform));
                    var box=collider as BoxCollider;
                    if(box==null || box.attachedRigidbody!=null)throw new InvalidOperationException("unsupported_box:"+Path(collider.transform));
                    var scale=box.transform.lossyScale;
                    if(scale.x<=0 || scale.y<=0 || scale.z<=0)throw new InvalidOperationException("unsupported_box_scale:"+Path(collider.transform));
                    bool constructed=collider.transform.IsChildOf(owned);
                    if(!constructed)mapBodies++;
                    var b=box.bounds;
                    // Local course floor only: broad horizontal slab ending at y=0.
                    var role=constructed?SurfaceRole.Construction:(Math.Abs(b.max.y)<.001f && b.size.x>10 && b.size.z>10?SurfaceRole.TerrainSurface:SurfaceRole.Structure);
                    AddBox(triangles,box,(constructed?"local-built/":"local-arena/")+Path(box.transform),role);
                    if(triangles.Count>4096)throw new InvalidOperationException("geometry_budget");
                }
                if(mapBodies==0)throw new InvalidOperationException("no_layer8_arena_boxes");
                geometry=new StructuralWorld(new CollisionAllocation(8,9,10,11),triangles,this,false,FloorBurialPolicy.LocalAllSamples025);
            }
            static void AddBox(List<SourceTriangle> output,BoxCollider box,string id,SurfaceRole role)
            {
                var a=box.center-box.size/2;var z=box.center+box.size/2;
                var v=new Vector3[]{new Vector3(a.x,a.y,a.z),new Vector3(z.x,a.y,a.z),new Vector3(z.x,z.y,a.z),new Vector3(a.x,z.y,a.z),new Vector3(a.x,a.y,z.z),new Vector3(z.x,a.y,z.z),new Vector3(z.x,z.y,z.z),new Vector3(a.x,z.y,z.z)};
                for(int i=0;i<v.Length;i++)v[i]=box.transform.TransformPoint(v[i]);
                int[] faces={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
                string hash;
                var identity=new StringBuilder(id);foreach(var point in v)identity.Append("|").Append(point.ToString("R"));
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()))).Replace("-","");
                for(int i=0;i<faces.Length;i+=3)output.Add(new SourceTriangle(id+"/tri"+(i/3),id,id,hash,role,8,F(v[faces[i]]),F(v[faces[i+1]]),F(v[faces[i+2]])));
            }
            public bool TrySnapshot(out ProtectedBox[] players,out Float3[] objectives)
            {
                players=new ProtectedBox[0];objectives=new Float3[0];
                if(context.Controller==null || !context.Controller.enabled)return false;
                var b=context.Controller.bounds;players=new[]{new ProtectedBox("local-builder",F(b.center),F(b.extents))};
                var targets=new List<Float3>();foreach(var t in context.Targets)if(t!=null && t.enabled && t.gameObject.activeInHierarchy)targets.Add(F(t.bounds.center));
                objectives=targets.ToArray();return true;
            }
            public bool HasStaticContact(Panel p,float tolerance){return geometry.HasStaticContact(p,tolerance);}
            public bool IsBuried(Panel p){return geometry.IsBuried(p);}
            public string Clearance(Panel p,string actor){return geometry.Clearance(p,actor);}
            public bool HasLineOfSight(string actor,Float3 from,Float3 to){return geometry.HasLineOfSight(actor,from,to);}
        }
    }
}
#endif
