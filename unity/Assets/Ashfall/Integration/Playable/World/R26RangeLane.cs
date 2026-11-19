#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;
using Ashfall.Integration.Playable.Explosives;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.World
{
    // The local offline simulation and host use the same lane. Moving box positions
    // follow the room clock, including replay of a late launch on the host.
    public sealed class R26RangeLane:MonoBehaviour
    {
        public const string MapId=R23MapCatalog.RangeId;public const int MoverLayer=24;public const float FlashSeconds=.18f;
        // Map-local firing line, 8 m behind the range spawn; the lane runs out over the open -Z floor edge.
        static readonly Vector3 FiringLine=new Vector3(0,0,-616);
        sealed class Target
        {
            public float Speed,Phase,Top,Flash;public bool Moving;public Vector3 From,To;
            public Transform Node;public Collider Collider;public MeshRenderer[] Faces;public Material Rest;public int ImpactFrame=-1;
        }
        R23PlayableLoop root;Target[] targets;Transform lane,resetButton;Collider resetCollider;Font font;Mesh cube;
        readonly List<Material> owned=new List<Material>();Material plate,mover,flash,post,stripe,floor,wall,rail,button,center;
        bool bound,built,failed,active,pokeHeld;int cueFrame=-1;float resetCooldown;
        bool rocketPending;double rocketTime;
        public bool Active{get{return active;}}
        public int TotalHits{get;private set;}
        public int TargetCount{get{return targets==null?0:targets.Length;}}
        public void Initialize(R23PlayableLoop loop)
        {
            if(loop==null||loop.Gameplay==null)throw new ArgumentNullException("loop");if(root!=null)throw new InvalidOperationException("Range lane already initialized");
            root=loop;var g=root.Gameplay;g.BulletLane=this;g.SurfaceImpact+=OnImpact;g.WorldHit+=OnWorldHit;g.AcceptedRocketLaunch+=OnRocketLaunch;g.SmawDetonated+=OnRocketBlast;bound=true;
        }
        void OnDestroy()
        {
            if(bound&&root!=null&&root.Gameplay!=null){var g=root.Gameplay;g.BulletLane=null;g.SurfaceImpact-=OnImpact;g.WorldHit-=OnWorldHit;g.AcceptedRocketLaunch-=OnRocketLaunch;g.SmawDetonated-=OnRocketBlast;}
            bound=false;foreach(var m in owned)if(m!=null)Destroy(m);owned.Clear();
        }
        public void SetVisible(bool show)
        {
            show&=!failed&&root!=null&&root.Map!=null&&root.Map.IsLoaded&&root.Map.MapId==MapId;if(show==active)return;
            if(show){if(!Build())return;var map=root.Map.transform;lane.SetPositionAndRotation(map.TransformPoint(FiringLine),map.rotation*Quaternion.Euler(0,180,0));}
            active=show;if(lane!=null)lane.gameObject.SetActive(show);rocketPending=false;pokeHeld=false;if(show){PlaceMovers();Physics.SyncTransforms();}
        }
        public void ResetLane()
        {
            resetCooldown=.75f;TotalHits=0;rocketPending=false;
            if(targets!=null)foreach(var t in targets){t.Flash=0;t.ImpactFrame=-1;Paint(t,false);}
        }
        // Called once per frame before the weapon step, so moved colliders are synced before this frame's shots.
        public void Tick(float dt)
        {
            if(!active)return;
            PlaceMovers();
            for(int i=0;i<targets.Length;i++){var t=targets[i];if(t.Flash>0){t.Flash-=dt;if(t.Flash<=0)Paint(t,false);}}
            if(rocketPending&&Time.timeAsDouble-rocketTime>R24SmawRuntime.ProjectileSeconds+1)rocketPending=false;
            TickResetButton(dt);
        }
        void PlaceMovers()
        {
            double clock=SharedClock();bool moved=false;
            for(int i=0;i<targets.Length;i++){var t=targets[i];if(!t.Moving)continue;t.Node.localPosition=PathPoint(t.From,t.To,t.Speed,t.Phase,clock);moved=true;}
            if(moved)Physics.SyncTransforms();
        }
        // Deterministic ping-pong at constant speed along From-To; phase in [0,1).
        public static Vector3 PathPoint(Vector3 from,Vector3 to,float speed,float phase,double clock)
        {
            float length=Vector3.Distance(from,to);if(!(length>0)||!(speed>0))return from;
            double period=2*length/speed,p=(clock/period+phase)%1;if(p<0)p+=1;float u=(float)(p<.5?p*2:2-p*2);return Vector3.Lerp(from,to,u);
        }
        double SharedClock()
        {
            var session=root.LanSession;var lobby=session==null?null:session.Lobby;
            if(lobby!=null&&(lobby.State==R24LanLobbyState.Hosting||lobby.State==R24LanLobbyState.Connected))return lobby.EstimatedHostTime;
            return Time.timeAsDouble;
        }
        // ---------- live weapon events ----------
        void OnImpact(Vector3 point,Collider collider)
        {
            if(!active||collider==null)return;
            if(collider==resetCollider){if(resetCooldown<=0)ResetLane();return;}
            var target=Find(collider);if(target!=null)Register(target);
        }
        void OnWorldHit(Collider collider,float amount)
        {
            if(!active||collider==null)return;var target=Find(collider);if(target==null)return;
            Register(target);Feedback(target,amount);
        }
        void OnRocketLaunch(string action,int hand){if(!active)return;rocketPending=true;rocketTime=Time.timeAsDouble;}
        void OnRocketBlast(R23GrenadeBlast blast)
        {
            if(!active||!rocketPending||blast==null||!blast.Center.IsFinite)return;rocketPending=false;
            var center=new Vector3(blast.Center.X,blast.Center.Y,blast.Center.Z);Target target=null;float best=2.5f;
            foreach(var candidate in targets){float distance=Vector3.Distance(candidate.Collider.ClosestPoint(center),center);if(distance<best){best=distance;target=candidate;}}
            if(target==null)return;
            Register(target);var point=target.Node.position+Vector3.up*(target.Top*.5f);
            Feedback(target,blast.DamageAt(new Float3(point.x,point.y,point.z)));
        }
        internal void SweepMovers(Vector3 origin,Vector3 direction,float length,double t0,double t1,ref R26BulletSurface hit)
        {
            if(!active)return;
            Vector3 a=lane.InverseTransformPoint(origin),delta=lane.InverseTransformVector(direction*length);
            for(int i=0;i<targets.Length;i++){
                var t=targets[i];if(!t.Moving)continue;
                var start=PathPoint(t.From,t.To,t.Speed,t.Phase,t0);var end=PathPoint(t.From,t.To,t.Speed,t.Phase,t1);
                var box=(BoxCollider)t.Collider;var rel=delta-(end-start);var from=a-start-box.center;var half=box.size*.5f;
                float near=0,far=1;int face=-1;
                for(int axis=0;axis<3;axis++){
                    if(Mathf.Abs(rel[axis])<1e-7f){if(Mathf.Abs(from[axis])>half[axis]){far=-1;break;}continue;}
                    float u=(-half[axis]-from[axis])/rel[axis],v=(half[axis]-from[axis])/rel[axis];
                    float enter=Mathf.Min(u,v);if(enter>near){near=enter;face=axis;}far=Mathf.Min(far,Mathf.Max(u,v));if(near>far)break;
                }
                float distance=near*length;if(near>far||distance>=hit.Distance)continue;
                var normal=-direction;if(face>=0){var local=Vector3.zero;local[face]=-Mathf.Sign(rel[face]);normal=lane.TransformDirection(local);}
                hit.Distance=distance;hit.Normal=new Float3(normal.x,normal.y,normal.z);hit.RangeTarget=(byte)(i+1);
            }
        }
        internal bool MovingCollider(Collider c){if(!active||c.gameObject.layer!=MoverLayer)return false;var target=Find(c);return target!=null&&target.Moving;}
        internal byte ContactId(Collider c){
            if(!active||c==null)return 0;if(c==resetCollider)return 255;
            for(int i=0;i<targets.Length;i++)if(targets[i].Collider==c)return (byte)(i+1);return 0;
        }
        internal Collider ContactCollider(byte id){return !active||id==0?null:id==255?resetCollider:id<=targets.Length?targets[id-1].Collider:null;}
        internal void ConfirmRangeDamage(Collider c,float amount){OnWorldHit(c,amount);}
        Target Find(Collider collider){foreach(var target in targets)if(target.Collider==collider)return target;return null;}
        void Register(Target target)
        {
            int frame=Time.frameCount;if(target.ImpactFrame==frame)return;
            target.ImpactFrame=frame;TotalHits++;Paint(target,true);target.Flash=FlashSeconds;
        }
        void Feedback(Target t,float amount)
        {
            var numbers=root.CombatDamageNumbers;if(numbers!=null&&amount>0)numbers.Emit(amount,t.Node.position+Vector3.up*t.Top,false);
            if(cueFrame!=Time.frameCount&&root.Gameplay!=null&&root.Gameplay.PlayRangeTargetHitCue())cueFrame=Time.frameCount;
        }
        void Paint(Target t,bool hit){if(t.Faces==null)return;foreach(var face in t.Faces)if(face!=null)face.sharedMaterial=hit?flash:t.Rest;}
        void TickResetButton(float dt)
        {
            if(resetCooldown>0)resetCooldown-=dt;var rig=root.Rig;if(rig==null||resetButton==null)return;
            Vector3 at=resetButton.position;bool touching=Near(rig.Left,at)||Near(rig.Right,at);
            if(touching&&!pokeHeld&&resetCooldown<=0)ResetLane();pokeHeld=touching;
        }
        static bool Near(Transform hand,Vector3 at){return hand!=null&&(hand.position-at).sqrMagnitude<.17f*.17f;}
        // ---------- build ----------
        bool Build()
        {
            if(built)return true;
            try{BuildLane();built=true;ResetLane();return true;}
            catch(Exception e){failed=true;Debug.LogWarning("R26 range lane disabled: "+e.Message);if(lane!=null)Destroy(lane.gameObject);lane=null;targets=null;return false;}
        }
        void BuildLane()
        {
            font=Resources.Load<Font>("R23InventoryUI");if(font==null)font=Resources.Load<Font>("R23GodotUI");if(font==null)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);temp.SetActive(false);cube=temp.GetComponent<MeshFilter>().sharedMesh;Destroy(temp);
            plate=Unlit("Range plate",new Color(.98f,.47f,.13f));center=Unlit("Range plate centre",new Color(.10f,.12f,.13f));mover=Unlit("Range moving target",new Color(.30f,.78f,.87f));flash=Unlit("Range hit flash",new Color(1,.96f,.62f));
            post=Unlit("Range post",new Color(.20f,.24f,.26f));stripe=Unlit("Range distance stripe",new Color(.86f,.86f,.80f));rail=Unlit("Range mover rail",new Color(.16f,.19f,.21f));
            button=Unlit("Range reset button",new Color(.85f,.14f,.12f));
            floor=Lit("Range lane floor",new Color(.2823529f,.3294118f,.3568627f),.35f,.35f);wall=Lit("Range lane backstop",new Color(.1490196f,.2039216f,.2274510f),.4f,.38f);
            lane=new GameObject("R26 range lane (map-local firing line)").transform;lane.SetParent(transform,false);lane.gameObject.SetActive(false);
            var mirror=new GameObject("R26 full avatar sandbox mirror").AddComponent<R26RangeMirror>();mirror.transform.SetParent(lane,false);mirror.transform.localPosition=new Vector3(-5,1.3f,-12);mirror.transform.localRotation=Quaternion.LookRotation(new Vector3(5,0,4));mirror.Initialize(root);
            // Lane space: +Z is downrange, +X is the shooter's right, y=0 is the range floor top.
            // The range floor ends 34 m downrange; this strip carries the lane to the 171 m backstop.
            Box("Lane floor extension",lane,new Vector3(0,-.2f,103),new Vector3(30,.4f,138),floor,true,0);
            Box("Lane backstop",lane,new Vector3(0,4,171),new Vector3(30,8,1),wall,true,0);
            Box("Firing line",lane,new Vector3(0,.006f,0),new Vector3(10,.012f,.1f),stripe,false,0);
            var list=new List<Target>();
            list.Add(Plate(10,-4,1.3f,.45f));list.Add(Plate(25,-6.5f,1.5f,.6f));list.Add(Plate(50,-3,1.8f,.8f));list.Add(Plate(100,2.5f,2.4f,1.2f));list.Add(Plate(150,8,3.2f,1.6f));
            list.Add(Mover("WALK",new Vector3(1,0,20),new Vector3(9,0,20),1.5f,0));
            list.Add(Mover("RUN",new Vector3(-6,0,35),new Vector3(6,0,35),4,.31f));
            list.Add(Mover("SPRINT",new Vector3(-9,0,60),new Vector3(9,0,60),6,.62f));
            list.Add(Mover("APPROACH",new Vector3(12,0,90),new Vector3(12,0,30),3,.17f));
            list.Add(Mover("FAR RUN",new Vector3(-9,0,110),new Vector3(9,0,110),4,.43f));
            list.Add(Mover("FAR SPRINT",new Vector3(-10,0,165),new Vector3(10,0,165),8,.13f));
            targets=list.ToArray();
            // Reset: touch with either hand, or shoot it.
            var reset=new GameObject("Range reset").transform;reset.SetParent(lane,false);reset.localPosition=new Vector3(1.2f,0,1.2f);reset.localRotation=Quaternion.LookRotation(new Vector3(1.2f,0,1.2f));
            Box("Reset post",reset,new Vector3(0,.47f,0),new Vector3(.08f,.94f,.08f),post,false,0);
            resetButton=Box("Reset button",reset,new Vector3(0,1,0),new Vector3(.24f,.12f,.24f),button,true,MoverLayer);resetCollider=resetButton.GetComponent<Collider>();
            Text("RESET",reset,new Vector3(0,1.18f,0),.010f);
        }
        Target Plate(float distance,float x,float height,float size)
        {
            string label=distance.ToString("0",CultureInfo.InvariantCulture)+" m";
            var node=new GameObject("Range plate "+label).transform;node.SetParent(lane,false);node.localPosition=new Vector3(x,0,distance);
            float postHeight=height-size*.5f;Box("Plate post",node,new Vector3(0,postHeight*.5f,0),new Vector3(.08f,postHeight,.08f),post,false,0);
            var face=Box("Plate",node,new Vector3(0,height,0),new Vector3(size,size,.05f),plate,true,0);
            Box("Plate aim point",node,new Vector3(0,height,-.03f),new Vector3(size*.28f,size*.28f,.01f),center,false,0);
            Box("Distance stripe",node,new Vector3(0,.006f,0),new Vector3(2.2f,.012f,.1f),stripe,false,0);
            Text(label,node,new Vector3(0,height+size*.5f+.10f+distance*.001f,0),Mathf.Max(.009f,distance*.00048f));
            return new Target{Top=height+size*.5f,Node=node,Collider=face.GetComponent<Collider>(),Faces=new[]{face.GetComponent<MeshRenderer>()},Rest=plate};
        }
        Target Mover(string kind,Vector3 from,Vector3 to,float speed,float phase)
        {
            bool approach=Mathf.Abs(to.z-from.z)>Mathf.Abs(to.x-from.x);
            var mid=(from+to)*.5f;float length=Vector3.Distance(from,to);
            Box("Mover rail "+kind,lane,new Vector3(mid.x,.02f,mid.z),approach?new Vector3(.12f,.04f,length+.6f):new Vector3(length+.6f,.04f,.12f),rail,false,0);
            var node=new GameObject("Range mover "+kind).transform;node.gameObject.layer=MoverLayer;node.SetParent(lane,false);node.localPosition=from;
            var body=node.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.None;
            var hitbox=node.gameObject.AddComponent<BoxCollider>();hitbox.center=new Vector3(0,1.2f,0);hitbox.size=new Vector3(.5f,1.5f,.14f);
            Box("Mover pole",node,new Vector3(0,.23f,0),new Vector3(.06f,.46f,.06f),post,false,MoverLayer);
            var torso=Box("Mover body",node,new Vector3(0,1.0f,0),new Vector3(.5f,1.1f,.12f),mover,false,MoverLayer);
            var head=Box("Mover head",node,new Vector3(0,1.78f,0),new Vector3(.26f,.3f,.12f),mover,false,MoverLayer);
            return new Target{Speed=speed,Phase=phase,Moving=true,From=from,To=to,Top=1.95f,Node=node,Collider=hitbox,Faces=new[]{torso.GetComponent<MeshRenderer>(),head.GetComponent<MeshRenderer>()},Rest=mover};
        }
        Transform Box(string name,Transform parent,Vector3 at,Vector3 size,Material material,bool collider,int layer)
        {
            var go=new GameObject(name);go.layer=layer;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;
            go.AddComponent<MeshFilter>().sharedMesh=cube;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            if(collider)go.AddComponent<BoxCollider>();return go.transform;
        }
        void Text(string value,Transform parent,Vector3 at,float size)
        {
            var go=new GameObject("Range label "+value);go.transform.SetParent(parent,false);go.transform.localPosition=at;
            var text=go.AddComponent<TextMesh>();text.font=font;text.fontSize=48;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;text.richText=false;text.text=value;
            R26WorldText.Bind(text);var renderer=go.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        Material Unlit(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.name=name;m.SetColor("_BaseColor",color);owned.Add(m);return m;}
        Material Lit(string name,Color color,float metallic,float smoothness){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);owned.Add(m);return m;}
    }
}
#endif
