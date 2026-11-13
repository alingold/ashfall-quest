using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;

internal sealed class Actors : IConstructionActors
{
    public BuilderState State;
    public bool TryRead(string id,out BuilderState state) { state=State;return id=="alice"||id=="bob"; }
}
internal sealed class Gate : IAuthorityGate
{
    public bool Allow=true;
    public RuleResult Validate(CommandContext c) {return Allow?RuleResult.Accept():RuleResult.Reject("actor_denied");}
}
internal sealed class World : IConstructionWorld
{
    public bool Ground=true, Buried=false, Visible=true;
    public string Block="";
    public bool HasStaticContact(Panel p,float tolerance) {return Ground&&Math.Abs(p.Center.Y-Geometry.HalfSize(p).Y)<=tolerance;}
    public bool IsBuried(Panel p) {return Buried||(Ground&&p.Center.Y+Geometry.HalfSize(p).Y<0);}
    public string Clearance(Panel p,string actor) {return Block;}
    public bool HasLineOfSight(string actor,Float3 origin,Float3 target) {return Visible;}
}
internal static class ConstructionTests
{
    static int tests;
    static long tick;
    static Actors actors;
    static World world;
    static Gate gate;
    static ConstructionService service;
    static float S=Geometry.Side;
    static void Check(bool pass,string name) {if(!pass)throw new Exception("FAIL "+name);tests++;Console.WriteLine("PASS "+name);}
    static CommandContext C(string command) {return new CommandContext("test", "alice",command,++tick,7,true);}
    static void Start(bool infinite)
    { actors=new Actors();world=new World();gate=new Gate();service=new ConstructionService("test",7,gate,actors,world,infinite);Floor(1); }
    static void Floor(float y) {actors.State=new BuilderState(new Float3(S/2,y,S/2),new Float3(0,-1,0),new Float3(S/2,y,S/2),true,true);}
    static void Wall(float level) {actors.State=new BuilderState(new Float3(S/2,S*(level+.5f),-1),new Float3(0,0,1),new Float3(S/2,S*(level+.5f),-1),true,true);}
    static void Main()
    {
        Check(Math.Abs(S-2.820318f)<.000001f,"calibrated eight-foot source panel");
        var hits=Geometry.Intersections(new Float3(-S/2,1,-S/2),new Float3(0,-1,0));
        Check(hits.Count>0&&hits[0].Kind=="floor"&&Math.Abs(hits[0].Center.X+S/2)<.00001&&hits[0].Slot=="floor:-1:0:1:0","negative lattice and reflected source slot");
        Check(Geometry.Intersections(new Float3(float.MaxValue,0,0),new Float3(1,0,0)).Count==0,"extreme pose bounded rejection");
        Check(Geometry.Intersections(new Float3(),new Float3(float.NaN,0,0)).Count==0,"nonfinite ray rejected");
        hits=Geometry.Intersections(new Float3(S/2,S/2,-.1f),new Float3(0,0,1));
        Check(hits.Count>0&&hits[0].Center.Z>2,"eighteen-inch wall minimum skips near face");
        var a=new Panel("floor",new Float3(0,0,0),1,false,0);
        var b=new Panel("floor",new Float3(S,0,0),1,false,0);
        var corner=new Panel("floor",new Float3(S,0,S),1,false,0);
        Check(Geometry.Connected(a,b)&&!Geometry.Connected(a,corner),"shared edge supports; corner cannot");
        Check(!Geometry.Connected(a,new Panel("floor",new Float3(S+.06f,0,0),1,false,0)),"finite contact gap rejection");
        Start(false);var d=service.Place(C("floor"));
        Check(d.Accepted&&d.Cost==1&&d.Panels[0].Kind=="floor"&&service.Charges("alice")==998,"ground floor production placement and charge");
        Check(service.Snapshot()[0].Health==140,"source floor health");
        int count=service.Snapshot().Length;d=service.Place(C("duplicate"));
        Check(!d.Accepted&&service.Snapshot().Length==count,"occupied slot cannot be placed twice");
        Start(false);Wall(0);
        actors.State=new BuilderState(new Float3(S-.001f,S/2,-1),new Float3(0,0,1),new Float3(S,S/2,-1),true,true);
        string stable=service.Preview("alice").Panels[0].Slot;
        actors.State=new BuilderState(new Float3(S+.001f,S/2,-1),new Float3(0,0,1),new Float3(S,S/2,-1),true,true);
        Check(service.Preview("alice").Panels[0].Slot==stable,"eight-centimeter hysteresis retains face across edge jitter");
        service.CancelPreview("alice");Check(service.Preview("alice").Panels[0].Slot!=stable,"cancel clears preferred face");
        Start(false);Floor(S+1);d=service.Place(C("pair"));
        Check(d.Accepted&&d.Cost==2&&d.Panels.Length==2&&d.Panels[0].LowerSupport&&service.Charges("alice")==997,"automatic lower wall and atomic pair cost");
        Check(service.Snapshot()[0].Health==180,"source support wall health");
        Start(false);world.Block="objective_clearance";d=service.Place(C("blocked"));
        Check(!d.Accepted&&d.Reason=="objective_clearance"&&service.Charges("alice")==999&&service.Snapshot().Length==0,"blocked construction has no cost or partial spawn");
        world.Block="player_clearance";Check(service.Place(C("player")).Reason=="player_clearance","physics player collision port rejects");
        world.Block="";world.Visible=false;Check(service.Place(C("los")).Reason=="occluded","semantic LOS contract enforced");
        world.Visible=true;world.Buried=true;Check(service.Place(C("buried")).Reason=="buried_floor","buried floor rejected");
        Start(false);world.Ground=false;Check(service.Place(C("unsupported")).Reason=="unsupported","floating components cannot support themselves");
        Start(false);var preview=service.Preview("alice");world.Block="player_clearance";
        Check(preview.Accepted&&!service.Place(C("changed")).Accepted&&service.Snapshot().Length==0,"commit revalidates collision after preview");
        Start(false);Floor(.1f);Check(service.Place(C("head")).Reason=="head_clearance","head embedded in floor cannot place");
        Start(false);actors.State=new BuilderState(new Float3(),new Float3(0,0,1),new Float3(),true,false);
        Check(service.Place(C("tracking")).Reason=="input_ineligible","tracking loss disarms build");
        Start(false);Check(!service.Place(new CommandContext("other","alice","wrong_session",++tick,7,true)).Accepted,"cross-session rejected");
        Check(!service.Place(new CommandContext("test","alice","old_epoch",++tick,6,true)).Accepted,"old authority epoch rejected");
        Check(!service.Place(new CommandContext("test","alice","client",++tick,7,false)).Accepted,"client mutation rejected");
        gate.Allow=false;Check(service.Place(C("denied")).Reason=="actor_denied","central authority gate used");gate.Allow=true;
        var cmd=C("supply");Check(service.AwardSupply(cmd,3).Accepted&&service.Charges("alice")==1002,"source uncapped supply grants");
        Check(!service.AwardSupply(cmd,3).Accepted&&service.Charges("alice")==1002,"same tick replay cannot duplicate supply");
        Check(service.AwardSupply(C("supply"),3).Reason=="replay"&&service.Charges("alice")==1002,"command replay with newer tick rejected");
        Check(!service.AwardSupply(C("overflow"),Int32.MaxValue).Accepted,"supply integer overflow rejected");
        Start(true);Check(service.Place(C("infinite")).Cost==0&&service.Charges("alice")==999,"infinite-build mode preserves balance");
        Start(false);Wall(0);Check(service.Place(C("root")).Accepted,"root wall placed");
        Wall(1);Check(service.Place(C("upper")).Accepted,"upper wall uses existing finite support");
        Wall(2);Check(service.Place(C("top")).Accepted,"third wall uses support chain");
        var panels=service.Snapshot();string root=null;
        foreach(var p in panels)if(p.Shape.Center.Y<S)root=p.Id;
        Check(service.Dismantle(C("manual"),root).Reason=="manual_dismantle_unavailable","no invented manual dismantle or refund");
        Check(service.Damage(C("nan"),root,float.NaN).Reason=="damage","invalid damage rejected");
        Check(service.Damage(C("partial"),root,179).Accepted&&service.Snapshot().Length==3,"damage below health retains wall");
        Check(service.Damage(C("break"),root,1).Accepted&&service.Snapshot().Length==2,"lethal damage removes root");
        int balance=service.Charges("alice");service.Advance(10,true);Check(service.Snapshot().Length==2,"paused simulation freezes collapse");
        service.Advance(.29f,false);Check(service.Snapshot().Length==2,"collapse delay honored");
        service.Advance(.02f,false);Check(service.Snapshot().Length==1,"first severed neighbor collapses at 0.30 seconds");
        service.Advance(.30f,false);Check(service.Snapshot().Length==0&&service.Charges("alice")==balance,"support cascade finishes without refund");
        Start(false);Wall(0);service.Place(C("rescue-root"));Wall(1);service.Place(C("rescue-upper"));world.Ground=false;service.Advance(.1f,false);world.Ground=true;service.Advance(.4f,false);
        Check(service.Snapshot().Length==2,"restored static anchor rescues pending collapse");
        Start(false);
        for(int i=0;i<256;i++)
        {
            actors.State=new BuilderState(new Float3((i+.5f)*S,1,S/2),new Float3(0,-1,0),new Float3((i+.5f)*S,1,S/2),true,true);
            if(!service.Place(C("capacity-"+i)).Accepted)throw new Exception("capacity setup "+i);
        }
        actors.State=new BuilderState(new Float3(256.5f*S,1,S/2),new Float3(0,-1,0),new Float3(256.5f*S,1,S/2),true,true);
        Check(service.Place(C("over-capacity")).Reason=="capacity"&&service.Snapshot().Length==256&&service.Charges("alice")==743,"256-panel live cap rejects without charging");
        Console.WriteLine("TOTAL "+tests+" checks passed");
    }
}
