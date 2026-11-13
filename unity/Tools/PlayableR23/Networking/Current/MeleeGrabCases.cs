using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Expansion;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    static void MeleeGrabFists()
    {
        Need(R25FistContact.Damage(1.6f)==8&&R25FistContact.Damage(4)==30&&R25FistContact.Damage(99)==30,"Fist curve bounds");
        Need(Math.Abs(R25FistContact.Damage(2.8f)-19)<.001f,"Fist curve midpoint");
        var f=new R25FistContact();string[] no=new string[0],body=new[]{"actor/a"};
        Need(f.Sample(0,new Float3(),true,no).Length==0,"Fist seeds without hit");
        Need(f.Sample(.05,new Float3(.15f,0,0),true,body).Length==1,"Fast closed fist enters");
        Need(f.Sample(.10,new Float3(.3f,0,0),true,body).Length==0,"Held overlap never repeats");
        f.Sample(.15,new Float3(.45f,0,0),true,no);
        Need(f.Sample(.20,new Float3(.6f,0,0),true,body).Length==0,"Per-hand per-target cooldown");
        f.Sample(.4,new Float3(.6f,0,0),true,no);
        Need(f.Sample(.45,new Float3(.75f,0,0),true,body).Length==1,"Exited fist returns after cooldown");
        f.Sample(.5,new Float3(.8f,0,0),false,no);
        Need(f.Sample(.55,new Float3(.95f,0,0),true,body).Length==0,"Unavailable hand cannot resume with a hit");
        f.Sample(.6,new Float3(),true,no);
        Need(f.Sample(.65,new Float3(2,0,0),true,body).Length==0,"Tracking jump cannot punch");
    }
    static void MeleeGrabParryGuard()
    {
        var a=new R25ParryWindow();var b=new R25ParryWindow();
        b.Observe(1,new Float3(-2,0,0));Need(b.Timed(1.14,new Float3(-1,0,0))&&!b.Timed(1.16,new Float3(-1,0,0))&&!b.Timed(1.1,new Float3(1,0,0)),"Timed parry window and direction");
        a.Clash(2,true);b.Clash(2,false);
        Need(a.Blocked(2.89)&&!a.Blocked(2.91)&&b.Blocked(2.39)&&!b.Blocked(2.41),"Clash suppresses both blades; timed attacker stagger lasts .9");
        foreach(string id in new[]{"katana","excalibur","cyberblade"}){var blade=new R24MeleeRuntime(id,R24MeleeProfiles.ForId(id));blade.UpdateSupport(true,true,false,false,.5f);blade.UpdateSupport(true,true,false,true,.1f);Ashfall.Modules.Combat.BulletGuard guard;Need(blade.TryReadBulletGuard(new R23ShotPose(new Float3(),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1)),true,out guard),"Two-handed guard "+id);}
        Float3[] av,bv,cv;int[] at,bt,ct;
        Need(R24KnifeHull.TryBuild(R24KnifeMath.HullPoints(new Float3(-1,0,0),new Float3(1,0,0),new Float3(-1,0,0),new Float3(1,0,0)),out av,out at),"Hull A");
        Need(R24KnifeHull.TryBuild(R24KnifeMath.HullPoints(new Float3(0,-1,0),new Float3(0,1,0),new Float3(0,-1,0),new Float3(0,1,0)),out bv,out bt),"Hull B");
        Need(R24KnifeHull.TryBuild(R24KnifeMath.HullPoints(new Float3(0,-1,1),new Float3(0,1,1),new Float3(0,-1,1),new Float3(0,1,1)),out cv,out ct),"Hull C");
        Need(R24LanMeleeGeometry.Hulls(av,at,bv,bt)&&!R24LanMeleeGeometry.Hulls(av,at,cv,ct),"Exact crossed blade hull vs separated hull");
    }
    static R24LanRawTracking PunchBody(string hash,int rev,float root,float hand)
    {
        var p=Body(hash,rev,root,0,0,7,rx:hand-root,ry:1.25f,rz:0);
        return new R24LanRawTracking(hash,p.Flags,p.Root,p.Head,p.Left,p.Right,0,1,rev);
    }
    static void MeleeGrabHostFists()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();bool wall=false;
            pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return wall?0:range;});
            string hash=new string('b',64);var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0).With("starting_shield",0);
            Need(rules.Fists&&rules.Parry&&!rules.With("fists",false).Fists&&!rules.With("parry",false).Parry,"Replicated melee rule defaults and kill switches");
            StartRound(pair,new HazardWorld(),rules,hash);
            float x=8,hand=8;
            Action sample=delegate{pair.Host.RequestEquipment(null,1,false);pair.Host.PublishRawTracking(PunchBody(hash,Rev(pair.Host),x,hand));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};
            while(x>1.5f){hand=x;sample();x=Math.Max(1.5f,x-.3f);}hand=.9f;sample();sample();
            R24LanHealthState before,after;pair.Host.TryReadHealth(pair.Client.OwnActor,out before);
            wall=true;for(int i=0;i<7;i++){hand-=.12f;sample();}for(int i=0;i<4;i++)sample();pair.Host.TryReadHealth(pair.Client.OwnActor,out after);Need(after.Health==before.Health,"Host wall blocks fists");
            wall=false;hand=.9f;sample();sample();for(int i=0;i<7;i++){hand-=.12f;sample();}for(int i=0;i<5;i++)sample();
            pair.Host.TryReadHealth(pair.Client.OwnActor,out after);Need(after.Health<before.Health&&before.Health-after.Health<=30,"Accepted tracked fist damages through live pipeline");
            R25MeleeHit hit;Need(pair.Host.TryDequeueMeleeHit(out hit)&&hit.Kind=="fist"&&hit.Target==pair.Client.OwnActor&&hit.Damage>=8&&hit.Point.IsFinite,"Attacker gets authoritative fist confirmation");Need(!pair.Host.TryDequeueMeleeHit(out hit),"Attacker gets contact once");
            Need(pair.Client.TryDequeueMeleeHit(out hit)&&hit.Kind=="fist"&&hit.Attacker==pair.Host.OwnActor,"Victim gets authoritative confirmation");Need(!pair.Client.TryDequeueMeleeHit(out hit),"Victim gets contact once");
            float hp=after.Health;for(int i=0;i<12;i++)sample();pair.Host.TryReadHealth(pair.Client.OwnActor,out after);Need(after.Health==hp&&!pair.Client.TryDequeueMeleeHit(out hit),"Inside fist and old hit events do not repeat");
        }
    }
    static void MeleeGrabHostParry()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return range;});
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("loadout","swords").With("protection",0).With("starting_health",300).With("starting_shield",0),hash);
            float x=8,hx=8,cx=-.3f;
            Action sample=delegate{pair.Host.RequestEquipment("katana",1,false);pair.Client.RequestEquipment("katana",1,false);pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),x,0,0,7,rx:hx-x,ry:1.2f,rz:-.6f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0,7,rx:cx,ry:1.2f,rz:-.6f));pair.Tick();};
            while(x>1.5f){hx=x;sample();x=Math.Max(1.5f,x-.3f);}hx=.95f;sample();sample();
            for(int i=0;i<7;i++){hx-=.1f;cx+=.1f;sample();}
            for(int i=0;i<3;i++)sample();R25MeleeHit hit;int host=0,guest=0;bool timed=false;
            while(pair.Host.TryDequeueMeleeHit(out hit))if(hit.Kind=="parry"){host++;timed=hit.Stagger>0||hit.TargetStagger>0;}
            while(pair.Client.TryDequeueMeleeHit(out hit))if(hit.Kind=="parry")guest++;
            Need(host==1&&guest==1&&timed,"Actual accepted raw blade clash emits one timed event to each endpoint");
            R24LanHealthState before,after;pair.Host.TryReadHealth(pair.Client.OwnActor,out before);hx=.1f;sample();for(int i=0;i<5;i++)sample();pair.Host.TryReadHealth(pair.Client.OwnActor,out after);Need(after.Health==before.Health,"Parried blade cannot deliver queued/body damage during stagger");
        }
    }
    static void MeleeGrabTransientWire()
    {
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range"),hash);
            var emit=typeof(R24LanLobby).GetMethod("EmitMelee",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int i=0;i<100;i++)emit.Invoke(pair.Host,new object[]{pair.Host.OwnActor,pair.Client.OwnActor,"blade",25f,new Float3(0,1,0),1,0f,0,0f});
            R25MeleeHit hit;int count=0;while(pair.Host.TryDequeueMeleeHit(out hit))count++;Need(count==64,"Local hit queue bounded at 64");
            for(int i=0;i<12;i++)pair.Tick();count=0;while(pair.Client.TryDequeueMeleeHit(out hit))count++;Need(count==64,"Reliable peer outbox bounded at 64 without replay");
            var pipe=new Pipe();var late=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));pipe.Lost=late.TransportDisconnected;pair.HostPipe.Links.Add("late",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=pair.HostPipe,Back="late"});late.StartClient("server","Late",pair.Now);pair.Host.TransportConnected("late");try{for(int i=0;i<10;i++){pair.Tick();late.Tick(pair.Now);}Need(!late.TryDequeueMeleeHit(out hit),"Late joiner never receives old hit events");}finally{late.Dispose();pipe.Dispose();}
            var read=typeof(R24LanLobby).GetMethod("ReadMeleeHit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);int rejected=pair.Client.RejectedFrames;read.Invoke(pair.Client,new object[]{new Ashfall.Modules.Networking.Packet{BaseSequence=-1,Sequence=999999,Payload=new byte[]{255}}});Need(!pair.Client.TryDequeueMeleeHit(out hit)&&pair.Client.RejectedFrames==rejected,"Malformed event ignored safely");
            emit.Invoke(pair.Host,new object[]{pair.Host.OwnActor,pair.Client.OwnActor,"blade",25f,new Float3(0,1,0),1,0f,0,0f});
            Need(pair.Host.BeginRoundSelection(R24PlayableMatchRules.ForPreset("free_for_all","range"),hash,321),"Round reset accepted");Need(!pair.Host.TryDequeueMeleeHit(out hit),"Round reset clears queued feedback");
        }
    }
}
