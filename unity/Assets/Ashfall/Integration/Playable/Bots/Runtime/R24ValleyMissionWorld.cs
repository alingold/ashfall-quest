#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Integration.Playable.UI;

namespace Ashfall.Integration.Playable.Bots
{
    // Actual source main.gd station meshes and exposed screen interaction point.
    // Stations are visual-only in Godot: no invented blocking/pickup collider.
    public sealed class R24ValleyMissionWorld:MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly Transform[] stations=new Transform[4];
        R24ValleyExtraction extraction;
        public R24ValleyExtraction Extraction {get{return extraction;}}
        R23PlayableLoop root;GameObject content;TextMesh waypoint,cue;Material fontMaterial;int viewedChapter=-1;float toastTime;string toast="";
        public bool Visible {get{return content!=null&&content.activeInHierarchy;}}
        public string SaveFailure {get;private set;}
        public int SuccessfulActivations {get;private set;}
        public void Initialize(R23PlayableLoop owner){if(root!=null||owner==null)throw new ArgumentException("Mission Root");root=owner;}
        public void Step(float dt,bool eligible,bool simulate)
        {
            if(!eligible){Withdraw();return;}
            var state=root.ValleyBots.Mission;if(content==null)Build();if(!Visible)content.SetActive(true);state.Step(dt,simulate);
            if(state.Complete){if(extraction==null){var arrival=new GameObject("Source Relay Valley extraction arrival owner");arrival.transform.SetParent(root.Map.transform,false);extraction=arrival.AddComponent<R24ValleyExtraction>();extraction.Initialize(root.Map);}extraction.Step(dt);}else ClearExtraction();
            if(viewedChapter!=state.Chapter){viewedChapter=state.Chapter;for(int i=0;i<4;i++)stations[i].gameObject.SetActive(i>=state.Chapter);}
            if(simulate)toastTime=Mathf.Max(0,toastTime-dt);
            Vector3 target=root.Map.transform.TransformPoint(V(state.Objective)),eye=root.Rig.Head.transform.position;float distance=Vector3.Distance(root.Rig.transform.position,target);
            waypoint.gameObject.SetActive(!state.Complete&&distance>7);waypoint.transform.position=target+Vector3.up*4;waypoint.text=(state.Chapter+1).ToString("D2")+" / "+new[]{"RELAY","TURBINE","UPLINK","EXTRACT","COMPLETE"}[state.Chapter]+"\n"+(int)distance+" m";FaceCamera(waypoint.transform,eye);
            // Source VR cue only shows brief terminal outcomes, not a permanent
            // verbose quest overlay. The normal tracked HUD stays independent.
            cue.gameObject.SetActive(toastTime>0&&!root.Rig.Paused);cue.text=toast;cue.transform.SetPositionAndRotation(root.Rig.Head.transform.TransformPoint(new Vector3(0,-.395f,.8f)),root.Rig.Head.transform.rotation);
        }
        public bool TryTarget(Vector3 actualHand,out string id,out Vector3 point,out float distance)
        {
            id=null;point=Vector3.zero;distance=0;if(!Visible||!root.Ready||root.UI.Page!=R23UiPage.Playing||root.Rig.Paused||root.Gameplay.Health<=0||root.Lobby.Visible||!root.ValleyBots.Ready||root.ValleyBots.LivingCount!=0||root.ValleyBots.Mission.Complete)return false;
            point=root.Map.transform.TransformPoint(V(root.ValleyBots.Mission.Screen));distance=Vector3.Distance(actualHand,point);if(distance>.35f)return false;
            RaycastHit hit;Vector3 delta=point-actualHand;if(delta.sqrMagnitude>.000001f&&Physics.Raycast(actualHand,delta.normalized,out hit,delta.magnitude,1<<0,QueryTriggerInteraction.Ignore)&&Vector3.Distance(hit.point,point)>.012f)return false;
            id=root.ValleyBots.Mission.MissionId;return true;
        }
        public bool Activate(string id,Vector3 actualHand)
        {
            string selected;Vector3 point;float distance;if(!TryTarget(actualHand,out selected,out point,out distance)||selected!=id||!root.ValleyBots.AdvanceMission())return false;
            SuccessfulActivations++;SaveCheckpoint();toast=root.ValleyBots.Mission.Complete?"CAMPAIGN COMPLETE":SaveFailure==null?"RELAY SYNCHRONIZED / CHECKPOINT SAVED":"RELAY SYNCHRONIZED / CHECKPOINT WRITE FAILED";toastTime=root.ValleyBots.Mission.Complete?5:3;return true;
        }
        public void SaveCheckpoint()
        {
            try{var state=root.ValleyBots.Mission;var data=new Checkpoint{version=1,campaign="relay-valley-normal",chapter=state.Chapter,kills=state.Kills,playTime=state.PlaySeconds};string json=JsonUtility.ToJson(data);PlayerPrefs.SetString("ashfall.unity.relay-valley.checkpoint.v1",json);PlayerPrefs.Save();SaveFailure=null;}
            catch(Exception e){SaveFailure=e.Message;Debug.LogWarning("Valley checkpoint unavailable: "+e.Message,this);}
        }
        [Serializable] sealed class Checkpoint{public int version;public string campaign;public int chapter,kills;public double playTime;}
        public bool HasCheckpoint
        {get{Checkpoint data;return TryReadCheckpoint(out data);}}
        bool TryReadCheckpoint(out Checkpoint data)
        {
            data=null;const string key="ashfall.unity.relay-valley.checkpoint.v1";if(!PlayerPrefs.HasKey(key))return false;
            try{data=JsonUtility.FromJson<Checkpoint>(PlayerPrefs.GetString(key));if(data==null||data.version!=1||data.campaign!="relay-valley-normal")return false;new R24ValleyMissionState().Restore(data.chapter,data.kills,data.playTime);return true;}catch(Exception){data=null;return false;}
        }
        public bool LoadCheckpoint()
        {
            Checkpoint data;if(!TryReadCheckpoint(out data))return false;root.ValleyBots.Mission.Restore(data.chapter,data.kills,data.playTime);return true;
        }
        void Build()
        {
            content=new GameObject("Original Relay Valley mission stations");content.transform.SetParent(root.Map.transform,false);
            var lit=Shader.Find("Universal Render Pipeline/Lit");if(lit==null)throw new InvalidOperationException("Source station Lit shader missing");
            var body=new Material(lit);materials.Add(body);body.SetColor("_BaseColor",R24KestrelAssets.ShaderColor(new Color(41/255f,57/255f,68/255f)));body.SetFloat("_Metallic",.7f);body.SetFloat("_Smoothness",.65f);
            var screen=new Material(lit);materials.Add(screen);var cyan=new Color(54/255f,214/255f,222/255f);screen.SetColor("_BaseColor",R24KestrelAssets.ShaderColor(cyan));screen.EnableKeyword("_EMISSION");screen.SetColor("_EmissionColor",R24KestrelAssets.ShaderColor(cyan*2));
            for(int i=0;i<4;i++){var station=new GameObject("Source station "+i).transform;station.SetParent(content.transform,false);station.localPosition=V(R24ValleyMissionState.ObjectiveAt(i));stations[i]=station;Box(station,"Source station base",Vector3.zero,new Vector3(.8f,1.2f,.65f),body);Box(station,"Source cyan screen",new Vector3(0,.65f,-.34f),new Vector3(.65f,.4f,.03f),screen);}
            var shader=Resources.Load<Shader>("R23VrUi");var font=Resources.Load<Font>("R23InventoryUI");if(shader==null||font==null)throw new InvalidOperationException("Source mission label resources");fontMaterial=new Material(shader);materials.Add(fontMaterial);fontMaterial.SetFloat("_Font",1);fontMaterial.mainTexture=font.material.mainTexture;fontMaterial.renderQueue=4102;
            waypoint=Text("Original mission waypoint",content.transform,font,new Color(121/255f,229/255f,228/255f),.12f);R26WorldText.Bind(waypoint);cue=Text("Original brief mission outcome",content.transform,font,new Color(200/255f,228/255f,231/255f),.007f,20);cue.gameObject.layer=5;Font.textureRebuilt+=RefreshFont;
        }
        void RefreshFont(Font font){if(waypoint!=null&&waypoint.font==font&&fontMaterial!=null)fontMaterial.mainTexture=font.material.mainTexture;}
        void Box(Transform parent,string name,Vector3 at,Vector3 size,Material material)
        {var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);box.transform.localPosition=at;box.transform.localScale=size;var collider=box.GetComponent<Collider>();collider.enabled=false;Destroy(collider);box.GetComponent<Renderer>().sharedMaterial=material;}
        TextMesh Text(string name,Transform parent,Font font,Color color,float characterSize,int sourceFontSize=36)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);var text=go.AddComponent<TextMesh>();text.font=font;text.fontSize=sourceFontSize;text.characterSize=characterSize;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;go.GetComponent<MeshRenderer>().sharedMaterial=fontMaterial;return text;}
        static Vector3 V(Ashfall.Core.Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static void FaceCamera(Transform label,Vector3 eye){Vector3 forward=label.position-eye;forward.y=0;if(forward.sqrMagnitude>.00001f)label.rotation=Quaternion.LookRotation(forward,Vector3.up);}
        void ClearExtraction(){if(extraction!=null){extraction.Dispose();Destroy(extraction.gameObject);extraction=null;}}
        public void Withdraw()
        {ClearExtraction();if(content==null)return;Font.textureRebuilt-=RefreshFont;content.SetActive(false);Destroy(content);content=null;viewedChapter=-1;foreach(var material in materials)if(material!=null)Destroy(material);materials.Clear();foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);meshes.Clear();waypoint=cue=null;}
        void OnDestroy(){Withdraw();}
    }
}
#endif
