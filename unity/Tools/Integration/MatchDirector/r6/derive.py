import pathlib,hashlib,difflib,json
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');out=R/'unity/Assets/Ashfall/Integration/MatchRounds';out.mkdir(parents=True,exist_ok=True)
D=R/'docs/agent-runs/20261104-three-chats/unity/MatchTransactionsR6';base=[]
for n in ['RoundController.cs','CommandLedger.cs']:
 p=R/'unity/Assets/Ashfall/Modules/Modes'/n;t=p.read_text();base.append(dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()))
 t=t.replace('namespace Ashfall.Modules.Modes','using Ashfall.Modules.Modes;\nnamespace Ashfall.Integration.MatchRounds')
 if n=='RoundController.cs':
  t=t.replace('sealed class RoundController','sealed partial class RoundController').replace('"Modes/2"','"MatchRounds/6-unreviewed"')
  t=t.replace('private ModeRules rules;','private long mutation;\n        private ModeRules rules;')
  t=t.replace('lifecycle.Commit(c);','mutation++; lifecycle.Commit(c);').replace('ledger.Commit(c);','mutation++; ledger.Commit(c);').replace('lastTime=now; CheckEnd(now); return RuleResult.Accept();','mutation++; lastTime=now; CheckEnd(now); return RuleResult.Accept();')
  t=t.replace('private RuleResult Check(CommandContext c,double now,bool control)\n        {','private RuleResult Check(CommandContext c,double now,bool control)\n        {\n            if(mutation==Int64.MaxValue)return RuleResult.Reject("revision-exhausted");').replace('public RuleResult AdvanceClock(double now)\n        {','public RuleResult AdvanceClock(double now)\n        {\n            if(mutation==Int64.MaxValue)return RuleResult.Reject("revision-exhausted");')
 (out/n).write_text(t)
(D/'round-base-provenance.json').write_text(json.dumps(base,indent=2))
