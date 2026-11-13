from pathlib import Path
import json,hashlib
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableLiveBindingsR4';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableLiveBindingsR4';p=R/'unity/Assets/Ashfall/Modules/InventoryItems/BuildChargeLedger.cs';b=p.read_bytes();q=O/'before'/p.relative_to(R)
if not q.exists():q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows=json.loads((E/'before.json').read_text());rows.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper()));(E/'before.json').write_text(json.dumps(rows,indent=2))
p=R/'unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs';s=p.read_text();needle='        private sealed class AmmoBinding'
s=s.replace(needle,'''        private BuildChargeAccounts builds;
        public bool BindBuilds(BuildChargeAccounts accounts)
        {return Serialize(delegate{if(active||builds!=null||accounts==null)return false;builds=accounts;return true;});}
''' +needle)
s=s.replace('private bool active;private long pulse;','private bool active;')
s=s.replace('if(!active||pulse==long.MaxValue)return ShieldProcessing.Failed;\n            var c=new CommandContext(current.SessionId,actor,"soda-pulse-"+(++pulse),current.Tick,current.AuthorityEpoch,current.IsAuthority);\n            var state=Lane.Read(actor);HealthService.PreparedGain gain;', '''if(!active)return ShieldProcessing.Failed;
            var state=Lane.Read(actor);if(state==null)return ShieldProcessing.Failed;
            var c=new CommandContext(current.SessionId,actor,"soda:"+state.LifeGeneration+":"+state.ConnectionGeneration+":"+current.Tick,current.Tick,current.AuthorityEpoch,current.IsAuthority);
            HealthService.PreparedGain gain;''')
needle='                Dictionary<string,AmmoBinding> map;AmmoBinding binding;'
s=s.replace(needle,'''                if(request.Kind=="builds") {
                    if(builds==null||request.Amount!=6)return AtomicOutcome.Failed;
                    BuildChargeLedger.PreparedCharge charge;
                    if(!builds.ForActor(request.ActorId).PrepareGrant(current,request.ItemId,"builds",request.Amount,current.Tick,out charge).Accepted)return AtomicOutcome.Failed;
                    try{if(!charge.Validate(current.Tick))return AtomicOutcome.Failed;charge.CommitValidated();return AtomicOutcome.Applied;}finally{charge.Abort();}
                }
''' +needle)
p.write_text(s)
