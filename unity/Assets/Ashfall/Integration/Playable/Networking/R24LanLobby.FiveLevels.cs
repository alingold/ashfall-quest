using System;
using System.Collections.Generic;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Playable.Match;
using Ashfall.Modules.Modes;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        sealed class FiveLevelsRoster:IModeRoster
        {
            internal ModeActor[] Actors=new ModeActor[0];
            public ModeActor[] Snapshot(){return (ModeActor[])Actors.Clone();}
            public bool TryGet(string id,out ModeActor actor){foreach(var value in Actors)if(value.Id==id){actor=value;return true;}actor=new ModeActor();return false;}
        }
        RoundController fiveLevelsElimination;FiveLevelsRoster fiveLevelsRoster;long fiveLevelsEpoch=-1,fiveLevelsHealthSequence=-1,fiveLevelsRosterRevision=-1;
        bool FiveLevelsSideEliminated()
        {
            if(!R24ArchiveRoundPolicy.FiveLevels(MapId)||roundRules==null||roundRules.Mode!="revive"||roundRules.Teams<2||hostHealth==null)return false;
            if(fiveLevelsEpoch==Epoch&&fiveLevelsHealthSequence==healthStateSequence&&fiveLevelsRosterRevision==revision)return fiveLevelsElimination!=null&&fiveLevelsElimination.Stage==RoundStage.Results;
            fiveLevelsHealthSequence=healthStateSequence;fiveLevelsRosterRevision=revision;
            var actors=new List<ModeActor>();var teams=new HashSet<int>();
            foreach(var member in sessions.CaptureMembers()){
                if(member.Life==SessionLife.Spectator)continue;
                if(!member.Loaded||!member.HasPosition||member.Team<0)return false;
                var health=hostHealth.Read(member.CanonicalActorId);if(health==null||!health.Connected)return false;
                string life=health.Life==LifeState.Alive?"alive":health.Life==LifeState.Downed?"downed":"eliminated";
                actors.Add(new ModeActor(member.ActorId,member.Name,member.Team,life,true,false));teams.Add(member.Team);
            }
            // A lone offline player or an unplaced/missing actor is not evidence
            // that a real opposing side was eliminated.
            if(teams.Count<2)return false;
            if(fiveLevelsEpoch!=Epoch||fiveLevelsElimination==null){
                fiveLevelsEpoch=Epoch;fiveLevelsRoster=new FiveLevelsRoster();fiveLevelsRoster.Actors=actors.ToArray();
                fiveLevelsElimination=new RoundController(new ModeRules("revive",roundRules.Teams,0,0,1),fiveLevelsRoster,OwnActor,SessionId,Epoch,new HostGate(OwnActor),16);
                var started=fiveLevelsElimination.StartRound(LifecycleContext(),clock.Now);if(!started.Accepted)throw new InvalidOperationException("Five Levels round start: "+started.Reason);
            }else fiveLevelsRoster.Actors=actors.ToArray();
            var result=fiveLevelsElimination.AdvanceClock(clock.Now);if(!result.Accepted)throw new InvalidOperationException("Five Levels elimination clock: "+result.Reason);
            return fiveLevelsElimination.Stage==RoundStage.Results&&fiveLevelsElimination.EndReason=="last_side";
        }
    }
}
