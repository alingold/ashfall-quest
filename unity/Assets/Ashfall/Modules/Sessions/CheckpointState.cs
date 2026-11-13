using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Modules.Sessions
{
    // Owner-private schema. Returned objects require root quarantine/adoption; no network grant.
    public sealed partial class Session
    {
        private static void CheckFields(){RecoveryWire.Fields(typeof(Session),"<Epoch>k__BackingField:System.Int64\n<Id>k__BackingField:System.String\n<LastLoadingRecovery>k__BackingField:System.String\n<LoadingDeadline>k__BackingField:System.Double\n<Rules>k__BackingField:Ashfall.Modules.Sessions.SessionRules\n<Stage>k__BackingField:Ashfall.Modules.Sessions.SessionStage\nadmissionRevision:System.Object\ncheckpointReservations:System.Collections.Generic.HashSet`1[System.Object]\nclock:Ashfall.Modules.Sessions.ISessionClock\ncommands:System.Collections.Generic.HashSet`1[System.String]\ncontentHash:System.String\ngate:Ashfall.Core.IAuthorityGate\nhostReceipt:System.Int64\nissuedTokens:System.Collections.Generic.HashSet`1[System.String]\nlastTime:System.Double\nlifeProjections:Ashfall.Modules.Sessions.LifeProjection[]\nlifecycleTick:System.Int64\nloadingTarget:Ashfall.Modules.Sessions.SessionStage\nmanagement:Ashfall.Modules.Sessions.ISessionManagement\nplayers:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.Sessions.Member]\nprojectionCount:System.Int32\nreservations:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.Sessions.Reservation]\nteamLayout:System.Int64\nticks:System.Collections.Generic.Dictionary`2[System.String,System.Int64]\ntokens:Ashfall.Modules.Sessions.IReconnectTokens\nworld:Ashfall.Modules.Sessions.ISpawnWorld\nworldReady:System.Boolean");RecoveryWire.Fields(typeof(Member),"CanonicalId:System.String\nConnectionIdentity:System.Object\nDeaths:System.Int32\nHasPosition:System.Boolean\nId:System.String\nLife:Ashfall.Modules.Sessions.SessionLife\nLoaded:System.Boolean\nName:System.String\nPosition:Ashfall.Core.Float3\nRespawnAt:System.Double\nRevision:System.Int32\nSpectator:System.Boolean\nTeam:System.Int32\nTeamLayout:System.Int64\nToken:System.String\nWaiting:System.Boolean");RecoveryWire.Fields(typeof(Reservation),"Epoch:System.Int64\nPlayer:Ashfall.Modules.Sessions.Member\nUntil:System.Double");RecoveryWire.Fields(typeof(LifeProjection),"CanonicalActorId:System.String\nConnectionGeneration:System.Int64\nEpoch:System.Int64\nHealthRevision:System.Int64\nLife:Ashfall.Modules.HealthStates.LifeState\nLifeGeneration:System.Int64\nTeam:System.Int32");RecoveryWire.Fields(typeof(SessionRules),"Capacity:System.Int32\nIndoor:System.Boolean\nLateJoin:System.Boolean\nLocked:System.Boolean\nMapHash:System.String\nMapId:System.String\nMode:System.String\nRespawnDelay:System.Double\nSpectators:System.Boolean\nTeams:System.Int32");}
        public const string CheckpointCodec="SessionsCold/1";
        private static void Need(bool ok){RecoveryWire.Require(ok);}
        private static long Long(BinaryReader r,long min){long n=r.ReadInt64();Need(n>=min);return n;}
        private static double TimeValue(BinaryReader r){double n=r.ReadDouble();Need(!Double.IsNaN(n)&&!Double.IsInfinity(n)&&n>=0);return n;}
        private static string Text(BinaryReader r){string s=RecoveryWire.Text(r);Need(s.Length>0);return s;}
        private static void WriteMember(BinaryWriter w,Member p)
        {
            foreach(string s in new[]{p.Id,p.CanonicalId,p.Name,p.Token})RecoveryWire.Text(w,s);
            w.Write(p.Team);w.Write(p.Revision);w.Write(p.Deaths);w.Write(p.Loaded);w.Write(p.Waiting);w.Write(p.HasPosition);w.Write(p.Spectator);
            w.Write(p.Position.X);w.Write(p.Position.Y);w.Write(p.Position.Z);w.Write((int)p.Life);w.Write(p.RespawnAt);w.Write(p.TeamLayout);
        }
        private static Member ReadMember(BinaryReader r,SessionRules rules)
        {
            var p=new Member();p.Id=Text(r);p.CanonicalId=Text(r);p.Name=RecoveryWire.Text(r);p.Token=Text(r);
            p.Team=r.ReadInt32();Need(p.Team>=-1&&p.Team<8);p.Revision=RecoveryWire.Count(r,Int32.MaxValue);p.Deaths=RecoveryWire.Count(r,Int32.MaxValue);
            p.Loaded=RecoveryWire.Bool(r);p.Waiting=RecoveryWire.Bool(r);p.HasPosition=RecoveryWire.Bool(r);p.Spectator=RecoveryWire.Bool(r);
            p.Position=new Float3(RecoveryWire.Float(r),RecoveryWire.Float(r),RecoveryWire.Float(r));p.Life=(SessionLife)RecoveryWire.Count(r,3);p.RespawnAt=TimeValue(r);p.TeamLayout=Long(r,0);
            Need(p.Id.Length<=128&&p.CanonicalId.Length<=128);return p;
        }
        public byte[] CaptureCheckpoint(CheckpointBinding binding)
        {
            CheckFields();Need(binding!=null&&binding.Session==Id&&binding.Epoch==Epoch&&checkpointReservations.Count==0);
            Need(players.Count<=Rules.Capacity&&reservations.Count<=65536&&issuedTokens.Count<=65536&&commands.Count<=65536&&ticks.Count<=65536);
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m)) {
                RecoveryWire.Text(w,CheckpointCodec);binding.Write(w);RecoveryWire.Text(w,contentHash);
                RecoveryWire.Text(w,Rules.MapId);RecoveryWire.Text(w,Rules.Mode);RecoveryWire.Text(w,Rules.MapHash);w.Write(Rules.Teams);w.Write(Rules.Capacity);
                w.Write(Rules.Indoor);w.Write(Rules.Locked);w.Write(Rules.LateJoin);w.Write(Rules.Spectators);w.Write(Rules.RespawnDelay);
                w.Write((int)Stage);w.Write(lastTime);w.Write(lifecycleTick);w.Write(hostReceipt);w.Write(worldReady);w.Write((int)loadingTarget);w.Write(teamLayout);w.Write(LoadingDeadline);RecoveryWire.Text(w,LastLoadingRecovery??"");
                w.Write(players.Count);foreach(var pair in players)WriteMember(w,pair.Value);
                w.Write(reservations.Count);foreach(var pair in reservations){RecoveryWire.Text(w,pair.Key);WriteMember(w,pair.Value.Player);w.Write(pair.Value.Epoch);w.Write(pair.Value.Until);}
                w.Write(issuedTokens.Count);foreach(string token in issuedTokens)RecoveryWire.Text(w,token);
                w.Write(commands.Count);foreach(string command in commands)RecoveryWire.Text(w,command);
                w.Write(ticks.Count);foreach(var pair in ticks){RecoveryWire.Text(w,pair.Key);w.Write(pair.Value);}
                w.Write(projectionCount);for(int i=0;i<projectionCount;i++){LifeProjection p=lifeProjections[i];RecoveryWire.Text(w,p.CanonicalActorId);w.Write(p.Epoch);w.Write(p.HealthRevision);w.Write(p.LifeGeneration);w.Write(p.ConnectionGeneration);w.Write((int)p.Life);w.Write(p.Team);}
                w.Flush();byte[] bytes=m.ToArray();Need(bytes.Length<=RecoveryWire.Limit);return bytes;
            }
        }
        public static Session RestoreCheckpoint(byte[] bytes,CheckpointBinding binding,IAuthorityGate authority,ISessionClock clock,IReconnectTokens tokens,ISpawnWorld world,ISessionManagement management)
        {
            CheckFields();Need(binding!=null);using(var r=RecoveryWire.Reader(bytes)) {
                Need(RecoveryWire.Text(r)==CheckpointCodec);binding.Check(r);string content=Text(r),map=Text(r),mode=Text(r),hash=Text(r);int teams=RecoveryWire.Count(r,8),capacity=RecoveryWire.Count(r,256);
                var rules=new SessionRules(map,mode,hash,teams,capacity,RecoveryWire.Bool(r),RecoveryWire.Bool(r),RecoveryWire.Bool(r),RecoveryWire.Bool(r),TimeValue(r));
                var s=new Session(binding.Session,content,rules,authority,clock,tokens,world,management);s.Epoch=binding.Epoch;s.Stage=(SessionStage)RecoveryWire.Count(r,3);s.lastTime=TimeValue(r);s.lifecycleTick=Long(r,-1);s.hostReceipt=Long(r,-1);s.worldReady=RecoveryWire.Bool(r);s.loadingTarget=(SessionStage)RecoveryWire.Count(r,3);s.teamLayout=Long(r,0);s.LoadingDeadline=TimeValue(r);s.LastLoadingRecovery=RecoveryWire.Text(r);
                Need(!Double.IsNaN(clock.Now)&&!Double.IsInfinity(clock.Now)&&clock.Now>=s.lastTime);
                var canonicals=new HashSet<string>(StringComparer.Ordinal);var memberTokens=new HashSet<string>(StringComparer.Ordinal);
                int n=RecoveryWire.Count(r,capacity);for(int i=0;i<n;i++){Member p=ReadMember(r,rules);Need((rules.Teams==0?p.Team==-1:p.Team>=0&&p.Team<rules.Teams)&&canonicals.Add(p.CanonicalId)&&memberTokens.Add(p.Token));s.players.Add(p.Id,p);}
                n=RecoveryWire.Count(r,65536);for(int i=0;i<n;i++){string key=Text(r);Member p=ReadMember(r,rules);long epoch=Long(r,0);double until=TimeValue(r);Need(key==p.Token&&epoch<=s.Epoch&&canonicals.Add(p.CanonicalId)&&memberTokens.Add(p.Token));s.reservations.Add(key,new Reservation{Player=p,Epoch=epoch,Until=until});}
                n=RecoveryWire.Count(r,65536);for(int i=0;i<n;i++)Need(s.issuedTokens.Add(Text(r)));foreach(string token in memberTokens)Need(s.issuedTokens.Contains(token));
                n=RecoveryWire.Count(r,65536);for(int i=0;i<n;i++)Need(s.commands.Add(Text(r)));
                n=RecoveryWire.Count(r,65536);for(int i=0;i<n;i++)s.ticks.Add(Text(r),Long(r,0));
                s.projectionCount=RecoveryWire.Count(r,256);canonicals.Clear();for(int i=0;i<s.projectionCount;i++){string id=Text(r);long epoch=Long(r,0),revision=Long(r,1),life=Long(r,1),connection=Long(r,1);var state=(LifeState)RecoveryWire.Count(r,3);int team=r.ReadInt32();Need(canonicals.Add(id)&&epoch<=s.Epoch&&team>=-1&&team<Math.Max(1,teams));s.lifeProjections[i]=new LifeProjection(id,epoch,revision,life,connection,state,team);}
                RecoveryWire.End(r);return s;
            }
        }
    }
}
