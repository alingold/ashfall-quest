using System;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable.World.Loot;
using Ashfall.Integration.Playable.Consumables;
class Cases
{
    static int checks;
    static void Need(bool value,string text){checks++;if(!value)throw new Exception(text);}
    sealed class Effects:IR23ConsumableEffects
    {
        public int Calls;public R23ConsumableVitals ReadVitals(){return new R23ConsumableVitals{Alive=true,Health=50,Shield=0,Stamina=1,MaxStamina=6};}
        public bool TryApplyGain(float h,float s,bool refill){Calls++;return true;}
    }
    sealed class Query:IInteractionQuery {public bool HasLineOfSight(string a,string o,Float3 f,Float3 t,float r){return true;}}
    static ActorFrame Frame(){return new ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,OtherHandEligible=true,Head=new Float3(0,1.65f,0),Hand=new Float3(0,1,.2f),Forward=new Float3(0,0,1),LocalHand=new Float3(0,1,.2f),OtherLocal=new Float3(0,.1f,0),Anchor=new Float3(0,1.65f,.2f),HeadLocalAnchor=new Float3(0,0,.2f),ItemUpDot=0};}
    static void Main()
    {
        var counts=new int[4];int total=0,mixed=0,sites=0;var food=new int[3];
        foreach(string map in new[]{"archive:alfa-aglae-souza","archive:classic-bigbox-vr","archive:white-house-micro","range","custom-map"})for(int site=0;site<4096;site++)
        {
            var rows=R24MixedLootRecipe.Cache(map,site);var repeat=R24MixedLootRecipe.Cache(map,site);var seen=new bool[4];Need(rows.Length==6,"six finite draws");sites++;
            for(int slot=0;slot<rows.Length;slot++)
            {
                var x=rows[slot];Need(x.Slot==slot&&x.Stars>=1&&x.Stars<=5,"valid slot/stars");Need(x.Category==repeat[slot].Category&&x.Catalog==repeat[slot].Catalog&&x.Stars==repeat[slot].Stars&&x.FoodKind==repeat[slot].FoodKind,"deterministic map/site");
                counts[(int)x.Category]++;total++;seen[(int)x.Category]=true;
                bool melee=x.Catalog==14||x.Catalog==15||x.Catalog==16||x.Catalog==29;
                if(x.Category==R24CacheCategory.Heal){int i=Array.IndexOf(new[]{"soda","shaker","banana"},x.FoodKind);Need(i>=0&&x.Catalog==-1&&x.Stars==1,"real consumable family");food[i]++;}
                else if(x.Category==R24CacheCategory.Grenade)Need(R23LootRecipe.GrenadeKind(x.Catalog)!=null&&!melee,"real thrown grenade dispatch");
                else if(x.Category==R24CacheCategory.Melee)Need(melee,"actual melee catalog");
                else Need(!melee&&R23LootRecipe.GrenadeKind(x.Catalog)==null&&(x.Catalog==32||Array.IndexOf(R23LootRecipe.ReviewCatalogIds,x.Catalog)>=0),"real ranged weapon/tool including SMAW and GRAPPLE GUN");
            }
            int types=0;foreach(bool present in seen)if(present)types++;if(types>1)mixed++;
        }
        Need(Math.Abs((double)counts[0]/total-.5)<.01,"average three guns per cache");
        for(int i=1;i<4;i++)Need(Math.Abs((double)counts[i]/total-1.0/6)<.01,"average one category per cache");
        Need(mixed>sites*.9,"probabilistic within-cache draws rather than six identical category draws");foreach(int n in food)Need(n>1000,"all three heals reachable");
        foreach(string map in new[]{"archive:magnum-chaosvr","archive:magnum-3-chaosvr"})for(int site=0;site<128;site++)
        {var old=R23LootRecipe.Cache(map,site);var now=R24MixedLootRecipe.Cache(map,site);for(int i=0;i<6;i++)Need(now[i].Catalog==old[i].Catalog&&now[i].Stars==old[i].Stars&&now[i].Slot==old[i].Slot&&now[i].Category==R24CacheCategory.Gun,"Magnum exact source exception");}
        bool refused=false;try{R24MixedLootRecipe.Cache("a",-1);}catch(ArgumentOutOfRangeException){refused=true;}Need(refused,"negative site rejected");
        var effects=new Effects();var state=new R23ConsumableState(effects,new Query());state.SetFrame(Frame());state.Advance(.02f);Float3 at=new Float3(0,1,.3f);
        Need(state.Register("starter","banana",at)&&state.Register("cache","soda",at,false),"source starter plus ephemeral cache");
        Need(state.Execute(PickupAction.Reserve,"cache").Accepted&&!state.RetireUnownedWorldItem("cache"),"live reservation preserved");
        Need(state.Execute(PickupAction.Release,"cache").Accepted&&state.RetireUnownedWorldItem("cache")&&state.Inspect("cache")==null,"released unowned cache retired");
        Need(state.Register("owned-cache","soda",at,false)&&state.Execute(PickupAction.Reserve,"owned-cache").Accepted&&state.Execute(PickupAction.Collect,"owned-cache").Accepted&&state.Execute(PickupAction.Equip,"owned-cache").Accepted,"cache uses actual pickup custody");
        Need(!state.RetireUnownedWorldItem("owned-cache")&&state.OwnedItems.Length==1&&state.HeldId=="owned-cache","map teardown cannot remove held/owned cache food");
        Need(state.Execute(PickupAction.Open,"owned-cache").Accepted&&state.Execute(PickupAction.Consume,"owned-cache").Accepted&&state.PendingSoda==50,"actual consumed cache schedules effect once");
        Need(state.RetireUnownedWorldItem("owned-cache")&&state.PendingSoda==50&&effects.Calls==0,"retiring spent source leaves pending real effect intact");
        for(int i=0;i<20;i++){state.SetFrame(Frame());state.Advance(.1f);}Need(effects.Calls>0&&state.PendingSoda<50,"pending soda continues through source retirement");
        Need(state.Register("unspent-cache","shaker",at,false),"ephemeral unspent source");state.Reset();Need(state.Inspect("starter")!=null&&state.Inspect("cache")==null&&state.Inspect("owned-cache")==null&&state.Inspect("unspent-cache")==null,"new round restores starter only, no stale cache resurrection");
        for(int i=0;i<2048;i++){string id="retire-"+i;Need(state.Register(id,"soda",at,false)&&state.RetireUnownedWorldItem(id),"repeated map turnover does not exhaust source capacity");}
        Need(effects.Calls>0&&state.OwnedItems.Length==0,"cleanup grants no inventory");
        Console.WriteLine("PASS "+checks+" checks; category totals "+String.Join(",",Array.ConvertAll(counts,x=>x.ToString()))+" over "+sites+" caches; food "+String.Join(",",Array.ConvertAll(food,x=>x.ToString()))+". Native dispatch/render remains unrun.");
    }
}
