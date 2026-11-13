using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
static class M1014Cases
{
    sealed class Gate:IAuthorityGate{public bool Active=true;public RuleResult Validate(CommandContext c){return Active?RuleResult.Accept():RuleResult.Reject("retired private actor");}}
    static int count;static long serial;
    static CommandContext Command(){return new CommandContext("m1014-test","player","m"+(++serial),serial,0,true);}
    static R23M1014Pose Pose(){return new R23M1014Pose{Origin=new Float3(0,1,0),Forward=new Float3(0,0,1),Right=new Float3(1,0,0),Up=new Float3(0,1,0)};}
    static HandlingInput Input(float dt=0){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,DeltaSeconds=dt,ForegripDistance=1};}
    static void Need(bool condition,string label){count++;if(!condition)throw new Exception(label);}
    static bool Near(double a,double b,double tolerance=.00001){return Math.Abs(a-b)<tolerance;}
    static R23M1014Owner Owner(Gate gate,R23RifleReserve reserve){return new R23M1014Owner(Command(),"m1014/"+serial,reserve,gate);}
    static RuleResult Step(R23M1014Owner owner,HandlingInput input,HandlingAction action,out R23M1014Volley volley,bool ejectAfter=false){var c=Command();input.Sequence=c.Tick;return owner.Apply(c,action,input,Pose(),out volley,ejectAfter);}
    static void Main()
    {
        var gate=new Gate();var reserve=new R23RifleReserve(480);var owner=Owner(gate,reserve);var input=Input();R23M1014Volley shot;
        Need(owner.Snapshot.Loaded==6&&owner.Reserve==480,"source initial six plus finite480 reserve");
        input.Trigger=true;Step(owner,input,HandlingAction.Sample,out shot);Need(shot==null&&owner.Snapshot.Loaded==6,"held acquisition requires trigger release");
        input.Trigger=false;Step(owner,input,HandlingAction.Sample,out shot);input.Trigger=true;Step(owner,input,HandlingAction.Sample,out shot);
        Need(shot!=null&&owner.Snapshot.Loaded==5&&owner.Snapshot.Shots==1,"one accepted volley debits one actual native shell");
        Need(shot.Directions.Length==8&&shot.PelletDamage==9.625f&&shot.Range==40,"0991 total77 and eight bounded source rays");
        var original=shot.Directions;original[0]=new Float3(9,9,9);Need(shot.Directions[0].Z<1,"accepted pellet directions are immutable clones");
        var directions=R23M1014Volley.DirectionsFor(Pose());double inner=Math.Tan(3.5*Math.PI/180)*.4,outer=Math.Tan(3.5*Math.PI/180);
        for(int i=0;i<8;i++){var d=directions[i];Need(Near(Math.Sqrt(d.X*d.X+d.Y*d.Y)/d.Z,i%2==0?inner:outer),"actual alternating source spread radii");Need(Near(d.X*d.X+d.Y*d.Y+d.Z*d.Z,1),"normalized source pellet direction");}
        Need(Near(R23M1014Definition.DamageAtDistance(8)*8,77)&&Near(R23M1014Definition.DamageAtDistance(25)*8,19.25)&&R23M1014Definition.DamageAtDistance(41)==0,"source40m ray and8..25m falloff");
        foreach(float hz in new[]{72f,90f,120f})
        {
            List<float> first=null;
            foreach(bool supported in new[]{false,true})
            {
                owner=Owner(gate,new R23RifleReserve(480));input=Input();Step(owner,input,HandlingAction.Sample,out shot);input.Trigger=true;input.Grip=supported;input.ForegripDistance=supported?.1f:1;
                var stamps=new List<float>();float time=0;
                for(int f=0;f<600&&stamps.Count<6;f++){input.DeltaSeconds=f==0?0:1/hz;time+=input.DeltaSeconds;Step(owner,input,HandlingAction.Sample,out shot);if(shot!=null)stamps.Add(time);}
                Need(stamps.Count==6&&owner.Snapshot.Loaded==0&&owner.Snapshot.Phase==ReloadPhase.NeedsMagazine,"six source auto volleys at "+hz+" support "+supported);
                Need(Math.Abs(stamps[5]-5*R23M1014Definition.Interval)<=1/hz+.00002,"source conditional midpoint within one frame");
                Need(Near(owner.Snapshot.RecoilPitch,(supported?2:3.5)*Math.PI/180*1.25),"source plateau also correct on final supported shot");
                if(first==null)first=stamps;else for(int i=0;i<6;i++)Need(stamps[i]==first[i],"one/two hand source cadence identical");
            }
        }
        foreach(bool left in new[]{false,true})
        {
            reserve=new R23RifleReserve(2);owner=Owner(gate,reserve);input=Input();input.LeftHanded=left;Step(owner,input,HandlingAction.Sample,out shot);Step(owner,input,HandlingAction.Eject,out shot);Need(owner.Reserve==8&&owner.Snapshot.Loaded==0,"manual source eject refunds actual remaining rounds");
            // Spend six refunded shells elsewhere to exercise actual partial finite reserve.
            reserve.Take(6);input.SupportLocal=R23M1014Definition.Offer;Step(owner,input,HandlingAction.Sample,out shot);input.Grip=true;Step(owner,input,HandlingAction.Sample,out shot);
            Need(owner.Snapshot.Phase==ReloadPhase.NeedsMagazine&&owner.Snapshot.ClaimsSupport&&owner.Reserve==2,"fresh pack grip claims support but does not debit");
            input.SupportLocal=new Float3(0,-.23f,.14f);Step(owner,input,HandlingAction.Sample,out shot);Need(owner.Reserve==2&&owner.Snapshot.Seated==0,"upward jitter cannot seat shell pack");
            input.SupportLocal=R23M1014Definition.Socket;Step(owner,input,HandlingAction.Sample,out shot);Need(owner.Reserve==0&&owner.Snapshot.Seated==2&&owner.Snapshot.Loaded==0&&owner.Snapshot.Phase==ReloadPhase.NeedsRack,"source UP seats only finite2; no chambering yet");
            input.Grip=false;Step(owner,input,HandlingAction.Sample,out shot);input.SupportLocal=new Float3(left?-.06f:.06f,.045f,.10f);input.Grip=true;Step(owner,input,HandlingAction.Sample,out shot);input.SupportLocal=new Float3(left?-.06f:.06f,.045f,.05f);Step(owner,input,HandlingAction.Sample,out shot);
            Need(owner.Snapshot.Phase==ReloadPhase.NeedsRack&&Near(owner.Snapshot.BoltTravel,.05),"source mirrored handle follows actual partial BACK");
            input.Grip=false;Step(owner,input,HandlingAction.Sample,out shot);Need(owner.Snapshot.Loaded==0&&owner.Snapshot.BoltTravel==0,"release cancels partial BACK without granting rounds");
            input.SupportLocal=new Float3(left?-.06f:.06f,.045f,.10f);input.Grip=true;Step(owner,input,HandlingAction.Sample,out shot);input.SupportLocal=new Float3(left?-.06f:.06f,.045f,.01f);Step(owner,input,HandlingAction.Sample,out shot);
            Need(owner.Snapshot.Loaded==2&&owner.Snapshot.Seated==0&&owner.Snapshot.Phase==ReloadPhase.Ready&&owner.Reserve==0,"source .085m BACK chambers finite seated pack");
            Need(owner.Snapshot.TriggerReleaseRequired,"reload completion requires trigger rearm");
        }
        reserve=new R23RifleReserve(0);owner=Owner(gate,reserve);input=Input();input.Desktop=true;Step(owner,input,HandlingAction.ReloadStep,out shot);Need(owner.Reserve==6&&owner.Snapshot.Loaded==0,"desktop eject refunds six");
        Step(owner,input,HandlingAction.ReloadStep,out shot);Need(owner.Snapshot.Seated==6&&owner.Snapshot.Phase==ReloadPhase.NeedsRack,"desktop single UP pack stage");Step(owner,input,HandlingAction.ReloadStep,out shot);Need(owner.Snapshot.Loaded==6&&owner.Reserve==0,"desktop BACK stage completes actual reload");
        input=Input(.2f);Step(owner,input,HandlingAction.Sample,out shot);input.Trigger=true;Step(owner,input,HandlingAction.Sample,out shot);Need(shot!=null,"ready restored after desktop reload");
        int loaded=owner.Snapshot.Loaded;owner.SuspendControls();Step(owner,input,HandlingAction.Sample,out shot);Need(shot==null&&owner.Snapshot.Loaded==loaded,"suspension cannot repeat held trigger");
        input.Trigger=false;Step(owner,input,HandlingAction.Sample,out shot);input.Trigger=true;input.DeltaSeconds=.25f;Step(owner,input,HandlingAction.Sample,out shot);Need(shot!=null,"release/repress resumes actual native owner");
        loaded=owner.Snapshot.Loaded;gate.Active=false;Step(owner,input,HandlingAction.Sample,out shot);Need(shot==null&&owner.Snapshot.Loaded==loaded,"released gallery authority cannot mutate ammo");gate.Active=true;
        var c=Command();input.Sequence=c.Tick;owner.Apply(c,HandlingAction.Sample,input,Pose(),out shot);loaded=owner.Snapshot.Loaded;owner.Apply(c,HandlingAction.Sample,input,Pose(),out shot);Need(shot==null&&owner.Snapshot.Loaded==loaded,"replay cannot repeat accepted volley");
        var controls=new R23M1014Controls();Need(controls.Project(true,true,true,0,-1).Action==HandlingAction.Sample,"held click/stick starts disarmed");controls.Project(true,false,true,0,0);
        Need(controls.Project(true,true,true,0,0).Action==HandlingAction.Eject,"source primary click fresh edge");Need(controls.Project(true,true,true,0,0).Action==HandlingAction.Sample,"source held click cannot eject repeatedly");
        controls.Project(true,false,true,0,0);Need(controls.Project(true,true,false,0,0).Action==HandlingAction.Sample,"left-stick sprint ownership blocks manual click");
        Need(controls.Project(true,false,true,0,-1).EjectAfterShot,"source stick-down is projected after fire");Need(!controls.Project(true,false,true,0,-1).EjectAfterShot,"held stick-down cannot re-eject");
        controls.Project(false,false,true,0,0);Need(!controls.Project(true,false,true,0,-1).EjectAfterShot,"focus return requires stick neutral");
        Console.WriteLine("PASS "+count+" actual M1014 native-owner/cadence/pellet/reload/control cases; CPU only.");
    }
}
