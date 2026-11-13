#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using UnityEngine;
using UnityEngine.Networking;
using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Consumables
{
    public struct R24SupplyInputFrame {public bool Enabled,LeftEligible,RightEligible;}
    // Source ammo_pickups: explicit grip collects directly into actual reserve/build owner.
    // No slots, stored ammo shadow counter, held item or pose/movement writer.
    public sealed partial class R24VRSupplies:MonoBehaviour
    {
        sealed class Box{internal string Id,Family;internal Vector3 Position,Home;internal Quaternion Rotation;internal GameObject View;internal bool Visible=true;}
        [Serializable]sealed class GlyphManifest{public string glyph=null;public float[] glyphPosition=null,glyphSize=null;}
        Transform actor,head,left,right,content;R24SupplyState state;R24SupplyInputFrame frame;AudioSource audio;AudioClip ammoSound,buildSound;float sfx=1,audioGain=.19952623f;long dropSerial,sourceLayoutGeneration;
        readonly Dictionary<string,Box> boxes=new Dictionary<string,Box>(StringComparer.Ordinal);readonly Dictionary<string,R23VRConsumables.Asset> assets=new Dictionary<string,R23VRConsumables.Asset>();readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();bool visible=true;
        readonly Dictionary<string,R24PickupSettleResult> sourceSettleResults=new Dictionary<string,R24PickupSettleResult>(StringComparer.Ordinal);
        public string[] WorldItemIds{get{return new List<string>(boxes.Keys).ToArray();}}
        public string[] SourceCheckpointIds {get{var ids=new List<string>();foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())if(row.IsSupply)ids.Add(row.Id);foreach(var row in R24WorldPickupLayout.Ammo())ids.Add(row.Id);return ids.ToArray();}}
        public bool TryReadSourceSettleResult(string id,out R24PickupSettleResult result){result=null;return id!=null&&sourceSettleResults.TryGetValue(id,out result);}
        public bool Ready {get;private set;}public string Failure {get;private set;}public string CurrentEventId {get;private set;}
        public Func<string,int,int> WithdrawSupply;public Func<Vector3,Vector3,bool> LineClear;
        public event Action<string,string,int> Collected;
        public void Initialize(Transform root,Transform acceptedHead,Transform acceptedLeft,Transform acceptedRight,Func<string,string,int,int> tryGrantSupply)
        {
            if(state!=null)throw new InvalidOperationException("Supply owner initialized");if(root==null||acceptedHead==null||acceptedLeft==null||acceptedRight==null||tryGrantSupply==null)throw new ArgumentException("accepted poses and real supply owner required");
            actor=root;head=acceptedHead;left=acceptedLeft;right=acceptedRight;state=new R24SupplyState(delegate(string id,string family,int amount){CurrentEventId=id;return tryGrantSupply(id,family,amount);});content=new GameObject("R24 original finite world supplies").transform;
            var audioNode=new GameObject("Independent world supply pickup audio");audioNode.transform.SetParent(content,false);audio=audioNode.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1;audio.minDistance=1;audio.maxDistance=8;audio.dopplerLevel=0;audio.volume=.19952623f*sfx;StartCoroutine(Load());
        }
        static string Url(string file){string p=Application.streamingAssetsPath+"/PlayableR23/Consumables/Supplies/"+file;return p.Contains("://")?p:new Uri(p).AbsoluteUri;}
        IEnumerator Read(string file,Dictionary<string,byte[]> bytes)
        {using(var request=UnityWebRequest.Get(Url(file))){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=file+": "+request.error;yield break;}bytes.Add(file,request.downloadHandler.data);}}
        IEnumerator Load()
        {
            foreach(string family in R24SupplyState.Families)
            {
                var data=new Dictionary<string,byte[]>();yield return Read(family+".json",data);yield return Read(family+".meshbin",data);if(Failure!=null)yield break;
                var asset=new R23VRConsumables.Asset();try{asset.Build(family,data[family+".json"],data[family+".meshbin"],content);assets.Add(family,asset);}catch(Exception e){asset.Dispose();Failure=e.ToString();yield break;}
                var m=JsonUtility.FromJson<GlyphManifest>(System.Text.Encoding.UTF8.GetString(data[family+".json"]));if(String.IsNullOrEmpty(m.glyph))continue;
                yield return Read(m.glyph,data);if(Failure!=null)yield break;var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false);resources.Add(texture);if(!texture.LoadImage(data[m.glyph],true)){Failure=m.glyph+" invalid image";yield break;}
                var material=new Material(Resources.Load<Shader>("R24ItemSurface"));material.SetTexture("_MainTex",texture);material.SetColor("_Color",Color.white);resources.Add(material);
                var mesh=new Mesh();resources.Add(mesh);float x=m.glyphSize[0]*.5f,y=m.glyphSize[1]*.5f;mesh.vertices=new[]{new Vector3(-x,-y,0),new Vector3(x,-y,0),new Vector3(x,y,0),new Vector3(-x,y,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
                var glyph=new GameObject("Source supply Sprite3D glyph");glyph.transform.SetParent(asset.Template.transform,false);glyph.transform.localPosition=new Vector3(m.glyphPosition[0],m.glyphPosition[1],m.glyphPosition[2]);glyph.AddComponent<R24SettleIgnoreMesh>();glyph.AddComponent<MeshFilter>().sharedMesh=mesh;glyph.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            foreach(string action in new[]{"ammo_pickup","build_pickup"})using(var request=UnityWebRequestMultimedia.GetAudioClip(Url(action+".wav"),AudioType.WAV))
            {yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=action+": "+request.error;yield break;}var clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Decoded(request);resources.Add(clip);if(action=="ammo_pickup")ammoSound=clip;else buildSound=clip;}
            Ready=true;foreach(var box in boxes.Values)CreateView(box);
        }
        void CreateView(Box box){if(box.View!=null||!Ready)return;box.View=Instantiate(assets[box.Family].Template);box.View.name=box.Id;box.View.transform.SetParent(content,false);box.View.transform.SetPositionAndRotation(box.Position,box.Rotation);Refresh(box);}
        public bool Spawn(string id,string family,Vector3 position,int amount=-1){return Spawn(id,family,position,Quaternion.identity,amount);}
        public bool Spawn(string id,string family,Vector3 position,Quaternion rotation,int amount=-1)
        {if(state==null||!Finite(position)||!Valid(rotation)||!state.Register(id,family,amount))return false;var box=new Box{Id=id,Family=family,Position=position,Home=position,Rotation=rotation.normalized};boxes.Add(id,box);CreateView(box);return true;}
        public R24PickupLayoutResult EnsureSourceCheckpointLayout(Vector3 checkpointUnity,Func<bool> mapCollisionReady)
        {
            if(!Finite(checkpointUnity)||mapCollisionReady==null)throw new ArgumentException("Actual checkpoint and map collision readiness required");
            LeaveArchivePickupLayout();int created=0,moved=0,preserved=0;long generation=++sourceLayoutGeneration;sourceSettleResults.Clear();var rows=new List<R24CheckpointPickup>();foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())if(row.IsSupply)rows.Add(row);rows.AddRange(R24WorldPickupLayout.Ammo());
            foreach(var row in rows)
            {
                var p=row.At(new Ashfall.Core.Float3(checkpointUnity.x,checkpointUnity.y,checkpointUnity.z));Vector3 at=new Vector3(p.X,p.Y,p.Z);Box box;
                if(!boxes.TryGetValue(row.Id,out box)){if(!Spawn(row.Id,row.Kind,at)){preserved++;sourceSettleResults[row.Id]=new R24PickupSettleResult(row.Id,R24PickupSettleOutcome.Unavailable,p,p);continue;}created++;box=boxes[row.Id];}
                else{if(box.Family!=row.Kind)throw new InvalidOperationException("Source supply ID/family binding changed");if(!RepositionWorldItem(row.Id,at)){preserved++;sourceSettleResults[row.Id]=new R24PickupSettleResult(row.Id,R24PickupSettleOutcome.Preserved,p,new Ashfall.Core.Float3(box.Position.x,box.Position.y,box.Position.z));continue;}moved++;}
                string id=row.Id;Box captured=box;sourceSettleResults[id]=new R24PickupSettleResult(id,R24PickupSettleOutcome.Pending,p,new Ashfall.Core.Float3(box.Position.x,box.Position.y,box.Position.z));
                // Creation may still be streaming the genuine family asset. Wait for
                // that actual view before entering the source physics-boundary settle.
                StartCoroutine(SettleSourceBox(id,captured,generation,mapCollisionReady,p));
            }
            return new R24PickupLayoutResult(created,moved,preserved);
        }
        bool AvailableSourceBox(string id,Box expected)
        {Box current;R24SupplyItem item;return state!=null&&boxes.TryGetValue(id,out current)&&System.Object.ReferenceEquals(current,expected)&&state.TryRead(id,out item)&&!item.Collected;}
        IEnumerator SettleSourceBox(string id,Box expected,long generation,Func<bool> mapCollisionReady,Ashfall.Core.Float3 requested)
        {
            while(expected.View==null&&generation==sourceLayoutGeneration&&AvailableSourceBox(id,expected)&&Failure==null)yield return null;
            if(expected.View==null||generation!=sourceLayoutGeneration||!AvailableSourceBox(id,expected)){if(generation==sourceLayoutGeneration)sourceSettleResults[id]=new R24PickupSettleResult(id,R24PickupSettleOutcome.Cancelled,requested,new Ashfall.Core.Float3(expected.Position.x,expected.Position.y,expected.Position.z));yield break;}
            bool accepted=false;
            yield return R24WorldPickupSettler.Defer(expected.View.transform,actor,mapCollisionReady,delegate{return generation==sourceLayoutGeneration&&AvailableSourceBox(id,expected);},delegate(Vector3 at){if(generation==sourceLayoutGeneration&&AvailableSourceBox(id,expected))accepted=RepositionWorldItem(id,at);},false,delegate(R24PickupSettleOutcome outcome){if(generation==sourceLayoutGeneration)sourceSettleResults[id]=new R24PickupSettleResult(id,outcome==R24PickupSettleOutcome.Settled&&!accepted?R24PickupSettleOutcome.Refused:outcome,requested,new Ashfall.Core.Float3(expected.Position.x,expected.Position.y,expected.Position.z));});
        }
        public bool TryReadWorldPose(string id,out Vector3 position,out Quaternion rotation,out bool collected)
        {position=Vector3.zero;rotation=Quaternion.identity;collected=false;Box box;R24SupplyItem item;if(id==null||!boxes.TryGetValue(id,out box)||!state.TryRead(id,out item)||box.View==null)return false;position=box.View.transform.position;rotation=box.View.transform.rotation;collected=item.Collected;return true;}
        public bool TryReadItem(string id,out R24SupplyItem item){item=null;return state!=null&&state.TryRead(id,out item);}
        public void UpdateInputAvailability(R24SupplyInputFrame input){frame=input;}
        bool Eligible(int hand){return Ready&&frame.Enabled&&isActiveAndEnabled&&(hand==1?frame.LeftEligible:hand==2&&frame.RightEligible);}
        Transform Hand(int hand){return hand==1?left:right;}
        bool Clear(Vector3 a,Vector3 b)
        {if(LineClear!=null)return LineClear(a,b);Vector3 d=b-a;if(d.sqrMagnitude<.000001f)return true;foreach(var h in Physics.RaycastAll(a,d.normalized,d.magnitude,(1<<0)|(1<<8)|(1<<19),QueryTriggerInteraction.Ignore))if(!h.collider.transform.IsChildOf(actor)&&!h.collider.transform.IsChildOf(content))return false;return true;}
        bool Reach(Box box,int hand)
        {if(!Eligible(hand)||!visible||!box.Visible)return false;var h=Hand(hand);Vector3 d=box.Position-h.position;float distance=d.magnitude;return distance<=2&&(distance<=.35f||Vector3.Dot(R23TrackedGripPose.Forward(h),d/distance)>.94f)&&Clear(head.position,h.position)&&Clear(h.position,box.Position);}
        public bool TryReadObjectiveGrip(int hand,out string id,out Vector3 position,out float distance)
        {
            id=null;position=Vector3.zero;distance=float.PositiveInfinity;if(!Eligible(hand))return false;
            foreach(var box in boxes.Values){R24SupplyItem s;state.TryRead(box.Id,out s);if(s.Collected||!Reach(box,hand))continue;float d=Vector3.Distance(Hand(hand).position,box.Position);if(d>=distance)continue;id=box.Id;position=box.Position;distance=d;}return id!=null;
        }
        public int TryCollect(string id,int hand)
        {Box box;if(id==null||!boxes.TryGetValue(id,out box)||!Reach(box,hand))return 0;int accepted=state.Collect(id);if(accepted<=0)return 0;Refresh(box);audio.transform.position=box.Position;audio.spatialBlend=box.Family=="backpack"?0:1;audioGain=box.Family=="backpack"?.39810717f:.19952623f;audio.volume=audioGain*sfx;audio.clip=Ashfall.Integration.Playable.UI.R25LegacyPack.Resolve(box.Family=="builds"?buildSound:ammoSound);audio.Play();if(Collected!=null)Collected(id,box.Family,accepted);return accepted;}
        public bool TryDropReserve(string family,Vector3 position,Quaternion rotation,out string id)
        {
            id=null;int amount=R24SupplyState.DefaultAmount(family);if(!Ready||!frame.Enabled||WithdrawSupply==null||amount==0||family=="backpack"||!Finite(position)||!Valid(rotation)||dropSerial==long.MaxValue)return false;
            string key;R24SupplyItem existing;do{if(dropSerial==long.MaxValue)return false;key="source-supply-drop/"+(++dropSerial);}while(state.TryRead(key,out existing));int taken=WithdrawSupply(family,amount);if(taken<=0)return false;
            if(!Spawn(key,family,position,rotation,taken))throw new InvalidOperationException("Accepted reserve withdrawal must publish its finite source box");id=key;return true;
        }
        public Bounds GetDropBounds(string family){R23VRConsumables.Asset a;return assets.TryGetValue(family,out a)?a.Bounds:new Bounds();}
        public event Action<string,Vector3> WorldItemMoved;
        public bool RepositionWorldItem(string id,Vector3 position){Box box;R24SupplyItem s;if(!Finite(position)||id==null||!boxes.TryGetValue(id,out box)||!state.TryRead(id,out s)||s.Collected)return false;box.Position=box.Home=position;if(box.View!=null)box.View.transform.position=position;if(WorldItemMoved!=null)WorldItemMoved(id,position);return true;}
        public bool SetWorldItemVisible(string id,bool value){Box box;if(id==null||!boxes.TryGetValue(id,out box))return false;box.Visible=value;Refresh(box);return true;}
        public void SetWorldVisible(bool value){visible=value;foreach(var box in boxes.Values)Refresh(box);}
        public void ResetRound(){if(state==null)return;sourceLayoutGeneration++;foreach(string id in new List<string>(sourceSettleResults.Keys)){var pending=sourceSettleResults[id];if(pending.Outcome==R24PickupSettleOutcome.Pending)sourceSettleResults[id]=new R24PickupSettleResult(id,R24PickupSettleOutcome.Cancelled,pending.RequestedPosition,pending.Position);}state.ResetRound();foreach(var box in boxes.Values){box.Position=box.Home;if(box.View!=null)box.View.transform.position=box.Position;Refresh(box);}if(audio!=null)audio.Stop();}
        public void Suspend(){frame=new R24SupplyInputFrame();if(audio!=null)audio.Stop();}
        public void SetSfxVolume(float value){sfx=float.IsNaN(value)?0:Mathf.Clamp01(value);if(audio!=null)audio.volume=audioGain*sfx;}
        void Refresh(Box box){R24SupplyItem s;if(box.View!=null&&state.TryRead(box.Id,out s))box.View.SetActive(visible&&box.Visible&&!s.Collected);}
        static bool Finite(Vector3 p){return new Ashfall.Core.Float3(p.x,p.y,p.z).IsFinite;}static bool Valid(Quaternion q){return new Ashfall.Core.Float3(q.x,q.y,q.z).IsFinite&&!float.IsNaN(q.w)&&!float.IsInfinity(q.w)&&Quaternion.Dot(q,q)>.5f;}
        void OnDisable(){Suspend();}void OnApplicationPause(bool paused){if(paused)Suspend();}void OnApplicationFocus(bool focused){if(!focused)Suspend();}
        void OnDestroy(){StopAllCoroutines();if(content!=null)Destroy(content.gameObject);foreach(var asset in assets.Values)asset.Dispose();foreach(var o in resources)if(o!=null)Destroy(o);}
    }
}
#endif
