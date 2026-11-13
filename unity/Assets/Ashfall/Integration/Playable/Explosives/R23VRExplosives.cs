#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Integration.Playable.Explosives
{
    public struct R23ExplosiveInputFrame
    {public bool Enabled,LeftReady,RightReady,LeftTriggerClick,RightTriggerClick;public float LeftTrigger,RightTrigger;}
    public sealed class R23GrenadeLaunch
    {public readonly string ItemId,Kind;public readonly int Hand;public readonly Vector3 Origin,Velocity;internal R23GrenadeLaunch(string id,string kind,int hand,Vector3 p,Vector3 v){ItemId=id;Kind=kind;Hand=hand;Origin=p;Velocity=v;}}
    public struct R23ProjectileReadout
    {public string ItemId,Kind;public Vector3 Position;public float Age,ZoneAge,Radius,GroundRoll;public int ZoneTicks;public bool Stuck,Active;}
    // Passive input/custody port. Only owned grenade bodies have independent FixedUpdate motion.
    public sealed partial class R23VRExplosives:MonoBehaviour
    {
        sealed class Item {internal string Id,Kind;internal Vector3 Position,InitialPosition;internal Quaternion Rotation,InitialRotation;internal GameObject View;internal bool Visible=true;internal int Revision;}
        readonly Dictionary<string,Item> items=new Dictionary<string,Item>(StringComparer.Ordinal);readonly R23GrenadeState state=new R23GrenadeState();
        readonly List<R23NativeGrenade> projectiles=new List<R23NativeGrenade>();readonly List<Vector3> preview=new List<Vector3>();
        Transform actor,head,left,right,content,fragTemplate,zoneTemplate,flashTemplate,smokeTemplate;IR23ExplosiveEffects effects;R23GrenadeAssets assets;Coroutine loading;
        R23ExplosiveInputFrame input;Mesh arcMesh,zoneMesh;GameObject arc;Material arcFrag,arcZone,zoneMaterial;bool worldVisible=true,simulationEnabled;float sfx=1;
        readonly AudioSource[] voices=new AudioSource[16];readonly float[] voiceGain=new float[16];int voiceCursor;
        readonly R24ExplosionVisuals[] explosionVisuals=new R24ExplosionVisuals[8];R24ExplosionVisuals.Library explosionMeshes;int effectCursor;
        public string VisualFailure {get;private set;}
        public const int ProjectileLayer=21,MaximumProjectiles=64;
        internal const int WorldMask=(1<<0)|(1<<8)|(1<<19);
        public bool Ready {get{return assets!=null&&assets.Ready;}}public string Failure {get{return assets==null?null:assets.Failure;}}
        public string HeldId {get{return state.HeldId;}}public int HeldHand {get{return state.HeldHand;}}
        public bool LeftOccupied {get{return HeldHand==1;}}public bool RightOccupied {get{return HeldHand==2;}}
        public R23OwnedGrenade[] OwnedItems {get{return state.OwnedItems;}}public int LiveProjectiles {get{return projectiles.Count;}}
        public int SlotCapacity {get{return state.SlotCapacity;}set{state.SlotCapacity=value;}}
        public bool TrajectoryVisible {get{return arc!=null&&arc.activeSelf;}}public Vector3[] TrajectoryPoints {get{return preview.ToArray();}}
        public Vector3 HeldGripContactWorld {get{var hand=Hand(HeldHand);return hand==null?Vector3.zero:hand.position+Grip(hand)*new Vector3(0,-.057f,-.025f);}}
        public event Action InventoryChanged;public event Action<R23GrenadeLaunch> Thrown;public event Action<R23GrenadeBlast> Blast;
        public event Action<int,float,float> Haptic;
        public Func<Vector3,Vector3,bool> LaunchLineClear;
        public Func<string,int,bool> SharedClaim,SharedThrow;public Func<string,Vector3,bool> SharedDrop;
        public string[] WorldItemIds{get{return new List<string>(items.Keys).ToArray();}}
        internal bool ReconcileNetworkItem(string id,bool owned,bool spent){bool ok=state.ReconcileNetworkItem(id,owned,spent);if(ok)Changed();return ok;}
        internal Ashfall.Integration.Playable.FlashSmoke.R25FlashSmokePresentation FlashSmoke;
        public Func<string,bool> KindAllowed;
        string ReadHeldKind(){R23GrenadeItemState i;return state.TryRead(state.HeldId,out i)?i.Kind:"";}
        bool Allowed(string kind){return KindAllowed==null||KindAllowed(kind);}
        public float TargetDamageMultiplier=1;
        internal bool SimulationEnabled {get{return simulationEnabled&&isActiveAndEnabled;}}
        internal Material ZoneMaterial {get{return zoneMaterial;}}internal Mesh ZoneMesh {get{return zoneMesh;}}
        public void Initialize(Transform playerRoot,Transform headPose,Transform leftPose,Transform rightPose,IR23ExplosiveEffects damagePort)
        {
            if(actor!=null)throw new InvalidOperationException("Explosives initialized");if(playerRoot==null||headPose==null||leftPose==null||rightPose==null||damagePort==null)throw new ArgumentException("accepted rig poses and real damage port required");
            actor=playerRoot;head=headPose;left=leftPose;right=rightPose;effects=damagePort;content=new GameObject("R23 grenade items and projectiles").transform;
            fragTemplate=new GameObject("R23 actual frag template").transform;fragTemplate.SetParent(content,false);fragTemplate.gameObject.SetActive(false);
            zoneTemplate=new GameObject("R23 actual zone template").transform;zoneTemplate.SetParent(content,false);zoneTemplate.gameObject.SetActive(false);
            flashTemplate=R25ThrowableArt.Create(content,"flash");flashTemplate.gameObject.SetActive(false);smokeTemplate=R25ThrowableArt.Create(content,"smoke");smokeTemplate.gameObject.SetActive(false);
            arcFrag=Unlit(new Color(1,.81960785f,.49803922f,1),false);arcZone=Unlit(new Color(.81960785f,.63529414f,1,1),false);var mistShader=Resources.Load<Shader>("R24ZoneMist");if(mistShader==null)throw new InvalidOperationException("Zone mist shader missing");zoneMaterial=new Material(mistShader);zoneMaterial.SetColor("_BaseColor",new Color(.55f,.15f,.95f,.06f));zoneMesh=SphereMesh();
            arc=new GameObject("Source large grenade trajectory");arc.layer=2;arc.transform.SetParent(content,false);arcMesh=new Mesh();arc.AddComponent<MeshFilter>().sharedMesh=arcMesh;arc.AddComponent<MeshRenderer>().sharedMaterial=arcFrag;arc.SetActive(false);
            for(int i=0;i<voices.Length;i++){var go=new GameObject("R23 grenade cue "+i);go.transform.SetParent(content,false);voices[i]=go.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=1;voices[i].dopplerLevel=0;voices[i].rolloffMode=AudioRolloffMode.Logarithmic;voices[i].minDistance=1;}
            FlashSmoke=gameObject.AddComponent<Ashfall.Integration.Playable.FlashSmoke.R25FlashSmokePresentation>();FlashSmoke.Initialize(this,head);assets=new R23GrenadeAssets();assets.TrackArt(flashTemplate);assets.TrackArt(smokeTemplate);loading=StartCoroutine(Load());
            var explosionShader=Resources.Load<Shader>("R24ExplosionSurface");if(explosionShader==null)VisualFailure="Original source explosion shader missing";else{explosionMeshes=new R24ExplosionVisuals.Library();for(int i=0;i<explosionVisuals.Length;i++)explosionVisuals[i]=new R24ExplosionVisuals(content,explosionMeshes,explosionShader);}
        }
        IEnumerator Load(){yield return assets.Load(fragTemplate,zoneTemplate);loading=null;if(Ready)foreach(var item in items.Values)CreateView(item);}
        static Material Unlit(Color color,bool transparent)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",color);m.SetFloat("_Cull",0);if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}return m;}
        internal static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}internal static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        static bool Finite(Vector3 p){return F(p).IsFinite;}static Quaternion Grip(Transform hand){return hand.rotation*R23TrackedGripPose.LocalRotation;}
        static bool Finite(Quaternion q){return new Float3(q.x,q.y,q.z).IsFinite&&!float.IsNaN(q.w)&&!float.IsInfinity(q.w)&&Quaternion.Dot(q,q)>.5f;}
        Transform Hand(int hand){return hand==1?left:hand==2?right:null;}
        bool HandReady(int hand){return hand==1?input.LeftReady:hand==2&&input.RightReady;}
        public void SetOtherSlotsUsed(int slots){state.SetOtherSlotsUsed(slots);}
        public bool CanStore(string kind){return state.CanStore(kind);}
        public bool TryReadItem(string id,out R23GrenadeItemState item){return state.TryRead(id,out item);}
        public bool CanCollect(string id){R23GrenadeItemState item;return Ready&&state.TryRead(id,out item)&&!item.Owned&&!item.Spent&&state.CanStore(item.Kind);}
        public bool Spawn(string id,string kind,Vector3 position,Quaternion rotation)
        {
            if(!Allowed(kind)||actor==null||!Finite(position)||!Finite(rotation)||!state.Register(id,kind))return false;rotation=rotation.normalized;
            var item=new Item{Id=id,Kind=kind,Position=position,InitialPosition=position,Rotation=rotation,InitialRotation=rotation};items.Add(id,item);if(Ready)CreateView(item);return true;
        }
        public bool Spawn(string id,string kind,Vector3 position){return Spawn(id,kind,position,Quaternion.identity);}
        Transform Template(string kind){return kind=="flash"?flashTemplate:kind=="smoke"?smokeTemplate:kind=="zone"?zoneTemplate:fragTemplate;}
        static float ArtScale(string kind){return kind=="zone"?2:.9f;}
        void CreateView(Item item)
        {R23GrenadeItemState value;if(item.View!=null||!state.TryRead(item.Id,out value)||value.Spent||!value.Owned&&!item.Visible)return;item.View=Instantiate(Template(item.Kind).gameObject,content);item.View.name="R23 "+item.Kind+" "+item.Id;item.View.transform.localScale=Vector3.one*ArtScale(item.Kind);item.View.transform.SetPositionAndRotation(item.Position,item.Rotation);item.View.SetActive(worldVisible);QueueWorldSettle(item);}
        public bool GrantOwned(string id,string kind,int hand,bool handAvailable)
        {if(!handAvailable||hand<1||hand>2||!state.CanStore(kind)||!Spawn(id,kind,Hand(hand).position)||!state.Pickup(id,hand,true))return false;Changed();return true;}
        public bool TryEquip(string id,int hand,bool handAvailable){if(!state.Equip(id,hand,handAvailable))return false;Changed();return true;}
        public void Stow(){state.Stow();Changed();}
        public bool TryReadWorldItem(string id,out Vector3 position,out Quaternion rotation,out bool owned)
        {position=Vector3.zero;rotation=Quaternion.identity;owned=false;Item i;R23GrenadeItemState s;if(id==null||!items.TryGetValue(id,out i)||!state.TryRead(id,out s)||s.Spent)return false;position=i.Position;rotation=i.Rotation;owned=s.Owned;return true;}
        public event Action<string,Vector3> WorldItemMoved;
        public bool RepositionWorldItem(string id,Vector3 position,bool settle=true)
        {Item i;R23GrenadeItemState s;if(!Finite(position)||id==null||!items.TryGetValue(id,out i)||!state.TryRead(id,out s)||s.Owned||s.Spent)return false;if((i.Position-position).sqrMagnitude<.0000001f)return true;i.Revision++;i.Position=i.InitialPosition=position;if(i.View!=null)i.View.transform.position=position;if(settle)QueueWorldSettle(i);if(WorldItemMoved!=null)WorldItemMoved(id,position);return true;}
        public void SetWorldVisible(bool visible){worldVisible=visible;RefreshViews();}
        public bool SetWorldItemVisible(string id,bool visible){Item i;if(id==null||!items.TryGetValue(id,out i))return false;bool changed=i.Visible!=visible,hadView=i.View!=null;if(changed)i.Revision++;i.Visible=visible;if(visible&&Ready){CreateView(i);if(changed&&hadView)QueueWorldSettle(i);}RefreshViews();return true;}
        public bool Drop(string id,Vector3 position,Quaternion rotation)
        {Item item;if(id==null||!Finite(position)||!Finite(rotation)||!items.TryGetValue(id,out item)||SharedDrop!=null&&!SharedDrop(id,position)||!state.Drop(id))return false;item.Revision++;item.Position=position;item.Rotation=rotation.normalized;item.Visible=true;Changed();QueueWorldSettle(item);return true;}
        public bool Drop(string id){return Drop(id,actor.position+actor.forward*.5f+Vector3.up*.3f,Quaternion.identity);}
        public bool GetDropBounds(string id,out Bounds bounds)
        {
            bounds=new Bounds();Item item;if(!Ready||id==null||!items.TryGetValue(id,out item))return false;bool found=false;var root=item.View==null?Template(item.Kind):item.View.transform;
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))for(int k=0;k<8;k++){var b=mesh.sharedMesh.bounds;var p=root.InverseTransformPoint(mesh.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))))*ArtScale(item.Kind);if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);}return found;
        }
        public Bounds GetDropBounds(string id){Bounds b;GetDropBounds(id,out b);return b;}
        public bool DropSlot(int slot,Vector3[] positions,Quaternion[] rotations)
        {
            if(positions==null||rotations==null)return false;var members=new List<R23OwnedGrenade>();foreach(var i in OwnedItems)if(i.Slot==slot)members.Add(i);
            if(members.Count<1||members.Count>2||positions.Length!=members.Count||rotations.Length!=members.Count)return false;
            for(int i=0;i<members.Count;i++)if(!Finite(positions[i])||!Finite(rotations[i])||!items.ContainsKey(members[i].Id))return false;
            if(SharedDrop!=null)for(int i=0;i<members.Count;i++)if(!SharedDrop(members[i].Id,positions[i]))return false;
            if(!state.DropSlot(slot))return false;for(int i=0;i<members.Count;i++){var item=items[members[i].Id];item.Revision++;item.Position=positions[i];item.Rotation=rotations[i].normalized;item.Visible=true;}Changed();foreach(var member in members)QueueWorldSettle(items[member.Id]);return true;
        }
        // Projection-only: Root may call before motion/aim arbitration without pumping fire/time.
        public void UpdateInputAvailability(R23ExplosiveInputFrame frame){input=frame;SetSimulation(frame.Enabled&&isActiveAndEnabled);RefreshViews();}
        public bool TryReadAimedWorldItem(int hand,out string id,out Vector3 center,out float distance)
        {
            id=null;center=Vector3.zero;distance=float.PositiveInfinity;var h=Hand(hand);if(!SimulationEnabled||!worldVisible||!HandReady(hand)||h==null||!Clear(head.position,h.position))return false;
            Vector3 forward=Grip(h)*Vector3.forward;
            foreach(var item in items.Values){R23GrenadeItemState s;state.TryRead(item.Id,out s);if(s.Owned||s.Spent||!item.Visible)continue;Vector3 delta=item.Position-h.position;float d=delta.magnitude;if(d>2||d>distance||(d>.35f&&Vector3.Dot(forward,delta/d)<=.94f)||!Clear(h.position,item.Position))continue;id=item.Id;center=item.Position;distance=d;}return id!=null;
        }
        public bool TryPickup(string id,int hand,bool handAvailable){return TryPickupToHand(id,hand,hand,handAvailable);}
        public bool CanReceivePickup(int hand){return SimulationEnabled&&HandReady(hand);}
        public bool TryPickupToHand(string id,int hand,int receivingHand,bool handAvailable)
        {
            Item item;var h=Hand(hand);if(!Ready||!SimulationEnabled||!handAvailable||!HandReady(hand)||h==null||!worldVisible||id==null||!items.TryGetValue(id,out item)||!item.Visible)return false;
            Vector3 delta=item.Position-h.position;float distance=delta.magnitude;if(distance>2||(distance>.35f&&Vector3.Dot(Grip(h)*Vector3.forward,delta/distance)<=.94f)||!Clear(head.position,h.position)||!Clear(h.position,item.Position)||!CanReceivePickup(receivingHand)||SharedClaim!=null&&!SharedClaim(id,hand)||!PickupOrEquip(id,receivingHand))return false;Changed();return true;
        }
        bool PickupOrEquip(string id,int hand){R23GrenadeItemState item;return state.TryRead(id,out item)&&item.Owned?state.Equip(id,hand,true):state.Pickup(id,hand,true);}
        bool Clear(Vector3 from,Vector3 to)
        {
            if(!Finite(from)||!Finite(to))return false;if(LaunchLineClear!=null)return LaunchLineClear(from,to);Vector3 delta=to-from;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,WorldMask,QueryTriggerInteraction.Ignore)){var t=hit.collider.transform;if(t==actor||t.IsChildOf(actor))continue;return false;}return true;
        }
        public void Tick(float seconds,R23ExplosiveInputFrame frame)
        {
            if(actor==null)return;input=frame;bool enabled=frame.Enabled&&isActiveAndEnabled;SetSimulation(enabled);if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0){Suspend();return;}seconds=Mathf.Min(.25f,seconds);state.Advance(seconds);
            if(enabled)foreach(var fx in explosionVisuals)if(fx!=null)fx.Step(seconds);
            var hand=Hand(HeldHand);if(!Ready||!enabled||hand==null||!HandReady(HeldHand)){state.Suspend();if(arc!=null)arc.SetActive(false);RefreshViews();return;}
            var rotation=Grip(hand);Vector3 origin=hand.position+rotation*new Vector3(0,.045f,.10f),velocity=V(R23GrenadeMath.LaunchVelocity(F(rotation*Vector3.forward),F(rotation*Vector3.right),Mathf.Max(0,origin.y-actor.position.y)));
            bool down=HeldHand==1?frame.LeftTrigger>.65f||frame.LeftTriggerClick:frame.RightTrigger>.65f||frame.RightTriggerClick;int heldHand=HeldHand;R23OwnedGrenade consumed;
            if(state.TryThrow(down&&Allowed(state.HeldId==null?"":ReadHeldKind()),true,projectiles.Count<MaximumProjectiles&&Finite(velocity)&&Clear(head.position,origin),SharedThrow==null?null:(Func<R23OwnedGrenade,bool>)delegate(R23OwnedGrenade item){SharedThrow(item.Id,heldHand);return false;},out consumed))
            {
                var launch=new R23GrenadeLaunch(consumed.Id,consumed.Kind,heldHand,origin,velocity);Launch(launch);Play("grenade_throw",origin,.31622777f,14,UnityEngine.Random.Range(.985f,1.015f),false);if(Haptic!=null)Haptic(heldHand,.45f,.06f);if(Thrown!=null)Thrown(launch);Changed();
            }
            RefreshViews();UpdateArc();
        }
        void RefreshViews()
        {
            foreach(var item in items.Values)
            {
                R23GrenadeItemState s;state.TryRead(item.Id,out s);bool held=HeldId==item.Id;bool show=!s.Spent&&(held?SimulationEnabled&&HandReady(HeldHand):!s.Owned&&worldVisible&&item.Visible);if(item.View==null&&show&&Ready)CreateView(item);if(item.View==null)continue;item.View.SetActive(show);
                if(held&&Hand(HeldHand)!=null){var h=Hand(HeldHand);var q=Grip(h);item.View.transform.SetPositionAndRotation(h.position+q*new Vector3(0,-.015f,.03f),q);}
                else if(!s.Owned)item.View.transform.SetPositionAndRotation(item.Position,item.Rotation);
            }
        }
        void Changed(){RefreshViews();if(InventoryChanged!=null)InventoryChanged();}
        void UpdateArc()
        {
            preview.Clear();if(!Ready||!SimulationEnabled||HeldId==null||!HandReady(HeldHand)){arc.SetActive(false);return;}R23GrenadeItemState item;state.TryRead(HeldId,out item);var h=Hand(HeldHand);var q=Grip(h);Vector3 at=h.position+q*new Vector3(0,.045f,.10f),speed=V(R23GrenadeMath.LaunchVelocity(F(q*Vector3.forward),F(q*Vector3.right),Mathf.Max(0,at.y-actor.position.y)));preview.Add(at);float max=Ashfall.Integration.Playable.FlashSmoke.R25Throwables.ArcSeconds(item.Kind),elapsed=0;
            for(int i=0;i<100;i++){float dt=Mathf.Min(.06f,max-elapsed);if(dt<=0)break;Vector3 next=at+speed*dt+Vector3.down*(9.8f*.5f*dt*dt);RaycastHit hit;bool blocked=Ray(at,next,out hit);preview.Add(blocked?hit.point:next);if(blocked)break;speed+=Vector3.down*(9.8f*dt);at=next;elapsed+=dt;}
            var v=new Vector3[Mathf.Max(0,preview.Count-1)*6];var ix=new int[v.Length];for(int i=1;i<preview.Count;i++){Vector3 side=Vector3.Cross((preview[i]-preview[i-1]).normalized,head.position-preview[i]).normalized*.042f;int p=(i-1)*6;v[p]=preview[i-1]-side;v[p+1]=preview[i-1]+side;v[p+2]=preview[i]+side;v[p+3]=v[p];v[p+4]=v[p+2];v[p+5]=preview[i]-side;}for(int i=0;i<ix.Length;i++)ix[i]=i;arcMesh.Clear();arcMesh.vertices=v;arcMesh.triangles=ix;arcMesh.RecalculateBounds();arc.GetComponent<Renderer>().sharedMaterial=item.Kind=="zone"?arcZone:arcFrag;arc.SetActive(true);
        }
        internal bool Ray(Vector3 from,Vector3 to,out RaycastHit result)
        {result=new RaycastHit();float nearest=float.PositiveInfinity;foreach(var h in Physics.RaycastAll(from,(to-from).normalized,(to-from).magnitude,WorldMask,QueryTriggerInteraction.Ignore)){if(h.collider.transform==actor||h.collider.transform.IsChildOf(actor)||h.distance>=nearest)continue;nearest=h.distance;result=h;}return nearest<float.PositiveInfinity;}
        void Launch(R23GrenadeLaunch launch)
        {
            var go=new GameObject("R23 live "+launch.Kind+" "+launch.ItemId);go.layer=ProjectileLayer;go.transform.SetParent(content,false);go.transform.position=launch.Origin;
            var visual=Instantiate(Template(launch.Kind).gameObject,go.transform);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one*ArtScale(launch.Kind)*(.035f/.047f);visual.SetActive(true);
            var projectile=go.AddComponent<R23NativeGrenade>();projectiles.Add(projectile);projectile.Initialize(this,actor,launch,assets.Clip(launch.Kind+"_warning"));projectile.SetSfx(sfx);foreach(var other in projectiles)if(other!=projectile)Physics.IgnoreCollision(projectile.Shape,other.Shape);
        }
        internal void Deliver(R23GrenadeBlast blast)
        {if(Blast!=null)Blast(blast);blast.Apply(effects,F(head.position),Mathf.Max(0,TargetDamageMultiplier));}
        internal void Finished(R23NativeGrenade projectile){projectiles.Remove(projectile);}
        internal void Play(string action,Vector3 position,float gain,float distance,float pitch=1,bool effect=false)
        {
            if(effect)PlaySourceVisual(action=="zone_bloom"?"zone":action=="rocket_impact"?"smaw":"frag",position);
            var clip=assets.Clip(action);if(clip==null)return;int i=voiceCursor;voiceCursor=(voiceCursor+1)%voices.Length;var voice=voices[i];voice.Stop();voice.transform.position=position;voice.clip=clip;voice.pitch=pitch;voiceGain[i]=gain;voice.volume=gain*sfx;voice.maxDistance=distance;voice.Play();
        }
        // Shared source SMAW presenter may request graphics only; damage/audio remain its real owners.
        public bool PlaySourceVisual(string kind,Vector3 position)
        {
            if(!SimulationEnabled||!Finite(position)||(kind!="frag"&&kind!="zone"&&kind!="smaw")||explosionMeshes==null)return false;
            bool grounded=false;Vector3 ground=position;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(position+Vector3.up*.25f,Vector3.down,3.25f,1<<0,QueryTriggerInteraction.Ignore))
            {if(hit.collider.transform.IsChildOf(actor)||hit.distance>=nearest)continue;nearest=hit.distance;ground=hit.point;grounded=hit.normal.y>.55f;}
            var fx=explosionVisuals[effectCursor];effectCursor=(effectCursor+1)%explosionVisuals.Length;fx.Play(kind,position,grounded,ground);return true;
        }
        public R24ExplosionVisualState[] ReadSourceVisuals(){var result=new List<R24ExplosionVisualState>();foreach(var fx in explosionVisuals)if(fx!=null)result.Add(fx.Read());return result.ToArray();}
        void SetSimulation(bool value)
        {if(simulationEnabled==value)return;simulationEnabled=value;foreach(var p in projectiles)p.SetPaused(!value);foreach(var voice in voices)if(voice!=null){if(value)voice.UnPause();else voice.Pause();}if(!value){state.Suspend();if(FlashSmoke!=null)FlashSmoke.Release();}}
        public void SetSfxVolume(float volume){float next=float.IsNaN(volume)?0:Mathf.Clamp01(volume);for(int i=0;i<voices.Length;i++)if(voices[i]!=null)voices[i].volume=voiceGain[i]*next;sfx=next;foreach(var p in projectiles)p.SetSfx(next);}
        public void Suspend(){SetSimulation(false);state.Suspend();if(arc!=null)arc.SetActive(false);RefreshViews();}
        public void ClearProjectiles(){if(FlashSmoke!=null)FlashSmoke.Clear();foreach(var p in projectiles.ToArray())if(p!=null){p.Cancel();Destroy(p.gameObject);}projectiles.Clear();foreach(var voice in voices)if(voice!=null)voice.Stop();foreach(var fx in explosionVisuals)if(fx!=null)fx.Clear();}
        public R23ProjectileReadout[] ReadProjectiles(){var result=new List<R23ProjectileReadout>();foreach(var p in projectiles)if(p!=null)result.Add(p.Read());return result.ToArray();}
        public void ResetRound(){ClearProjectiles();state.ResetRound();var removed=new List<string>();foreach(var pair in items){R23GrenadeItemState value;if(!state.TryRead(pair.Key,out value)){if(pair.Value.View!=null){pair.Value.View.SetActive(false);Destroy(pair.Value.View);}removed.Add(pair.Key);}else{pair.Value.Revision++;pair.Value.Position=pair.Value.InitialPosition;pair.Value.Rotation=pair.Value.InitialRotation;}}foreach(var id in removed)items.Remove(id);Changed();foreach(var item in items.Values)QueueWorldSettle(item);}
        void OnDisable(){Suspend();}void OnApplicationPause(bool paused){if(paused)Suspend();}
        void OnDestroy(){ClearSharedProjectiles();if(loading!=null)StopCoroutine(loading);ClearProjectiles();foreach(var fx in explosionVisuals)if(fx!=null)fx.Dispose();if(explosionMeshes!=null)explosionMeshes.Dispose();if(content!=null)Destroy(content.gameObject);if(assets!=null)assets.Dispose();foreach(var r in new UnityEngine.Object[]{arcMesh,zoneMesh,arcFrag,arcZone,zoneMaterial})if(r!=null)Destroy(r);}
        static Mesh SphereMesh()
        {
            const int x=24,y=13;var v=new Vector3[(x+1)*(y+1)];var ix=new int[x*y*6];int at=0;for(int j=0;j<=y;j++)for(int i=0;i<=x;i++){float u=i*Mathf.PI*2/x,w=j*Mathf.PI/y;int p=j*(x+1)+i;v[p]=new Vector3(Mathf.Sin(w)*Mathf.Cos(u),Mathf.Cos(w),Mathf.Sin(w)*Mathf.Sin(u));if(i<x&&j<y){ix[at++]=p;ix[at++]=p+1;ix[at++]=p+x+1;ix[at++]=p+1;ix[at++]=p+x+2;ix[at++]=p+x+1;}}var m=new Mesh();m.name="Source zone sphere24x12";m.vertices=v;m.normals=v;m.triangles=ix;m.RecalculateBounds();return m;
        }
    }
}
#endif
