#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        struct LocalBullet
        {
            public Vector3 Origin,Direction;public R24LanBallisticProfile Gun;
            public long Volley;public double StartTime;public float Distance;public System.Action<Collider,float> Mounted;public int Stars,Slot;public long Visual;
        }
        struct Streak
        {
            public LineRenderer Line;public Vector3 Origin,Direction;public double Start;
            public R24LanBallisticProfile Gun;public string Actor;public long Id,Visual;public int Pellet;
        }
        readonly List<LocalBullet> localBullets=new List<LocalBullet>(64);
        readonly Streak[] streaks=new Streak[96];int streakCursor;long visualGeneration;
        internal World.R26RangeLane BulletLane;
        long localVolley,lastShotgunVolley;
        readonly HashSet<string> shotgunTargets=new HashSet<string>();
        struct DamageGroup{public LocalBullet Bullet;public Collider Surface;public R23PracticeTarget Target;public bool Head;public float Damage;}
        readonly List<DamageGroup> localDamage=new List<DamageGroup>(8);
        internal long PredictedVolleyId;internal string PredictedActor;
        internal System.Func<float> ReadHeadshotMultiplier;internal System.Func<Collider,bool> WorldBulletHeadshot;
        float HeadshotMultiplier{get{if(ReadHeadshotMultiplier==null)return 1;float m=ReadHeadshotMultiplier();return float.IsNaN(m)||float.IsInfinity(m)||m<0?1:m;}}
        internal int ShowFlight(Vector3 origin,Vector3 direction,R24LanBallisticProfile gun,double age,string actor,long id,int pellet)
        {
            int slot=streakCursor;for(int i=0;i<streaks.Length;i++){if(streaks[i].Line==null||!streaks[i].Line.enabled){slot=i;break;}}
            streakCursor=(slot+1)%streaks.Length;var s=streaks[slot];
            if(s.Line==null){
                var node=new GameObject("Pooled firearm streak");node.transform.SetParent(transform,false);
                s.Line=node.AddComponent<LineRenderer>();s.Line.useWorldSpace=true;s.Line.positionCount=2;
                s.Line.sharedMaterial=RemoteTracerMaterial;s.Line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;s.Line.receiveShadows=false;
            }
            s.Origin=origin;s.Direction=direction;s.Gun=gun;s.Actor=actor;s.Id=id;s.Pellet=pellet;
            s.Start=Time.unscaledTimeAsDouble-System.Math.Max(0,age);s.Visual=++visualGeneration;s.Line.enabled=true;
            streaks[slot]=s;DrawStreak(slot,gun.Travel(age));
            
            return slot;
        }
        void DrawStreak(int slot,float distance)
        {
            var s=streaks[slot];s.Line.SetPosition(0,s.Origin+s.Direction*Mathf.Max(0,distance-s.Gun.Speed*.007f));
            s.Line.SetPosition(1,s.Origin+s.Direction*Mathf.Max(.025f,distance));R26CombatFxTracer.Present(s.Line,TracerViewerPosition);
        }
        void ClearFlights()
        {
            localDamage.Clear();localBullets.Clear();foreach(var s in streaks)if(s.Line!=null)s.Line.enabled=false;
        }
        internal void StopFlight(R26BulletContact c)
        {
            for(int i=0;i<streaks.Length;i++){var s=streaks[i];if(s.Line!=null&&s.Actor==c.Actor&&s.Id==c.Id&&(s.Pellet==c.Pellet||c.Pellet==255))s.Line.enabled=false;}
        }
        internal void AcceptBulletContact(R26BulletContact c,Collider surface,bool own)
        {
            StopFlight(c);
            var point=From(c.Point);var normal=From(c.Normal);
            if(gunFeedback!=null)gunFeedback.Impact(point,normal,Time.unscaledTime);
            if(surface!=null&&impactMarks!=null)impactMarks.AddHit(point,normal,surface,false);
            if(own&&c.MapSurface&&surface!=null&&SurfaceImpact!=null)SurfaceImpact(point,surface);
        }
        internal void ShowMountedFlight(Vector3 origin,Vector3 direction,R24LanBallisticProfile gun,string actor,long id,System.Action<Collider,float> offline)
        {
            int slot=ShowFlight(origin,direction,gun,0,actor,id,0);
            if(offline!=null)localBullets.Add(new LocalBullet{Origin=origin,Direction=direction,Gun=gun,StartTime=Time.timeAsDouble,Stars=1,Slot=slot,Visual=streaks[slot].Visual,Mounted=offline});
        }
        bool LaunchBullets(Float3 origin,Float3[] rays,float flash,int side=0)
        {
            R24LanBallisticProfile gun;if(!R24LanBallisticProfile.TryRead(ActiveWeaponId,out gun))return false;
            PublishBallistic(origin,rays,side);muzzleFlashUntil=Time.unscaledTime+flash;
            if(SharedProjectileAuthority&&PredictedVolleyId==0)return true;
            var a=From(origin);var delta=a-head.transform.position;RaycastHit hit;
            if(delta.sqrMagnitude>.000001f&&Nearest(head.transform.position,delta.normalized,delta.magnitude,out hit))return true;
            R23WeaponInstanceInfo instance;int stars=round.TryReadWeaponInstance(activeActionId,out instance)?instance.Stars:1;
            long volley=++localVolley;System.Array.Clear(latestLocalGenerations,0,8);
            if(gun.Pellets>1){lastShotgunVolley=volley;shotgunTargets.Clear();LastShotgunPelletCount=rays.Length;LastShotgunTargetGroups=0;}
            for(int i=0;i<rays.Length;i++){
                var direction=From(rays[i]).normalized;int slot=ShowFlight(a,direction,gun,0,SharedProjectileAuthority?PredictedActor:null,PredictedVolleyId,i);
                latestLocalSlots[i]=slot;latestLocalGenerations[i]=streaks[slot].Visual;
                if(i==0&&IsAwp&&awpView!=null&&awpView.Scope!=null)awpView.Scope.BindTracer(streaks[slot].Line,true);
                if(!SharedProjectileAuthority)localBullets.Add(new LocalBullet{Origin=a,Direction=direction,Volley=volley,StartTime=Time.timeAsDouble,Gun=gun,Stars=stars,Slot=slot,Visual=streaks[slot].Visual});
            }
            return true;
        }
        void AdvanceFlights()
        {
            double now=Time.unscaledTimeAsDouble;
            for(int i=0;i<streaks.Length;i++){var s=streaks[i];if(s.Line==null||!s.Line.enabled)continue;
                float distance=s.Gun.Travel(now-s.Start);if(distance>=R24LanBallisticProfile.MaximumRange)s.Line.enabled=false;else DrawStreak(i,distance);
            }
            for(int i=localBullets.Count-1;i>=0;i--){
                var b=localBullets[i];float end=b.Gun.Travel(Time.timeAsDouble-b.StartTime);if(end<=b.Distance)continue;
                RaycastHit hit;var start=b.Origin+b.Direction*b.Distance;bool contact=b.Mounted==null?FlightNearest(start,b.Direction,end-b.Distance,out hit):Physics.Raycast(start,b.Direction,out hit,end-b.Distance,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore);
                float travel=contact?hit.distance:end-b.Distance+1;Collider collider=hit.collider;
                Vector3 point=hit.point,normal=hit.normal;
                if(b.Mounted==null&&BulletLane!=null&&BulletLane.Active){
                    var result=new R26BulletSurface{Distance=travel};
                    BulletLane.SweepMovers(start,b.Direction,end-b.Distance,b.StartTime+b.Distance/b.Gun.Speed,b.StartTime+end/b.Gun.Speed,ref result);
                    if(result.RangeTarget!=0){travel=result.Distance;point=start+b.Direction*travel;normal=From(result.Normal);collider=BulletLane.ContactCollider(result.RangeTarget);contact=true;}
                }
                if(contact){
                    if(gunFeedback!=null)gunFeedback.Impact(point,normal,Time.unscaledTime);
                    if(impactMarks!=null)impactMarks.AddHit(point,normal,collider,b.Gun.Pellets>1);
                    if(SurfaceImpact!=null)SurfaceImpact(point,collider);
                    float distance=b.Distance+travel;
                    if(b.Mounted!=null)b.Mounted(collider,b.Gun.Damage(b.Stars,false,distance));
                    else AccumulateContact(b,collider,distance);
                }
                if(contact||end>=R24LanBallisticProfile.MaximumRange){
                    if(streaks[b.Slot].Visual==b.Visual)streaks[b.Slot].Line.enabled=false;
                    localBullets[i]=localBullets[localBullets.Count-1];localBullets.RemoveAt(localBullets.Count-1);
                }else{b.Distance=end;localBullets[i]=b;}
            }
            ApplyGroups();
        }
        void AccumulateContact(LocalBullet b,Collider collider,float distance)
        {
            R23PracticeTarget target=null;foreach(var t in targets)if(t.Owns(collider)){target=t;break;}
            bool headshot=target!=null?collider==target.Head:WorldBulletHeadshot!=null&&WorldBulletHeadshot(collider);
            int index=0;
            while(index<localDamage.Count&&(localDamage[index].Bullet.Volley!=b.Volley||localDamage[index].Target!=target||target==null&&localDamage[index].Surface!=collider))index++;
            var g=index<localDamage.Count?localDamage[index]:new DamageGroup{Bullet=b,Target=target,Surface=collider};
            g.Head|=headshot;g.Damage+=headshot?b.Gun.Damage(b.Stars,true,distance)*HeadshotMultiplier:b.Gun.Damage(b.Stars,false,distance);
            if(index<localDamage.Count)localDamage[index]=g;else localDamage.Add(g);
        }
        void ApplyGroups()
        {
            foreach(var g in localDamage){
                if(g.Surface!=null&&WorldHit!=null)WorldHit(g.Surface,g.Damage);
                var t=g.Target;if(t==null)continue;var before=round.ReadTarget(t.TargetId);
                if(!round.ApplyTargetDamage(t.TargetId,g.Damage,g.Head))continue;
                t.Refresh(round);var after=round.ReadTarget(t.TargetId);if(audioCues!=null)audioCues.Play(g.Bullet.Gun.Id,"hit",false);
                if(TargetDamaged!=null)TargetDamaged(t.TargetId,g.Head,before.Shield-after.Shield,before.Health-after.Health);
                if(g.Bullet.Volley==lastShotgunVolley&&shotgunTargets.Add(t.TargetId))LastShotgunTargetGroups++;
            }
            localDamage.Clear();
        }
        readonly RaycastHit[] bulletHits=new RaycastHit[32];
        bool FlightNearest(Vector3 origin,Vector3 direction,float length,out RaycastHit nearest)
        {
            int mask=Physics.DefaultRaycastLayers&Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask;
            int count=Physics.RaycastNonAlloc(origin,direction,bulletHits,length,mask,QueryTriggerInteraction.Ignore);
            var hits=bulletHits;if(count==hits.Length){hits=Physics.RaycastAll(origin,direction,length,mask,QueryTriggerInteraction.Ignore);count=hits.Length;}
            nearest=new RaycastHit();float best=float.PositiveInfinity;
            for(int i=0;i<count;i++){var h=hits[i];if(h.distance<best&&(BulletLane==null||!BulletLane.MovingCollider(h.collider))&&!Excluded(h.collider)&&!R23ImpactMarkSurface.ExcludesBallistic(h.collider)){best=h.distance;nearest=h;}}
            return best<float.PositiveInfinity;
        }
    }
}
#endif
