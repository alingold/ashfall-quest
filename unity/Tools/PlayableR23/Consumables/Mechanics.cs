using System;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable.Consumables;
class Mechanics
{
    static void Check(bool okay,string label){if(!okay)throw new Exception(label);Console.WriteLine("PASS "+label);}
    sealed class Effects:IR23ConsumableEffects
    {
        public R23ConsumableVitals V=new R23ConsumableVitals{Alive=true,Health=20,Shield=0,Stamina=1,MaxStamina=8};public int Calls;public bool Reject;
        public R23ConsumableVitals ReadVitals(){return V;}
        public bool TryApplyGain(float health,float shield,bool refill){if(Reject)return false;Calls++;V.Health=Math.Min(100,V.Health+health);V.Shield=Math.Min(100,V.Shield+shield);if(refill)V.Stamina=V.MaxStamina;return true;}
    }
    sealed class Query:IInteractionQuery {public bool HasLineOfSight(string actor,string owner,Float3 from,Float3 to,float range){return true;}}
    static ActorFrame Frame(){return new ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,OtherHandEligible=true,Head=new Float3(0,1.65f,0),Hand=new Float3(0,1,.2f),Forward=new Float3(0,0,1),LocalHand=new Float3(0,1,.2f),OtherLocal=new Float3(0,.1f,0),Anchor=new Float3(0,1.65f,.2f),HeadLocalAnchor=new Float3(0,0,.2f),ItemUpDot=0};}
    static bool Take(R23ConsumableState state,string id){return state.Execute(PickupAction.Reserve,id).Accepted&&state.Execute(PickupAction.Collect,id).Accepted&&state.Execute(PickupAction.Equip,id).Accepted;}
    static bool Peel(R23ConsumableState state,string id)
    {
        var frame=Frame();frame.OtherGrip=false;state.SetFrame(frame);state.Execute(PickupAction.SamplePeel,id);
        frame=Frame();frame.OtherGrip=true;state.SetFrame(frame);if(!state.Execute(PickupAction.BeginPeel,id).Accepted)return false;
        frame.OtherLocal=new Float3(0,-.01f,0);state.SetFrame(frame);return state.Execute(PickupAction.SamplePeel,id).Accepted;
    }
    static void ReadyBanana(R23ConsumableState state,string id)
    {state.Register(id,"banana",new Float3(0,1,.3f));state.SetFrame(Frame());if(!Take(state,id))throw new Exception("banana admission");for(int i=0;i<4;i++)if(!Peel(state,id))throw new Exception("banana peel");}
    static void Main()
    {
        var effects=new Effects();var state=new R23ConsumableState(effects,new Query());var frame=Frame();state.SetFrame(frame);state.Advance(.02f);
        Check(state.Register("soda0","soda",new Float3(0,1,.3f))&&!state.Register("soda0","soda",new Float3()),"world identity registration prevents duplicate item grant");
        Check(state.RepositionWorldItem("soda0",new Float3(0,1,.25f))&&state.Inspect("soda0").Position.Z==.25f,"map relocation changes only the existing available world source");
        Check(Take(state,"soda0")&&state.OwnedItems.Length==1,"source reserve collect equip transfers real instance once");
        Check(!state.RepositionWorldItem("soda0",new Float3(99,0,0))&&state.OwnedItems.Length==1,"map relocation refuses owned exact instances");
        Check(!state.Execute(PickupAction.Collect,"soda0").Accepted&&state.OwnedItems.Length==1,"repeated pickup cannot duplicate inventory");
        Check(state.Execute(PickupAction.Open,"soda0").Accepted&&state.Phase==Preparation.Consume,"free other-hand cap opening uses source reach check");
        Check(state.Execute(PickupAction.Consume,"soda0").Accepted&&state.OwnedItems.Length==0&&state.PendingSoda==50&&effects.V.Shield==0,"soda consumes once and schedules source50 point dose");
        Check(!state.RepositionWorldItem("soda0",new Float3())&&state.PendingSoda==50,"map relocation cannot revive consumed sources or alter pending effects");
        Check(!state.Execute(PickupAction.Consume,"soda0").Accepted,"consumed exact instance cannot apply duplicate effect");
        for(int i=0;i<100;i++){state.SetFrame(Frame());state.Advance(.1f);}Check(Math.Abs(effects.V.Shield-50)<.01&&state.PendingSoda<.01,"soda actual effect port receives50 shield over10 active seconds");
        state.Register("shaker0","shaker",new Float3(0,1,.3f));state.SetFrame(Frame());Check(Take(state,"shaker0"),"shaker grip pickup and equip works");
        for(int i=0;i<130;i++){frame=Frame();frame.LocalHand=new Float3(0,1+(i%2==0?.04f:0),.2f);state.SetFrame(frame);state.Advance(.02f);state.Execute(PickupAction.SampleShake,"shaker0");}
        Check(state.Phase==Preparation.Consume,"source shake distance reversals and1.3 charge rate reaches readiness");
        effects.Reject=true;state.SetFrame(Frame());Check(!state.Execute(PickupAction.Consume,"shaker0").Accepted&&state.OwnedItems.Length==1,"refused effect leaves the real owned item available");effects.Reject=false;
        state.SetFrame(Frame());Check(state.Execute(PickupAction.Consume,"shaker0").Accepted&&Math.Abs(effects.V.Shield-100)<.01,"shaker applies actual instant50 shield through effect port");
        state=new R23ConsumableState(effects,new Query());state.SetFrame(Frame());state.Advance(.02f);state.SetOtherSlotsUsed(2);
        foreach(string id in new[]{"a","b","c"})state.Register(id,"soda",new Float3(0,1,.3f));
        Check(Take(state,"a")&&Take(state,"b")&&!Take(state,"c")&&state.OwnedItems.Length==2,"two weapon slots leave one source soda stack of at most2");
        state.SetFrame(Frame());Check(state.Drop("a",new Float3(0,0,.5f)).Accepted&&state.OwnedItems.Length==1,"drop returns exact instance to world without a duplicate");
        frame=Frame();frame.Hand=new Float3(0,.15f,.5f);state.SetFrame(frame);Check(Take(state,"a")&&state.OwnedItems.Length==2,"dropped instance can be picked up again");
        frame=Frame();frame.Allowed=false;state.SetFrame(frame);state.Advance(.02f);Check(state.HeldId==null&&state.OwnedItems.Length==2,"pause or tracking loss stows preparation without deleting inventory");
        effects.V.Shield=0;state.SetFrame(Frame());state.Execute(PickupAction.Equip,"a");state.Execute(PickupAction.Open,"a");frame=Frame();frame.ItemUpDot=1;state.SetFrame(frame);Check(!state.Execute(PickupAction.Consume,"a").Accepted,"untilted held soda cannot consume");
        int effectsBeforeReset=effects.Calls;state.Reset();Check(state.OwnedItems.Length==0&&state.HeldId==null&&state.PendingSoda==0&&state.Inspect("a").Owner==null&&!state.Inspect("a").Removed&&effects.Calls==effectsBeforeReset,"new-round reset restores world starters without inventory or effect grants");
        state.Reset(false);Check(state.Inspect("a")==null&&state.Register("a","soda",new Float3()),"explicit map lifecycle clears old world identities for caller-owned placement");
        state=new R23ConsumableState(effects,new Query());effects.V.Health=20;effects.V.Stamina=1;state.SetFrame(Frame());state.Advance(.02f);
        state.Register("banana0","banana",new Float3(0,1,.3f));Check(Take(state,"banana0")&&state.Phase==Preparation.Open,"real banana world instance collects and equips closed");
        Check(!state.Execute(PickupAction.Open,"banana0").Accepted&&!state.Execute(PickupAction.Consume,"banana0").Accepted,"banana cannot bypass four actual peels");
        frame=Frame();frame.OtherGrip=true;state.SetFrame(frame);Check(state.Execute(PickupAction.BeginPeel,"banana0").Accepted&&state.PeelActive,"free opposite-hand grip starts a real source peel");
        frame.OtherLocal=new Float3(0,.05f,0);state.SetFrame(frame);state.Execute(PickupAction.SamplePeel,"banana0");Check(state.Peels==0&&state.PeelActive,"partial five-centimetre pull does not complete a peel");
        frame.OtherGrip=false;state.SetFrame(frame);state.Execute(PickupAction.SamplePeel,"banana0");Check(!state.PeelActive&&state.Peels==0,"releasing grip cancels partial peel without inventing progress");
        frame=Frame();frame.OtherGrip=true;frame.OtherHandEligible=false;state.SetFrame(frame);Check(!state.Execute(PickupAction.BeginPeel,"banana0").Accepted,"occupied or untracked opposite hand cannot peel");
        for(int i=0;i<4;i++){Check(Peel(state,"banana0")&&state.Peels==i+1,"source ten-centimetre peel "+(i+1)+" of4");}
        Check(state.Phase==Preparation.Consume,"four source peels expose ready fruit");
        frame=Frame();frame.ItemUpDot=1;state.SetFrame(frame);int beforeBanana=effects.Calls;
        Check(state.Execute(PickupAction.Consume,"banana0").Accepted&&effects.V.Health==100&&effects.V.Stamina==effects.V.MaxStamina&&effects.Calls==beforeBanana+1,"banana actual effect port applies capped100 health and full stamina without drink tilt");
        Check(!state.Execute(PickupAction.Consume,"banana0").Accepted&&effects.Calls==beforeBanana+1,"eaten banana cannot apply duplicate heal or stamina refill");
        effects.V.Health=100;effects.V.Stamina=1;ReadyBanana(state,"banana1");state.SetFrame(Frame());Check(state.Execute(PickupAction.Consume,"banana1").Accepted&&effects.V.Stamina==effects.V.MaxStamina,"full-health player can consume banana for real missing stamina");
        ReadyBanana(state,"banana2");state.SetFrame(Frame());Check(!state.Execute(PickupAction.Consume,"banana2").Accepted&&state.OwnedItems.Length==1,"full health and full stamina preserve prepared banana");
        state.Execute(PickupAction.Stow,"banana2");state.Execute(PickupAction.Equip,"banana2");frame=Frame();frame.OtherGrip=true;state.SetFrame(frame);state.Execute(PickupAction.BeginPeel,"banana2");frame.OtherLocal=new Float3(0,-.1f,.6f);state.SetFrame(frame);
        Check(!state.Execute(PickupAction.SamplePeel,"banana2").Accepted&&state.Peels==0&&!state.PeelActive,"over-half-metre peel movement cancels source gesture");
        state.SetOtherSlotsUsed(2);state.Register("banana3","banana",new Float3(0,1,.3f));state.SetFrame(Frame());Check(!Take(state,"banana3")&&state.OwnedItems.Length==1,"bananas remain unstacked in the shared three-slot loadout");
        var closed=R23BananaPeelGeometry.BuildStrip(0,0);var opened=R23BananaPeelGeometry.BuildStrip(0,1);
        Check(closed.Length==864&&opened.Length==864&&Math.Abs(closed[0].Y+.11f)<.000001&&Math.Abs(closed[closed.Length-1].Y-.11f)<.000001,"source quarter-shell grid retains22cm closed fruit length");
        Check(closed[0].X==opened[0].X&&closed[0].Y==opened[0].Y&&closed[0].Z==opened[0].Z&&opened[opened.Length-1].Y<-.11f,"fully peeled shell retains its anchored base and rolls tip downward");
        bool finite=true;for(int strip=0;strip<4;strip++)foreach(var point in R23BananaPeelGeometry.BuildStrip(strip,.5f))finite&=point.IsFinite;Check(finite,"four partial source quarter-shells produce finite render vertices");
        Console.WriteLine("Tests use a real effect-port fixture, not runtime fake vitals. Native engine and headset not executed.");
    }
}
