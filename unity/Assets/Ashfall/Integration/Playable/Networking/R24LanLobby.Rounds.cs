using System;using System.IO;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        const string ClockFeature="playable.clock/1";readonly R24LanHostClock hostClock=new R24LanHostClock();double nextClockQuery,lastClockQuery=-1;int shownCountdown=-1;
        public bool ClockSynchronized{get{return State==R24LanLobbyState.Hosting||hostClock.Synchronized;}}
        public double ClockRoundTrip{get{return State==R24LanLobbyState.Hosting?0:hostClock.RoundTrip;}}
        public double EstimatedHostTime{get{return State==R24LanLobbyState.Hosting?clock.Now:hostClock.Now(clock.Now);}}
        public double RoundCountdown{get{return SelectedMap==null||!SelectedMap.Active||SelectedMap.Rules==null?0:Math.Max(0,SelectedMap.StartsAt-EstimatedHostTime);}}
        public double RoundElapsed{get{if(SelectedMap==null||!SelectedMap.Active||SelectedMap.Rules==null)return 0;double value=Math.Max(0,EstimatedHostTime-SelectedMap.StartsAt);return SelectedMap.Rules.TimeLimit>0?Math.Min(SelectedMap.Rules.TimeLimit,value):value;}}
        public bool CanEditRoomTeams{get{return State==R24LanLobbyState.Hosting&&MapStage==SessionStage.Lobby&&(SelectedMap==null||SelectedMap.Home);}}
        // Warm-up rules the host can change live without restarting the room: team count/mode (room lobby only)
        // plus the tunables that shape combat and the clock (any warm-up, lobby or warm-up world). They ride the
        // existing map-state rule payload to guests; a match start still carries the full rule set.
        static readonly string[] WarmupTunables={"headshots","build_damage","time_limit"};
        public bool CanEditWarmupRules{get{return State==R24LanLobbyState.Hosting&&roundRules==null&&warmupRules!=null&&!roundResults;}}
        public bool HostSetRoomTeamCount(Match.R24PlayableMatchRules rules)
        {
            Check();if(rules==null||!CanEditRoomTeams&&!CanEditWarmupRules)return false;
            bool teamsChanged=CanEditRoomTeams&&warmupRules.Teams!=rules.Teams,tunedChanged=false;
            if(CanEditWarmupRules)foreach(string key in WarmupTunables)if(!Equals(warmupRules.ReadSourceValue(key),rules.ReadSourceValue(key)))tunedChanged=true;
            if(!teamsChanged&&!tunedChanged)return true;
            var values=new System.Collections.Generic.Dictionary<string,object>();
            foreach(string key in Match.R24PlayableMatchRules.SourceKeys)values[key]=warmupRules.ReadSourceValue(key);
            if(teamsChanged){values["teams"]=rules.Teams;values["mode"]=rules.Mode;}
            if(tunedChanged)foreach(string key in WarmupTunables)values[key]=rules.ReadSourceValue(key);
            var updated=Match.R24PlayableMatchRules.FromSourceValues(values,delegate(string id){return id==MapId;});
            if(teamsChanged&&!sessions.SetLobbyTeams(LifecycleContext(),updated.Teams,null,-1).Accepted)return false;
            warmupRules=updated;
            if(teamsChanged){sessionTeams=updated.Teams;PublishRoster();}
            if(tunedChanged&&SelectedMap!=null&&SelectedMap.Rules==null)PublishMapState(SelectedMap.Active,SelectedMap.Cancelled);
            return true;
        }
        public bool HostAssignRoomTeam(string actor,int team)
        {
            Check();if(!CanEditRoomTeams||String.IsNullOrEmpty(actor)||!sessions.SetLobbyTeams(LifecycleContext(),warmupRules.Teams,actor,team).Accepted)return false;
            PublishRoster();return true;
        }
        void TickRoundClock()
        {
            if(State==R24LanLobbyState.Connected&&clock.Now>=nextClockQuery){nextClockQuery=clock.Now+1;lastClockQuery=clock.Now;SendPresence(serverPeer,Packet(PacketKind.Delta,ClockFeature,OwnActor,BitConverter.GetBytes(lastClockQuery),0));}
            if(SelectedMap!=null&&SelectedMap.Active&&SelectedMap.Rules!=null){int countdown=(int)Math.Ceiling(RoundCountdown);if(countdown!=shownCountdown){shownCountdown=countdown;Dirty(SelectedMap.Results?"LAN MATCH RESULTS":countdown>0?"LAN ROUND STARTS IN "+countdown:"LAN ROUND "+SelectedMap.Round+" / "+SelectedMap.Rules.Rounds);}}
        }
        void ReadClockRequest(Peer peer,Packet packet)
        {if(packet.Payload.Length!=8)return;double sent=BitConverter.ToDouble(packet.Payload,0);if(!Finite(sent)||sent<0)return;peer.ClockReply=MatchEndClockFrame(sent,peer.Actor);}
        void ReadClockReply(Packet packet)
        {ReadMatchEndClock(packet);}
        void TickSharedRounds()
        {
            if(State!=R24LanLobbyState.Hosting||SelectedMap==null||!SelectedMap.Active||SelectedMap.Cancelled||SelectedMap.Rules==null||clock.Now<roundStartsAt)return;
            if(roundResults){PublishMatchEnd();if(finalResults!=null&&clock.Now>=finalResults.EndsAt)ReturnMatchEndToWarmup();return;}
            bool expired=roundRules.TimeLimit>0&&clock.Now>=roundStartsAt+roundRules.TimeLimit;bool eliminated=!expired&&(FiveLevelsSideEliminated()||ScoreLimitReached());if(!expired&&!eliminated)return;
            if(roundNumber>=roundRules.Rounds)finalResults=CaptureMatchEnd();
            var result=sessions.Lifecycle(LifecycleContext(),LifecycleAction.EnterResults,null,null,false);if(!result.Accepted)throw new InvalidOperationException("Host round results: "+result.Reason);
            if(roundNumber<roundRules.Rounds){int seed=roundSeed>Int32.MaxValue-104729?1:roundSeed+104729;if(!BeginMapCore(MapId,MapManifestHash,roundRules,seed,roundNumber+1))throw new InvalidOperationException("Host next-round readiness transition refused");}
            else{roundResults=true;PublishMapState(true,false);PublishMatchEnd();PublishRoster();Dirty("LAN MATCH RESULTS");}
        }
    }
}
