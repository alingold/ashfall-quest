using System;using Ashfall.Integration.Playable.UI;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Modules.HealthStates;
partial class LobbyCases
{
    static void PendingHealthCases()
    {
        int first=checks;
        using(var pair=new Pair()){
            for(int i=0;i<5;i++)pair.Tick();var world=new WarmupRecoveryWorld();pair.Host.BindSpawnWorld(world);
            string hash=new string('b',64);var rules=R24PlayableMatchRules.ForPreset("team_revive","range").With("starting_health",75).With("starting_shield",12);
            Need(pair.Host.BeginMapSelection("range",hash,rules)&&pair.Host.ReportMapLoaded(hash),"Pending presentation uses actual loaded warmup epoch");
            for(int i=0;i<3;i++)pair.Tick();Need(pair.Client.ReportMapLoaded(hash),"Guest reports its actual warmup bytes");for(int i=0;i<4;i++)pair.Tick();
            var view=new R24LanPlayerHealthView(pair.Client);var round=new R23RoundState(new[]{"practice"},new RoundHealthQueries(),true);round.BindPlayerHealthView(view);
            Need(view.Read()==null&&round.PlayerHealthState==null&&round.Health==0,"Before host spawn there is no fabricated Alive or local Health fallback");
            Need(!R24MatchLifeText.UseLegacyDeathModal(round.Health<=0,null,true),"Waiting for actual host Health must not enter offline restart modal");
            Need(R24MatchLifeText.UseLegacyDeathModal(round.Health<=0,null),"Ordinary offline empty Health still retains legacy death screen");
            Need(!round.LocalMatchAlive&&!round.TryRespawnLocalMatch()&&!view.ApplyDamage(1,false,"damage"),"Pending guest cannot respawn itself or mutate absent authoritative Health");
            bool invalid=false;try{R24MatchLifeText.Normalize("pending");}catch(ArgumentException){invalid=true;}Need(invalid,"Pending network data must not invent a gameplay life state");
            Need(pair.Host.RequestSpawn()&&pair.Client.RequestSpawn(),"Real host-owned source-safe spawn requests accepted");for(int i=0;i<7;i++)pair.Tick();
            var actual=view.Read();R24LanHealthState wire;Need(actual!=null&&pair.Client.TryReadHealth(pair.Client.OwnActor,out wire)&&actual.Revision==wire.Revision&&actual.Health==75&&actual.Shield==12,"UI arrival reads actual host-configured Health revision and values");
            Need(round.LocalMatchAlive&&!R24MatchLifeText.UseLegacyDeathModal(round.Health<=0,"alive",false),"Actual Alive presentation releases pending state without offline restart");
            Need(!R24MatchLifeText.UseLegacyDeathModal(true,"eliminated",false),"Actual authoritative death uses existing life presentation, not local restart");
            pair.Client.Dispose();Need(view.Read()==null&&round.PlayerHealthState==null,"Departed epoch cannot retain prior Health snapshot");
            Need(!R24MatchLifeText.UseLegacyDeathModal(round.Health<=0,null,true),"Retired authoritative view remains pending until lifecycle detaches");
            Need(round.UnbindPlayerHealthView(view),"Cleanup detaches exact view");view.Dispose();
            Need(!round.PlayerHealthViewBound&&round.Health==100&&!R24MatchLifeText.UseLegacyDeathModal(false,null,false),"Offline Health resumes only after exact owner cleanup");
        }
        Console.WriteLine((checks-first)+" pending-host-Health/life-modal policy observations PASS; Root/UI coroutine native unrun");
    }
}
