#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Consumables
{
    public struct R23ConsumableSample
    {public float DeltaSeconds,LeftGrip,RightGrip;public bool Enabled,LeftReady,RightReady,LeftFree,RightFree;}
    // The root samples input, arbitrates hand custody and applies real effects through its port.
    // Props have no collision, matching source field_consumables. No player/root pose writes.
    public sealed partial class R23VRConsumables:MonoBehaviour,IInteractionQuery
    {
        sealed class WorldItem {internal string Id,Kind;internal bool WorldVisible=true,Culled,RestPosed;internal GameObject View;internal Vector3 InitialPosition;internal Quaternion InitialRotation=Quaternion.identity;internal Mesh RingMesh,PeelMesh;internal Transform Ring,Peels;internal float RingFill=-1;internal float[] PeelAmounts;}
        Transform root,head,left,right,content;IR23ConsumableEffects effects;R23ConsumableState state;
        readonly Dictionary<string,Asset> assets=new Dictionary<string,Asset>(StringComparer.Ordinal);
        readonly Dictionary<string,WorldItem> items=new Dictionary<string,WorldItem>(StringComparer.Ordinal);
        R23ConsumableSample sample;bool previousLeft=true,previousRight=true,worldVisible=true;int heldHand,slotCapacity=3;
        R23ConsumableAudio audio;long sourceLayoutGeneration;
        readonly Dictionary<string,R24PickupSettleResult> sourceSettleResults=new Dictionary<string,R24PickupSettleResult>(StringComparer.Ordinal);
        public string[] WorldItemIds{get{return new List<string>(items.Keys).ToArray();}}
        public bool AcknowledgeSharedConsume(Ashfall.Integration.Playable.Networking.R24LanLobby lobby,Ashfall.Integration.Playable.Networking.R24LanFoodState receipt){Ashfall.Integration.Playable.Networking.R24LanFoodState actual;if(lobby==null||receipt==null||!receipt.Consumed||receipt.Owner!=lobby.OwnActor||!lobby.TryReadFood(receipt.Id,out actual)||!System.Object.ReferenceEquals(actual,receipt)||state==null)return false;bool wasHeld=HeldId==receipt.Id;if(!state.AcknowledgeSharedConsume(receipt.Id))return false;if(wasHeld)heldHand=0;SyncViews();return true;}
        public void SetSharedConsume(Func<string,bool> consume){if(state!=null)state.SharedConsume=consume;}
        public Func<string,Vector3,bool> SharedDrop;
        public string[] SourceCheckpointIds {get{var ids=new List<string>();foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())if(!row.IsSupply)ids.Add(row.Id);return ids.ToArray();}}
        public bool TryReadSourceSettleResult(string id,out R24PickupSettleResult result){result=null;return id!=null&&sourceSettleResults.TryGetValue(id,out result);}
        public R23ConsumableAudio Audio {get{return audio;}}
        // Source auto-draw after a consumed stack uses actor.get_weapon_hand().
        public int PreferredHand=2;
        public int SlotCapacity {get{return state==null?slotCapacity:state.SlotCapacity;}set{slotCapacity=Math.Max(3,Math.Min(6,value));if(state!=null)state.SlotCapacity=slotCapacity;}}
        public void SetSfxVolume(float value){if(audio!=null)audio.SetSfxVolume(value);}
        string visualHeld;float shakeProgress,shakeDistance,shakeSeconds;Vector3 previousShake;bool haveShake;
        public bool Ready {get;private set;}
        public string Failure {get;private set;}
        public string Status {get;private set;}
        public int HeldHand {get{return state==null||state.HeldId==null?0:heldHand;}}
        public string HeldId {get{return state==null?null:state.HeldId;}}
        public Preparation Phase {get{return state==null?Preparation.Idle:state.Phase;}}
        public float PendingSoda {get{return state==null?0:state.PendingSoda;}}
        public int PeelCount {get{return state==null?0:state.Peels;}}
        public Vector3 ConsumptionAnchor {get{WorldItem item;return HeldId!=null&&items.TryGetValue(HeldId,out item)?item.View.transform.TransformPoint(assets[item.Kind].Anchor):Vector3.zero;}}
        public bool LeftOccupied {get{return HeldHand==1;}}public bool RightOccupied {get{return HeldHand==2;}}
        public R23OwnedConsumable[] OwnedItems {get{return state==null?new R23OwnedConsumable[0]:state.OwnedItems;}}
        // Called only after a real target reservation. Hand is1left/2right.
        public Func<string,int,bool> MakeRoomForWorldItem;
        // Optional source objective router. ID is pickup:<instance> or open;
        // raw grip still reaches the local latch when another objective wins.
        public Func<string,int,bool> CanClaimObjectiveGrip;
        // Collection reach uses the physical hand; Root equips into the configured
        // weapon hand through its existing custody/holster/rollback path.
        public Func<string,int,bool> EquipCollectedItem;
        public Action<int> Equipped;
        bool ObjectiveAllowed(string id,int hand){return CanClaimObjectiveGrip==null||CanClaimObjectiveGrip(id,hand);}
        public void UpdateInputAvailability(R23ConsumableSample input){sample=input;if(state!=null)UpdateHeldPose();}
        public bool TryReadObjectiveGrip(int hand,out string id,out Vector3 position,out float distance)
        {
            bool found=TryReadAimedWorldItem(hand,out id,out position,out distance);
            if(!Allowed||HeldHand==0||hand==HeldHand||Phase!=Preparation.Open||!HandReady(hand)||!HandFree(hand))return found;
            WorldItem held;if(!items.TryGetValue(HeldId,out held))return found;
            Vector3 cap=held.View.transform.TransformPoint(Vector3.up*.10f);float capDistance=Vector3.Distance(Hand(hand).position,cap);
            if(capDistance>.35f||capDistance>distance||!HasLineOfSight("player","player",F(head.position),F(Hand(hand).position),Vector3.Distance(head.position,Hand(hand).position))||!HasLineOfSight("player",HeldId,F(Hand(hand).position),F(cap),.35f))return found;
            id="open";position=cap;distance=capDistance;return true;
        }
        public bool CanStore(string kind){return state!=null&&state.CanStore(kind);}
        public PickupSnapshot InspectItem(string id){return state==null||String.IsNullOrEmpty(id)?null:state.Inspect(id);}
        public bool TryReadWorldItem(string id,out Vector3 position,out Quaternion rotation,out bool owned)
        {
            position=Vector3.zero;rotation=Quaternion.identity;owned=false;WorldItem item;
            if(state==null||id==null||!items.TryGetValue(id,out item)||item.View==null)return false;var snapshot=state.Inspect(id);if(snapshot==null||snapshot.Removed)return false;
            position=item.View.transform.position;rotation=item.View.transform.rotation;owned=snapshot.Owner!=null;return true;
        }
        public bool TryReadAimedWorldItem(int hand,out string id,out Vector3 position,out float distance)
        {
            id=null;position=Vector3.zero;distance=float.PositiveInfinity;
            if(!Allowed||!worldVisible||!HandReady(hand)||!HandFree(hand))return false;Transform grip=Hand(hand);
            if(!HasLineOfSight("player","player",F(head.position),F(grip.position),Vector3.Distance(head.position,grip.position)))return false;
            foreach(var item in items.Values)
            {
                if(item.View==null||!item.View.activeInHierarchy)continue;var snapshot=state.Inspect(item.Id);if(snapshot==null||snapshot.Removed||snapshot.Owner!=null)continue;
                Vector3 delta=item.View.transform.position-grip.position;float length=delta.magnitude;
                if(length>2||length>distance||(length==distance&&String.CompareOrdinal(item.Id,id)>=0)||(length>.35f&&Vector3.Dot(R23TrackedGripPose.Forward(grip),delta/length)<=.94f))continue;
                if(!HasLineOfSight("player",item.Id,F(grip.position),F(item.View.transform.position),2))continue;
                id=item.Id;position=item.View.transform.position;distance=length;
            }
            return id!=null;
        }
        public static string[] AvailableKinds {get{return new[]{"soda","shaker","banana"};}}
        public void SetOtherSlotsUsed(int weaponSlots){if(state!=null)state.SetOtherSlotsUsed(weaponSlots);}
        public void Initialize(Transform playerRoot,Transform headPose,Transform leftPose,Transform rightPose,IR23ConsumableEffects effectPort)
        {
            if(root!=null)throw new InvalidOperationException("consumables already initialized");
            if(playerRoot==null||headPose==null||leftPose==null||rightPose==null||effectPort==null)throw new ArgumentException("accepted rig and real effect port required");
            Application.onBeforeRender+=RefreshTrackedHeldPresentation;root=playerRoot;head=headPose;left=leftPose;right=rightPose;effects=effectPort;state=new R23ConsumableState(effects,this);state.SlotCapacity=slotCapacity;
            content=new GameObject("R23 source consumable world props").transform;Status="Loading source consumables";StartCoroutine(Load());
            audio=gameObject.AddComponent<R23ConsumableAudio>();audio.Initialize(root);
        }
        IEnumerator Read(string name,Dictionary<string,byte[]> files)
        {
            string url=Application.streamingAssetsPath+"/PlayableR23/Consumables/"+name;if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var request=UnityWebRequest.Get(url)){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=name+": "+request.error;yield break;}files.Add(name,request.downloadHandler.data);}
        }
        IEnumerator Load()
        {
            foreach(string kind in AvailableKinds)
            {
                var files=new Dictionary<string,byte[]>();yield return Read(kind+".json",files);if(Failure!=null)yield break;
                yield return Read(kind+".meshbin",files);if(Failure!=null)yield break;
                var asset=new Asset();
                try{asset.Build(kind,files[kind+".json"],files[kind+".meshbin"],content);assets.Add(kind,asset);}catch(Exception error){asset.Dispose();Failure=error.ToString();}
                if(Failure!=null)yield break;
            }
            Ready=true;Status="Grip a supply with a free hand";
        }
        public bool Spawn(string id,string kind,Vector3 position)
        {return Spawn(id,kind,position,true);}
        public bool Spawn(string id,string kind,Vector3 position,bool restoreOnReset)
        {
            Asset asset;if(String.IsNullOrEmpty(id)||String.IsNullOrEmpty(kind)||!Ready||!assets.TryGetValue(kind,out asset)||items.ContainsKey(id)||!F(position).IsFinite)return false;
            if(!state.Register(id,kind,F(position),restoreOnReset))return false;
            var go=Instantiate(asset.Template);go.name=id;go.transform.SetParent(content,false);go.transform.position=position;bool culled=!Ashfall.Integration.Playable.World.Loot.R26PickupCulling.Within(position,Ashfall.Integration.Playable.World.Loot.R26PickupCulling.ShowRange);go.SetActive(worldVisible&&!culled);
            items.Add(id,new WorldItem{Id=id,Kind=kind,View=go,InitialPosition=position,Culled=culled});return true;
        }
        public void SetWorldVisible(bool visible){worldVisible=visible;if(state!=null)SyncViews();}
        public bool SetWorldItemVisible(string id,bool visible)
        {WorldItem item;if(id==null||!items.TryGetValue(id,out item))return false;if(item.WorldVisible==visible)return true;item.WorldVisible=visible;SyncView(item);return true;}
        public bool RetireUnownedWorldItem(string id)
        {
            WorldItem item;if(id==null||!items.TryGetValue(id,out item)||!state.RetireUnownedWorldItem(id))return false;
            items.Remove(id);RetireSharedFood(id);sourceSettleResults.Remove(id);item.View.SetActive(false);Destroy(item.View);
            if(item.RingMesh!=null)Destroy(item.RingMesh);if(item.PeelMesh!=null)Destroy(item.PeelMesh);return true;
        }
        public bool SettleWorldItem(string id,Func<bool> collisionReady,Func<bool> stillCurrent)
        {
            WorldItem item;if(collisionReady==null||stillCurrent==null||id==null||!items.TryGetValue(id,out item)||!AvailableSourceItem(id,item))return false;
            StartCoroutine(R24WorldPickupSettler.Defer(item.View.transform,root,collisionReady,
                delegate{return stillCurrent()&&AvailableSourceItem(id,item);},
                delegate(Vector3 at){if(stillCurrent()&&AvailableSourceItem(id,item))RepositionWorldItem(id,at,item.View.transform.rotation);}));return true;
        }
        public event Action<string,Vector3> WorldItemMoved;
        public bool RepositionWorldItem(string id,Vector3 position){return RepositionWorldItem(id,position,Quaternion.identity);}
        public bool RepositionWorldItem(string id,Vector3 position,Quaternion rotation)
        {
            WorldItem item;if(String.IsNullOrEmpty(id)||!items.TryGetValue(id,out item)||!F(new Vector3(rotation.x,rotation.y,rotation.z)).IsFinite||Single.IsNaN(rotation.w)||Single.IsInfinity(rotation.w)||Quaternion.Dot(rotation,rotation)<.5f)return false;
            // The session re-sends every shared prop each frame: an unmoved prop is already placed, shown and published.
            rotation=rotation.normalized;if((item.InitialPosition-position).sqrMagnitude<.0000001f&&Mathf.Abs(Quaternion.Dot(item.InitialRotation,rotation))>.999999f&&(item.View.transform.position-position).sqrMagnitude<.0000001f){var placed=state.Inspect(id);if(placed!=null&&!placed.Removed&&placed.Owner==null&&placed.ReservedBy==null&&placed.Position.X==position.x&&placed.Position.Y==position.y&&placed.Position.Z==position.z)return true;}
            if(!state.RepositionWorldItem(id,F(position)))return false;
            item.InitialPosition=position;item.InitialRotation=rotation;item.View.transform.SetPositionAndRotation(position,item.InitialRotation);SyncView(item);if(WorldItemMoved!=null)WorldItemMoved(id,position);return true;
        }
        // Idempotent checkpoint composition: no Reset, removal, owned mutation or
        // consumed-item resurrection. Explicit ResetRound remains caller-owned.
        public R24PickupLayoutResult EnsureSourceCheckpointLayout(Vector3 checkpointUnity,Func<bool> mapCollisionReady)
        {
            if(!F(checkpointUnity).IsFinite||mapCollisionReady==null)throw new ArgumentException("Actual checkpoint and map collision readiness required");
            int created=0,moved=0,preserved=0;long generation=++sourceLayoutGeneration;sourceSettleResults.Clear();
            foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())
            {
                if(row.IsSupply)continue;var point=row.At(F(checkpointUnity));var position=new Vector3(point.X,point.Y,point.Z);WorldItem item;
                if(!items.TryGetValue(row.Id,out item)){if(!Spawn(row.Id,row.Kind,position)){preserved++;sourceSettleResults[row.Id]=new R24PickupSettleResult(row.Id,R24PickupSettleOutcome.Unavailable,point,point);continue;}created++;item=items[row.Id];}
                else{if(item.Kind!=row.Kind)throw new InvalidOperationException("Source food ID/profile binding changed");if(!RepositionWorldItem(row.Id,position)){preserved++;var actual=item.View==null?point:F(item.View.transform.position);sourceSettleResults[row.Id]=new R24PickupSettleResult(row.Id,R24PickupSettleOutcome.Preserved,point,actual);continue;}moved++;}
                string id=row.Id;WorldItem captured=item;bool accepted=false;
                sourceSettleResults[id]=new R24PickupSettleResult(id,R24PickupSettleOutcome.Pending,point,F(captured.View.transform.position));
                StartCoroutine(R24WorldPickupSettler.Defer(captured.View.transform,root,mapCollisionReady,delegate{return generation==sourceLayoutGeneration&&AvailableSourceItem(id,captured);},delegate(Vector3 at){if(generation==sourceLayoutGeneration&&AvailableSourceItem(id,captured))accepted=RepositionWorldItem(id,at,captured.View.transform.rotation);},false,delegate(R24PickupSettleOutcome outcome){if(generation==sourceLayoutGeneration)sourceSettleResults[id]=new R24PickupSettleResult(id,outcome==R24PickupSettleOutcome.Settled&&!accepted?R24PickupSettleOutcome.Refused:outcome,point,captured.View==null?point:F(captured.View.transform.position));}));
            }
            return new R24PickupLayoutResult(created,moved,preserved);
        }
        bool AvailableSourceItem(string id,WorldItem expected)
        {
            WorldItem current;if(state==null||!items.TryGetValue(id,out current)||!System.Object.ReferenceEquals(current,expected)||current.View==null)return false;
            var snapshot=state.Inspect(id);return snapshot!=null&&!snapshot.Removed&&snapshot.Owner==null;
        }
        // Explicit new-round operation only: restores registered world starters, grants
        // no owned item or effect. Map selection must not call this as a reward path.
        public bool ResetActorLife(){if(state==null||!state.ResetActorLife())return false;heldHand=0;visualHeld=null;haveShake=false;shakeProgress=0;previousLeft=previousRight=true;if(audio!=null)audio.ResetRound();SyncViews();return true;}
        internal void ClearCarriedForRespawn()
        {
            ResetActorLife();
            foreach(var item in OwnedItems)state.RetireOwned(item.Id);
            SyncViews();
        }
        public void ResetRound()
        {
            if(state==null)return;sourceLayoutGeneration++;foreach(string id in new List<string>(sourceSettleResults.Keys)){var pending=sourceSettleResults[id];if(pending.Outcome==R24PickupSettleOutcome.Pending)sourceSettleResults[id]=new R24PickupSettleResult(id,R24PickupSettleOutcome.Cancelled,pending.RequestedPosition,pending.Position);}state.Reset();heldHand=0;visualHeld=null;haveShake=false;shakeProgress=0;previousLeft=previousRight=true;
            if(audio!=null)audio.ResetRound();
            foreach(string id in new List<string>(items.Keys))if(state.Inspect(id)==null){var expired=items[id];items.Remove(id);RetireSharedFood(id);expired.View.SetActive(false);Destroy(expired.View);if(expired.RingMesh!=null)Destroy(expired.RingMesh);if(expired.PeelMesh!=null)Destroy(expired.PeelMesh);}
            foreach(var item in items.Values)item.View.transform.SetPositionAndRotation(item.InitialPosition,item.InitialRotation);
            SyncViews();Status="Grip a supply with a free hand";
        }
        // Explicit fresh map/world lifecycle. Caller places its new map's world props.
        public void ClearItems()
        {
            if(state==null)return;sourceLayoutGeneration++;sourceSettleResults.Clear();state.Reset(false);foreach(var item in items.Values){RetireSharedFood(item.Id);item.View.SetActive(false);Destroy(item.View);if(item.RingMesh!=null)Destroy(item.RingMesh);if(item.PeelMesh!=null)Destroy(item.PeelMesh);}items.Clear();heldHand=0;visualHeld=null;haveShake=false;shakeProgress=0;previousLeft=previousRight=true;
            if(audio!=null)audio.ResetRound();
        }
        bool Allowed {get{return Ready&&sample.Enabled&&effects.ReadVitals().Alive&&isActiveAndEnabled;}}
        bool HandReady(int hand){return hand==1?sample.LeftReady:hand==2&&sample.RightReady;}
        bool HandFree(int hand){return hand==1?sample.LeftFree:hand==2&&sample.RightFree;}
        Transform Hand(int hand){return hand==1?left:right;}
        ActorFrame Frame(int hand)
        {
            Transform grip=Hand(hand),other=hand==1?right:left;WorldItem item=null;Asset asset=null;
            if(state.HeldId!=null&&items.TryGetValue(state.HeldId,out item))asset=assets[item.Kind];
            Transform prop=item==null?grip:item.View.transform;Vector3 anchor=asset==null?grip.position:prop.TransformPoint(asset.Anchor);
            return new ActorFrame{Allowed=Allowed,HandEligible=HandReady(hand),TransferSafe=true,OtherHandEligible=HandReady(hand==1?2:1)&&HandFree(hand==1?2:1),OtherGrip=(hand==1?sample.RightGrip:sample.LeftGrip)>.55f,
                Head=F(head.position),Hand=F(grip.position),Forward=F(R23TrackedGripPose.Forward(grip)),LocalHand=F(root.InverseTransformPoint(grip.position)),OtherLocal=F(prop.InverseTransformPoint(other.position)),Anchor=F(anchor),HeadLocalAnchor=F(head.InverseTransformPoint(anchor)),ItemUpDot=Vector3.Dot(prop.up,Vector3.up)};
        }
        public bool TryHostFoodFrame(string kind,int hand,Ashfall.Integration.Playable.Networking.R24LanRawTracking raw,out ActorFrame frame){frame=null;Asset asset;if(raw==null||hand<0||hand>1||!assets.TryGetValue(kind,out asset))return false;var h=hand==0?raw.Left:raw.Right;var other=hand==0?raw.Right:raw.Left;Quaternion rotation=new Quaternion(h.Rotation.X,h.Rotation.Y,h.Rotation.Z,h.Rotation.W)*R23TrackedGripPose.LocalRotation;Vector3 at=new Vector3(h.Position.X,h.Position.Y,h.Position.Z),prop=at+rotation*asset.Offset,anchor=prop+rotation*asset.Anchor;Quaternion rootRotation=new Quaternion(raw.Root.Rotation.X,raw.Root.Rotation.Y,raw.Root.Rotation.Z,raw.Root.Rotation.W),headRotation=new Quaternion(raw.Head.Rotation.X,raw.Head.Rotation.Y,raw.Head.Rotation.Z,raw.Head.Rotation.W);Vector3 headAt=new Vector3(raw.Head.Position.X,raw.Head.Position.Y,raw.Head.Position.Z),otherAt=new Vector3(other.Position.X,other.Position.Y,other.Position.Z);frame=new ActorFrame{Allowed=true,HandEligible=(raw.Flags&(hand==0?2:4))!=0,OtherHandEligible=(raw.Flags&(hand==0?4:2))!=0,TransferSafe=true,OtherGrip=(hand==0?raw.RightGrip:raw.LeftGrip)>.55f,Head=raw.Head.Position,Hand=h.Position,Forward=F(rotation*Vector3.forward),LocalHand=F(Quaternion.Inverse(rootRotation)*(at-new Vector3(raw.Root.Position.X,raw.Root.Position.Y,raw.Root.Position.Z))),OtherLocal=F(Quaternion.Inverse(rotation)*(otherAt-prop)),Anchor=F(anchor),HeadLocalAnchor=F(Quaternion.Inverse(headRotation)*(anchor-headAt)),ItemUpDot=Vector3.Dot(rotation*Vector3.up,Vector3.up)};return true;}
        public bool TryPickup(string id,int hand)
        {
            WorldItem item;if(String.IsNullOrEmpty(id)||!Allowed||!HandReady(hand)||!HandFree(hand)||!CanClaimWorldFood(id)||!items.TryGetValue(id,out item)||!ObjectiveAllowed("pickup:"+id,hand))return false;
            state.SetFrame(Frame(hand));
            if(!state.Execute(PickupAction.Reserve,id).Accepted)return false;
            if(!state.CanStore(item.Kind)&&MakeRoomForWorldItem!=null&&!MakeRoomForWorldItem(item.Kind,hand)){state.Execute(PickupAction.Release,id);Status="No reachable place to drop the selected slot";return false;}
            state.SetFrame(Frame(hand));
            if(!state.Execute(PickupAction.Collect,id).Accepted){state.Execute(PickupAction.Release,id);return false;}
            int equipHand=PreferredHand==1?1:2;
            bool equipped=EquipCollectedItem!=null?EquipCollectedItem(id,equipHand):Equip(id,equipHand);
            SyncViews();if(!equipped)Status="Collected into backpack; weapon hand unavailable";
            return true;
        }
        public bool DropSlot(int slot,Vector3[] positions,Quaternion[] rotations)
        {
            if(!Allowed||positions==null||rotations==null)return false;
            var members=new List<R23OwnedConsumable>();foreach(var item in OwnedItems)if(item.Slot==slot)members.Add(item);
            if(members.Count<1||members.Count>2||members.Count!=positions.Length||members.Count!=rotations.Length)return false;
            int hand=HeldHand!=0?HeldHand:HandReady(2)?2:1;if(!HandReady(hand))return false;
            var points=new Float3[members.Count];var orientations=new Quaternion[members.Count];
            for(int i=0;i<members.Count;i++)
            {
                Quaternion q=rotations[i];if(!F(positions[i]).IsFinite||!F(new Vector3(q.x,q.y,q.z)).IsFinite||Single.IsNaN(q.w)||Single.IsInfinity(q.w)||Quaternion.Dot(q,q)<.5f)return false;q=q.normalized;
                Bounds bounds=assets[members[i].Kind].Bounds;
                foreach(var collider in Physics.OverlapBox(positions[i]+q*bounds.center,bounds.extents,q,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(!Excluded(collider))return false;
                points[i]=F(positions[i]);orientations[i]=q;
            }
            if(SharedDrop!=null){bool accepted=true;for(int i=0;i<members.Count;i++)if(!SharedDrop(members[i].Id,positions[i]))accepted=false;if(!accepted)return false;}
            WorldItem heldBefore=null;if(HeldId!=null)items.TryGetValue(HeldId,out heldBefore);
            state.SetFrame(Frame(hand));var result=state.DropSlot(slot,points);if(!result.Accepted){Status=result.Reason;return false;}
            if(heldBefore!=null&&state.HeldId==null)Cue(R23ConsumableAudioEvent.Stow,heldBefore);
            for(int i=0;i<members.Count;i++){var item=items[members[i].Id];item.View.transform.SetParent(content,false);item.View.transform.SetPositionAndRotation(positions[i],orientations[i]);}
            if(state.HeldId==null)heldHand=0;SyncViews();Status="Dropped selected slot";return true;
        }
        public bool Equip(string id,int hand)
        {
            if(!Allowed||!HandReady(hand)||!HandFree(hand))return false;
            WorldItem previous=null;if(HeldId!=null)items.TryGetValue(HeldId,out previous);
            state.SetFrame(Frame(hand));var result=state.Execute(PickupAction.Equip,id);if(!result.Accepted){Status=result.Reason;return false;}
            if(previous!=null)Cue(R23ConsumableAudioEvent.Stow,previous);
            heldHand=hand;shakeProgress=0;haveShake=false;SyncViews();Cue(R23ConsumableAudioEvent.Equip,items[id]);if(Equipped!=null)Equipped(hand);Status=Phase==Preparation.Shake?"Shake, then drink":items[id].Kind=="banana"?"Other hand: grip and pull down four peels":"Other free hand grip: open, then drink";return true;
        }
        public void Stow(){if(state==null)return;WorldItem held;if(HeldId!=null&&items.TryGetValue(HeldId,out held)&&Allowed)Cue(R23ConsumableAudioEvent.Stow,held);state.Execute(PickupAction.Stow,state.HeldId);heldHand=0;haveShake=false;shakeProgress=0;SyncViews();}
        void Cue(R23ConsumableAudioEvent value,WorldItem item){if(audio!=null&&item!=null&&item.View!=null){audio.SetEnabled(Allowed);audio.Notify(value,item.Kind,item.View.transform.position);}}
        internal bool ReconcileNetworkPreparationReset(string id){if(HeldId!=id)return true;if(!Allowed||!HandReady(heldHand))return false;state.SetFrame(Frame(heldHand));if(!state.Execute(PickupAction.Equip,id).Accepted)return false;shakeProgress=0;haveShake=false;SyncViews();Status="Host tracking reset: prepare this item again";return true;}
        public void CancelGestures(){Stow();previousLeft=previousRight=true;}
        public bool Drop(string id)
        {
            WorldItem item;if(String.IsNullOrEmpty(id)||!Allowed||!items.TryGetValue(id,out item))return false;
            int hand=HeldHand==0?2:HeldHand;Transform grip=Hand(hand);Vector3 desired=grip.position+grip.forward*.25f;RaycastHit closest=new RaycastHit();float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(desired+Vector3.up*.15f,Vector3.down,3,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(!Excluded(hit.collider)&&hit.normal.y>.5f&&hit.distance<nearest){nearest=hit.distance;closest=hit;}
            if(float.IsPositiveInfinity(nearest))return false;
            Vector3 position=closest.point+Vector3.up*(item.Kind=="banana"?.118f:item.Kind=="soda"?.068f:.103f);return Drop(id,position,Quaternion.identity);
        }
        // Source-local mesh bounds, excluding preparation text; root can transform them
        // using its planned position/rotation for reachable native ground placement.
        public Bounds GetDropBounds(string id)
        {WorldItem item;return id!=null&&items.TryGetValue(id,out item)?assets[item.Kind].Bounds:new Bounds();}
        public bool Drop(string id,Vector3 position,Quaternion rotation)
        {
            WorldItem item;if(String.IsNullOrEmpty(id)||!Allowed||!items.TryGetValue(id,out item)||!F(position).IsFinite)return false;
            if(!F(new Vector3(rotation.x,rotation.y,rotation.z)).IsFinite||Single.IsNaN(rotation.w)||Single.IsInfinity(rotation.w)||Quaternion.Dot(rotation,rotation)<.5f)return false;
            rotation=rotation.normalized;
            Bounds bounds=assets[item.Kind].Bounds;
            foreach(var collider in Physics.OverlapBox(position+rotation*bounds.center,bounds.extents,rotation,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(!Excluded(collider))return false;
            if(SharedDrop!=null&&!SharedDrop(id,position))return false;
            bool wasHeld=HeldId==id;int hand=HeldHand==0?2:HeldHand;state.SetFrame(Frame(hand));var result=state.Drop(id,F(position));
            if(!result.Accepted){Status=result.Reason;return false;}
            if(wasHeld)Cue(R23ConsumableAudioEvent.Stow,item);
            item.View.transform.SetParent(content,false);item.View.transform.SetPositionAndRotation(position,rotation);heldHand=0;SyncViews();Status="Dropped";return true;
        }
        public void Sample(R23ConsumableSample input)
        {
            if(state==null)return;sample=input;
            float seconds=input.DeltaSeconds;
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<=0||seconds>.1f){CancelGestures();return;}
            WorldItem current=null;if(HeldId!=null)items.TryGetValue(HeldId,out current);if(audio!=null)audio.Tick(seconds,current==null?head.position:current.View.transform.position,Allowed);
            UpdateHeldPose();state.SetFrame(Frame(HeldHand==0?2:HeldHand));state.Advance(seconds);
            if(!Allowed){CancelGestures();SyncViews();return;}
            if(!input.LeftReady)previousLeft=true;else if(input.LeftGrip<=.2f)previousLeft=false;
            if(!input.RightReady)previousRight=true;else if(input.RightGrip<=.2f)previousRight=false;
            bool leftEdge=input.LeftReady&&input.LeftGrip>=.65f&&!previousLeft,rightEdge=input.RightReady&&input.RightGrip>=.65f&&!previousRight;
            if(leftEdge)previousLeft=true;if(rightEdge)previousRight=true;
            if(HeldHand!=0)
            {
                WorldItem held=items[HeldId];int other=HeldHand==1?2:1;bool openEdge=other==1?leftEdge:rightEdge;
                if(openEdge&&HandReady(other)&&HandFree(other))
                {
                    string target;Vector3 point;float distance;
                    float openDistance=Phase==Preparation.Open?Vector3.Distance(Hand(other).position,held.View.transform.TransformPoint(Vector3.up*.10f)):float.PositiveInfinity;
                    if(TryReadAimedWorldItem(other,out target,out point,out distance)&&distance<openDistance&&ObjectiveAllowed("pickup:"+target,other))
                    {TryPickup(target,other);SyncViews();return;}
                }
                state.SetFrame(Frame(HeldHand));
                if(openEdge&&HandReady(other)&&HandFree(other)&&Phase==Preparation.Open&&ObjectiveAllowed("open",other))
                {var result=state.Execute(held.Kind=="banana"?PickupAction.BeginPeel:PickupAction.Open,HeldId);if(!result.Accepted)Status=result.Reason;else if(held.Kind!="banana")Cue(R23ConsumableAudioEvent.Open,held);}
                if(held.Kind=="banana"&&Phase==Preparation.Open){int before=state.Peels;state.Execute(PickupAction.SamplePeel,HeldId);if(state.Peels>before){Cue(R23ConsumableAudioEvent.Peel,held);if(Phase==Preparation.Consume)Cue(R23ConsumableAudioEvent.Open,held);}}
                if(Phase==Preparation.Shake)
                {
                    state.Execute(PickupAction.SampleShake,HeldId);
                    if(Phase==Preparation.Consume)Cue(R23ConsumableAudioEvent.Ready,held);
                    Vector3 local=root.InverseTransformPoint(Hand(HeldHand).position);
                    if(haveShake){float distance=Vector3.Distance(local,previousShake),speed=distance/seconds;if(speed>=.45f&&speed<=8&&distance<.3f){if(audio!=null&&Phase==Preparation.Shake)audio.ShakeMotion(held.View.transform.position);shakeDistance+=distance*1.3f;shakeSeconds+=Mathf.Min(seconds,.1f)*1.3f;shakeProgress=Mathf.Min(.98f,Mathf.Min(shakeDistance/1.8f,shakeSeconds/3));}}
                    previousShake=local;haveShake=true;
                }
                var consume=state.Execute(PickupAction.Consume,HeldId);
                if(consume.Accepted)
                {
                    Cue(R23ConsumableAudioEvent.Consume,held);heldHand=0;Status="Consumed";
                    if(HeldId!=null)
                    {
                        int nextHand=PreferredHand==1?1:2;
                        if(HandReady(nextHand)){heldHand=nextHand;previousLeft=previousRight=true;SyncViews();Cue(R23ConsumableAudioEvent.Equip,items[HeldId]);if(Equipped!=null)Equipped(nextHand);}
                        else state.Execute(PickupAction.Stow,HeldId);
                    }
                }
                else if(Phase==Preparation.Consume)Status=consume.Reason=="full"?(held.Kind=="banana"?"Health and stamina are full":"Shield is full"):held.Kind=="banana"?"Bring the exposed fruit end to your headset":"Tilt the drinking end to your headset";
                if(HeldId!=null)UpdatePreparation(items[HeldId]);
            }
            else
            {
                if(rightEdge&&input.RightReady&&input.RightFree)PickupNearest(2);
                if(HeldHand==0&&leftEdge&&input.LeftReady&&input.LeftFree)PickupNearest(1);
            }
            SyncViews();
        }
        void PickupNearest(int hand)
        {
            string nearest;Vector3 point;float distance;if(TryReadAimedWorldItem(hand,out nearest,out point,out distance))TryPickup(nearest,hand);
        }
        void UpdateHeldPose()
        {
            if(HeldHand==0)return;WorldItem item;if(!items.TryGetValue(HeldId,out item)||item.View==null)return;
            var grip=Hand(HeldHand);var asset=assets[item.Kind];item.View.transform.SetParent(content,false);
            Quaternion corrected=grip.rotation*R23TrackedGripPose.LocalRotation;
            item.View.transform.SetPositionAndRotation(grip.position+corrected*asset.Offset,corrected);
        }
        void UpdatePreparation(WorldItem item)
        {
            bool open=item.Kind=="banana"||(item.Kind=="soda"&&Phase==Preparation.Consume);
            foreach(Transform part in item.View.transform)if(part.name=="Cap")part.gameObject.SetActive(!open);
            var label=item.View.GetComponentInChildren<TextMesh>(true);if(label!=null)
            {
                label.gameObject.SetActive(true);label.text=item.Kind=="banana"?(Phase==Preparation.Consume?"EAT":"PEEL "+state.Peels+" / 4"):item.Kind=="shaker"?(Phase==Preparation.Shake?"SHAKE":"READY \u2014 DRINK"):Phase==Preparation.Consume?"DRINK":"OPEN";
                Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);if(forward.sqrMagnitude<.000001f)forward=root.forward;label.transform.rotation=Quaternion.LookRotation(forward,Vector3.up);
                label.font.RequestCharactersInTexture(label.text,label.fontSize);var material=label.GetComponent<Renderer>().sharedMaterial;material.SetTexture("_MainTex",label.font.material.mainTexture);material.SetColor("_Color",item.Kind=="shaker"?(Phase==Preparation.Consume?Color.green:Color.cyan):Color.white);
            }
            if(item.Kind=="shaker")UpdateShakerRing(item,Phase==Preparation.Consume?1:shakeProgress);
            if(item.Kind=="banana")UpdateBananaPeels(item);
        }
        void UpdateBananaPeels(WorldItem item)
        {
            if(item.Peels==null)
            {
                item.Peels=new GameObject("FourPeelStrips").transform;item.Peels.SetParent(item.View.transform,false);
                item.PeelMesh=new Mesh();item.PeelMesh.MarkDynamic();item.Peels.gameObject.AddComponent<MeshFilter>().sharedMesh=item.PeelMesh;
                item.Peels.gameObject.AddComponent<MeshRenderer>().sharedMaterial=assets[item.Kind].PeelMaterial;
                item.PeelAmounts=new[]{-1f,-1f,-1f,-1f};
            }
            item.Peels.gameObject.SetActive(true);
            float partial=0;
            if(state.PeelActive&&state.Peels<4)
            {Vector3 other=item.View.transform.InverseTransformPoint(Hand(HeldHand==1?2:1).position);partial=Mathf.Clamp01((state.PeelStart.Y-other.y)/.10f);}
            bool changed=false;
            for(int i=0;i<4;i++){float amount=i<state.Peels?1:i==state.Peels?partial:0;if(Mathf.Abs(item.PeelAmounts[i]-amount)>.00001f)changed=true;item.PeelAmounts[i]=amount;}
            if(!changed)return;
            var vertices=new Vector3[4*R23BananaPeelGeometry.VerticesPerStrip];var indices=new int[vertices.Length];int at=0;
            for(int strip=0;strip<4;strip++)foreach(var point in R23BananaPeelGeometry.BuildStrip(strip,item.PeelAmounts[strip])){vertices[at]=U(point);indices[at]=at;at++;}
            item.PeelMesh.Clear();item.PeelMesh.vertices=vertices;item.PeelMesh.triangles=indices;item.PeelMesh.RecalculateNormals();item.PeelMesh.RecalculateBounds();
        }
        void UpdateShakerRing(WorldItem item,float fill)
        {
            if(item.Ring==null)
            {
                item.Ring=new GameObject("ChargeRing").transform;item.Ring.SetParent(item.View.transform,false);
                item.RingMesh=new Mesh();item.Ring.gameObject.AddComponent<MeshFilter>().sharedMesh=item.RingMesh;
                item.Ring.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{assets[item.Kind].RingDark,assets[item.Kind].RingCyan};
            }
            item.Ring.gameObject.SetActive(true);Float3 axisX,axisY,axisZ;
            Quaternion ringRotation=head.rotation;if(Ashfall.Integration.Playable.PickupHints.R23PickupHintGeometry.Upright(F(head.forward),out axisX,out axisY,out axisZ))ringRotation=Quaternion.LookRotation(U(axisZ),U(axisY));item.Ring.SetPositionAndRotation(item.View.transform.position,ringRotation);
            if(Mathf.Abs(item.RingFill-fill)<.0001f)return;item.RingFill=fill;
            var vertices=new List<Vector3>();var background=new List<int>();var foreground=new List<int>();
            for(int layer=0;layer<2;layer++)
            {
                int count=layer==0?64:Mathf.CeilToInt(fill*64);var indices=layer==0?background:foreground;
                for(int step=0;step<count;step++)
                {
                    float a=Mathf.PI*.5f-step*Mathf.PI*2/64,b=Mathf.PI*.5f-Mathf.Min((step+1)/64f,layer==0?1:fill)*Mathf.PI*2;int start=vertices.Count;float z=layer==0?0:-.002f;
                    vertices.Add(new Vector3(Mathf.Cos(a)*.09f,Mathf.Sin(a)*.09f,z));vertices.Add(new Vector3(Mathf.Cos(a)*.102f,Mathf.Sin(a)*.102f,z));vertices.Add(new Vector3(Mathf.Cos(b)*.102f,Mathf.Sin(b)*.102f,z));vertices.Add(new Vector3(Mathf.Cos(b)*.09f,Mathf.Sin(b)*.09f,z));
                    foreach(int index in new[]{0,1,2,0,2,3})indices.Add(start+index);
                }
            }
            item.RingMesh.Clear();item.RingMesh.vertices=vertices.ToArray();item.RingMesh.subMeshCount=2;item.RingMesh.SetTriangles(background.ToArray(),0);item.RingMesh.SetTriangles(foreground.ToArray(),1);item.RingMesh.RecalculateBounds();
            item.Ring.GetComponent<MeshRenderer>().sharedMaterials=new[]{assets[item.Kind].RingDark,fill>=1?assets[item.Kind].RingGreen:assets[item.Kind].RingCyan};
        }
        void SyncViews()
        {
            foreach(var item in items.Values)SyncView(item);
            if(HeldId!=visualHeld){visualHeld=HeldId;haveShake=false;shakeProgress=shakeDistance=shakeSeconds=0;}UpdateHeldPose();
        }
        bool Excluded(Collider collider){return collider==null||collider.gameObject.layer==19||collider.gameObject.layer==20||collider.gameObject.layer==21||collider.transform.IsChildOf(root)||collider.transform.IsChildOf(content);}
        public bool HasLineOfSight(string actor,string owner,Float3 from,Float3 to,float range)
        {
            Vector3 start=U(from),motion=U(to)-start;float length=motion.magnitude;if(length>range+.0001f)return false;
            if(length<.00001f)return true;
            foreach(var hit in Physics.RaycastAll(start,motion/length,length,(1<<0)|(1<<8)|(1<<19),QueryTriggerInteraction.Ignore))if(!hit.collider.transform.IsChildOf(root)&&!hit.collider.transform.IsChildOf(content))return false;return true;
        }
        void OnDisable(){if(state!=null)CancelGestures();if(audio!=null)audio.Suspend();if(content!=null)content.gameObject.SetActive(false);}
        void OnEnable(){if(content!=null)content.gameObject.SetActive(true);}
        void OnDestroy(){Application.onBeforeRender-=RefreshTrackedHeldPresentation;StopAllCoroutines();if(content!=null)Destroy(content.gameObject);foreach(var item in items.Values){if(item.RingMesh!=null)Destroy(item.RingMesh);if(item.PeelMesh!=null)Destroy(item.PeelMesh);}foreach(var asset in assets.Values)asset.Dispose();assets.Clear();}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}static Vector3 U(Float3 v){return new Vector3(v.X,v.Y,v.Z);}

        [Serializable]sealed class Manifest {public Surface[] materials=null;public float[] anchor=null,heldOffset=null;}
        [Serializable]sealed class Surface {public float[] color=null;public float metallic=0,roughness=.65f;}
        internal sealed class Asset:IDisposable
        {
            public GameObject Template;public Vector3 Anchor,Offset;public Bounds Bounds;public Material RingDark,RingCyan,RingGreen,PeelMaterial;
            readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
            public void Build(string kind,byte[] json,byte[] binary,Transform parent)
            {
                var manifest=JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(json));Anchor=U(Floats(manifest.anchor));Offset=U(Floats(manifest.heldOffset));
                Template=new GameObject("R23 source "+kind);Template.transform.SetParent(parent,false);Template.SetActive(false);
                if(kind=="shaker")
                {
                    var unlit=Shader.Find("Universal Render Pipeline/Unlit");if(unlit==null)throw new IOException("URP Unlit missing");
                    RingDark=RingMaterial(unlit,new Color(.08f,.15f,.19f));RingCyan=RingMaterial(unlit,Color.cyan);RingGreen=RingMaterial(unlit,Color.green);
                }
                var cap=new GameObject("Cap");cap.transform.SetParent(Template.transform,false);
                var materials=new Material[manifest.materials.Length];var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new IOException("URP Lit missing");
                if(kind=="banana"){PeelMaterial=new Material(shader);resources.Add(PeelMaterial);PeelMaterial.SetColor("_BaseColor",new Color(.94f,.70f,.065f));PeelMaterial.SetFloat("_Smoothness",.35f);PeelMaterial.SetFloat("_Metallic",0);PeelMaterial.SetFloat("_Cull",0);}
                for(int i=0;i<materials.Length;i++){var s=manifest.materials[i];var m=new Material(shader);resources.Add(m);materials[i]=m;m.SetColor("_BaseColor",new Color(s.color[0],s.color[1],s.color[2],s.color[3]));m.SetFloat("_Metallic",s.metallic);m.SetFloat("_Smoothness",1-s.roughness);m.SetFloat("_Cull",0);}
                using(var reader=new BinaryReader(new MemoryStream(binary,false)))
                {
                    if(reader.ReadInt32()!=0x52323343)throw new IOException("source consumable header");int count=reader.ReadInt32();
                    for(int part=0;part<count;part++)
                    {
                        int length=reader.ReadInt32();string name=System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));int nv=reader.ReadInt32(),nt=reader.ReadInt32(),mi=reader.ReadInt32();bool removable=reader.ReadInt32()!=0;
                        if(nv<3||nv>100000||nt<3||nt>300000||mi<0||mi>=materials.Length)throw new IOException("consumable mesh limits");
                        var positions=new Vector3[nv];var normals=new Vector3[nv];var indices=new int[nt];
                        for(int i=0;i<nv;i++){positions[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());normals[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());}
                        for(int i=0;i<nt;i++){indices[i]=reader.ReadInt32();if(indices[i]<0||indices[i]>=nv)throw new IOException("consumable index");}
                        var mesh=new Mesh();resources.Add(mesh);mesh.name=kind+"/"+name;mesh.vertices=positions;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateBounds();
                        if(part==0)Bounds=mesh.bounds;else Bounds.Encapsulate(mesh.bounds);
                        var go=new GameObject(name);go.transform.SetParent(removable?cap.transform:Template.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=materials[mi];
                    }
                }
                if(kind!="banana"&&kind!="soda"&&kind!="shaker")return;
                var title=new GameObject("ItemTitle");title.transform.SetParent(Template.transform,false);title.transform.localPosition=new Vector3(0,.25f,0);var text=title.AddComponent<TextMesh>();text.fontSize=28;text.characterSize=.014f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;text.richText=false;
                var sourceFont=Resources.Load<Font>("R23GodotUI");if(sourceFont==null)throw new IOException("Actual source item font missing");text.font=sourceFont;text.font.RequestCharactersInTexture("PEEL 01234/ EATOPEN SHAKE READY \u2014 DRINK",28);
                var captionShader=Resources.Load<Shader>("R24ItemSurface");if(captionShader==null)throw new IOException("Source depth-tested item text shader missing");var caption=new Material(captionShader);caption.SetFloat("_Font",1);caption.SetColor("_Color",Color.white);caption.SetTexture("_MainTex",text.font.material.mainTexture);resources.Add(caption);title.GetComponent<Renderer>().sharedMaterial=caption;title.SetActive(false);
            }
            static Float3 Floats(float[] a){if(a==null||a.Length!=3)throw new IOException("source vector");return new Float3(a[0],a[1],a[2]);}
            Material RingMaterial(Shader shader,Color color){var material=new Material(shader);resources.Add(material);material.SetColor("_BaseColor",color);material.SetFloat("_Cull",0);return material;}
            public void Dispose(){if(Template!=null)UnityEngine.Object.Destroy(Template);foreach(var resource in resources)if(resource!=null)UnityEngine.Object.Destroy(resource);resources.Clear();}
        }
    }
}
#endif
