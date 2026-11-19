// Maintained LAN coverage for shotgun falloff and shared firearm reach.
using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    sealed class ShotgunQuery:IInteractionQuery { public bool HasLineOfSight(string a,string b,Float3 p,Float3 q,float r){return true;} }
    sealed class ShotgunPort:IR24DualCombatPort
    {
        internal R23RoundState Round;internal float Distance,Range,Amount;internal bool World,Head;internal int Traces,DamageCalls;
        public bool Trace(Float3 origin,Float3 direction,float range,int hand,out R24DualContact contact)
        {
            Range=range;Traces++; // Deliberately return contacts beyond range: the combat binding must reject them too.
            contact=new R24DualContact{Point=new Float3(origin.X,origin.Y,origin.Z+Distance),TargetId=World?null:"target",WorldCollider=World?this:null,Head=Head};return true;
        }
        public bool DamageTarget(string id,float amount,Float3 origin,bool head)
        {
            var before=Round.ReadTarget(id);bool applied=Round.ApplyTargetDamage(id,amount);var after=Round.ReadTarget(id);
            Need(head==Head&&Math.Abs(before.Shield-after.Shield+before.Health-after.Health-amount)<.0001f,"Local target feedback receives actual falloff damage");Amount+=amount;DamageCalls++;return applied;
        }
        public void DamageWorld(object collider,float amount,Float3 point,Float3 direction){Amount+=amount;DamageCalls++;}
    }
    static R24DualHandInput ShotgunHand(bool fire){return new R24DualHandInput{Tracked=true,InputReady=true,Trigger=fire?1:0,FlickPoint=new Float3(0,1,0),Forward=new Float3(0,0,1),FiringPose=new R23ShotPose(new Float3(0,1,0),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1))};}
    static R24DualFrame ShotgunFrame(bool fire){return new R24DualFrame{Enabled=true,Focused=true,DeltaSeconds=.02f,DominantHand=1,Left=ShotgunHand(fire),Right=ShotgunHand(fire)};}
    static void R26LocalShotguns()
    {
        const float reach=3700;
        float[] distances={0,8,16.5f,25,40,100,180,400,reach,reach+.125f};
        float[] scales={1,1,.625f,.25f,.25f,.25f,.25f,.25f,.25f,0};
        for(int i=0;i<distances.Length;i++){
            float d=distances[i],scale=scales[i];
            Need(Math.Abs(R23M1014Definition.DamageAtDistance(d)-9.625f*scale)<.0001f,"M1014 boundary damage at "+d);
            Need(Math.Abs(R24Dt11Definition.DamageAtDistance(d)-12*scale)<.0001f,"DT11 boundary damage at "+d);
            foreach(string id in new[]{"m1014","dt11","matadors"})for(int stars=1;stars<=5;stars++){
                R24LanBallisticProfile profile;Need(R24LanBallisticProfile.TryRead(id,out profile),"Host profile");
                float pellet=id=="m1014"?9.625f:id=="dt11"?12:(16+4*stars)/8f;
                Need(Math.Abs(profile.Damage(stars,false,d)-pellet*scale)<.0001f&&Math.Abs(profile.Damage(stars,true,d)-pellet*scale)<.0001f,"Host shotgun boundary damage "+id+" at "+d);
            }
        }
        foreach(float invalid in new[]{-1f,float.NaN,float.PositiveInfinity})Need(R23M1014Definition.DamageScaleAtDistance(invalid)==0,"Invalid shotgun distance rejected");
        foreach(string id in new[]{"matadors","uzis"})for(int stars=1;stars<=5;stars++)for(int i=0;i<distances.Length;i++)foreach(bool world in new[]{false,true})foreach(bool head in new[]{false,true}){
            var round=new R23RoundState(new[]{"target"},new ShotgunQuery(),true);string instance="check/"+id;
            Need(round.RegisterWeaponInstance(instance,id,stars)&&round.SelectWeapon(instance)&&round.EquipDual(1,true,true),"Native dual owner equipped");
            round.StepDual(ShotgunFrame(false));Need(round.StepDual(ShotgunFrame(true)),"Both native hands fire");
            var step=round.LastDualStep;var combat=new R24DualCombat();
            float expected=id=="matadors"?(16+4*stars)*scales[i]:distances[i]<=reach?14:0;
            foreach(var volley in new[]{step.Dominant,step.Other}){
                var port=new ShotgunPort{Round=round,Distance=distances[i],World=world,Head=head};combat.Apply(volley,port);
                Need(port.Traces==volley.PelletCount&&port.Range==reach,"Correct local ray/tracer range for both hands of "+id);
                Need(Math.Abs(port.Amount-expected)<.0001f,"Target/world falloff "+id+" at "+distances[i]);
                Need(expected>0||port.DamageCalls==0,"No target/world damage beyond range");
                Need(combat.Apply(volley,port)==0&&port.Traces==volley.PelletCount,"Volley cannot apply twice");
            }
            round.Retire();
        }
    }
    internal static void R26ShotgunChecks(){int before=checks;R26LocalShotguns();R26AllShotgunReach();Console.WriteLine("PASS SHOTGUN: "+(checks-before)+" checks (local target/world, both hands, five rarities, boundaries, host ranges)");}
    static void R26AllShotgunReach()
    {
        foreach(string profile in new[]{"m1014","dt11","matadors"})using(var pair=new Pair()){
            float hostReach=0;for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){hostReach=Math.Max(hostReach,r);return r;});
            var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0);string hash=new string('b',64);
            StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor;float hx=8;for(int i=0;i<3;i++)KeepRaw(pair,hash,0,hx);
            string instance=profile;
            if(profile!="m1014"){
                instance="shared/"+profile;
                Need(pair.Host.RequestLoot("px4",1,true,new Float3(8,1,0)),"Free shotgun slot");
                Need(pair.Host.RegisterWorldWeapon(instance,profile,1,new Float3(8,1,0))&&pair.Host.RequestLoot(instance,1,false,new Float3()),"Host takes "+profile);
            }
            while(hx<45){hx=Math.Min(45,hx+.2f);KeepRaw(pair,hash,0,hx);}for(int i=0;i<4;i++)KeepRaw(pair,hash,0,hx);
            var muzzle=new Float3(hx-.4f,1.6f,0);var chest=Dir(muzzle,new Float3(0,1.25f,0));var rays=new[]{chest,chest,chest,chest,chest,chest,chest,chest};R26HitConfirm hit=null;float before=Health(pair.Host,guest).Health;
            Need(pair.Host.SubmitShot(profile,0,muzzle,rays,instance),"Far shotgun volley accepted (consumes a shell)");KeepRaw(pair,hash,0,hx);
            for(int i=0;i<3;i++)KeepRaw(pair,hash,0,hx);Need(hostReach>0&&hostReach<=8.01f&&R24LanBallisticProfile.MaximumRange==3700,"Finite 20 ms pellet segments, shared 3,700 m cap");
            Need(pair.Host.TryDequeueHitConfirm(out hit)&&hit.Victim==guest&&Health(pair.Host,guest).Health<before,"Host confirms shotgun damage beyond the former 40 m limit");
            Need(Math.Abs(hit.Total-(profile=="m1014"?77:profile=="dt11"?96:20)*.25f)<.0001f,"Host far damage retains 25% for "+profile);
            Console.WriteLine("PASS host "+profile+": host trace cap 3,700 m and 25% damage at 44.6 m");
        }
    }
}
