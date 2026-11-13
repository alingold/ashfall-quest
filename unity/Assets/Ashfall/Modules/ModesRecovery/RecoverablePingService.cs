using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Modes;

namespace Ashfall.Modules.ModesRecovery
{
    public sealed partial class RecoverablePingRecord
    {
        public readonly string OwnerId, MapId, Action;
        public readonly long Epoch;
        public readonly int Team, Color, Catalog;
        public readonly Float3 Point, Normal;
        public readonly double Created, Until;
        internal RecoverablePingRecord(ModeActor owner,string map,long epoch,int color,int catalog,Float3 point,Float3 normal,double now)
        { OwnerId=owner.Id; Team=owner.Team; MapId=map; Epoch=epoch; Color=color; Catalog=catalog;
          Point=point; Normal=normal; Created=now; Until=now+30; Action=RecoverablePingService.Action(color); }
        public bool SurfaceDecal { get { return Color>=3&&Color<=5; } }
    }
    public sealed partial class RecoverablePingService
    {
        private static readonly string[] Actions={"hold","enemy","location","emote_local","target","emote_alt","help","go_here","weapon"};
        private readonly IModeRoster roster;
        private readonly RecoveryPingLedger ledger;
        private readonly Dictionary<string,RecoverablePingRecord> marks=new Dictionary<string,RecoverablePingRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string,double> last=new Dictionary<string,double>(StringComparer.Ordinal);
        private readonly int teams;
        private readonly string map;
        private readonly long epoch;
        private readonly double cooldown;
        private double lastTime;
        // Scope instance to map + epoch. Recreate on loading/epoch change, clearing old marks.
        // Source has generic command rate limit, no ping-specific interval: pass 0 for parity.
        public RecoverablePingService(IModeRoster roster,IAuthorityGate gate,string session,long epoch,string map,int teams,double cooldownSeconds,int journalCapacity)
        {
            if(roster==null||String.IsNullOrEmpty(map)||teams<0||teams>8||!Finite(cooldownSeconds)||cooldownSeconds<0) throw new ArgumentException("ping configuration");
            this.roster=roster; this.map=map; this.teams=teams; this.epoch=epoch; cooldown=cooldownSeconds;
            ledger=new RecoveryPingLedger(session,epoch,gate,journalCapacity);
        }
        public static string Action(int index) { if(index<0||index>=Actions.Length) throw new ArgumentOutOfRangeException("index"); return Actions[index]; }
        public RuleResult Place(CommandContext c,RoundStage stage,Float3 point,Float3 normal,int color,int catalog,double now)
        {
            RuleResult r=ledger.Check(c); if(!r.Accepted) return r;
            if(!Finite(now)||now<lastTime) return RuleResult.Reject("time");
            ModeActor owner;
            if((stage!=RoundStage.Match&&stage!=RoundStage.Lobby&&stage!=RoundStage.Countdown)||!roster.TryGet(c.ActorId,out owner)||owner.Spectator||!owner.Loaded||owner.Life!="alive") return RuleResult.Reject("not_living");
            double length=Math.Sqrt((double)normal.X*normal.X+(double)normal.Y*normal.Y+(double)normal.Z*normal.Z);
            if(!point.IsFinite||!normal.IsFinite||length<.01||color<0||color>8||catalog< -1||catalog>29||(color==8&&catalog<0)) return RuleResult.Reject("payload");
            double previous;
            if(last.TryGetValue(c.ActorId,out previous)&&now-previous<cooldown) return RuleResult.Reject("cooldown");
            ledger.Commit(c); lastTime=now; last[c.ActorId]=now;
            marks[c.ActorId]=new RecoverablePingRecord(owner,map,epoch,color,catalog,point,new Float3((float)(normal.X/length),(float)(normal.Y/length),(float)(normal.Z/length)),now);
            return RuleResult.Accept();
        }
        public RecoverablePingRecord[] Visible(string viewerId,string currentMap,long currentEpoch,double now)
        {
            ModeActor viewer; if(!Finite(now)||currentMap!=map||currentEpoch!=epoch||!roster.TryGet(viewerId,out viewer)) return new RecoverablePingRecord[0];
            List<RecoverablePingRecord> result=new List<RecoverablePingRecord>();
            foreach(RecoverablePingRecord mark in marks.Values)
            {
                ModeActor owner;
                if(now>=mark.Created&&now<=mark.Until&&roster.TryGet(mark.OwnerId,out owner)&&!owner.Spectator&&owner.Team==mark.Team&&RoundController.Allied(viewer,owner,teams)) result.Add(mark);
            }
            result.Sort(delegate(RecoverablePingRecord a,RecoverablePingRecord b) { return StringComparer.Ordinal.Compare(a.OwnerId,b.OwnerId); }); return result.ToArray();
        }
        private static bool Finite(double value) { return !Double.IsNaN(value)&&!Double.IsInfinity(value); }
    }
}
