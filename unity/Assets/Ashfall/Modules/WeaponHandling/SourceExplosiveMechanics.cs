using System;
using Ashfall.Core;

namespace Ashfall.Modules.WeaponHandling
{
    public interface ISourceWeaponLifetimeAuthority : IWeaponLifetimeAuthority
    {
        RuleResult AuthorizeSourceNew(CommandContext command, string instanceId, string profile, int loaded, int reloads);
    }
    // Root attests the un-kicked source frame, source geometry identity, tuning,
    // camera-to-muzzle occlusion and scheduler seconds. This port grants no ammo.
    public interface ISourceLaunchBinding : IWeaponFireBinding
    {
        RuleResult ValidateSource(CommandContext command, string instanceId, HandlingInput input, double simulationSeconds, SourceLaunchGeometry geometry);
    }
    public sealed class SourceLaunchGeometry
    {
        public readonly string SourceIdentity;
        public readonly Float3 Origin, Aim, Right;
        public readonly float HandHeight, Gravity, RocketSpeed, ModelScale;
        public SourceLaunchGeometry(string identity, Float3 origin, Float3 aim, Float3 right,
            float height, float gravity, float rocketSpeed, float modelScale)
        { SourceIdentity=identity; Origin=origin; Aim=aim; Right=right; HandHeight=height; Gravity=gravity; RocketSpeed=rocketSpeed; ModelScale=modelScale; }
    }
    public struct SourceThrowInput { public HandlingInput Frame; public double SimulationSeconds; public SourceLaunchGeometry Geometry; }
    public struct SourceRocketInput { public HandlingInput Frame; public double SimulationSeconds; public SourceLaunchGeometry Geometry; }
    public struct SourceExplosiveSnapshot
    {
        public string Profile; public int Loaded, Reloads; public long Revision, Frame, Shots;
        public bool HeldRocket, TriggerReleaseRequired; public double ReadyAtSeconds, SimulationSeconds;
    }
    public sealed class SourceLaunchDescriptor
    {
        public readonly CommandContext Command;
        public readonly string InstanceId;
        public readonly long EquipGeneration, LifeGeneration, ConnectionGeneration, ExpiresTick;
        public readonly SourceExplosiveSnapshot Before, After;
        public readonly SourceLaunchGeometry Geometry;
        public readonly Float3 Velocity;
        internal SourceLaunchDescriptor(CommandContext c,string id,long equip,long life,long connection,long expires,
            SourceExplosiveSnapshot before,SourceExplosiveSnapshot after,SourceLaunchGeometry geometry,Float3 velocity)
        { Command=c;InstanceId=id;EquipGeneration=equip;LifeGeneration=life;ConnectionGeneration=connection;ExpiresTick=expires;
          Before=before;After=after;Geometry=geometry;Velocity=velocity; }
    }
    public interface IPreparedSourceLaunch
    {
        SourceLaunchDescriptor Descriptor { get; }
        bool Committed { get; }
        RuleResult Validate(long authoritativeTick);
        void CommitValidated();
        void Abort();
    }
    public sealed partial class WeaponHandling
    {
        // Immutable by publication: every mutation first copies this owner-private state.
        internal sealed class SourceMechanics
        {
            internal string Profile; internal int Loaded, Reloads; internal bool Release=true, GripRelease=true, Previous=true, Grip, Held;
            internal double Seconds, ReadyAt; internal Float3 Grab;
            internal SourceMechanics Copy(){return (SourceMechanics)MemberwiseClone();}
        }
        SourceMechanics sourceMechanics;
        public bool TryReadSourceExplosive(out SourceExplosiveSnapshot value)
        {
            value=new SourceExplosiveSnapshot(); if(sourceMechanics==null)return false;
            var s=sourceMechanics;value=new SourceExplosiveSnapshot {Profile=s.Profile,Loaded=s.Loaded,Reloads=s.Reloads,
                Revision=revision,Frame=sequence,Shots=shots,HeldRocket=s.Held,TriggerReleaseRequired=s.Release,
                ReadyAtSeconds=s.ReadyAt,SimulationSeconds=s.Seconds};return true;
        }
        internal void SuspendSource()
        {
            if(sourceMechanics==null)return;sourceMechanics=sourceMechanics.Copy();
            sourceMechanics.Release=sourceMechanics.Previous=sourceMechanics.GripRelease=true;
            sourceMechanics.Held=sourceMechanics.Grip=false;
        }
        static bool Number(double n){return !Double.IsNaN(n)&&!Double.IsInfinity(n);}
        internal static bool SourceGeometryValid(SourceLaunchGeometry g)
        {
            return g!=null&&!String.IsNullOrEmpty(g.SourceIdentity)&&g.SourceIdentity.Length<=256&&g.Origin.IsFinite&&g.Aim.IsFinite&&g.Right.IsFinite&&
                Number(g.HandHeight)&&g.HandHeight>=0&&g.HandHeight<=10000&&Number(g.Gravity)&&g.Gravity>0&&g.Gravity<=1000&&
                Number(g.RocketSpeed)&&g.RocketSpeed>=.05&&g.RocketSpeed<=240&&Number(g.ModelScale)&&g.ModelScale>=.001&&g.ModelScale<=100&&
                Math.Abs(Length(g.Aim)-1)<.0001&&Math.Abs(Length(g.Right)-1)<.0001&&Math.Abs(Dot(g.Aim,g.Right))<.0001;
        }
        static double Dot(Float3 a,Float3 b){return (double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;}
        static double Length(Float3 a){return Math.Sqrt(Dot(a,a));}
        static Float3 Mul(Float3 a,double s){return new Float3((float)(a.X*s),(float)(a.Y*s),(float)(a.Z*s));}
        // Reflection of grenade_ballistics.gd: Godot forward.cross(UP) becomes
        // UP.cross(Unity forward). Positive elevation remains upward after Z reflection.
        internal static Float3 SourceVelocity(SourceLaunchGeometry g,bool rocket)
        {
            if(rocket)return Mul(g.Aim,g.RocketSpeed);
            Float3 axis=new Float3(g.Aim.Z,0,-g.Aim.X);
            if(Length(axis)<.001)axis=g.Right;
            axis=Mul(axis,1/Length(axis));
            var cross=new Float3(axis.Y*g.Aim.Z-axis.Z*g.Aim.Y,axis.Z*g.Aim.X-axis.X*g.Aim.Z,axis.X*g.Aim.Y-axis.Y*g.Aim.X);
            // Unity uses the negative rotation of the reflected Godot axis.
            var d=new Float3((float)((g.Aim.X-cross.X)*Math.Sqrt(.5)),(float)((g.Aim.Y-cross.Y)*Math.Sqrt(.5)),(float)((g.Aim.Z-cross.Z)*Math.Sqrt(.5)));
            const double range=29.2608*48/41.5;
            double h=Math.Max(0,g.HandHeight-.035),speed=Math.Sqrt(g.Gravity*range*range/(range+h));
            double horizontal=Math.Sqrt((double)d.X*d.X+(double)d.Z*d.Z);
            double flight=(d.Y*speed+Math.Sqrt(d.Y*d.Y*speed*speed+2*g.Gravity*h))/g.Gravity;
            if(horizontal*speed*flight>range&&horizontal>.000001)
                speed=Math.Sqrt(g.Gravity*range*range/(2*(h+range*d.Y/horizontal)))/horizontal;
            return Mul(d,speed);
        }
        // Input sampling advances only source edges and physical reload. Launch is
        // exclusively prepared; a sampled held trigger can never publish a projectile.
        public RuleResult SampleSourceExplosive(CommandContext c,HandlingInput input,double seconds,SourceLaunchGeometry geometry)
        {
            if(sourceMechanics==null)return RuleResult.Reject("not source explosive");
            if(FireReservation!=null)return RuleResult.Reject("prepared operation busy");
            RuleResult r=ValidateSourceSample(c,input,seconds,geometry);if(!r.Accepted)return r;
            var n=sourceMechanics.Copy();n.Seconds=seconds;
            RecordEnvelope(c);sequence=input.Sequence;
            if(!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed)
            { sourceMechanics=n;SuspendSource();return RuleResult.Reject("input unavailable"); }
            if(!input.Trigger){n.Release=false;n.Previous=false;}
            else n.Previous=true;
            bool support=input.SupportTracked&&!input.SupportReserved;
            if(!support){n.Held=false;n.GripRelease=true;n.Grip=false;}
            else
            {
                if(!input.Grip)n.GripRelease=false;
                if(n.Profile=="smaw"&&n.Loaded==0&&n.Reloads>0&&!n.GripRelease)
                {
                    float scale=geometry.ModelScale;Float3 p=input.SupportLocal; // reflected MODEL local
                    if(!n.Held&&input.Grip&&!n.Grip)
                    {
                        var offer=new Float3(0,.045f,1.15f-.3048f/scale);
                        var rear=new Float3(offer.X,offer.Y,offer.Z-.06f/scale);
                        if(Reach(Mul(p,scale),Mul(offer,scale),.18f)||Reach(Mul(p,scale),Mul(rear,scale),.18f)) {n.Held=true;n.Grab=p;n.ReadyAt=seconds+.12;}
                    }
                    else if(n.Held)
                    {
                        double room=(n.Grab.Z-(.83-.08))*scale,margin=Math.Min(.01,Math.Max(0,room-.02));
                        double travel=Math.Min(.14,Math.Max(.02/scale,(room-margin)/scale));
                        double radial=Math.Sqrt((double)p.X*p.X+(p.Y-.045)*(p.Y-.045));
                        if(input.Grip&&n.Grab.Z-p.Z>=travel&&p.Z<=.96+.035&&p.Z>=.83-.08&&radial<=.14)
                        {n.Loaded=1;n.Reloads--;n.Held=false;n.Release=true;n.ReadyAt=seconds+.12;}
                        else if(!input.Grip){n.Held=false;n.ReadyAt=seconds+.12;}
                    }
                }
                n.Grip=input.Grip;
            }
            sourceMechanics=n;return RuleResult.Accept();
        }
        RuleResult ValidateSourceSample(CommandContext c,HandlingInput i,double seconds,SourceLaunchGeometry geometry)
        {if(!SourceGeometryValid(geometry))return RuleResult.Reject("invalid source sample");return ValidateSourceFrame(c,i,seconds);}
        RuleResult ValidateSourceFrame(CommandContext c,HandlingInput i,double seconds)
        {
            if(sourceMechanics==null||revision==long.MaxValue||shots==long.MaxValue||i.Sequence<=sequence||!Number(seconds)||seconds<sourceMechanics.Seconds||seconds<0||seconds+.12<=seconds||
                !i.SupportLocal.IsFinite||!Number(i.DeltaSeconds)||i.DeltaSeconds<0||i.DeltaSeconds>.25)
                return RuleResult.Reject("invalid source sample");
            return ValidatePreparation(c);
        }
        // Source desktop_reload_step performs grab+insert together. XR sampling
        // retains the physical offer/grab/insertion path above.
        public RuleResult ReloadSourceDesktop(CommandContext c,HandlingInput i,double seconds)
        {
            if(FireReservation!=null)return RuleResult.Reject("prepared operation busy");
            RuleResult r=ValidateSourceFrame(c,i,seconds);if(!r.Accepted)return r;
            var s=sourceMechanics;
            if(!i.Desktop||s.Profile!="smaw"||!i.Enabled||i.Paused||!i.PrimaryTracked||i.PrimaryClimbing||i.HeadObstructed||s.Loaded!=0||s.Reloads<=0)return RuleResult.Reject("source reload unavailable");
            var n=s.Copy();n.Loaded=1;n.Reloads--;n.Held=n.Grip=false;n.GripRelease=n.Release=true;n.Seconds=seconds;n.ReadyAt=seconds+.12;
            sourceMechanics=n;sequence=i.Sequence;RecordEnvelope(c);return RuleResult.Accept();
        }
        internal RuleResult PrepareSourceState(CommandContext c,HandlingInput i,double seconds,SourceLaunchGeometry geometry)
        {
            RuleResult r=ValidateSourceSample(c,i,seconds,geometry);if(!r.Accepted)return r;
            var s=sourceMechanics;
            if(!i.Enabled||i.Paused||!i.PrimaryTracked||i.PrimaryClimbing||i.HeadObstructed||!i.Trigger||s.Release||(s.Profile!="smaw"&&s.Previous)||s.Loaded<=0||seconds<s.ReadyAt)
                return RuleResult.Reject("source not fireable");
            sourceMechanics=s.Copy();s=sourceMechanics;s.Loaded--;s.Seconds=seconds;s.ReadyAt=seconds+.35;
            if(s.ReadyAt<=seconds||!Number(s.ReadyAt))return RuleResult.Reject("source clock exhausted");
            s.Release=s.Previous=true;s.Held=false;shots++;sequence=i.Sequence;RecordEnvelope(c);return RuleResult.Accept();
        }
    }
    public sealed partial class WeaponLifetimes
    {
        public RuleResult CreateSourceExplosive(CommandContext c,string id,string profile,IAuthorityGate authority,out WeaponHandling weapon)
        {
            weapon=null;var sourceApproval=approval as ISourceWeaponLifetimeAuthority;
            if(!Valid(c,id)||id.Length>128||entries.ContainsKey(id)||sourceApproval==null||authority==null||
                (profile!="frag"&&profile!="zone"&&profile!="smaw"))return RuleResult.Reject("invalid source lifetime");
            RuleResult r=authority.Validate(c);if(!r.Accepted)return r;
            int loaded=profile=="smaw"?1:2,reloads=profile=="smaw"?2:0;
            r=sourceApproval.AuthorizeSourceNew(c,id,profile,loaded,reloads);if(!r.Accepted)return r;
            // Existing handler supplies the actual shared reservation/transfer lane.
            // Its firearm entry points are explicitly disabled for this family.
            var n=new WeaponHandling(session,c.ActorId,c.AuthorityEpoch,WeaponMechanism.ServiceRifle,new FiniteAmmoReserve(0,0),authority);
            n.InitializeSource(profile,loaded,reloads);n.RecordEnvelope(c);
            if(entries.ContainsKey(id))return RuleResult.Reject("source created during authorization");
            entries.Add(id,new Entry{Active=n,Actor=c.ActorId,Epoch=c.AuthorityEpoch});weapon=n;return RuleResult.Accept();
        }
        public RuleResult PrepareSourceThrow(CommandContext c,string id,SourceThrowInput input,long equip,long life,long connection,long expires,IWeaponFireBinding binding,out IPreparedSourceLaunch prepared)
        { return PrepareSource(c,id,input.Frame,input.SimulationSeconds,input.Geometry,false,equip,life,connection,expires,binding,out prepared); }
        public RuleResult PrepareSourceRocketFire(CommandContext c,string id,SourceRocketInput input,long equip,long life,long connection,long expires,IWeaponFireBinding binding,out IPreparedSourceLaunch prepared)
        { return PrepareSource(c,id,input.Frame,input.SimulationSeconds,input.Geometry,true,equip,life,connection,expires,binding,out prepared); }
        RuleResult PrepareSource(CommandContext c,string id,HandlingInput input,double seconds,SourceLaunchGeometry geometry,bool rocket,
            long equip,long life,long connection,long expires,IWeaponFireBinding binding,out IPreparedSourceLaunch prepared)
        {
            prepared=null;Entry e;var sourceBinding=binding as ISourceLaunchBinding;
            if(!Valid(c,id)||id.Length>128||sourceBinding==null||equip<0||life<0||connection<0||expires<c.Tick||expires-c.Tick>MaximumPreparedFireTicks||
                !entries.TryGetValue(id,out e)||e.Active==null||e.Actor!=c.ActorId||e.Epoch!=c.AuthorityEpoch)return RuleResult.Reject("invalid source binding");
            var o=e.Active;SourceExplosiveSnapshot before;
            if(!o.TryReadSourceExplosive(out before)||(before.Profile=="smaw")!=rocket)return RuleResult.Reject("wrong source family");
            var n=new WeaponHandling(o,o.VersionedReserve);
            var token=new SourcePreparation(e,o,n,c,id,equip,life,connection,expires,input.Sequence,input,seconds,geometry,sourceBinding);
            if(!o.ReserveOperation(token,expires,c.Tick))return RuleResult.Reject("prepared operation busy");
            RuleResult r=sourceBinding.Validate(c,id,equip,life,connection,input.Sequence);
            if(r.Accepted)r=sourceBinding.ValidateSource(c,id,input,seconds,geometry);
            if(r.Accepted)r=n.PrepareSourceState(c,input,seconds,geometry);
            if(!r.Accepted||!token.Current()){token.Abort();return r.Accepted?RuleResult.Reject("source changed preparing"):r;}
            SourceExplosiveSnapshot after;n.TryReadSourceExplosive(out after);
            token.SetDescriptor(new SourceLaunchDescriptor(c,id,equip,life,connection,expires,before,after,geometry,WeaponHandling.SourceVelocity(geometry,rocket)));
            prepared=token;return RuleResult.Accept();
        }
        sealed class SourcePreparation : IPreparedSourceLaunch
        {
            readonly Entry entry;internal readonly WeaponHandling original,next;internal readonly CommandContext c;readonly string id;
            readonly HandlingInput input;readonly double seconds;
            readonly long equip,life,connection,frame,revision,reserveRevision;internal readonly long expires;readonly SourceLaunchGeometry geometry;readonly ISourceLaunchBinding binding;
            long observed;bool validated,aborted;internal bool committed;SourceLaunchDescriptor descriptor;
            internal SourceDispositionReservation disposition;
            internal SourcePreparation(Entry e,WeaponHandling o,WeaponHandling n,CommandContext c,string id,long equip,long life,long connection,long expires,long frame,HandlingInput input,double seconds,SourceLaunchGeometry geometry,ISourceLaunchBinding binding)
            {this.input=input;this.seconds=seconds;entry=e;original=o;next=n;this.c=c;this.id=id;this.equip=equip;this.life=life;this.connection=connection;this.expires=expires;this.frame=frame;this.geometry=geometry;this.binding=binding;revision=o.Revision;reserveRevision=o.VersionedReserve.Revision;observed=c.Tick;}
            internal bool Current(){return original.FireReservation==this&&entry.Active==original&&original.Live&&original.Revision==revision&&original.VersionedReserve.Revision==reserveRevision&&entry.Actor==c.ActorId&&entry.Epoch==c.AuthorityEpoch;}
            internal bool BelongsTo(WeaponLifetimes owner){Entry e;return owner.entries.TryGetValue(id,out e)&&Object.ReferenceEquals(e,entry);}
            internal void SetDescriptor(SourceLaunchDescriptor d){descriptor=d;}
            public SourceLaunchDescriptor Descriptor{get{return descriptor;}}
            public bool Committed{get{return committed;}}
            public RuleResult Validate(long now)
            {
                validated=false;if(now>expires){Abort();}
                if(aborted||committed||now<observed||now>expires||!Current())return RuleResult.Reject("stale source launch");observed=now;
                RuleResult r=original.ValidatePreparation(c);if(r.Accepted)r=binding.Validate(c,id,equip,life,connection,frame);
                if(r.Accepted)r=binding.ValidateSource(c,id,input,seconds,geometry);
                if(!r.Accepted||!Current())return r.Accepted?RuleResult.Reject("source changed validating"):r;
                validated=true;return RuleResult.Accept();
            }
            public void CommitValidated(){if(disposition!=null||!validated||aborted||committed||!Current())return;original.PublishFireState(next);original.ReleaseOperation(this);committed=true;}
            public void Abort(){if(!committed){aborted=true;validated=false;original.ReleaseOperation(this);}}
        }
    }
    public sealed partial class WeaponHandling
    {
        internal void InitializeSource(string profile,int loaded,int reloads){sourceMechanics=new SourceMechanics{Profile=profile,Loaded=loaded,Reloads=reloads};}
    }
}
