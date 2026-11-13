using System;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable.Consumables;
static class SlotReplacementCases
{
    static int checks;static void Need(bool okay,string message){checks++;if(!okay)throw new Exception(message);}
    sealed class Effects:IR23ConsumableEffects
    {public R23ConsumableVitals ReadVitals(){return new R23ConsumableVitals{Alive=true,Health=20,MaxStamina=6};}public bool TryApplyGain(float health,float shield,bool stamina){return true;}}
    sealed class Query:IInteractionQuery
    {public bool BlockSecond;public bool HasLineOfSight(string actor,string owner,Float3 from,Float3 to,float range){return !(BlockSecond&&to.X>.5f);}}
    static ActorFrame Frame(){return new ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,Head=new Float3(0,1.65f,0),Hand=new Float3(0,1,.2f),Forward=new Float3(0,0,1)};}
    static R23ConsumableState State(Query query){var state=new R23ConsumableState(new Effects(),query);state.SetFrame(Frame());state.Advance(.02f);return state;}
    static void Collect(R23ConsumableState state,string id,string kind){Need(state.Register(id,kind,new Float3(0,1,.3f)),"register");Need(state.Execute(PickupAction.Reserve,id).Accepted&&state.Execute(PickupAction.Collect,id).Accepted,"collect");}
    static void Main()
    {
        var query=new Query();var state=State(query);Collect(state,"s0","shaker");Collect(state,"s1","shaker");Need(state.OwnedItems[0].Slot==state.OwnedItems[1].Slot,"source two shakers share a slot");
        Need(state.Execute(PickupAction.Equip,"s0").Accepted,"equip selected stack member");int slot=state.OwnedItems[0].Slot;
        query.BlockSecond=true;Need(!state.DropSlot(slot,new[]{new Float3(.1f,1,.6f),new Float3(.7f,1,.6f)}).Accepted,"second blocked placement refuses whole stack");Need(state.OwnedItems.Length==2&&state.HeldId=="s0"&&state.Inspect("s0").Owner=="player"&&state.Inspect("s1").Owner=="player","failed batch leaves both custody and selection intact");
        query.BlockSecond=false;Need(state.DropSlot(slot,new[]{new Float3(.1f,1,.6f),new Float3(.7f,1,.6f)}).Accepted,"two valid source placements drop the complete slot");Need(state.OwnedItems.Length==0&&state.HeldId==null&&state.Inspect("s0").Owner==null&&state.Inspect("s1").Owner==null,"both world publications and selection change together");Need(state.Inspect("s0").Position.X==.1f&&state.Inspect("s1").Position.X==.7f,"exact staged positions published");Need(!state.DropSlot(slot,new[]{new Float3(0,1,1)}).Accepted,"already-dropped slot cannot repeat");
        state=State(query);Collect(state,"b0","banana");Collect(state,"b1","banana");Collect(state,"b2","banana");Need(state.Drop("b0",new Float3(0,1,.5f)).Accepted,"free low-index slot");state.SetOtherSlotsUsed(1);Need(!state.CanStore("soda"),"noncompact remaining slots still consume two slots alongside one weapon");Need(state.Register("new","soda",new Float3(0,1,.3f))&&state.Execute(PickupAction.Reserve,"new").Accepted&&!state.Execute(PickupAction.Collect,"new").Accepted&&state.OwnedItems.Length==2,"slot-index hole cannot create a fourth occupied slot");
        state=State(query);Collect(state,"s0","soda");Collect(state,"s1","soda");slot=state.OwnedItems[0].Slot;IStagedPickupDropGroup stage;
        Need(!state.TryStageDropGroup("player",new[]{new EvictedInstance("s0","soda","consumable/s0",new Float3(0,1,1))},out stage),"partial selected stack refuses staging");
        var drops=new[]{new EvictedInstance("s0","soda","consumable/s0",new Float3(0,1,1)),new EvictedInstance("s1","soda","consumable/s1",new Float3(.1f,1,1))};Need(state.TryStageDropGroup("player",drops,out stage),"exact whole-stack stage");Collect(state,"b0","banana");Need(!stage.TryCommit()&&state.OwnedItems.Length==3,"stale membership refuses without removing either stack member");stage.Abort();
        Need(!state.DropSlot(slot,new[]{new Float3(float.NaN,0,0),new Float3(0,1,1)}).Accepted&&state.OwnedItems.Length==3,"nonfinite placement never drops half a slot");
        Console.WriteLine("PASS "+checks+" real PickupRules/consumable selected-slot, failed-second-placement, stale-stage and noncompact shared-budget checks; no engine run");
    }
}
