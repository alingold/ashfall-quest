using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Modes;
namespace Ashfall.Integration.Playable.Match
{
    // Offline adapter to the existing round scorer, not a second scoring store.
    // Only the actual registered player exists here. Hidden practice targets and
    // campaign enemies cannot masquerade as competing match participants.
    public sealed class R24LocalMatchScores : IModeRoster
    {
        sealed class Gate:IAuthorityGate
        {
            public RuleResult Validate(CommandContext c)
            {return c.IsAuthority&&c.SessionId=="r24-local-scores"&&c.ActorId=="director"&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local-score-owner");}
        }
        readonly RoundController scorer;
        readonly Func<HealthSnapshot> readHealth;
        long sequence;int observedDeaths;
        public long Revision {get;private set;}
        CommandContext Context(){long next=++sequence;return new CommandContext("r24-local-scores","director","score-"+next,next,0,true);}
        public R24LocalMatchScores(R24PlayableMatchRules rules,Func<HealthSnapshot> actualPlayerHealth)
        {
            if(rules==null||actualPlayerHealth==null||actualPlayerHealth()==null)throw new ArgumentException("Actual configured local match player required");
            readHealth=actualPlayerHealth;observedDeaths=readHealth().Deaths;
            scorer=new RoundController(new ModeRules(rules.Mode,rules.Teams,rules.ScoreLimit,rules.TimeLimit,1),this,"director","r24-local-scores",0,new Gate(),2048);
            if(!scorer.StartRound(Context(),0).Accepted)throw new InvalidOperationException("Local score round start refused");
        }
        public bool TryGet(string actorId,out ModeActor actor)
        {
            var p=readHealth();actor=new ModeActor();if(actorId!="player"||p==null)return false;
            actor=new ModeActor("player","Player",p.Team,p.Life==LifeState.Alive?"alive":p.Life==LifeState.Downed?"downed":"eliminated",true,false);return true;
        }
        public ModeActor[] Snapshot(){ModeActor player;return TryGet("player",out player)?new[]{player}:new ModeActor[0];}
        public void Observe(HealthSnapshot actualHealth,double elapsed)
        {
            if(actualHealth==null||actualHealth.Id!="player"||actualHealth.Deaths<=observedDeaths)return;
            // This local owner currently receives world/zone/fall deaths, without
            // an authenticated competing player as killer. They award no kill.
            var r=scorer.RecordKnockdown(Context(),new Knockdown("player",null,actualHealth.LifeGeneration),elapsed);
            if(!r.Accepted)throw new InvalidOperationException("Actual local death score observation: "+r.Reason);
            observedDeaths=actualHealth.Deaths;Revision++;
        }
        public int PersonalKills {get{return scorer.PersonalScore("player");}}
        public int TeamKills(int team)
        {foreach(var row in scorer.Scores())if(row.Id=="team:"+team)return row.Score;return 0;}
    }
}
