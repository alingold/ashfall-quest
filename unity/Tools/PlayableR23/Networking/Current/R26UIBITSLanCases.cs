using System;
using System.Collections;
using System.Reflection;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
partial class LobbyCases
{
    public static void R26UIBITSRun()
    {
        using(var pair=new Pair()){
            StartHealthRound(pair,true);var host=pair.Host;var guest=pair.Client;string actor=guest.OwnActor;string life;
            Need(host.TryReadSurvivalLife(actor,out life)&&life=="alive","Survival read uses placed authoritative health");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var rulesField=typeof(R24LanLobby).GetField("roundRules",flags);var rules=(R24PlayableMatchRules)rulesField.GetValue(host);
            var combat=(IDictionary)typeof(R24LanLobby).GetField("combat",flags).GetValue(host);object entry=combat[host.OwnActor];var score=entry.GetType().GetField("Score",flags);score.SetValue(entry,99);
            var capped=typeof(R24LanLobby).GetMethod("ScoreLimitReached",flags);
            Need(!(bool)capped.Invoke(host,null),"Team Revive ignores 99 kills against default cap25");
            rulesField.SetValue(host,rules.With("mode","respawn"));Need((bool)capped.Invoke(host,null),"Respawn retains kill-cap completion");rulesField.SetValue(host,rules);score.SetValue(entry,0);
            var initial=Health(host,actor);pair.Now=initial.ProtectionUntil+.01;host.Tick(pair.Now);guest.Tick(pair.Now);
            Need(host.ApplyHostHealthLoss(actor,initial.Revision,80,25,true).Accepted,"Real host down accepted");
            var held=new System.Collections.Generic.List<Frame>();
            for(int i=0;i<12;i++){pair.Now+=.05;host.Tick(pair.Now);int queued=pair.ClientPipe.Inbox.Count;for(int n=0;n<queued;n++){var frame=pair.ClientPipe.Inbox.Dequeue();Ashfall.Modules.Networking.Packet packet;if(Ashfall.Modules.Networking.Wire.TryDecode(frame.Bytes,out packet)&&packet.FeatureId=="playable.health.state/1")held.Add(frame);else pair.ClientPipe.Inbox.Enqueue(frame);}guest.Tick(pair.Now);}
            foreach(var frame in held)pair.ClientPipe.Inbox.Enqueue(frame);pair.Tick();
            Need(host.SelectedMap.Results&&guest.SelectedMap.Results,"Range 1v1 eliminates at last standing side on both peers");
            var final=Health(host,actor);Need(final.Life==Ashfall.Modules.HealthStates.LifeState.Downed&&Health(guest,actor).Revision==final.Revision&&!host.ApplyHostHealthLoss(actor,final.Revision,1,0,false).Accepted,"Results retain the downed Health receipt and refuse damage");
            Need(host.TryReadSurvivalLife(actor,out life)&&life=="downed","Host results retain committed downed presentation");
            Need(guest.TryReadSurvivalLife(actor,out life)&&life=="downed","Guest results retain committed downed presentation");
            Need(host.BeginMapSelection("range",new string('b',64)),"Next map accepted");
            Need(!host.TryReadSurvivalLife(actor,out life),"Next epoch invalidates survival presentation");
        }
        Console.WriteLine("PASS UIBITS host elimination, kill-cap control, results life replication and epoch invalidation");
    }
}
