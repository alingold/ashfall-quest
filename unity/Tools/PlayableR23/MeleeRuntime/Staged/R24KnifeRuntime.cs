using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.Combat;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed class R24KnifeEntry
    {
        internal readonly R24KnifeRuntime Owner;internal readonly SwordEntry Native;internal readonly R24KnifeSweep Frame;
        public string InstanceId{get{return Owner.InstanceId;}}public string TargetId{get{return Native.TargetId;}}public long Sequence{get{return Native.Sequence;}}public float Damage{get{return 35;}}
        internal R24KnifeEntry(R24KnifeRuntime owner,SwordEntry entry,R24KnifeSweep frame){Owner=owner;Native=entry;Frame=frame;}
    }
    public sealed class R24KnifeSweep
    {
        internal readonly R24KnifeRuntime Owner;internal int Next;
        public readonly Float3 PreviousBase,PreviousTip,Base,Tip;public readonly R23ShotPose Actor;
        public readonly float Delta,Travel,BladeSpeed,HandSpeed;public readonly int Steps;
        internal R24KnifeSweep(R24KnifeRuntime owner,Float3 a,Float3 b,Float3 c,Float3 d,R23ShotPose actor,float dt,float travel,float bladeSpeed,float handSpeed,int steps){Owner=owner;PreviousBase=a;PreviousTip=b;Base=c;Tip=d;Actor=actor;Delta=dt;Travel=travel;BladeSpeed=bladeSpeed;HandSpeed=handSpeed;Steps=steps;}
        public void ReadStep(int step,out Float3 previousBase,out Float3 previousTip,out Float3 nextBase,out Float3 nextTip){if(step<0||step>=Steps)throw new ArgumentOutOfRangeException("step");ReadAt((float)step/Steps,out previousBase,out previousTip);ReadAt((float)(step+1)/Steps,out nextBase,out nextTip);}
        void ReadAt(float t,out Float3 lower,out Float3 upper){var before=R24KnifeMath.Sub(PreviousTip,PreviousBase);var after=R24KnifeMath.Sub(Tip,Base);var local=R24KnifeMath.Lerp(PreviousBase,Base,t);var along=R24KnifeMath.Scale(R24KnifeMath.Slerp(R24KnifeMath.Unit(before),R24KnifeMath.Unit(after),t),R24KnifeMath.Length(before)+(R24KnifeMath.Length(after)-R24KnifeMath.Length(before))*t);lower=Actor.Point(local);upper=Actor.Point(R24KnifeMath.Add(local,along));}
    }
    // No firearm/ammo substitute: native bound SwordContacts owns contact entry,
    // reentry and per-instance lifetime. The real scene supplies ordered swept IDs.
    public sealed class R24KnifeRuntime:IAuthorityGate,ICombatEvidence,IInteractionQuery
    {
        public const float Damage=35,ContactRadius=.026f,ExitHysteresis=.008f,MinimumSpeed=.2f,MovementMultiplier=1.15f;
        public static readonly Float3 Grip=new Float3(0,-.057f,-.025f),BladeBase=new Float3(0,.025f,-.025f),BladeTip=new Float3(0,.246f,-.025f);
        const string Session="r24-local-melee",Actor="player";readonly CombatRules registry;readonly WeaponLease lease;readonly SwordContacts contacts;readonly string id;
        Float3 previousBase,previousTip,previousGrip,strokeDirection;bool haveHistory,retired,whooshPlayed;float strokeTravel,restTime,clock,nextWhoosh;long tick,commands,latestContact;R24KnifeSweep pending;
        public R24KnifeRuntime(string instanceId){if(String.IsNullOrEmpty(instanceId)||instanceId.Length>128)throw new ArgumentException("instanceId");id=instanceId;registry=new CombatRules(Session,0,3,128,this,this,this,Actor);lease=registry.Equip(Command(),id,Actor,"knife",1,tick);contacts=new SwordContacts(lease,64);}
        CommandContext Command(){return new CommandContext(Session,Actor,"knife/"+(++commands),tick,0,true);}
        public string InstanceId{get{return id;}}public long AcceptedContacts{get{return latestContact;}}public int WhooshEvents{get;private set;}public bool WhooshRequested{get;private set;}public float WhooshPitch{get;private set;}
        public HandlingSnapshot Snapshot{get{return new HandlingSnapshot{Phase=ReloadPhase.Ready,Shots=latestContact};}}
        public RuleResult Validate(CommandContext c){return !retired&&c.IsAuthority&&c.SessionId==Session&&c.ActorId==Actor&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local knife lifetime");}
        public bool TryResolve(CommandContext c,string hitId,out HitEvidence evidence){evidence=null;return false;}
        public bool HasLineOfSight(string actor,string target,Float3 from,Float3 to,float range){return false;}// Spatial adapter, not this tracker, owns actual map LOS.
        void ResetStroke(){strokeTravel=restTime=0;strokeDirection=new Float3();whooshPlayed=false;}
        public void SuspendControls(){contacts.Suspend();haveHistory=false;pending=null;WhooshRequested=false;ResetStroke();}
        public void Retire(){if(retired)return;SuspendControls();tick++;registry.RetireWeapon(Command(),lease,tick);retired=true;}
        public R24KnifeSweep BeginFrame(float dt,bool usable,R23ShotPose actor,Float3 worldBase,Float3 worldTip,Float3 worldGrip,string[] currentOverlaps){WhooshRequested=false;pending=null;if(Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");clock+=dt;
            if(retired||!usable||dt<=0||dt>.15f||!actor.Valid||!worldBase.IsFinite||!worldTip.IsFinite||!worldGrip.IsFinite){SuspendControls();return null;}
            var lower=R24KnifeMath.Local(actor,worldBase);var upper=R24KnifeMath.Local(actor,worldTip);var grip=R24KnifeMath.Local(actor,worldGrip);
            if(!haveHistory){previousBase=lower;previousTip=upper;previousGrip=grip;haveHistory=true;contacts.Sample(true,dt,0,0,new string[0],currentOverlaps,currentOverlaps);return null;}
            float travel=Math.Max(R24KnifeMath.Length(R24KnifeMath.Sub(lower,previousBase)),R24KnifeMath.Length(R24KnifeMath.Sub(upper,previousTip))),speed=travel/dt,handSpeed=R24KnifeMath.Length(R24KnifeMath.Sub(grip,previousGrip))/dt;
            if(travel>1){SuspendControls();return null;}
            var baseMotion=R24KnifeMath.Sub(lower,previousBase);var tipMotion=R24KnifeMath.Sub(upper,previousTip);var direction=R24KnifeMath.Unit(R24KnifeMath.Length(tipMotion)>R24KnifeMath.Length(baseMotion)?tipMotion:baseMotion);
            if(speed<MinimumSpeed){restTime+=dt;if(restTime>=.15f)ResetStroke();}
            else{if(R24KnifeMath.Length(strokeDirection)>0&&R23WeaponPoseMath.Dot(direction,strokeDirection)<-.35f)ResetStroke();strokeDirection=direction;restTime=0;strokeTravel+=travel;if(!whooshPlayed&&speed>=1.4f&&strokeTravel>=.12f&&clock>=nextWhoosh){whooshPlayed=true;nextWhoosh=clock+.18f;WhooshRequested=true;WhooshEvents++;WhooshPitch=Math.Max(.90f,Math.Min(1.25f,.85f+speed*.045f));}}
            var before=R24KnifeMath.Sub(previousTip,previousBase);var after=R24KnifeMath.Sub(upper,lower);float arc=R24KnifeMath.Angle(before,after)*Math.Max(R24KnifeMath.Length(before),R24KnifeMath.Length(after));int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(R24KnifeMath.Length(baseMotion)+arc,R24KnifeMath.Length(tipMotion))/ContactRadius));
            pending=new R24KnifeSweep(this,previousBase,previousTip,lower,upper,actor,dt,travel,speed,handSpeed,steps);previousBase=lower;previousTip=upper;previousGrip=grip;return pending;
        }
        public R24KnifeEntry[] SampleStep(R24KnifeSweep sweep,int step,string[] orderedSwept,string[] overlaps,string[] retained){if(retired||sweep==null||!Object.ReferenceEquals(sweep,pending)||sweep.Owner!=this||step!=sweep.Next||step>=sweep.Steps)return new R24KnifeEntry[0];sweep.Next++;var entries=contacts.Sample(true,sweep.Delta,sweep.Travel,sweep.BladeSpeed,orderedSwept,overlaps,retained);var result=new R24KnifeEntry[entries.Length];for(int i=0;i<entries.Length;i++){result[i]=new R24KnifeEntry(this,entries[i],sweep);latestContact=entries[i].Sequence;}return result;}
        public bool ApplyContact(R24KnifeEntry entry,Func<string,float,bool> actualDamage){if(retired||!haveHistory||entry==null||!Object.ReferenceEquals(entry.Frame,pending)||entry.Owner!=this||entry.Native.Weapon!=lease||actualDamage==null)return false;long prior;if(lease.Entries.TryGetValue(entry.TargetId,out prior)&&entry.Sequence<=prior)return false;lease.Entries[entry.TargetId]=entry.Sequence;return actualDamage(entry.TargetId,Damage);}
    }
    public static class R24KnifeMath
    {
        public static Float3 Add(Float3 a,Float3 b){return R23WeaponPoseMath.Add(a,b);}public static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}public static Float3 Scale(Float3 a,float n){return R23WeaponPoseMath.Scale(a,n);}public static float Length(Float3 a){return (float)Math.Sqrt(R23WeaponPoseMath.Dot(a,a));}public static Float3 Unit(Float3 a){float n=Length(a);return n<.00000001f?new Float3():Scale(a,1/n);}public static Float3 Lerp(Float3 a,Float3 b,float t){return Add(a,Scale(Sub(b,a),t));}public static float Angle(Float3 a,Float3 b){float n=Length(a)*Length(b);return n<.000000001f?0:(float)Math.Acos(Math.Max(-1,Math.Min(1,R23WeaponPoseMath.Dot(a,b)/n)));}
        public static Float3 Local(R23ShotPose basis,Float3 world){var d=Sub(world,basis.Origin);return new Float3(R23WeaponPoseMath.Dot(d,basis.Right),R23WeaponPoseMath.Dot(d,basis.Up),R23WeaponPoseMath.Dot(d,basis.Forward));}
        public static Float3 Slerp(Float3 a,Float3 b,float t){float angle=Angle(a,b),sin=(float)Math.Sin(angle);if(Math.Abs(sin)<.00001f)return Lerp(a,b,t);return Add(Scale(a,(float)Math.Sin((1-t)*angle)/sin),Scale(b,(float)Math.Sin(t*angle)/sin));}
        // Reflected original _sweep_query octahedral blade padding, including
        // intermediate orientations every .10 radians; no sparse-ray substitute.
        public static Float3[] HullPoints(Float3 previousBase,Float3 previousTip,Float3 lower,Float3 tip,float extraMargin=0){float radius=ContactRadius(extraMargin);var before=Sub(previousTip,previousBase);var after=Sub(tip,lower);int steps=Math.Max(1,(int)Math.Ceiling(Angle(before,after)/.10f));var points=new List<Float3>();for(int i=0;i<=steps;i++){float t=(float)i/steps;var bottom=Lerp(previousBase,lower,t);var along=Scale(Slerp(Unit(before),Unit(after),t),Length(before)+(Length(after)-Length(before))*t);foreach(var at in new[]{bottom,Add(bottom,along)})foreach(var axis in new[]{new Float3(1,0,0),new Float3(-1,0,0),new Float3(0,1,0),new Float3(0,-1,0),new Float3(0,0,1),new Float3(0,0,-1)})points.Add(Add(at,Scale(axis,radius)));}return points.ToArray();}
        static float ContactRadius(float extra){if(Single.IsNaN(extra)||Single.IsInfinity(extra)||extra<0||extra>.1f)throw new ArgumentOutOfRangeException("extraMargin");return R24KnifeRuntime.ContactRadius+extra;}
    }
}
