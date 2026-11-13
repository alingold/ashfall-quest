#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Calibration;
using Ashfall.Integration.Playable.Explosives;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.Playable.Bots
{
    [Serializable] public sealed class R24KestrelDischarge
    {
        public int sequence;public Vector3 muzzle,lockedAim,actualHead,endpoint;public string obstruction="";
        public bool inRange,clear,constructed,guarded,queryOverflow;public float appliedPlayerDamage;
    }
    // The original Relay Valley normal campaign encounters. This owner has
    // no network authority or fabricated campaign/objective completion.
    public sealed class R24ValleyBots:MonoBehaviour
    {
        public const int CombatLayer=24;
        const int WorldMask=(1<<0)|(1<<8);
        const int SightMask=WorldMask|(1<<10)|(1<<19)|(1<<CombatLayer);
        readonly List<Agent> agents=new List<Agent>(8);readonly R24RagdollWorld corpses=new R24RagdollWorld();
        public readonly R24ValleyMissionState Mission=new R24ValleyMissionState();
        readonly RaycastHit[] rayHits=new RaycastHit[128];
        readonly Collider[] routeInside=new Collider[64];
        R24GuardRoutes routes;string routeMapIdentity;
        public int CompletedRouteQueries{get{return routes==null?0:routes.CompletedQueries;}}public int PendingRouteQueries{get{return routes==null?0:routes.PendingCount;}}
        R23PlayableLoop root;string mapIdentity;GameObject encounter;Light[] lights=new Light[0];
        bool active,reportedFailure,ownsSkinWeights;SkinWeights previousSkinWeights;float accumulator;int generation;
        Material warningMaterial,streakMaterial;R24DamageNumbers damageNumbers;float sfxVolume=1,difficulty=.5f;
        public bool EncounterActive {get{return active;}}
        public float Difficulty {get{return difficulty;}}
        public void SetDifficulty(float value){difficulty=R24KestrelBrain.Finite(value)?Mathf.Clamp01(value):.5f;foreach(var agent in agents)agent.Brain.SetDifficulty(difficulty);}
        public bool Ready {get;private set;}
        public string Failure {get;private set;}
        public int Generation {get{return generation;}}
        public int Count {get{return agents.Count;}}
        public Transform EncounterTransform {get{return encounter==null?null:encounter.transform;}}
        public int CorpseCount{get{return corpses.Count;}}public int CorpseBodyCount{get{return corpses.BodyCount;}}public string CorpseFailure{get{return corpses.Failure;}}
        public int LivingCount {get{int count=0;foreach(var a in agents)if(a.Brain.State!=R24KestrelState.Dead)count++;return count;}}
        public float MaximumDetection {get{float detection=0;foreach(var a in agents)if(a.Node.activeInHierarchy)detection=Mathf.Max(detection,a.Brain.Detection);return detection;}}
        public int Shots {get;private set;}
        public int CoverStops {get;private set;}
        public int PlayerHits {get;private set;}
        public int GuardBlocks {get;private set;}
        public int OverflowRefusals {get;private set;}
        public int DamageNumberCount {get{return damageNumbers==null?0:damageNumbers.ActiveCount;}}
        public long DamageNumbersEmitted {get{return damageNumbers==null?0:damageNumbers.Emitted;}}
        public float LastDamageNumber {get{return damageNumbers==null?0:damageNumbers.LastAmount;}}
#if UNITY_EDITOR
        public bool CaptureRouteDiagnostics {get;set;}
        readonly Queue<string> routeDiagnostics=new Queue<string>();
        public int FixtureRouteReturnQueries {get;private set;}
        public int FixtureRouteReturns {get;private set;}
        public void ResetRouteDiagnostics(){routeDiagnostics.Clear();FixtureRouteReturnQueries=FixtureRouteReturns=0;}
        public string RouteDiagnostics {get{return String.Join("\n",routeDiagnostics.ToArray());}}
        void RecordRoute(Agent agent,string phase,string detail)
        {if(!CaptureRouteDiagnostics)return;if(routeDiagnostics.Count>=512)routeDiagnostics.Dequeue();routeDiagnostics.Enqueue("slot="+agent.Brain.Slot+" age="+agent.Brain.Age.ToString("F3")+" phase="+phase+" position="+agent.Position.ToString("F5")+" state="+agent.Brain.State+" sight="+agent.Brain.HasSight+" lost="+agent.Brain.LostTime.ToString("F3")+" "+detail);}
        public R24DamageNumbers FixtureDamageNumbers {get{return damageNumbers;}}
        public bool FixturePlaceRouteActor(int index,Vector3 mapLocalFeet)
        {if(!Ready||index<0||index>=agents.Count||agents[index].Brain.State==R24KestrelState.Dead)return false;agents[index].FixturePlace(root.Map.transform.TransformPoint(mapLocalFeet));Physics.SyncTransforms();return true;}
        public bool FixtureMoveRouteActor(int index,Float3 mapLocalDirection,float dt)
        {if(!Ready||!active||index<0||index>=agents.Count||dt<=0||dt>.25f||!mapLocalDirection.IsFinite)return false;var actor=agents[index];actor.AdvancePresentation(dt);actor.Move(dt,SafeDirection(actor,root.Map.transform.TransformDirection(U(mapLocalDirection))));actor.Present(dt);return true;}
        public R24GuardRoutes.Traversal FixtureActorRoute(int index){return index>=0&&index<agents.Count?agents[index].FixtureRoute:null;}
#endif
        public string Scope {get{return "Relay Valley four normal missions; authored guard corridors CPU port; bounded source corpses; extended dam/spillway/network authority separate";}}

        public void Initialize(R23PlayableLoop owner)
        {
            if(root!=null||owner==null)throw new ArgumentException("Bot Root owner");root=owner;sfxVolume=owner.UI.Settings.SfxVolume;difficulty=owner.UI.Settings.BotDifficulty;routes=new R24GuardRoutes(RouteRay);
            // New enemy body role: source world+enemy body collisions, never the
            // tracked player or ballistic/player/foot-only proxy surfaces.
            for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(CombatLayer,layer,layer!=0&&layer!=8&&layer!=CombatLayer);
        }
        public void Step(float dt,bool worldVisible,bool simulate)
        {
            if(!R24KestrelBrain.Finite(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            bool eligible=worldVisible&&root.Map!=null&&root.Map.IsLoaded&&root.Map.MapId=="valley";
            if(!eligible){if(encounter!=null)Clear();return;}
            string identity=root.Map.MapId+"/"+root.Map.ManifestSha256;
            if(encounter==null||mapIdentity!=identity)Spawn(identity);
            // Root advances the shared practice/bot pool once per presentation step.
            Ready=true;foreach(var a in agents){if(a.ArtFailure!=null){Failure=a.ArtFailure;Ready=false;}else Ready&=a.ArtReady;}
            corpses.SetPaused(!simulate||!Ready||Failure!=null);
            if(Failure!=null){if(!reportedFailure){Debug.LogError("Valley source bots unavailable: "+Failure,this);reportedFailure=true;}foreach(var a in agents)a.SetCollision(false);return;}
            if(!Ready){accumulator=0;foreach(var a in agents){a.SetCollision(false);a.HideTransient();a.Audio.Stop();}return;}
            foreach(var a in agents)if(a.Brain.State!=R24KestrelState.Dead)a.SetCollision(true);
            if(!simulate){accumulator=0;foreach(var a in agents){a.HideTransient();a.Audio.Stop();}return;}
            if(!active&&root.Map.transform.InverseTransformPoint(root.Rig.transform.position).z>-1)active=true;
            accumulator+=dt;
            // Match source physics cadence; no unbounded catch-up after a stall.
            int steps=0;while(accumulator>=.02f&&steps++<13){accumulator-=.02f;Tick(.02f);}
        }
        void Spawn(string identity)
        {
            Clear();if(routeMapIdentity!=identity){routes.InvalidateStaticCache();routeMapIdentity=identity;}generation++;mapIdentity=identity;Failure=null;reportedFailure=false;
            encounter=new GameObject("Relay Valley source encounter "+Mission.Chapter);encounter.transform.SetParent(root.Map.transform,false);
            damageNumbers=root.CombatDamageNumbers;if(damageNumbers==null)throw new InvalidOperationException("Actual shared combat feedback owner required");
            var warning=Shader.Find("Ashfall/R24 Kestrel line");if(warning==null){Failure="Missing source bot warning/streak shader";return;}
            warningMaterial=new Material(warning){name="KESTREL warning (shared)"};warningMaterial.SetColor("_Tint",new Color(1,.12f,.02f,.7f));warningMaterial.SetVector("_Clearance",new Vector4(.65f,1.15f,0,0));
            streakMaterial=new Material(warning){name="KESTREL shot (shared)"};streakMaterial.SetColor("_Tint",new Color(1,.77f,.38f,.9f));streakMaterial.SetVector("_Clearance",new Vector4(.45f,.85f,0,0));
            previousSkinWeights=QualitySettings.skinWeights;ownsSkinWeights=previousSkinWeights!=SkinWeights.Unlimited;if(ownsSkinWeights)QualitySettings.skinWeights=SkinWeights.Unlimited;
            lights=root.Map.GetComponentsInChildren<Light>(true);
            Vector3 approach=root.Map.transform.TransformPoint(U(Mission.GuardApproach));
            for(int i=0;i<Mission.EnemyCount;i++)agents.Add(new Agent(this,i,Mission.VariantAt(i),root.Map.transform.TransformPoint(U(Mission.SpawnAt(i))),approach));
            active=Mission.BeginsAlerted;
        }
        public bool AdvanceMission()
        {
            if(!Ready||root.Gameplay.Health<=0||!Mission.Advance(LivingCount,true))return false;
            root.Gameplay.ApplyPlayerGain(Mathf.Max(0,100-root.Gameplay.Health),Mathf.Max(0,100-root.Gameplay.Shield));
            string identity=mapIdentity;Spawn(identity);return true;
        }
        void Tick(float dt)
        {
            corpses.Step(dt);
            routes.ProcessOne(requester=>{var agent=requester as Agent;return agent!=null&&!agent.Retired;});
            Vector3 feet=root.Rig.transform.position,head=root.Rig.Head.transform.position;
            float speed=root.Rig.MotionVelocity.magnitude;float exposure=-1;
            foreach(var a in agents){
                a.AdvancePresentation(dt);
                if(a.Brain.State==R24KestrelState.Dead){a.TickDeath(dt);continue;}
                if(!active){a.Move(dt,Vector3.zero);a.Present(dt);continue;}
                float elapsed;if(a.Brain.SensorDue(dt,out elapsed)){
                    float distance=Vector3.Distance(head,a.Position+Vector3.up*1.4f);
                    bool clear=distance<a.Brain.SightRange&&a.Brain.InFieldOfView(F(a.Position),F(a.Yaw.forward),F(head))&&ClearSight(a,head);
                    if(exposure<0)exposure=ReadExposure(head);
                    a.Brain.Observe(elapsed,F(a.Position),F(feet),F(head),speed,exposure,clear);
                    if(a.Brain.TryRadio())foreach(var other in agents)if(other!=a&&Vector3.Distance(other.Position,a.Position)<=24)other.Brain.ReceiveRadio(a.Brain.LastFeet,a.Brain.LastAim);
                }
                var priorState=a.Brain.State;Vector3 desired=U(a.Brain.Tick(dt,F(a.Position),a.PrepareNavigation,a.HasNavigation,a.FollowNavigation));if(priorState!=R24KestrelState.Windup&&a.Brain.State==R24KestrelState.Windup){
                    if(a.Brain.NeedsGroundMark){RaycastHit mark;bool overflow;Vector3 foot=U(a.Brain.LastFeet);bool found=Ray(foot+Vector3.up*.7f,foot-Vector3.up*6,WorldMask,a,out mark,out overflow);a.Brain.ResolveGroundMark(found&&!overflow,F(mark.point+Vector3.up*.06f));}
                    a.Audio.Play("charge");
                }desired=Separate(a,desired);a.Move(dt,SafeDirection(a,desired));a.Present(dt);
                if(a.Brain.ShotRequested)Fire(a,head);
            }
        }
        R24GuardRoutes.Hit RouteRay(Float3 from,Float3 to)
        {
            // Same synchronous save/restore pattern as the existing head/ping
            // adapters. Source ray queries include backfaces; never leave a
            // changed global policy behind or alter combat-ray filtering.
            bool backfaces=Physics.queriesHitBackfaces;
            try
            {
                Physics.queriesHitBackfaces=true;var frame=root.Map.transform;Vector3 origin=frame.TransformPoint(U(from));
                // Source hit_from_inside returns a zero normal for primitive or
                // convex shapes. ClosestPoint is unsupported on concave meshes,
                // which the source flag likewise does not cover. The tiny broad
                // query only finds candidates; exact closest-point equality is
                // required before reporting an inside hit.
                int count=Physics.OverlapSphereNonAlloc(origin,.00001f,routeInside,WorldMask,QueryTriggerInteraction.Ignore);
                if(count>=routeInside.Length){OverflowRefusals++;return new R24GuardRoutes.Hit(from,new Float3());}
                for(int i=0;i<count;i++){var collider=routeInside[i];var mesh=collider as MeshCollider;if(collider==null||mesh!=null&&!mesh.convex||!(collider is BoxCollider||collider is SphereCollider||collider is CapsuleCollider||mesh!=null))continue;Vector3 point=collider.ClosestPoint(origin);if(point.x==origin.x&&point.y==origin.y&&point.z==origin.z)return new R24GuardRoutes.Hit(from,new Float3());}
                RaycastHit hit;bool overflow;bool found=Ray(origin,frame.TransformPoint(U(to)),WorldMask,null,out hit,out overflow);if(overflow)return new R24GuardRoutes.Hit(from,new Float3());return found?new R24GuardRoutes.Hit(F(frame.InverseTransformPoint(hit.point)),F(frame.InverseTransformDirection(hit.normal))):new R24GuardRoutes.Hit();
            }
            finally{Physics.queriesHitBackfaces=backfaces;}
        }
        Vector3 Separate(Agent agent,Vector3 desired)
        {
            if(agent.Brain.State==R24KestrelState.Windup)return desired;Vector3 push=Vector3.zero;
            foreach(var peer in agents){if(peer==agent||peer.Brain.State==R24KestrelState.Dead||Mathf.Abs(peer.Position.y-agent.Position.y)>1.7f)continue;Vector3 away=agent.Position-peer.Position;away.y=0;float d=away.magnitude,gap=agent.Brain.Variant==3||peer.Brain.Variant==3?2.4f:1.8f;if(d>=gap)continue;
                if(d<.001f){float axis=Math.Min(agent.Brain.Slot,peer.Brain.Slot)*2.399963229728653f;away=new Vector3(Mathf.Cos(axis),0,-Mathf.Sin(axis))*(agent.Brain.Slot>peer.Brain.Slot?1:-1);}else away/=d;
                push+=away*(1-d/gap)*2;
            }
            return push.sqrMagnitude<.0001f?desired:Vector3.ClampMagnitude(desired+push*1.8f,1);
        }
        Vector3 SafeDirection(Agent a,Vector3 desired)
        {
            if(desired.sqrMagnitude<=.01f)return desired;Vector3 direction=desired.normalized;RaycastHit hit;
            Vector3 origin=a.Position+Vector3.up*(a.Brain.Variant==1||a.Brain.Variant==3?1.05f:.65f);
            bool overflow;bool found=Ray(origin,origin+direction,WorldMask,a,out hit,out overflow);
            if(found){if(a.AvoidTime<=0){Vector3 tangent=Vector3.Cross(Vector3.up,hit.normal).normalized;if(Mathf.Sin(a.Brain.Phase)<0)tangent=-tangent;a.AvoidDirection=(tangent+hit.normal*.08f).normalized;}a.AvoidTime=.7f;}
#if UNITY_EDITOR
            if(CaptureRouteDiagnostics)RecordRoute(a,"safe","desired="+desired.ToString("F5")+" wall="+(found&&hit.collider!=null?hit.collider.name:"none")+" distance="+hit.distance+" normal="+hit.normal.ToString("F5")+" avoidTime="+a.AvoidTime+" avoid="+a.AvoidDirection.ToString("F5")+" overflow="+overflow);
#endif
            if(overflow)return Vector3.zero;if(a.AvoidTime>0)direction=a.AvoidDirection;
            if(a.Brain.Variant!=1&&a.Brain.Variant!=3&&a.Body.isGrounded){Vector3 ahead=a.Position+direction*.7f+Vector3.up*.6f;bool supported=Ray(ahead,ahead-Vector3.up*1.25f,WorldMask,a,out hit,out overflow);
#if UNITY_EDITOR
                if(CaptureRouteDiagnostics)RecordRoute(a,"ledge","from="+ahead.ToString("F5")+" to="+(ahead-Vector3.up*1.25f).ToString("F5")+" supported="+supported+" overflow="+overflow+" hit="+(supported&&hit.collider!=null?hit.collider.name:"none"));
#endif
                if(overflow)return Vector3.zero;if(!supported)return ReturnToRoute(a,origin);}

            return direction;
        }
        Vector3 ReturnToRoute(Agent a,Vector3 origin)
        {
            // Only a live accepted route can propose a correction. Physics still
            // owns movement; overflow, walls or missing floor retain refusal.
            Vector3 inward=a.ReturnDirection();if(inward.sqrMagnitude<=.01f)return Vector3.zero;RaycastHit hit;bool overflow;
#if UNITY_EDITOR
            FixtureRouteReturnQueries++;
#endif
            bool wall=Ray(origin,origin+inward,WorldMask,a,out hit,out overflow);
#if UNITY_EDITOR
            if(CaptureRouteDiagnostics)RecordRoute(a,"return-wall","direction="+inward.ToString("F5")+" wall="+wall+" overflow="+overflow+" hit="+(wall&&hit.collider!=null?hit.collider.name:"none"));
#endif
            if(wall||overflow)return Vector3.zero;Vector3 ahead=a.Position+inward*.7f+Vector3.up*.6f;
#if UNITY_EDITOR
            FixtureRouteReturnQueries++;
#endif
            bool supported=Ray(ahead,ahead-Vector3.up*1.25f,WorldMask,a,out hit,out overflow);
#if UNITY_EDITOR
            if(CaptureRouteDiagnostics)RecordRoute(a,"return-floor","direction="+inward.ToString("F5")+" supported="+supported+" overflow="+overflow+" hit="+(supported&&hit.collider!=null?hit.collider.name:"none")+" point="+hit.point.ToString("F5"));
#endif
            if(!supported||overflow)return Vector3.zero;
#if UNITY_EDITOR
            FixtureRouteReturns++;
#endif
            return inward;
        }
        bool ClearSight(Agent a,Vector3 head)
        {RaycastHit hit;bool overflow;bool found=Ray(a.Position+Vector3.up*1.4f,head,SightMask,a,out hit,out overflow);return !overflow&&(!found||IsPlayer(hit.collider));}
        bool IsPlayer(Collider collider){return collider!=null&&(collider.transform==root.Rig.transform||collider.transform.IsChildOf(root.Rig.transform));}
        // Fixed buffer overflow refuses visibility/fire, never silently shoots
        // through an omitted nearer obstruction. Every query excludes only self.
        bool Ray(Vector3 from,Vector3 to,int mask,Agent self,out RaycastHit nearest,out bool overflow)
        {
            nearest=new RaycastHit();Vector3 delta=to-from;float distance=delta.magnitude;overflow=false;if(distance<.00001f)return false;
            int count=Physics.RaycastNonAlloc(from,delta/distance,rayHits,distance,mask,QueryTriggerInteraction.Ignore);if(count>=rayHits.Length){OverflowRefusals++;overflow=true;return false;}
            float best=float.PositiveInfinity;bool found=false;for(int i=0;i<count;i++){var hit=rayHits[i];if(hit.collider==null||self!=null&&hit.collider.transform.IsChildOf(self.Node.transform))continue;if(hit.distance<best){best=hit.distance;nearest=hit;found=true;}}return found;
        }
        float ReadExposure(Vector3 point)
        {
            float exposure=.12f;RaycastHit hit;bool overflow;Light sun=RenderSettings.sun;
            if(sun!=null&&sun.isActiveAndEnabled&&!Ray(point,point-sun.transform.forward*220,WorldMask,null,out hit,out overflow)&&!overflow)exposure+=Mathf.Clamp01(sun.intensity/1.15f)*.72f;
            foreach(var light in lights){if(light==null||!light.isActiveAndEnabled||light.type==LightType.Directional)continue;Vector3 delta=point-light.transform.position;float distance=delta.magnitude;if(distance>=light.range||light.range<=0)continue;
                if(light.type==LightType.Spot&&Vector3.Angle(light.transform.forward,delta)>light.spotAngle*.5f)continue;
                if(Ray(light.transform.position,point,WorldMask,null,out hit,out overflow)||overflow)continue;float falloff=1-distance/light.range;exposure+=falloff*falloff*Mathf.Min(light.intensity,3)*.32f;
            }
            return Mathf.Clamp01(exposure);
        }
        void Fire(Agent a,Vector3 actualHead)
        {
            Vector3 from=a.Muzzle,to=U(a.Brain.LockedAim);var report=new R24KestrelDischarge{sequence=a.LastDischarge==null?1:a.LastDischarge.sequence+1,muzzle=from,lockedAim=to,actualHead=actualHead,inRange=Vector3.Distance(from,to)<=a.Brain.EngageRange};a.LastDischarge=report;
            if(!report.inRange){a.Brain.CompleteShot(false);return;}
            a.Audio.Play("fire");
            a.StartBurst();
            if(a.Brain.Variant==3&&a.Brain.AreaAttack){ResolveGroundBlast(a,actualHead,report);a.Brain.CompleteShot(true);return;}
            RaycastHit hit;bool overflow;bool found=Ray(from,to,SightMask,a,out hit,out overflow);report.queryOverflow=overflow;if(overflow){a.Brain.CompleteShot(true);return;}
            Vector3 endpoint=found?hit.point:to;bool clear=!found||IsPlayer(hit.collider);
            report.clear=clear;report.obstruction=found?hit.collider.name:"";
            // Capture construction ownership BEFORE damage can retire its collider.
            Ashfall.Modules.Construction.PlacedPanel panel;bool constructed=found&&root.Activities!=null&&root.Activities.TryReadPanel(hit.collider,out panel);
            if(constructed){root.Activities.TryDamageCollider(hit.collider,(a.Brain.Variant==3?36:a.Brain.Variant==2?32:24)*a.Brain.DamageScale);CoverStops++;}
            Vector3 guard=Vector3.zero;bool blocked=!constructed&&clear&&root.Gameplay.TryInterceptMeleeBullet(from,endpoint,out guard);if(blocked){endpoint=guard;GuardBlocks++;}
            a.ShowShot(from,endpoint);Shots++;
            float before=root.Gameplay.Health+root.Gameplay.Shield;
            if(!constructed&&clear&&!blocked&&Vector3.Distance(to,actualHead)<.42f){root.Gameplay.ApplyPlayerDamage((a.Brain.Variant==3?16:a.Brain.Variant==2?19:12)*a.Brain.DamageScale);if(root.Gameplay.Health+root.Gameplay.Shield<before)PlayerHits++;}
            else if(!clear&&!constructed)CoverStops++;
            report.constructed=constructed;report.guarded=blocked;report.endpoint=endpoint;report.appliedPlayerDamage=before-root.Gameplay.Health-root.Gameplay.Shield;
            a.Brain.CompleteShot(true);
        }
        void ResolveGroundBlast(Agent a,Vector3 actualHead,R24KestrelDischarge report)
        {
            Vector3 point=U(a.Brain.ImpactPoint),feet=root.Rig.transform.position;report.endpoint=point;a.ImpactFlash=.35f;a.Audio.Play("death",true);Shots++;
            float horizontal=Vector2.Distance(new Vector2(feet.x,feet.z),new Vector2(point.x,point.z));if(horizontal>=2||Mathf.Abs(feet.y-point.y)>1.5f)return;
            RaycastHit hit;bool overflow;bool found=Ray(point+Vector3.up*.2f,actualHead,SightMask,a,out hit,out overflow);report.queryOverflow=overflow;if(overflow)return;
            if(found){report.obstruction=hit.collider.name;Ashfall.Modules.Construction.PlacedPanel panel;if(root.Activities!=null&&root.Activities.TryReadPanel(hit.collider,out panel)){report.constructed=true;root.Activities.TryDamageCollider(hit.collider,40*a.Brain.DamageScale);CoverStops++;return;}if(!IsPlayer(hit.collider)){CoverStops++;return;}}
            report.clear=true;float before=root.Gameplay.Health+root.Gameplay.Shield;root.Gameplay.ApplyPlayerDamage(Mathf.Lerp(18,8,horizontal/2)*a.Brain.DamageScale);report.appliedPlayerDamage=before-root.Gameplay.Health-root.Gameplay.Shield;if(report.appliedPlayerDamage>0)PlayerHits++;
        }
        public void NotifyGunshot(Vector3 origin,float loudness=1)
        {if(!Ready)return;foreach(var a in agents)a.Brain.HearShot(F(origin),F(a.Position),loudness);if(loudness>0&&agents.Exists(a=>Vector3.Distance(a.Position,origin)<=Mathf.Clamp(80*loudness,0,120)))active=true;}
        Agent Find(Collider collider){if(collider==null)return null;foreach(var a in agents)if(collider==a.Body||collider==a.Head)return a;return null;}
        public string TargetId(Collider collider){var a=Find(collider);return a!=null&&a.Brain.State!=R24KestrelState.Dead?a.Id:null;}
        public void Hit(Collider collider,float amount){var a=Find(collider);if(a!=null)Damage(a,amount);}
        bool Damage(Agent a,float amount)
        {if(!Ready)return false;float before=a.Brain.Health;if(!a.Brain.TakeDamage(amount,F(root.Rig.transform.position),F(root.Rig.Head.transform.position)))return false;if(damageNumbers!=null)damageNumbers.Emit(Mathf.Max(0,before-a.Brain.Health),a.Position+Vector3.up*1.85f,false);active=true;a.Audio.Play("hit",true);if(a.Brain.State==R24KestrelState.Dead){Mission.EnemyDefeated();root.SpawnValleyEnemySupply(a.Id,a.Position+Vector3.up*.75f);a.Audio.Play("death");a.SetCollision(false);a.HideTransient();a.SpawnCorpse();}return true;}
        public void SetSfxVolume(float value){if(!R24KestrelBrain.Finite(value))throw new ArgumentOutOfRangeException("sfx");sfxVolume=Mathf.Clamp01(value);foreach(var a in agents)a.Audio.SetSfxVolume(sfxVolume);}
        public bool ApplyTargetDamage(string id,float amount){foreach(var a in agents)if(a.Id==id)return Damage(a,amount);return false;}
        public void AppendTargets(List<R23ExplosiveTarget> targets){foreach(var a in agents)if(Ready&&a.Brain.State!=R24KestrelState.Dead)targets.Add(new R23ExplosiveTarget(a.Id,F(a.Position+Vector3.up*.7f)));}
        public bool TryRead(int index,out R24KestrelBrain brain,out Vector3 position,out bool grounded)
        {brain=null;position=Vector3.zero;grounded=false;if(index<0||index>=agents.Count)return false;var a=agents[index];brain=a.Brain;position=a.Position;grounded=a.Body.enabled&&a.Body.isGrounded;return true;}
        public R24KestrelDischarge ReadDischarge(int index){return index>=0&&index<agents.Count?agents[index].LastDischarge:null;}
        public void Clear()
        {
            if(damageNumbers!=null){damageNumbers.Clear();damageNumbers=null;}
            corpses.Clear();if(routes!=null)routes.CancelPending();foreach(var a in agents){a.Retired=true;a.SetCollision(false);a.HideTransient();a.Audio.Stop();}agents.Clear();if(encounter!=null){encounter.SetActive(false);Destroy(encounter);}encounter=null;
            if(ownsSkinWeights&&QualitySettings.skinWeights==SkinWeights.Unlimited)QualitySettings.skinWeights=previousSkinWeights;ownsSkinWeights=false;
            if(warningMaterial!=null)Destroy(warningMaterial);if(streakMaterial!=null)Destroy(streakMaterial);warningMaterial=streakMaterial=null;mapIdentity=null;active=Ready=false;Failure=null;accumulator=0;Shots=CoverStops=PlayerHits=GuardBlocks=OverflowRefusals=0;
        }
        void OnDestroy(){Clear();}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
        static Vector3 U(Float3 v){return new Vector3(v.X,v.Y,v.Z);}

        sealed class Agent
        {
            readonly R24ValleyBots owner;readonly LineRenderer warning;readonly LineRenderer[] streaks=new LineRenderer[3];readonly float[] streakTimes=new float[3];int cursor;
            Vector3 velocity;float pitch,deathTime,flashTime;Vector3 deathPosition;Quaternion deathYaw;bool deathStarted,artPublished,corpseAttempted;
            public bool Retired;readonly R24GuardRoutes.Intent routeIntent=new R24GuardRoutes.Intent();R24GuardRoutes.Traversal route;
            public readonly Action PrepareNavigation;public readonly Func<bool> HasNavigation;public readonly Func<float,Float3> FollowNavigation;
            public readonly GameObject Node;public readonly Transform Yaw;public readonly CharacterController Body;public readonly BoxCollider Head;public readonly R24KestrelBrain Brain;
            public readonly R24KestrelVisual Visual;public readonly R24WardenVisual Warden;public readonly R24KestrelAudio Audio;public readonly string Id;
            public bool ArtReady {get{return Visual!=null?Visual.Ready:Warden!=null&&Warden.Ready;}}
            public string ArtFailure {get{return Visual!=null?Visual.Failure:Warden!=null?Warden.Failure:"Missing source actor art owner";}}
            public R24KestrelDischarge LastDischarge;
            public float AvoidTime,ImpactFlash;public Vector3 AvoidDirection;
            public Vector3 Position{get{return Node.transform.position;}}
            public Vector3 Muzzle{get{var marker=Visual!=null?Visual.Muzzle:Warden.Muzzle;return marker!=null?marker.position:Position+Vector3.up*1.4f;}}
            public Agent(R24ValleyBots owner,int slot,int variant,Vector3 position,Vector3 approach)
            {
                this.owner=owner;PrepareNavigation=PrepareRoute;HasNavigation=RouteActive;FollowNavigation=FollowRoute;Id="r24-valley-bot/"+owner.generation+"/"+slot;Brain=new R24KestrelBrain(variant,slot,F(position),F(approach),owner.difficulty,owner.generation*7919+slot*101);
                Node=new GameObject("KESTREL "+variant+" / "+slot);Node.layer=CombatLayer;Node.transform.SetParent(owner.encounter.transform,false);Node.transform.position=position;
                Body=Node.AddComponent<CharacterController>();float ratio=variant==1||variant==3?1:R23CalibrationState.TargetEye/1.605f;
                Body.center=Vector3.up*(variant==3?1.1f:.88f)*ratio;Body.radius=(variant==3?.85f:variant==2?.55f:.42f)*ratio;Body.height=(variant==3?2.2f:1.65f)*ratio;Body.slopeLimit=48;Body.stepOffset=.39f;Body.skinWidth=.005f;Body.minMoveDistance=0;
                Body.enabled=false;
                Yaw=new GameObject("Source visual yaw").transform;Yaw.SetParent(Node.transform,false);Yaw.localRotation=Quaternion.Euler(0,Brain.GuardYaw*Mathf.Rad2Deg,0);Yaw.localScale=Vector3.one*ratio;
                if(variant==3){Warden=Yaw.gameObject.AddComponent<R24WardenVisual>();Warden.Initialize();}else{Visual=Yaw.gameObject.AddComponent<R24KestrelVisual>();Visual.Initialize(variant);}
                Audio=Node.AddComponent<R24KestrelAudio>();Audio.SetSfxVolume(owner.sfxVolume);Audio.Initialize(variant,Brain.EngageRange);
                var head=new GameObject("Animated source head hit shape");head.layer=CombatLayer;head.transform.SetParent(Node.transform,false);Head=head.AddComponent<BoxCollider>();Head.size=new Vector3(.28f,.32f,.28f)*ratio;Head.enabled=false;
                warning=MakeLine("Source locked warning",owner.warningMaterial,.02f);for(int i=0;i<3;i++)streaks[i]=MakeLine("Source enemy streak "+i,owner.streakMaterial,.01f);
            }
            bool RouteActive(){return route!=null&&!route.Finished&&!route.Aborted;}
#if UNITY_EDITOR
            public R24GuardRoutes.Traversal FixtureRoute{get{return route;}}
            public void FixturePlace(Vector3 worldFeet){SetCollision(false);Node.transform.position=worldFeet;velocity=Vector3.zero;AvoidTime=0;AvoidDirection=Vector3.zero;SetCollision(true);}
#endif
            void PrepareRoute()
            {
                if(Retired||!routeIntent.TryBegin(Brain.Variant,Brain.HasLastKnown,Brain.HasSight,Brain.LostTime,F(Position),Brain.LastFeet))return;var goal=U(Brain.LastFeet);
                float gain=Brain.Difficulty<=.5f?Mathf.Lerp(.85f,1,Brain.Difficulty*2):Mathf.Lerp(1,1.45f,(Brain.Difficulty-.5f)*2);float speed=(Brain.Variant==2?1.4f:2.2f)*gain;var frame=owner.root.Map.transform;route=owner.routes.RequestRoute(this,F(frame.InverseTransformPoint(Position)),F(frame.InverseTransformPoint(goal)),speed,Brain.Difficulty);
            }
            public Vector3 ReturnDirection()
            {if(!RouteActive())return Vector3.zero;var frame=owner.root.Map.transform;return frame.TransformDirection(U(route.ReturnToSegment(F(frame.InverseTransformPoint(Position)))));}
            Float3 FollowRoute(float dt)
            {var frame=owner.root.Map.transform;var direction=route.Direction(F(frame.InverseTransformPoint(Position)),dt);
#if UNITY_EDITOR
                if(owner.CaptureRouteDiagnostics)owner.RecordRoute(this,"follow","index="+route.index+" count="+route.PointCount+" pending="+route.Pending+" finished="+route.Finished+" aborted="+route.Aborted+" reason="+route.AbortReason+" point="+(route.index<route.PointCount?U(route.PointAt(route.index)).ToString("F5"):"end")+" direction="+U(direction).ToString("F5"));
#endif
                Brain.ResolveRouteTraversal(route.Finished,route.Aborted);return F(frame.TransformDirection(U(direction)));}
            LineRenderer MakeLine(string name,Material material,float width)
            {
                var go=new GameObject(name);go.layer=CombatLayer;go.transform.SetParent(Node.transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=2;line.startWidth=line.endWidth=width;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
            }
            public void SetCollision(bool enabled){if(enabled&&ArtReady&&!artPublished){foreach(var t in Yaw.GetComponentsInChildren<Transform>(true))t.gameObject.layer=CombatLayer;if(Warden!=null)Warden.PublishRenderLayer(CombatLayer);artPublished=true;}Body.enabled=enabled;Head.enabled=enabled&&ArtReady&&Brain.Variant!=1&&Brain.Variant!=3;}
            public void HideTransient(){warning.enabled=false;flashTime=ImpactFlash=0;if(Visual!=null)Visual.SetFlash(false);if(Warden!=null){Warden.SetFlash(false);Warden.SetImpactMarker(Vector3.zero,false,1);}for(int i=0;i<3;i++){streakTimes[i]=0;streaks[i].enabled=false;}}
            public void AdvancePresentation(float dt){AvoidTime=Mathf.Max(0,AvoidTime-dt);flashTime=Mathf.Max(0,flashTime-dt);ImpactFlash=Mathf.Max(0,ImpactFlash-dt);bool burst=flashTime>0&&flashTime%.075f>.035f;if(Visual!=null)Visual.SetFlash(burst);if(Warden!=null)Warden.SetFlash(burst);for(int i=0;i<3;i++){streakTimes[i]=Mathf.Max(0,streakTimes[i]-dt);streaks[i].enabled=streakTimes[i]>0;}}
            public void Move(float dt,Vector3 desired)
            {
                if(!Body.enabled)return;float speed=owner.active?Brain.MoveSpeed:0;if(Brain.Variant==3&&Brain.HasLastKnown&&Vector3.Distance(U(Brain.LastAim),Position+Vector3.up*1.4f)>48)speed=6;velocity.x=Mathf.MoveTowards(velocity.x,desired.x*speed,dt*5);velocity.z=Mathf.MoveTowards(velocity.z,desired.z*speed,dt*5);
                if(Brain.Variant==1||Brain.Variant==3){float y=Brain.Home.Y+2+Mathf.Sin(Brain.Age*.9f+Brain.Phase)*.5f;if(Brain.HasSight)y=Mathf.Clamp(Brain.LastAim.Y+.9f,Brain.Home.Y+1,Brain.Home.Y+7);if(Brain.Variant==3&&Brain.HasLastKnown&&Brain.LostTime<=Mathf.Lerp(5,11,Brain.Difficulty))y=Mathf.Clamp(Brain.LastAim.Y+2.1f+Mathf.Sin(Brain.Age*.5f)*.7f,Brain.Home.Y+2,Brain.Home.Y+8);velocity.y=Mathf.Clamp((y-Position.y)*1.7f,-2,2);}
                else velocity.y=Body.isGrounded?-.1f:velocity.y-18*dt;
                bool supported=Body.isGrounded;Vector3 before=Position;var flags=Body.Move(velocity*dt);if((flags&CollisionFlags.Above)!=0&&velocity.y>0)velocity.y=0;if((flags&CollisionFlags.Below)!=0&&velocity.y<0)velocity.y=-.1f;
                if(Brain.Variant!=1&&Brain.Variant!=3&&supported&&!Body.isGrounded&&velocity.y<=0){
                    Vector3 lower=Position+Body.center-Vector3.up*(Body.height*.5f-Body.radius);RaycastHit support;
                    if(Physics.SphereCast(lower,Body.radius*.98f,Vector3.down,out support,.42f,WorldMask,QueryTriggerInteraction.Ignore)&&support.normal.y>=Mathf.Cos(48*Mathf.Deg2Rad)){Body.Move(Vector3.down*Mathf.Max(0,support.distance-.005f));velocity.y=-.1f;}
                }
                #if UNITY_EDITOR
                if(owner.CaptureRouteDiagnostics)owner.RecordRoute(this,"move","before="+before.ToString("F5")+" velocity="+velocity.ToString("F5")+" flags="+flags+" grounded="+Body.isGrounded);
#endif
                if(Visual!=null)Visual.UpdatePose(dt,Position-before,Body.isGrounded,Brain.State==R24KestrelState.Windup,Brain.Recoil,pitch);if(Warden!=null)Warden.UpdatePose(dt,Position-before,Brain.Age,Brain.Phase,pitch,Brain.Recoil,Brain.CoreOpen,Brain.BossPhase);
            }
            public void Present(float dt)
            {
                Color threat=Brain.State==R24KestrelState.Windup?new Color(1,.12f+Mathf.Sin(Brain.Age*35)*.1f,.02f):new Color(.18f,.85f,1);if(Visual!=null){Visual.SetViewerPosition(owner.root.Rig.Head.transform.position);Visual.SetThreatEmission(threat);}if(Warden!=null){Warden.SetViewerPosition(owner.root.Rig.Head.transform.position);Warden.SetThreatEmission(threat);bool marking=Brain.State==R24KestrelState.Windup&&Brain.AreaAttack;Warden.SetImpactMarker(U(Brain.ImpactPoint),marking||ImpactFlash>0,marking?1+Mathf.Sin(Brain.StateTime*16)*.025f:1+(.35f-ImpactFlash)*2);}
                Vector3 look=U(Brain.LookPoint(F(Position)))-(Position+Vector3.up*1.4f),flat=Vector3.ProjectOnPlane(look,Vector3.up);
                if(flat.sqrMagnitude>.0001f)Yaw.rotation=Quaternion.RotateTowards(Yaw.rotation,Quaternion.LookRotation(flat,Vector3.up),dt*180);
                pitch=Mathf.MoveTowards(pitch,Mathf.Clamp(Mathf.Atan2(look.y,Mathf.Max(flat.magnitude,.001f)),-.6f,.6f),dt*1.8f);
                if(Visual!=null&&Visual.HeadPose!=null&&Brain.Variant!=1){float ratio=R23CalibrationState.TargetEye/1.605f;Head.transform.SetPositionAndRotation(Visual.HeadPose.position+Visual.HeadPose.up*(.065f*ratio),Visual.HeadPose.rotation);Head.enabled=true;}
                warning.enabled=false;if(Brain.State==R24KestrelState.Windup){Vector3 from=Muzzle,to=U(Brain.AreaAttack?Brain.ImpactPoint:Brain.LockedAim),direction=to-from;float length=direction.magnitude;if(length>1){direction/=length;to-=direction*.85f;RaycastHit hit;bool overflow;if(owner.Ray(from,to,WorldMask,this,out hit,out overflow))to=hit.point-direction*.03f;if(!overflow&&Vector3.Distance(from,to)>.03f){warning.SetPosition(0,from);warning.SetPosition(1,to);warning.enabled=true;}}}
            }
            public void ShowShot(Vector3 from,Vector3 to){flashTime=.25f;streaks[cursor].SetPosition(0,from);streaks[cursor].SetPosition(1,to);streakTimes[cursor]=.065f;streaks[cursor].enabled=true;cursor=(cursor+1)%3;}
            public void StartBurst(){flashTime=.25f;}
            public void SpawnCorpse()
            {
                if(corpseAttempted)return;corpseAttempted=true;R24RagdollSource captured=null;
                try{captured=Visual!=null?Visual.CaptureCorpseSource():Warden.CaptureCorpseSource();Vector3 impulse=Vector3.up*1.4f;if(Brain.HasLastKnown)impulse+=(Position-U(Brain.LastFeet)).normalized*2.5f;if(owner.corpses.Spawn(owner.encounter.transform,Position,Node.transform.rotation,impulse,captured)){if(Visual!=null)Visual.SetVisible(false);if(Warden!=null)Warden.SetVisible(false);}}
                catch(Exception error){if(captured!=null)captured.Dispose();Debug.LogError("Source corpse snapshot unavailable; authored fallback retained: "+error);}
            }
            public void TickDeath(float dt)
            {
                if(!deathStarted){deathStarted=true;deathPosition=Position;deathYaw=Yaw.localRotation;SetCollision(false);HideTransient();}deathTime+=dt;
                // Source's authored fallback when no ragdoll bridge is available.
                Node.transform.position=deathPosition-Vector3.up*(.6f*Mathf.Clamp01(deathTime/.6f));Yaw.localRotation=deathYaw*Quaternion.Euler(0,0,Mathf.Clamp01(deathTime/.6f)*-1.4f*Mathf.Rad2Deg);
                if(deathTime>1.1f)Yaw.localScale=Vector3.one*(Mathf.Lerp(1,.03f,Mathf.Clamp01((deathTime-1.1f)/.4f))*(Brain.Variant==1||Brain.Variant==3?1:R23CalibrationState.TargetEye/1.605f));if(deathTime>=1.5f)Node.SetActive(false);
            }
        }
    }
}
#endif
