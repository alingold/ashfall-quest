using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;

// Actual production owners/Health only. Synthetic semantic contact inputs test
// the tracker boundary; they do not claim Unity collision/LOS/render proof.
class Native141220Cases : IInteractionQuery
{
    static int count;
    static void Need(bool value,string why){count++;if(!value)throw new Exception(why);}
    public bool HasLineOfSight(string a,string b,Float3 from,Float3 to,float range){return true;}
    static R23ShotPose Unit{get{return new R23ShotPose(new Float3(),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1));}}
    static R23M1014Pose Muzzle{get{return new R23M1014Pose{Forward=Unit.Forward,Right=Unit.Right,Up=Unit.Up};}}
    void Knife()
    {
        var round=new R23RoundState(new[]{"target"},this,true);string id;
        Need(round.CreateWeaponInstance("knife",1,out id)&&round.SelectWeapon(id),"real exact knife instance");
        var owner=round.Melee;StepKnife(round,owner,id,-.55f);for(int i=1;i<=22;i++)StepKnife(round,owner,id,-.55f+i*.05f);
        Need(owner.AcceptedContacts==1&&round.ReadTarget("target").Health==65&&round.ReadTarget("target").Shield==100,"actual native tracker first35 bypass");
        int frames=owner.FrameCount,suspensions=owner.SuspensionCount;string instance=owner.InstanceId;
        Need(owner.HistoryValid&&!owner.IsRetired&&owner.LastFrameStatus=="spatial sweep","outside endpoint retains live source history");
        // Reading presentation/diagnostic state between simulation calls must not
        // clear contact history or impose a clock-based rearm delay.
        var snapshot=owner.Snapshot;long accepted=owner.AcceptedContacts;
        Need(owner.FrameCount==frames&&owner.SuspensionCount==suspensions&&owner.InstanceId==instance&&accepted==snapshot.Shots,"read-only inter-frame state preserves actual owner");
        for(int i=1;i<=11;i++)StepKnife(round,owner,id,.55f-i*.05f);
        Need(owner.AcceptedContacts==2&&round.ReadTarget("target").Health==30&&round.ReadTarget("target").Shield==100,"actual source geometric exit/reentry35 without time gate");
        for(int i=0;i<4;i++)StepKnife(round,owner,id,0);
        Need(owner.AcceptedContacts==2&&round.ReadTarget("target").Health==30,"rest inside cannot repeat real Health loss");
        owner.SuspendControls();Need(!owner.HistoryValid&&owner.SuspensionCount==suspensions+1&&owner.LastSuspension=="external custody/focus/pause","actual suspension is observable");
        StepKnife(round,owner,id,0);Need(owner.HistoryValid&&owner.LastFrameStatus=="seed actual overlaps"&&owner.AcceptedContacts==2,"resume inside seeds actual overlap without hit");
        owner.BeginFrame(.2f,true,Unit,new Float3(),new Float3(0,.221f,0),new Float3(),new string[0]);
        Need(!owner.HistoryValid&&owner.LastSuspension=="delta outside source(0,.15]","source delta suspension remains distinct from geometric reentry");
    }
    static void StepKnife(R23RoundState round,R24MeleeRuntime owner,string id,float x)
    {
        string[] now=Inside(x,.29f);var sweep=owner.BeginFrame(.02f,true,Unit,new Float3(x,0,0),new Float3(x,.221f,0),new Float3(x,-.08f,0),now);if(sweep==null)return;
        for(int step=0;step<sweep.Steps;step++)
        {
            Float3 a,b,c,d;sweep.ReadStep(step,out a,out b,out c,out d);
            var swept=Math.Min(a.X,c.X)<=.29f&&Math.Max(a.X,c.X)>=-.29f?new[]{"target"}:new string[0];
            foreach(var entry in owner.SampleStep(sweep,step,swept,Inside(c.X,.29f),Inside(c.X,.298f)))
                if(!round.ApplyKnifeContact(id,entry))throw new Exception("real native knife Health contact refused");
        }
    }
    static string[] Inside(float x,float radius){return Math.Abs(x)<=radius?new[]{"target"}:new string[0];}
    static HandlingInput Input(bool trigger=false,bool support=false,Float3? point=null)
    {
        // The source hand is already holding its gun; only the trigger changes.
        var frame=new R23HandsInputFrame{LeftTracked=true,RightTracked=true,RightGrip=.8f,RightTrigger=trigger?1:0,LeftGrip=support?.8f:0};
        return R23HandRoleRules.Project(frame.Snapshot,1,point??new Float3(),1);
    }
    static bool Step(R23RoundState round,float dt,bool trigger=false,bool support=false,Float3? point=null)
    {return round.StepDt11(dt,Input(trigger,support,point),HandlingAction.Sample,Muzzle,trigger,false);}
    static void Hit(R23RoundState round)
    {var volley=round.LastDt11Volley;var rays=volley.Directions;var contacts=new R23M1014Contact[8];for(int i=0;i<8;i++)contacts[i]=new R23M1014Contact(i,"target",R23WeaponPoseMath.Scale(rays[i],2));Need(round.ApplyDt11Contacts(volley,contacts)==1,"actual accepted eight-pellet native Health group");}
    void Dt11()
    {
        var round=new R23RoundState(new[]{"target"},this,true);string id;
        Need(round.CreateWeaponInstance("dt11",1,out id)&&round.SelectWeapon(id),"real exact DT11 instance");
        Step(round,.02f);Need(Step(round,.02f,true)&&round.ActiveLoaded==1,"actual first barrel accepted");Hit(round);
        for(int i=0;i<4;i++)Need(!Step(round,.1f,true)&&round.ActiveLoaded==1,"source semi held refusal");
        Step(round,.2f);for(int i=0;i<8;i++)Step(round,.025f);
        Need(Step(round,.02f,true)&&round.ActiveSnapshot.Phase==ReloadPhase.NeedsMagazine,"actual second barrel auto-open");Hit(round);
        Need(round.ReadTarget("target").Health==8&&round.ReadTarget("target").Shield==0,"two real96 groups leave actual8 Health");
        Step(round,.02f);var offer=R24Dt11Definition.Offer(.72f);Step(round,.02f,false,true,offer);
        var down=new Float3(offer.X,offer.Y-.025f,offer.Z);Step(round,.02f,false,true,down);
        Need(round.ActiveSnapshot.Phase==ReloadPhase.NeedsRack&&round.ActiveReserve==478,"source DOWN seats two actual finite shells");
        Step(round,.02f,false,true,new Float3(down.X,down.Y+.0275f,down.Z));
        Need(round.ActiveSnapshot.Phase==ReloadPhase.Ready&&round.ActiveLoaded==2,"source same-grip UP closes real2");
        Step(round,.2f);for(int i=0;i<8;i++)Step(round,.025f);
        Need(round.ActiveSnapshot.CooldownSeconds==0&&!round.ActiveSnapshot.TriggerReleaseRequired,"original fixture neutral timings satisfy source cadence/rearm without added delay");
        long shots=round.ActiveSnapshot.Shots;
        Need(Step(round,.02f,true)&&round.ActiveLoaded==1&&round.ActiveSnapshot.Shots==shots+1&&round.ActiveReserve==478,"post-reload actual accepted one-shell native debit");Hit(round);
        Need(round.ReadTarget("target").Health==0&&round.ReadTarget("target").Shield==0,"post-reload actual pellet volley consumes remaining8 Health");
        var held=new R23HandsInputFrame{RightTracked=true,RightGrip=.8f}.Snapshot.Right;
        var released=new R23HandsInputFrame{RightTracked=true}.Snapshot.Right;
        Need(!R23WeaponPickupRules.GripNeutral(held)&&R23WeaponPickupRules.GripNeutral(released),"trigger neutral differs from new world-pickup grip rearm");
    }
    static void Main(){var tests=new Native141220Cases();tests.Knife();tests.Dt11();Console.WriteLine("PASS "+count+" actual live melee/DT11 owner + Health boundary cases; Unity query/yield lifecycle unrun");}
}
