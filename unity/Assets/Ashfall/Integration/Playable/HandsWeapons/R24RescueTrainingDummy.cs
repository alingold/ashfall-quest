#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed class R24RescueTrainingDummy:MonoBehaviour
    {
        readonly R24RescueTrainingDummyState state=new R24RescueTrainingDummyState();Transform content;TextMesh label;Mesh bodyMesh,padMesh;Material bodyMaterial,downMaterial,upMaterial,fontMaterial;MeshRenderer[] pads;bool worldVisible=true;
        public bool Downed{get{return state.Downed;}}public float Health{get{return state.Health;}}public int ReviveCount{get{return state.ReviveCount;}}public float ResetRemaining{get{return state.ResetRemaining;}}public string Readout{get{return state.Readout;}}
        public Transform WorldRoot{get{return content;}}public Vector3 WorldPosition{get{return content==null?Vector3.zero:content.position;}}public bool WorldVisible{get{return content!=null&&content.gameObject.activeInHierarchy;}}public int ColliderCount{get{return content==null?0:content.GetComponentsInChildren<Collider>(true).Length;}}
        public event Action Revived;
        public void Initialize(Transform acceptedHead=null)
        {
            if(content!=null)throw new InvalidOperationException("rescue dummy already initialized");var lit=Shader.Find("Universal Render Pipeline/Lit");var padShader=Shader.Find("Universal Render Pipeline/Unlit");var unlit=Resources.Load<Shader>("R23GunFeedback");var font=Resources.Load<Font>("R23GodotUI");if(lit==null||padShader==null||unlit==null||font==null)throw new InvalidOperationException("source dummy presentation resources missing");
            content=new GameObject("R6 solo rescue training casualty").transform;content.SetParent(transform,false);bodyMesh=Mesh(R24RescueTrainingDummyGeometry.Body(),"source rescue BoxMesh");padMesh=Mesh(R24RescueTrainingDummyGeometry.Pad(),"source rescue CylinderMesh");
            bodyMaterial=new Material(lit);bodyMaterial.SetColor("_BaseColor",SourceColor(.12f,.24f,.28f));bodyMaterial.SetFloat("_Smoothness",0);bodyMaterial.SetFloat("_Metallic",0);Make("source body",bodyMesh,bodyMaterial,new Vector3(0,.51f,0));
            downMaterial=new Material(padShader);downMaterial.SetColor("_BaseColor",SourceColor(1,.38f,.15f));upMaterial=new Material(padShader);upMaterial.SetColor("_BaseColor",SourceColor(.25f,1,.45f));pads=new[]{Make("source left contact",padMesh,downMaterial,new Vector3(-.13f,.61f,-.13f)),Make("source right contact",padMesh,downMaterial,new Vector3(.13f,.61f,-.13f))};
            var text=new GameObject("source rescue readout");text.transform.SetParent(content,false);text.transform.localPosition=new Vector3(0,1.15f,0);label=text.AddComponent<TextMesh>();label.font=font;label.fontSize=30;label.characterSize=.002f*10;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;fontMaterial=new Material(unlit);fontMaterial.SetTexture("_MainTex",font.material.mainTexture);fontMaterial.SetFloat("_Font",1);fontMaterial.SetFloat("_Cull",0);label.GetComponent<MeshRenderer>().sharedMaterial=fontMaterial;text.AddComponent<R24SourceLabelBillboard>().Fallback=acceptedHead==null?null:acceptedHead.GetComponent<Camera>();Refresh();content.gameObject.SetActive(worldVisible);
        }
        static Color SourceColor(float r,float g,float b){var c=new Color(r,g,b,1);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
        static Vector3 V(Float3 a){return new Vector3(a.X,a.Y,a.Z);}static Float3 F(Vector3 a){return new Float3(a.x,a.y,a.z);}
        static Mesh Mesh(R24RescueTrainingDummyGeometry data,string name){var mesh=new Mesh();mesh.name=name;var v=new Vector3[data.Positions.Length];var n=new Vector3[v.Length];for(int i=0;i<v.Length;i++){v[i]=V(data.Positions[i]);n[i]=V(data.Normals[i]);}mesh.vertices=v;mesh.normals=n;mesh.triangles=data.Indices;mesh.RecalculateBounds();return mesh;}
        MeshRenderer Make(string name,Mesh mesh,Material material,Vector3 at){var go=new GameObject(name);go.transform.SetParent(content,false);go.transform.localPosition=at;go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;}
        public void SetCheckpoint(Vector3 actualWorldCheckpoint,bool actualSandbox)
        {if(!F(actualWorldCheckpoint).IsFinite)throw new ArgumentException("finite actual checkpoint required");if(content==null)throw new InvalidOperationException("Initialize first");content.SetPositionAndRotation(actualWorldCheckpoint+V(R24RescueTrainingDummyState.CheckpointOffset(actualSandbox)),Quaternion.identity);state.ResetTraining();Refresh();}
        public void SetWorldVisible(bool visible){worldVisible=visible;if(content!=null)content.gameObject.SetActive(visible);}
        public void Tick(float seconds,bool paused=false)
        {bool before=state.Downed;state.Tick(seconds,paused);if(before!=state.Downed)Refresh();if(label!=null)fontMaterial.SetTexture("_MainTex",label.font.material.mainTexture);}
        public bool TryReadTarget(out R24ReviveTarget target)
        {target=null;if(content==null)return false;var frame=new R23ShotPose(F(content.position),F(content.right),F(content.up),F(content.forward));return state.TryReadTarget(frame,WorldVisible,out target);}
        public bool TryRevive(string id)
        {if(!state.TryRevive(id,WorldVisible))return false;Refresh();if(Revived!=null)Revived();return true;}
        void Refresh(){if(label!=null)label.text=state.Readout;if(pads!=null)foreach(var pad in pads)pad.sharedMaterial=state.Downed?downMaterial:upMaterial;}
        void OnDestroy(){if(content!=null)Destroy(content.gameObject);foreach(var resource in new UnityEngine.Object[]{bodyMesh,padMesh,bodyMaterial,downMaterial,upMaterial,fontMaterial})if(resource!=null)Destroy(resource);}
    }
}
#endif
