using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.Pickups
{
    public enum PickupAction { Reserve, Collect, Release, Equip, Stow, Open, BeginPeel, SamplePeel, SampleShake, DesktopPress, DesktopRelease, Consume, Drop }
    public enum Preparation { Idle, Shake, Open, Consume }
    public sealed class PickupSnapshot
    {
        public readonly string Id, Kind, Owner, ReservedBy;
        public readonly Float3 Position;
        public readonly bool Removed;
        public PickupSnapshot(string id,string kind,string owner,string reserved,Float3 position,bool removed)
        { Id=id;Kind=kind;Owner=owner;ReservedBy=reserved;Position=position;Removed=removed; }
    }

    // One instance per authority session/epoch; create a NEW instance on migration.
    // Public calls are serialized, including port callbacks. Ports must not reenter.
    public sealed partial class PickupRules
    {
        private sealed class Item
        {
            public string Id, Kind, Semantic, Owner, Reserved;
            public Float3 Position;
            public long Until;
            public bool Removed;
            public int Amount;
        }
        private sealed class UseState
        {
            public string Held;
            public Preparation Phase;
            public int Peels, Reversals;
            public float Distance, Seconds, Soda;
            public Float3 Previous, LastMotion, PeelStart;
            public bool HaveSample, Peeling, Pressed, AwaitRelease=true;
            public long SampleTick;
            public void Stow() { Held=null;Phase=Preparation.Idle;Peels=Reversals=0;
                Distance=Seconds=0;HaveSample=Peeling=Pressed=false;AwaitRelease=true;LastMotion=new Float3(); }
        }
        private readonly object sync=new object();
        private readonly Dictionary<string,Item> items=new Dictionary<string,Item>(StringComparer.Ordinal);
        private readonly Dictionary<string,UseState> users=new Dictionary<string,UseState>(StringComparer.Ordinal);
        private Dictionary<string,Dictionary<string,long>> seen=new Dictionary<string,Dictionary<string,long>>(StringComparer.Ordinal);
        private readonly IPickupActors actors;
        private readonly IPickupTransactions transactions;
        private readonly IInteractionQuery query;
        private readonly IAuthorityGate gate;
        private readonly string session;
        private readonly long epoch;
        private readonly int hz, replayCapacity, maxActors, maxItems;
        private readonly long window, lease;
        private long now;
        private int replayCount;
        private bool executing;

        public PickupRules(string sessionId,long authorityEpoch,int tickRate,IAuthorityGate authority,
            IPickupActors actorPort,IPickupTransactions transactionPort,IInteractionQuery interaction,
            int replayLimit,int actorLimit,int itemLimit)
        {
            if(String.IsNullOrEmpty(sessionId)||authorityEpoch<0||tickRate<1||tickRate>1000||authority==null||actorPort==null||transactionPort==null||interaction==null||replayLimit<1||actorLimit<1||itemLimit<1)
                throw new ArgumentException("Invalid authority configuration");
            session=sessionId;epoch=authorityEpoch;hz=tickRate;gate=authority;actors=actorPort;
            transactions=transactionPort;query=interaction;replayCapacity=replayLimit;
            maxActors=actorLimit;maxItems=itemLimit;window=10L*hz;lease=hz;
        }
        public static bool IsConsumable(string kind) { return kind=="banana"||kind=="soda"||kind=="shaker"; }
        public static int SupplyAmount(string kind)
        { switch(kind) { case "pistol":return 16;case "smg":return 20;case "rifle":return 32;case "shells":return 6;case "sniper":return 5;case "builds":return 6;default:return 0; } }
        // Trusted map/session registration. Never exposed as client intent.
        public bool Register(string id,string kind,string semanticOwner,Float3 position)
        { return Register(id,kind,semanticOwner,position,SupplyAmount(kind)); }
        // Source spawn_box permits explicit quantities; negative values select defaults.
        public bool Register(string id,string kind,string semanticOwner,Float3 position,int amount)
        {
            lock(sync) {
                if(quarantined||pendingSupply!=null||revision==long.MaxValue||executing||!Id(id)||!Id(semanticOwner)||!position.IsFinite||(!IsConsumable(kind)&&kind!="backpack"&&SupplyAmount(kind)==0)||items.Count>=maxItems||items.ContainsKey(id))return false;
                if(SupplyAmount(kind)==0&&amount!=0)return false;
                items.Add(id,new Item {Id=id,Kind=kind,Semantic=semanticOwner,Position=position,Amount=amount<0?SupplyAmount(kind):amount});revision++;return true;
            }
        }
        public PickupSnapshot Inspect(string id)
        {
            lock(sync) { Item item;if(id==null||!items.TryGetValue(id,out item))return null;
                return new PickupSnapshot(item.Id,item.Kind,item.Owner,item.Until>now?item.Reserved:null,item.Position,item.Removed); }
        }
        public Preparation Phase(string actor) { lock(sync) { UseState u;return users.TryGetValue(actor,out u)?u.Phase:Preparation.Idle; } }
        public string HeldInstance(string actor) { lock(sync) { UseState u;return users.TryGetValue(actor,out u)?u.Held:null; } }
        public float PendingSoda(string actor) { lock(sync) { UseState u;return users.TryGetValue(actor,out u)?u.Soda:0; } }

        // Trusted host clock; advancing once drives soda at 5 shield/second.
        // Repeating the tick cannot apply it twice. Never pass a client timestamp here.
        public void Advance(long tick)
        {
            lock(sync) {
                if(quarantined||revision==long.MaxValue||executing)throw new InvalidOperationException("Quarantine, revision exhaustion or port reentry");
                if(tick<now)throw new ArgumentOutOfRangeException("tick");
                executing=true;
                try {
                    float dt=(float)((tick-now)/(double)hz);now=tick;
                    foreach(KeyValuePair<string,UseState> pair in users) {
                        ActorFrame f;UseState u=pair.Value;
                        if(!actors.TryRead(pair.Key,out f)||!Valid(f)||!f.Allowed) { u.Stow();continue; }
                        if(!f.HandEligible)u.Stow();
                        if(f.Desktop&&u.Held!=null&&u.Pressed&&u.Phase==Preparation.Shake) {
                            u.Seconds+=dt*1.3f;
                            if(u.Reversals>=6&&u.Seconds>=3)u.Phase=Preparation.Consume;
                        }
                        float portion=Math.Min(u.Soda,dt*5f);
                        if(portion>0) {
                            ShieldProcessing result=transactions.TryAddShield(pair.Key,portion);
                            if(result==ShieldProcessing.Applied||result==ShieldProcessing.CappedNoOp)u.Soda-=portion;
                        }
                    }
                    Prune();
                } finally { revision++;executing=false; }
            }
        }
        public RuleResult Execute(CommandContext context,PickupAction action,string itemId)
        {
            lock(sync) {
                if(quarantined)return RuleResult.Reject("quarantine");
                if(pendingSupply!=null)return RuleResult.Reject("supply-pending");
                if(revision==long.MaxValue)return RuleResult.Reject("revision-exhausted");
                if(executing)return RuleResult.Reject("reentry");
                executing=true;
                try { return ExecuteInner(context,action,itemId); }
                finally { revision++;executing=false; }
            }
        }
        private RuleResult ExecuteInner(CommandContext c,PickupAction action,string id)
        {
            ActorFrame f;UseState u;RuleResult start=BeginCommand(c,out f,out u);if(!start.Accepted)return start;
            if(action==PickupAction.Stow) {
                var projection=transactions as IPreparedPickupSelection;
                if(projection!=null&&!projection.PublishSelection(c.ActorId,u.Held,null))return No("selection-rejected");
                u.Stow();return Yes(); }
            return ExecuteItem(c,action,id,f,u);
        }
        private RuleResult BeginCommand(CommandContext c,out ActorFrame f,out UseState u)
        {
            f=null;u=null;
            if(!c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=epoch||!Id(c.ActorId)||!Id(c.CommandId)||c.Tick<0||c.Tick>now||now-c.Tick>window)return No("authority-context");
            RuleResult authorized=gate.Validate(c);if(!authorized.Accepted)return authorized;
            Dictionary<string,long> commands;
            if(!seen.TryGetValue(c.ActorId,out commands)) {
                if(seen.Count>=maxActors)return No("actor-limit");
                commands=new Dictionary<string,long>(StringComparer.Ordinal);seen.Add(c.ActorId,commands);
            }
            if(commands.ContainsKey(c.CommandId))return No("replay");
            if(replayCount>=replayCapacity)return No("replay-capacity");
            // Reserve ID even for rejected gameplay intents; retries need a new ID.
            commands.Add(c.CommandId,now);replayCount++;
            if(!users.TryGetValue(c.ActorId,out u)) { u=new UseState();users.Add(c.ActorId,u); }
            if(!actors.TryRead(c.ActorId,out f)||!Valid(f)||!f.Allowed||!f.HandEligible) { u.Stow();return No("actor-ineligible"); }
            return Yes();
        }
        private RuleResult ExecuteItem(CommandContext c,PickupAction action,string id,ActorFrame f,UseState u)
        {
            Item item;
            if(id==null||!items.TryGetValue(id,out item)||item.Removed)return No("item-missing");
            if(action==PickupAction.Reserve||action==PickupAction.Collect||action==PickupAction.Release) {
                if(item.Owner!=null)return No("already-owned");
                if(item.Reserved!=null&&item.Until>now&&item.Reserved!=c.ActorId)return No("reserved");
                if(action==PickupAction.Release) {
                    if(item.Reserved!=c.ActorId)return No("not-reserver");
                    item.Reserved=null;return Yes();
                }
                if(!f.TransferSafe||!Reach(c.ActorId,f,item))return No("blocked-interaction");
                if(action==PickupAction.Reserve) { if(now>long.MaxValue-lease)return No("reservation-overflow");item.Reserved=c.ActorId;item.Until=now+lease;return Yes(); }
                if(item.Reserved!=c.ActorId||item.Until<=now)return No("reservation-required");
                TransferKind op=IsConsumable(item.Kind)?TransferKind.Store:(item.Kind=="backpack"?TransferKind.UpgradeBackpack:TransferKind.Supply);
                if(op==TransferKind.Store) {
                    if(!Store(c.ActorId,f,item,u))return No("inventory-rejected");
                } else if((op==TransferKind.Supply&&item.Amount<=0)||!CommitEffect(Move(op,c.ActorId,item,item.Position,0,0,false)))return No("inventory-rejected");
                item.Reserved=null;
                if(op==TransferKind.Store) { item.Owner=c.ActorId; }
                else item.Removed=true;
                return Yes();
            }
            if(item.Owner!=c.ActorId||!IsConsumable(item.Kind))return No("not-owner");
            if(action==PickupAction.Drop) {
                Float3 position;
                if(!f.TransferSafe||!transactions.TryPlaceDrop(c.ActorId,id,out position)||!position.IsFinite||Distance(f.Head,position)>2.2f||!query.HasLineOfSight(c.ActorId,item.Semantic,f.Head,position,2.2f))return No("drop-blocked");
                if(!CommitEffect(Move(TransferKind.Drop,c.ActorId,item,position,0,0,false)))return No("inventory-rejected");
                item.Owner=null;item.Position=position;item.Reserved=null;if(u.Held==id)u.Stow();return Yes();
            }
            if(action==PickupAction.Equip) {
                if(!f.TransferSafe)return No("transfer-unsafe");
                var projection=transactions as IPreparedPickupSelection;
                if(projection!=null&&!projection.PublishSelection(c.ActorId,u.Held,id))return No("selection-rejected");
                u.Stow();u.Held=id;u.Phase=item.Kind=="shaker"?Preparation.Shake:Preparation.Open;return Yes();
            }
            if(u.Held!=id)return No("not-held");
            if(action==PickupAction.Open) {
                if(!f.Desktop&&(!f.OtherHandEligible||Distance(f.OtherLocal,new Float3(0,.10f,0))>.35f))return No("open-reach");
                if(u.Phase!=Preparation.Open||(item.Kind=="banana"&&u.Peels<4))return No("not-prepared");
                u.Phase=Preparation.Consume;return Yes();
            }
            if(action==PickupAction.BeginPeel) {
                if(item.Kind!="banana"||u.Phase!=Preparation.Open||!f.OtherHandEligible||!f.OtherGrip||Distance(f.OtherLocal,new Float3(0,.10f,0))>.35f)return No("peel-start");
                u.Peeling=true;u.PeelStart=f.OtherLocal;return Yes();
            }
            if(action==PickupAction.SamplePeel) {
                if(!u.Peeling||item.Kind!="banana"||u.Phase!=Preparation.Open)return No("peel-inactive");
                if(!f.OtherHandEligible||!f.OtherGrip||Distance(f.OtherLocal,u.PeelStart)>.5f) { u.Peeling=false;return No("peel-invalid"); }
                if(u.PeelStart.Y-f.OtherLocal.Y>=.10f) { u.Peels++;u.Peeling=false;if(u.Peels>=4)u.Phase=Preparation.Consume; }return Yes();
            }
            if(action==PickupAction.SampleShake) {
                if(f.Desktop||u.Phase!=Preparation.Shake)return No("shake-phase");
                if(u.HaveSample&&now<=u.SampleTick)return No("sample-time");
                if(u.HaveSample) {
                    Float3 motion=Subtract(f.LocalHand,u.Previous);float travel=Length(motion),dt=(now-u.SampleTick)/(float)hz,speed=travel/dt;
                    if(speed>=.45f&&speed<=8&&travel<.3f) {
                        u.Distance+=travel*1.3f;u.Seconds+=Math.Min(dt,.1f)*1.3f;
                        Float3 direction=Scale(motion,1/travel);
                        if(Dot(direction,u.LastMotion)<-.4f)u.Reversals++;
                        u.LastMotion=direction;
                    }
                }
                u.Previous=f.LocalHand;u.SampleTick=now;u.HaveSample=true;
                if(u.Distance>=1.8f&&u.Reversals*1.3f>=6&&u.Seconds>=3)u.Phase=Preparation.Consume;
                return Yes();
            }
            if(action==PickupAction.DesktopRelease||action==PickupAction.DesktopPress) {
                if(!f.Desktop)return No("desktop-only");
                if(action==PickupAction.DesktopRelease) { u.Pressed=false;u.AwaitRelease=false;return Yes(); }
                bool edge=!u.Pressed&&!u.AwaitRelease;
                if(edge) {
                    if(u.Phase==Preparation.Shake) { u.Reversals+=2;u.Distance+=.6f*1.3f; }
                    else if(u.Phase==Preparation.Open) { if(item.Kind=="banana")u.Peels=Math.Min(4,u.Peels+1);if(item.Kind!="banana"||u.Peels>=4)u.Phase=Preparation.Consume; }
                }
                if(u.Phase==Preparation.Shake&&u.Reversals>=6&&u.Seconds>=3)u.Phase=Preparation.Consume;
                u.Pressed=true;return Yes();
            }
            if(action==PickupAction.Consume) {
                if(u.Phase!=Preparation.Consume)return No("not-prepared");
                bool near=Distance(f.Anchor,f.Head)<=.450001f||
                    (Math.Abs(f.HeadLocalAnchor.X)<=.25f&&f.HeadLocalAnchor.Y>=-.35f&&f.HeadLocalAnchor.Y<=.12f&&f.HeadLocalAnchor.Z>=-.05f&&f.HeadLocalAnchor.Z<=.50f);
                if(f.Desktop)near=u.Pressed;
                if(!near||(!f.Desktop&&item.Kind!="banana"&&f.ItemUpDot>=.75f))return No("mouth-contact");
                if(item.Kind=="banana"?(f.Health>=100&&f.Stamina>=f.MaxStamina):f.Shield>=100)return No("full");
                string next;
                if(!Consume(c.ActorId,item,u,out next))return No("effect-rejected");
                if(item.Kind=="soda")u.Soda+=50;
                item.Owner=null;item.Removed=true;Select(u,next);return Yes();
            }
            return No("action");
        }
        private bool Store(string actor,ActorFrame frame,Item incoming,UseState user)
        {
            Transfer request=Move(TransferKind.Store,actor,incoming,incoming.Position,0,0,false);
            IStagedStore stage=null;
            try {
                ISelectedPickupTransactions selectedPort=transactions as ISelectedPickupTransactions;
                bool staged=selectedPort!=null?selectedPort.TryStageStore(request,user.Held,out stage):transactions.TryStageStore(request,out stage);
                if(!staged||stage==null)return false;
                StoreReceipt receipt=stage.Receipt;
                // Incoming is the exact immutable request, not an adapter replacement.
                if(receipt==null||!Object.ReferenceEquals(receipt.Incoming,request)||receipt.Count<0||receipt.Count>2||(selectedPort!=null&&receipt.Selection==null))return false;
                HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
                for(int n=0;n<receipt.Count;n++) {
                    EvictedInstance e=receipt.At(n);Item old;
                    if(e==null||!Id(e.ItemId)||!Id(e.Kind)||!Id(e.SemanticOwnerId)||e.ItemId==incoming.Id||!ids.Add(e.ItemId)||!e.Position.IsFinite||
                        Distance(frame.Head,e.Position)>2.2f||!query.HasLineOfSight(actor,e.SemanticOwnerId,frame.Head,e.Position,2.2f))return false;
                    if(IsConsumable(e.Kind)) {
                        if(!items.TryGetValue(e.ItemId,out old)||old.Removed||old.Owner!=actor||old.Kind!=e.Kind||old.Semantic!=e.SemanticOwnerId)return false;
                    } else if(items.ContainsKey(e.ItemId))return false;
                }
                string selected=null;
                if(receipt.Selection!=null) {
                    if(!ValidSelection(receipt.Selection,user,actor,incoming,true,out selected))return false;
                    for(int n=0;n<receipt.Count;n++)for(int j=0;j<receipt.Selection.Count;j++)
                        if(receipt.At(n).ItemId==receipt.Selection.At(j))return false;
                }
                if(!stage.TryCommit())return false;
                // No fallible external operations after commit. Execute holds sync;
                // coordinator's transaction lane covers inventory readers as well.
                for(int n=0;n<receipt.Count;n++) {
                    EvictedInstance e=receipt.At(n);
                    if(!IsConsumable(e.Kind))continue; // weapon world publication belongs to inventory port
                    Item old=items[e.ItemId];old.Owner=null;old.Reserved=null;old.Until=0;old.Position=e.Position;
                    UseState owner;if(users.TryGetValue(actor,out owner)&&owner.Held==old.Id)owner.Stow();
                }
                // Source collect always stows. XR grip explicitly equips incoming afterward;
                // consume continuation remains a separate original-slot operation.
                incoming.Owner=actor;Select(user,selected);
                return true;
            } finally { if(stage!=null)stage.Abort(); }
        }
        private bool CommitEffect(Transfer request)
        {
            IAtomicPickupEffects explicitPort=transactions as IAtomicPickupEffects;
            return explicitPort!=null?explicitPort.CommitEffect(request)==AtomicOutcome.Applied:transactions.TryCommit(request);
        }
        private bool Consume(string actor,Item item,UseState user,out string selected)
        {
            selected=null;
            Transfer request=Move(TransferKind.Consume,actor,item,item.Position,item.Kind=="banana"?100:0,item.Kind=="shaker"?50:0,item.Kind=="banana");
            ISelectedPickupTransactions port=transactions as ISelectedPickupTransactions;
            if(port==null)return transactions.TryCommit(request); // legacy: no continuation claim
            IStagedConsume stage=null;
            try {
                if(!port.TryStageConsume(request,user.Held,out stage)||stage==null)return false;
                ConsumeReceipt receipt=stage.Receipt;
                if(receipt==null||!Object.ReferenceEquals(receipt.Request,request)||
                    !ValidSelection(receipt.Selection,user,actor,item,false,out selected))return false;
                return stage.TryCommit()==AtomicOutcome.Applied;
            } finally { if(stage!=null)stage.Abort(); }
        }
        private bool ValidSelection(StackSelection selection,UseState user,string actor,Item item,bool collecting,out string selected)
        {
            selected=null;
            if(selection==null||selection.BeforeHeld!=user.Held||selection.Count<1||selection.Count>2)return false;
            bool found=false;string survivor=null;
            for(int n=0;n<selection.Count;n++) {
                string id=selection.At(n);Item member;
                if(!Id(id)||(n>0&&id==selection.At(0)))return false;
                if(id==item.Id) { found=true;continue; }
                if(item.Kind=="banana"||!items.TryGetValue(id,out member)||member.Owner!=actor||member.Removed||member.Kind!=item.Kind)return false;
                if(survivor==null)survivor=id;
            }
            if(!found)return false;
            if(collecting) {
                // Canonical collect stows even when adding to the currently held stack.
                if(selection.Selected!=null)return false;
            } else if(selection.Selected!=survivor)return false;
            selected=selection.Selected;return true;
        }
        private void Select(UseState user,string id)
        { user.Stow();if(id!=null) { user.Held=id;user.Phase=items[id].Kind=="shaker"?Preparation.Shake:Preparation.Open; } }
        private bool Reach(string actor,ActorFrame f,Item i)
        {
            Float3 delta=Subtract(i.Position,f.Hand);float distance=Length(delta),forward=Length(f.Forward);
            if(distance>2||forward<.001f||!Finite(forward))return false;
            if(distance>.35f&&Dot(f.Forward,delta)/(forward*distance)<=.94f)return false;
            return query.HasLineOfSight(actor,actor,f.Head,f.Hand,Distance(f.Head,f.Hand))&&query.HasLineOfSight(actor,i.Semantic,f.Hand,i.Position,2);
        }
        private static bool Valid(ActorFrame f) { return f!=null&&f.Head.IsFinite&&f.Hand.IsFinite&&f.Forward.IsFinite&&f.LocalHand.IsFinite&&f.OtherLocal.IsFinite&&f.Anchor.IsFinite&&f.HeadLocalAnchor.IsFinite&&Finite(f.ItemUpDot)&&Finite(f.Health)&&Finite(f.Shield)&&Finite(f.Stamina)&&Finite(f.MaxStamina)&&f.MaxStamina>=0; }
        private void Prune() {
            foreach(Dictionary<string,long> commands in seen.Values) {
                List<string> expired=new List<string>();foreach(KeyValuePair<string,long> e in commands)if(now-e.Value>window)expired.Add(e.Key);
                foreach(string key in expired) { commands.Remove(key);replayCount--; }
            }
        }
        private static Transfer Move(TransferKind op,string actor,Item i,Float3 p,float h,float s,bool stamina) { return new Transfer(op,actor,i.Id,i.Kind,p,h,s,stamina,i.Amount); }
        private static bool Id(string id) { return !String.IsNullOrEmpty(id)&&id.Length<=128; }
        private static bool Finite(float f) { return !float.IsInfinity(f)&&!float.IsNaN(f); }
        private static Float3 Subtract(Float3 a,Float3 b) { return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        private static Float3 Scale(Float3 a,float s) { return new Float3(a.X*s,a.Y*s,a.Z*s); }
        private static float Dot(Float3 a,Float3 b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
        private static float Length(Float3 a) { return (float)Math.Sqrt((double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z); }
        private static float Distance(Float3 a,Float3 b) { return Length(Subtract(a,b)); }
        private static RuleResult Yes() { return RuleResult.Accept(); }
        private static RuleResult No(string why) { return RuleResult.Reject(why); }
    }
}
