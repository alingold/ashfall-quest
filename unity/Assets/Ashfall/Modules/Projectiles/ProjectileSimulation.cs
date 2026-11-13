using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Projectiles
{
    // Candidate default tuning from one_star_050 and weapon_rarity_r6. No runtime overrides.
    public sealed partial class ProjectileSimulation
    {
        public const double StepSeconds=.05;
        sealed class Shot
        {
            public string Id,Kind; public Float3 Position,Velocity; public int Age,StuckAt=-1,ZoneTicks;
            public float Power,Roll; public bool Ended; public long Expires; public string Instance,LaunchOwner,Command; public long Serial,Generation,Born; public int Rarity;
            public readonly List<PendingBlast> Pending=new List<PendingBlast>();
        }
        sealed class PendingBlast
        { public int Sub; public Float3 Center; public float Radius,Power; public bool Falloff; public Target[] Targets; public long Attempt=-1; }
        readonly SortedDictionary<string,Shot> shots=new SortedDictionary<string,Shot>(StringComparer.Ordinal);
        readonly IProjectileWorld world; readonly IProjectileDamage damage; readonly IAuthorityGate gate;
        readonly string session,map,host; readonly long epoch; readonly int capacity; readonly RadialPolicy policy;
        long tick; public string LastFailure {get;private set;}
        public ProjectileSimulation(string sessionId,long authorityEpoch,string hostActor,string mapRevision,IAuthorityGate authority,IProjectileWorld queries,IProjectileDamage application,RadialPolicy radialPolicy,int maximum=128)
        {
            if(String.IsNullOrEmpty(sessionId)||authorityEpoch<0||String.IsNullOrEmpty(hostActor)||String.IsNullOrEmpty(mapRevision)||authority==null||queries==null||application==null||maximum<1||maximum>4096)throw new ArgumentException("configuration");
            session=sessionId;epoch=authorityEpoch;host=hostActor;map=mapRevision;gate=authority;world=queries;damage=application;capacity=maximum;policy=radialPolicy;
        }
        bool Authorized(CommandContext c){return !coldQuarantine&&c.IsAuthority&&c.SessionId==session&&c.AuthorityEpoch==epoch&&c.ActorId==host&&gate.Validate(c).Accepted&&world.MapRevision==map;}
        public RuleResult Launch(CommandContext c,string id,long combatSerial,string instance,string owner,long generation,string kind,int rarity,Float3 position,Float3 velocity)
        {
            if(!Authorized(c)||c.Tick!=tick)return RuleResult.Reject("launch-authority-clock-map");
            if(policy!=RadialPolicy.StructuralLineOfSightStaged&&policy!=RadialPolicy.SourceRadialV1)return RuleResult.Reject("source-through-walls-unresolved");
            if(String.IsNullOrEmpty(id)||id.Length>64||String.IsNullOrEmpty(owner)||generation<1||!position.IsFinite||!velocity.IsFinite||Length(position)>1000000||Length(velocity)>240.001||rarity<1||rarity>5||kind!="frag"&&kind!="zone"&&kind!="smaw"||tick>Int64.MaxValue-600)return RuleResult.Reject("launch-values");
            if(shots.ContainsKey(id))return RuleResult.Reject("launch-replay");
            if(shots.Count>=capacity)return RuleResult.Reject("launch-history-full");
            // Reserve the tombstone even if publication fails; no changed-payload retry.
            var s=new Shot{Id=id,Kind=kind,Position=position,Velocity=velocity,Power=kind=="frag"?150:kind=="zone"?5:new float[]{60,84,109,134,160}[rarity-1],Ended=true,Expires=tick+600,Instance=instance,LaunchOwner=owner,Command=c.CommandId,Serial=combatSerial,Generation=generation,Born=tick,Rarity=rarity};shots.Add(id,s);
            var source=damage as ISourceProjectileDamage;
            RuleResult r=policy==RadialPolicy.SourceRadialV1?(source==null?RuleResult.Reject("source-api-missing"):source.RegisterSource(c,id,combatSerial,tick+600,instance,owner,kind,generation)):damage.Register(c,id,combatSerial,tick+600,instance,owner,kind,generation);if(!r.Accepted)return r;
            owners.Add(id,owner);s.Ended=false;return RuleResult.Accept();
        }
        public bool Read(string id,out Float3 position,out bool ended)
        {Shot s;if(shots.TryGetValue(id,out s)){position=s.Position;ended=s.Ended&&s.Pending.Count==0;return true;}position=new Float3();ended=true;return false;}
        // Exactly one fixed step. Catch-up is the coordinator's bounded scheduling duty.
        public RuleResult Advance(CommandContext c)
        {
            if(!Authorized(c)||tick==Int64.MaxValue||c.Tick!=tick+1)return RuleResult.Reject("step-authority-clock-map");
            tick=c.Tick;LastFailure=null;
            foreach(var s in shots.Values){Retry(c,s);if(!s.Ended)Step(c,s);}
            return LastFailure==null?RuleResult.Accept():RuleResult.Reject(LastFailure);
        }
        void Fail(Shot s,string reason){s.Ended=true;LastFailure=reason;}
        void Step(CommandContext c,Shot s)
        {
            s.Age++;
            if(s.StuckAt<0)
            {
                var prior=s.Position;var velocity=Add(s.Velocity,new Float3(0,s.Kind=="smaw"?0:-.49f,0));
                var next=Add(s.Position,Scale(velocity,.05f));Contact contact;
                if(!next.IsFinite||Length(next)>1000000||!world.Sweep(s.Position,next,.035f,Owner(s.Id),out contact)){Fail(s,"sweep-failed");return;}
                if(contact!=null)
                {
                    double normal=Length(contact.Normal);
                    if(String.IsNullOrEmpty(contact.BodyId)||!Finite(contact.Fraction)||contact.Fraction<0||contact.Fraction>1||!contact.Normal.IsFinite||Math.Abs(normal-1)>.001){Fail(s,"invalid-contact");return;}
                    s.Position=Add(s.Position,Scale(Sub(next,s.Position),contact.Fraction));
                    if(s.Kind=="smaw"){Blast(c,s,0,4,s.Power,true);s.Ended=true;return;}
                    if(s.Kind=="zone"){s.StuckAt=s.Age;s.Velocity=new Float3();return;}
                    // Default source PhysicsMaterial has zero restitution. Tangent friction
                    // approximates source explicit ground-roll controller; engine parity open.
                    float dot=Dot(velocity,contact.Normal);if(dot<0)velocity=Sub(velocity,Scale(contact.Normal,dot));
                    if(contact.Normal.Y>.45f){s.Roll+=(float)Length(Sub(s.Position,prior));double speed=Length(velocity);velocity=Scale(velocity,s.Roll>=3.048f||speed<=.2?0:(float)((speed-.2)/speed));}
                }
                else s.Position=next;
                s.Velocity=velocity;
            }
            if(s.Kind=="frag"&&s.Age>=70){Blast(c,s,0,10,s.Power,true);s.Ended=true;}
            else if(s.Kind=="smaw"&&s.Age>=160){Blast(c,s,0,4,s.Power,true);s.Ended=true;}
            else if(s.Kind=="zone")
            {
                if(s.StuckAt<0){if(s.Age>=160)s.Ended=true;return;}
                int active=s.Age-s.StuckAt-54;
                while(s.ZoneTicks<20&&(s.ZoneTicks+1)*10<=active){s.ZoneTicks++;Blast(c,s,s.ZoneTicks,15*(1-s.ZoneTicks/20f),5,false);if(s.Ended)return;}
                if(active>=200)s.Ended=true;
            }
        }
        readonly Dictionary<string,string> owners=new Dictionary<string,string>(StringComparer.Ordinal);
        // Owner exclusions are frozen at successful host launch, not current life/connection.
        string Owner(string id){return owners.ContainsKey(id)?owners[id]:null;}
        void Blast(CommandContext c,Shot s,int sub,float radius,float power,bool falloff)
        {
            if(radius<=0)return;
            Target[] targets;if(!world.Overlap(s.Position,radius,out targets)||targets==null||targets.Length>256){Fail(s,"overlap-failed-or-overflow");return;}
            var unique=new SortedDictionary<string,Target>(StringComparer.Ordinal);
            foreach(var t in targets)
            {
                if(t==null||String.IsNullOrEmpty(t.Id)||t.Id.Length>64||t.Provenance!=map||!t.Center.IsFinite){Fail(s,"target-provenance");return;}
                Target previous;if(unique.TryGetValue(t.Id,out previous)&&(Length(Sub(previous.Center,t.Center))>0)){Fail(s,"target-conflict");return;}
                unique[t.Id]=t;
            }
            var bounded=new List<Target>();foreach(var t in unique.Values)if(Length(Sub(t.Center,s.Position))<=radius)bounded.Add(t);
            s.Pending.Add(new PendingBlast{Sub=sub,Center=s.Position,Radius=radius,Power=power,Falloff=falloff,Targets=bounded.ToArray()});
            Retry(c,s);
        }
        void Retry(CommandContext c,Shot s)
        {
            for(int i=0;i<s.Pending.Count;){
                var b=s.Pending[i];
                if(b.Attempt==c.Tick){i++;continue;}b.Attempt=c.Tick;
                var result=c.Tick>s.Expires?RuleResult.Reject("event-expired"):damage.Radial(c,s.Id,b.Sub,b.Center,b.Radius,b.Power,b.Falloff,(Target[])b.Targets.Clone(),policy);
                var disposition=ProjectileDelivery.Classify(result);
                if(disposition==DeliveryDisposition.Retryable){LastFailure=result.Reason;i++;}
                else {s.Pending.RemoveAt(i);if(!result.Accepted)LastFailure=result.Reason;}
            }
        }
        public int PendingDeliveries(string id){Shot s;return shots.TryGetValue(id,out s)?s.Pending.Count:0;}
        internal static bool Finite(float x){return !Single.IsNaN(x)&&!Single.IsInfinity(x);}
        internal static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        internal static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        internal static Float3 Scale(Float3 a,float b){return new Float3(a.X*b,a.Y*b,a.Z*b);}
        internal static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        internal static double Length(Float3 v){return Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z);}
    }
}
