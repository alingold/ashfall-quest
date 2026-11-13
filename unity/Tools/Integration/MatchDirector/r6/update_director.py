import pathlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');p=R/'unity/Assets/Ashfall/Integration/MatchDirector/Director.cs';t=p.read_text()
t=t.replace('using Ashfall.Modules.Modes;','using Ashfall.Modules.Modes;\nusing RoundController = Ashfall.Integration.MatchRounds.RoundController;')
t=t.replace('private bool scoring;','private bool scoring;\n        private RoundController.CommittedEvent retainedScore;\n        private string retainedKiller;\n        private int retainedTeam;')
t=t.replace('            if(pendingScoreEvent==e.Sequence&&round!=null&&round.Stage!=RoundStage.Match)return RuleResult.Reject("uncommitted-score");','')
a=t.index('            if(e.Sequence!=scoredEvent&&knockdown&&round.Stage!=RoundStage.Match)');b=t.index('            // Final death',a)
t=t[:a]+'''            if(e.Sequence!=scoredEvent&&knockdown)
            {
                if(retainedScore==null)
                {
                    retainedKiller=CanonicalActor(attribution.KillerFor(e));
                    ModeActor scorer;retainedTeam=retainedActors.TryGetValue(retainedKiller??"",out scorer)?scorer.Team:-1;
                    scoring=true;
                    try {r=round.CaptureCommittedEvent(e,life.Sequence,retainedKiller,out retainedScore);}
                    finally {scoring=false;}
                    if(!r.Accepted)return r;
                }
                pendingScoreEvent=e.Sequence;
                if(journal!=null){r=journal.Retain(e,retainedKiller,life.Sequence,retainedTeam);if(!r.Accepted)return r;}
                r=round.ReconcileCommittedEvent(Context(principal,round.Epoch),retainedScore,lastTime);
                if(!r.Accepted){Fault="score:"+r.Reason;return r;}
                scoredEvent=e.Sequence;
            }
''' + t[b:]
t=t.replace('eventSequence=e.Sequence;return RuleResult.Accept();','eventSequence=e.Sequence;pendingScoreEvent=0;retainedScore=null;Fault="";return RuleResult.Accept();')
a=t.index('            long old=session.Epoch;');b=t.index('        // Sessions owner',a)
t=t[:a]+'''            if(session.LifecycleSequence==Int64.MaxValue||session.Epoch>=Int64.MaxValue-1)return RuleResult.Reject("lifecycle-exhausted");
            var c=new CommandContext(session.Id,principal,"sessions-lifecycle/"+(session.LifecycleSequence+1).ToString(CultureInfo.InvariantCulture),session.LifecycleSequence+1,session.Epoch,true);
            Session.PreparedMatchRollover preparedSession;
            r=session.PrepareMatchRollover(c,lastTime+5,out preparedSession);if(!r.Accepted)return r;
            RoundController.PreparedRollover preparedRound=null;
            RoundController nextRound=round;
            if(round==null)nextRound=new RoundController(initialRules,this,principal,session.Id,session.Epoch+1,gate,capacity+ScoreAdmissions);
            else {r=round.PrepareRollover(Context(principal,round.Epoch),lastTime,lastTime+5,session.Epoch+1,out preparedRound);if(!r.Accepted){session.AbortMatchRollover(preparedSession);return r;}}
            var nextPings=new PingService(this,gate,session.Id,session.Epoch+1,session.Rules.MapId,session.Rules.Teams,0,capacity);
            r=session.ValidateMatchRollover(preparedSession);
            if(r.Accepted&&preparedRound!=null)r=round.ValidateRollover(preparedRound,lastTime);
            if(!r.Accepted){session.AbortMatchRollover(preparedSession);if(preparedRound!=null)round.AbortRollover(preparedRound);return r;}
            // Trusted frozen lane: no callbacks, IO or allocations from here to publication end.
            session.CommitMatchRolloverValidated(preparedSession);
            if(preparedRound!=null)round.CommitRolloverValidated(preparedRound);
            round=nextRound;pings=nextPings;
            lives.Clear();retainedActors.Clear();limitAdmissions=0;eventSequence=0;scoredEvent=0;return RuleResult.Accept();
        }
''' +t[b:]
p.write_text(t)
