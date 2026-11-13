using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.Construction
{
    public sealed class PlacedPanel
    {
        public readonly string Id, Owner;
        public readonly Panel Shape;
        public readonly float Health;
        public PlacedPanel(string id,string owner,Panel shape,float health) { Id=id;Owner=owner;Shape=shape;Health=health; }
    }
    public sealed class BuildDecision
    {
        public readonly bool Accepted;
        public readonly string Reason;
        public readonly int Cost;
        private readonly Panel[] panels;
        public Panel[] Panels { get { return (Panel[])panels.Clone(); } }
        internal BuildDecision(string reason,List<Panel> plan,int cost)
        { Reason=reason;Accepted=reason.Length==0;panels=plan.ToArray();Cost=cost; }
    }
    // Single host simulation thread. No client-authored transforms or support flags.
    public sealed class ConstructionService
    {
        public const string ApiVersion = "Construction/2";
        private readonly string session;
        private readonly long epoch;
        private readonly IAuthorityGate authority;
        private readonly IConstructionActors actors;
        private readonly IConstructionWorld world;
        private readonly Dictionary<string,PlacedPanel> live=new Dictionary<string,PlacedPanel>(StringComparer.Ordinal);
        private readonly Dictionary<string,int> balances=new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly Dictionary<string,long> lastTicks=new Dictionary<string,long>(StringComparer.Ordinal);
        private readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<CommandContext> replayOrder=new Queue<CommandContext>();
        private readonly Dictionary<string,long> retiredTicks=new Dictionary<string,long>(StringComparer.Ordinal);
        private readonly Dictionary<string,long> sequences=new Dictionary<string,long>(StringComparer.Ordinal);
        private readonly Dictionary<string,bool> sequencedActors=new Dictionary<string,bool>(StringComparer.Ordinal);
        private HashSet<string> staticRoots=new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,double> pending=new Dictionary<string,double>(StringComparer.Ordinal);
        private readonly Dictionary<string,Panel> preferred=new Dictionary<string,Panel>(StringComparer.Ordinal);
        private long serial;
        private double clock;
        public readonly bool InfiniteBuilds;
        public ConstructionService(string sessionId,long authorityEpoch,IAuthorityGate gate,IConstructionActors actorPort,IConstructionWorld worldPort,bool infiniteBuilds)
        {
            if(String.IsNullOrEmpty(sessionId)||gate==null||actorPort==null||worldPort==null)throw new ArgumentException("construction dependencies");
            session=sessionId;epoch=authorityEpoch;authority=gate;actors=actorPort;world=worldPort;InfiniteBuilds=infiniteBuilds;
        }
        public int Charges(string actor) { int value;return balances.TryGetValue(actor,out value)?value:999; }
        public PlacedPanel[] Snapshot() { var list=new List<PlacedPanel>(live.Values);list.Sort(delegate(PlacedPanel a,PlacedPanel b){return String.CompareOrdinal(a.Id,b.Id);});return list.ToArray(); }
        private string Gate(CommandContext c,long? sequence)
        {
            if(!c.IsAuthority || c.SessionId!=session || c.AuthorityEpoch!=epoch)return "authority";
            if(String.IsNullOrEmpty(c.ActorId)||String.IsNullOrEmpty(c.CommandId)||c.CommandId.Length>128||c.ActorId.Length>128||c.Tick<0)return "context";
            RuleResult allowed=authority.Validate(c);if(!allowed.Accepted)return allowed.Reason.Length==0?"authority":allowed.Reason;
            long last; if(lastTicks.TryGetValue(c.ActorId,out last)&&c.Tick<last)return "stale_tick";
            bool mode;
            if(sequencedActors.TryGetValue(c.ActorId,out mode)&&mode!=sequence.HasValue)return "sequence_mode";
            if(sequence.HasValue && (sequence.Value<0 || (sequences.TryGetValue(c.ActorId,out last)&&sequence.Value<=last)))return "stale_sequence";
            // Compatibility port fails closed once an ID's tick is retired.
            // The explicit sequence port supports arbitrarily many same-tick events.
            if(!sequence.HasValue&&retiredTicks.TryGetValue(c.ActorId,out last)&&c.Tick<=last)return "replay_window";
            string key=c.ActorId.Length+":"+c.ActorId+c.CommandId;
            if(!seen.Add(key))return "replay";
            replayOrder.Enqueue(c);while(replayOrder.Count>2048)
            {
                CommandContext old=replayOrder.Dequeue();seen.Remove(old.ActorId.Length+":"+old.ActorId+old.CommandId);
                retiredTicks[old.ActorId]=old.Tick;
            }
            sequencedActors[c.ActorId]=sequence.HasValue;
            if(sequence.HasValue)sequences[c.ActorId]=sequence.Value;
            lastTicks[c.ActorId]=c.Tick;
            return "";
        }
        private static BuildDecision Decision(string reason,List<Panel> panels,int cost) { return new BuildDecision(reason,panels,cost); }
        public BuildDecision Preview(string actorId)
        {
            BuilderState state;
            if(!actors.TryRead(actorId,out state)||state==null||!state.Eligible||!state.Tracked)return Decision("input_ineligible",new List<Panel>(),0);
            if(!state.Head.IsFinite||!state.Origin.IsFinite||!state.Direction.IsFinite||Geometry.Length(state.Origin)>100000||Geometry.Length(state.Head)>100000)return Decision("pose",new List<Panel>(),0);
            string first="reach";
            List<Panel> candidates=Geometry.Intersections(state.Origin,state.Direction);
            Panel old;
            if(candidates.Count>0&&preferred.TryGetValue(actorId,out old))
            {
                Float3 ray=Geometry.Mul(state.Direction,1/Geometry.Length(state.Direction));
                float component=Geometry.C(ray,old.Axis);
                if(Math.Abs(component)>.000001f)
                {
                    float depth=(Geometry.C(old.Center,old.Axis)-Geometry.C(state.Origin,old.Axis))/component;
                    Float3 offset=Geometry.Sub(Geometry.Add(state.Origin,Geometry.Mul(ray,depth)),old.Center);
                    bool inside=true;for(int axis=0;axis<3;axis++)if(axis!=old.Axis&&Math.Abs(Geometry.C(offset,axis))>Geometry.Side/2+.08f)inside=false;
                    float minimum=old.Kind=="wall"?Geometry.WallMinimum:0;
                    if(inside&&depth>=minimum&&depth<=Geometry.Range&&Math.Abs(depth-candidates[0].Distance)<=.08f)
                        candidates.Insert(0,new Panel(old.Kind,old.Center,old.Axis,false,depth));
                }
            }
            foreach(Panel candidate in candidates)
            {
                if(Occupied(candidate))continue;
                var plan=new List<Panel>();plan.Add(candidate);
                if(!Supported(plan)) { Panel lower=Geometry.Lower(candidate);if(!Occupied(lower))plan.Insert(0,lower); }
                string reason=Validate(plan,state,actorId);
                if(reason.Length==0){preferred[actorId]=candidate;return Decision("",plan,InfiniteBuilds?0:plan.Count);}
                if(first=="reach")first=reason;
            }
            return Decision(first,new List<Panel>(),0);
        }
        public BuildDecision Place(CommandContext context)
        { return PlaceCore(context,null); }
        public BuildDecision Place(ConstructionCommand command)
        { return PlaceCore(command.Context,command.Sequence); }
        private BuildDecision PlaceCore(CommandContext context,long? sequence)
        {
            string rejected=Gate(context,sequence);if(rejected.Length>0)return Decision(rejected,new List<Panel>(),0);
            // Recompute from host pose and current collisions/support; never trust a preview.
            BuildDecision d=Preview(context.ActorId);preferred.Remove(context.ActorId);if(!d.Accepted)return d;
            foreach(Panel p in d.Panels)
            {
                string id="build_"+epoch+"_"+(++serial);
                live.Add(id,new PlacedPanel(id,context.ActorId,p,p.Kind=="wall"?180:140));
            }
            balances[context.ActorId]=Charges(context.ActorId)-d.Cost;
            Refresh(null);return d;
        }
        public void CancelPreview(string actorId) { if(actorId!=null)preferred.Remove(actorId); }
        private bool Occupied(Panel panel) { foreach(PlacedPanel p in live.Values)if(p.Shape.Slot==panel.Slot)return true;return false; }
        private string Validate(List<Panel> plan,BuilderState state,string actor)
        {
            if(live.Count+plan.Count>256||(!InfiniteBuilds&&Charges(actor)<plan.Count))return "capacity";
            foreach(Panel p in plan)
            {
                if(Geometry.Length(Geometry.Sub(p.Center,state.Head))>Geometry.Range+Geometry.Side)return "reach";
                if(Geometry.Contains(p,state.Head,.20f))return "head_clearance";
                if(world.IsBuried(p))return p.Kind=="wall"?"buried_wall":"buried_floor";
                string clearance=world.Clearance(p,actor);if(!String.IsNullOrEmpty(clearance))return clearance;
                if(!world.HasLineOfSight(actor,state.Origin,p.Center))return "occluded";
            }
            return Supported(plan)?"":"unsupported";
        }
        private HashSet<string> SupportedLive()
        {
            var supported=new HashSet<string>(StringComparer.Ordinal);
            foreach(PlacedPanel p in live.Values)if(world.HasStaticContact(p.Shape,Geometry.ContactTolerance))supported.Add(p.Id);
            bool changed=true;
            while(changed) { changed=false;foreach(PlacedPanel p in live.Values)if(!supported.Contains(p.Id))foreach(PlacedPanel other in live.Values)if(supported.Contains(other.Id)&&Geometry.Connected(p.Shape,other.Shape)){supported.Add(p.Id);changed=true;break;} }
            return supported;
        }
        private bool Supported(List<Panel> plan)
        {
            HashSet<string> supported=SupportedLive();var accepted=new HashSet<int>();
            for(int i=0;i<plan.Count;i++)
            {
                if(world.HasStaticContact(plan[i],Geometry.ContactTolerance))accepted.Add(i);
                foreach(PlacedPanel p in live.Values)if(supported.Contains(p.Id)&&Geometry.Connected(plan[i],p.Shape))accepted.Add(i);
            }
            bool changed=true;while(changed){changed=false;for(int i=0;i<plan.Count;i++)if(!accepted.Contains(i))for(int j=0;j<plan.Count;j++)if(accepted.Contains(j)&&Geometry.Connected(plan[i],plan[j])){accepted.Add(i);changed=true;break;}}
            return accepted.Count==plan.Count;
        }
        public RuleResult AwardSupply(CommandContext context,int amount)
        { return AwardSupplyCore(context,null,amount); }
        public RuleResult AwardSupply(ConstructionCommand command,int amount)
        { return AwardSupplyCore(command.Context,command.Sequence,amount); }
        private RuleResult AwardSupplyCore(CommandContext context,long? sequence,int amount)
        {
            string reason=Gate(context,sequence);if(reason.Length>0)return RuleResult.Reject(reason);
            if(amount<=0||Charges(context.ActorId)>Int32.MaxValue-amount)return RuleResult.Reject("amount");
            balances[context.ActorId]=Charges(context.ActorId)+amount;return RuleResult.Accept();
        }
        public RuleResult Damage(CommandContext context,string pieceId,float validatedDamage)
        { return DamageCore(context,null,pieceId,validatedDamage); }
        public RuleResult Damage(ConstructionCommand command,string pieceId,float validatedDamage)
        { return DamageCore(command.Context,command.Sequence,pieceId,validatedDamage); }
        private RuleResult DamageCore(CommandContext context,long? sequence,string pieceId,float validatedDamage)
        {
            string reason=Gate(context,sequence);if(reason.Length>0)return RuleResult.Reject(reason);
            PlacedPanel p;if(pieceId==null||!live.TryGetValue(pieceId,out p))return RuleResult.Reject("missing_piece");
            if(float.IsNaN(validatedDamage)||float.IsInfinity(validatedDamage)||validatedDamage<=0)return RuleResult.Reject("damage");
            float hp=Math.Max(0,p.Health-validatedDamage);
            if(hp>0)live[pieceId]=new PlacedPanel(p.Id,p.Owner,p.Shape,hp);
            else { live.Remove(pieceId);pending.Remove(pieceId);Refresh(p.Shape); }
            return RuleResult.Accept();
        }
        // Source has no manual dismantle/refund interaction. Do not invent one.
        public RuleResult Dismantle(CommandContext context,string pieceId)
        { string reason=Gate(context,null);return RuleResult.Reject(reason.Length>0?reason:"manual_dismantle_unavailable"); }
        public RuleResult Dismantle(ConstructionCommand command,string pieceId)
        { string reason=Gate(command.Context,command.Sequence);return RuleResult.Reject(reason.Length>0?reason:"manual_dismantle_unavailable"); }
        private void Refresh(Panel severed)
        {
            HashSet<string> supported=SupportedLive();var distance=new Dictionary<string,int>();
            var frontier=new Queue<PlacedPanel>();
            if(severed!=null)foreach(PlacedPanel p in live.Values)if(!supported.Contains(p.Id)&&Geometry.Connected(severed,p.Shape)){distance[p.Id]=1;frontier.Enqueue(p);}
            foreach(PlacedPanel p in live.Values)if(staticRoots.Contains(p.Id)&&!supported.Contains(p.Id)&&!distance.ContainsKey(p.Id)){distance[p.Id]=1;frontier.Enqueue(p);}
            staticRoots=new HashSet<string>(StringComparer.Ordinal);
            foreach(PlacedPanel p in live.Values)if(world.HasStaticContact(p.Shape,Geometry.ContactTolerance))staticRoots.Add(p.Id);
            while(frontier.Count>0)
            {
                PlacedPanel current=frontier.Dequeue();
                foreach(PlacedPanel p in live.Values)if(!supported.Contains(p.Id)&&!distance.ContainsKey(p.Id)&&Geometry.Connected(p.Shape,current.Shape))
                { distance[p.Id]=distance[current.Id]+1;frontier.Enqueue(p); }
            }
            foreach(PlacedPanel p in live.Values)
            {
                if(supported.Contains(p.Id))pending.Remove(p.Id);
                else if(!pending.ContainsKey(p.Id)){int depth;pending[p.Id]=clock+.30*(distance.TryGetValue(p.Id,out depth)?depth:1);}
            }
        }
        // Host-only simulation step, not exposed as a client command. Paused time does not advance.
        public void Advance(float seconds,bool paused)
        {
            if(paused)return;
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException("seconds");
            clock+=seconds;Refresh(null);var due=new List<string>();foreach(var p in pending)if(p.Value<=clock+.00001)due.Add(p.Key);
            due.Sort(StringComparer.Ordinal);foreach(string id in due){pending.Remove(id);live.Remove(id);}
        }
    }
}
