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
        void TickRoundClock()
        {
            if(State==R24LanLobbyState.Connected&&clock.Now>=nextClockQuery){nextClockQuery=clock.Now+1;lastClockQuery=clock.Now;SendPresence(serverPeer,Packet(PacketKind.Delta,ClockFeature,OwnActor,BitConverter.GetBytes(lastClockQuery),0));}
            if(SelectedMap!=null&&SelectedMap.Active&&SelectedMap.Rules!=null){int countdown=(int)Math.Ceiling(RoundCountdown);if(countdown!=shownCountdown){shownCountdown=countdown;Dirty(SelectedMap.Results?"LAN MATCH RESULTS":countdown>0?"LAN ROUND STARTS IN "+countdown:"LAN ROUND "+SelectedMap.Round+" / "+SelectedMap.Rules.Rounds);}}
        }
        void ReadClockRequest(Peer peer,Packet packet)
        {if(packet.Payload.Length!=8)return;double sent=BitConverter.ToDouble(packet.Payload,0);if(!Finite(sent)||sent<0)return;using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(sent);w.Write(clock.Now);peer.ClockReply=Packet(PacketKind.Snapshot,ClockFeature,OwnActor,s.ToArray(),0);}}
        void ReadClockReply(Packet packet)
        {if(packet.Payload.Length!=16)return;double sent=BitConverter.ToDouble(packet.Payload,0),host=BitConverter.ToDouble(packet.Payload,8);if(sent==lastClockQuery)hostClock.Reply(sent,host,clock.Now);}
        void TickSharedRounds()
        {
            if(State!=R24LanLobbyState.Hosting||SelectedMap==null||!SelectedMap.Active||SelectedMap.Cancelled||SelectedMap.Rules==null||roundResults||clock.Now<roundStartsAt)return;
            bool expired=roundRules.TimeLimit>0&&clock.Now>=roundStartsAt+roundRules.TimeLimit;bool eliminated=!expired&&(FiveLevelsSideEliminated()||ScoreLimitReached());if(!expired&&!eliminated)return;
            var result=sessions.Lifecycle(LifecycleContext(),LifecycleAction.EnterResults,null,null,false);if(!result.Accepted)throw new InvalidOperationException("Host round results: "+result.Reason);
            if(roundNumber<roundRules.Rounds){int seed=roundSeed>Int32.MaxValue-104729?1:roundSeed+104729;if(!BeginMapCore(MapId,MapManifestHash,roundRules,seed,roundNumber+1))throw new InvalidOperationException("Host next-round readiness transition refused");}
            else{roundResults=true;PublishMapState(true,false);PublishRoster();Dirty("LAN MATCH RESULTS");}
        }
    }
}
