using System;using Ashfall.Core;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    internal static void R26Hitreg()
    {
        Need(R26WarmupFeedback.HostLine=="HOST STARTS THE MATCH","Plain warmup host line (HUD19)");
        foreach(R26RoomStage stage in Enum.GetValues(typeof(R26RoomStage)))Need(R26WarmupFeedback.DamageOff(stage)==(stage==R26RoomStage.FreeRoam),"Only shared exploration uses neutral reticle and damage-off notice: "+stage);
        R26HitregNonCombat();
        foreach(string preset in new[]{"team_respawn","free_for_all"})using(var pair=new Pair()){
            var rules=R24PlayableMatchRules.ForPreset(preset,"range").With("zone_enabled",false).With("protection",0f).With("damage",.1f);
            string hash=new string('b',64);StartRound(pair,new HazardWorld(),rules,hash);pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
            string guest=pair.Client.OwnActor,host=pair.Host.OwnActor;
            Need(rules.Teams==0||pair.Host.ReadAuthorityMember(host).Team!=pair.Host.ReadAuthorityMember(guest).Team,"Actual host and joining bot have distinct authority teams in TDM");
            pair.Host.ShotDiagnostics=Console.WriteLine;
            for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
            var muzzle=new Float3(7.6f,1.6f,0);var rays=new[]{Dir(muzzle,new Float3(0,1.25f,0))};float before=Health(pair.Host,guest).Shield;
            Need(pair.Host.SubmitShot("cx4",0,muzzle,rays),"Host volley admitted");R26HitConfirm hit;pair.Tick();
            Need(Health(pair.Host,guest).Shield<before&&pair.Host.TryDequeueHitConfirm(out hit)&&hit.Victim==guest,"Host damages guest and receives hit confirmation in "+preset);
            // A stationary low-frame-rate guest still supplies valid history, sampled at 2 Hz.
            for(int i=0;i<6;i++){
                KeepRaw(pair,hash,0,8);pair.Tick();pair.Now+=.4;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0));
                R24LanRawTrackingSample sample;Need(pair.Host.TryReadRawTracking(guest,pair.Now,out sample)&&pair.Now-sample.Time>=.39,"Guest last sample is actually 400 ms old");before=Health(pair.Host,guest).Shield;Need(pair.Host.SubmitShot("cx4",0,muzzle,rays),"Low-rate volley accepted");pair.Tick();Need(Health(pair.Host,guest).Shield<before,"Low-rate current guest tracking remains targetable");
            }
        }
        Console.WriteLine("PASS HITREG authority teams, host damage, hit confirms, and 2 Hz target pose in TDM/FFA: "+checks+" checks");
    }
    static void R26HitregNonCombat()
    {
        foreach(bool warmup in new[]{true,false})using(var pair=new Pair()){
            string hash=new string('b',64);var rules=R24PlayableMatchRules.ForPreset("team_respawn","range").With("zone_enabled",false).With("protection",0f).With("damage",warmup?1f:0f);
            if(warmup){
                for(int i=0;i<5;i++)pair.Tick();pair.Host.BindSpawnWorld(new HazardWorld());pair.Host.BindRawTrackingWorldQuery(Open);
                Need(pair.Host.BeginMapSelection("range",hash,rules)&&pair.Host.ReportMapLoaded(hash),"Actual warmup selection");for(int i=0;i<3;i++)pair.Tick();Need(pair.Client.ReportMapLoaded(hash),"Actual warmup guest ready");for(int i=0;i<4;i++)pair.Tick();
                Need(pair.Client.RequestSpawn(),"Actual warmup guest placement");for(int i=0;i<4;i++)pair.Tick();Need(pair.Host.RequestSpawn(),"Actual warmup host placement");for(int i=0;i<4;i++)pair.Tick();
            }else StartRound(pair,new HazardWorld(),rules,hash);
            Need(R26RoomStatus.StageOf(pair.Host.State,pair.Host.SelectedMap,pair.Host.RoundCountdown,false)==(warmup?R26RoomStage.FreeRoam:R26RoomStage.Match),"HUD stage follows actual warmup-versus-match authority, not selected preset or epoch parity");
            pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});pair.Host.ShotDiagnostics=Console.WriteLine;
            R24LanSpawn g,h;Need(pair.Client.TryReadSpawn(pair.Client.OwnActor,out g)&&pair.Host.TryReadSpawn(pair.Host.OwnActor,out h),"Actual placements readable");pair.Host.TryReadSpawn(pair.Host.OwnActor,out h);
            for(int i=0;i<3;i++){pair.Client.PublishRawTracking(Body(hash,g.Revision,g.Feet.X,g.Feet.Y,g.Feet.Z));pair.Host.PublishRawTracking(Body(hash,h.Revision,h.Feet.X,h.Feet.Y,h.Feet.Z));pair.Tick();}
            var muzzle=new Float3(h.Feet.X+.4f,h.Feet.Y+1.25f,h.Feet.Z);var target=new Float3(g.Feet.X,g.Feet.Y+1.25f,g.Feet.Z);float before=Health(pair.Host,g.Actor).Health+Health(pair.Host,g.Actor).Shield;
            Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,target)}),"Non-damaging volley still admitted and consumes ammunition");R26HitConfirm hit;pair.Tick();
            Need(Health(pair.Host,g.Actor).Health+Health(pair.Host,g.Actor).Shield==before&&!pair.Host.TryDequeueHitConfirm(out hit),warmup?"Reproduced: shared-map warmup yields no damage or markers even for a hit":"Reproduced: rig damage-zero round yields no damage or markers even for a hit");
        }
    }
}
