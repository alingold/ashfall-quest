// Derived from exact accepted Construction/2; see R4 manifest and semantic diff.
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
namespace Ashfall.Modules.ConstructionCandidate {
    // Single host simulation thread. No client-authored transforms or support flags.
    internal sealed class CandidateState
    {
        public const string ApiVersion = "ConstructionCandidateState/4";
        private readonly string session;
        private readonly long epoch;
        private readonly IAuthorityGate authority;
        private readonly IConstructionActors actors;
        private readonly IConstructionWorld world;
        private readonly Dictionary<string,PlacedPanel> live=new Dictionary<string,PlacedPanel>(StringComparer.Ordinal);
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
        public CandidateState(string sessionId,long authorityEpoch,IAuthorityGate gate,IConstructionActors actorPort,IConstructionWorld worldPort,bool infiniteBuilds)
        {
            if(String.IsNullOrEmpty(sessionId)||gate==null||actorPort==null||worldPort==null)throw new ArgumentException("construction dependencies");
            session=sessionId;epoch=authorityEpoch;authority=gate;actors=actorPort;world=worldPort;InfiniteBuilds=infiniteBuilds;
        }


        internal CandidateState Copy(IConstructionActors poses)
        {
            var n=new CandidateState(session,epoch,authority,poses,world,InfiniteBuilds);
            foreach(var p in live)n.live.Add(p.Key,p.Value);
            foreach(var p in lastTicks)n.lastTicks.Add(p.Key,p.Value);
            foreach(var p in seen)n.seen.Add(p);
            foreach(var p in replayOrder)n.replayOrder.Enqueue(p);
            foreach(var p in retiredTicks)n.retiredTicks.Add(p.Key,p.Value);
            foreach(var p in sequences)n.sequences.Add(p.Key,p.Value);
            foreach(var p in sequencedActors)n.sequencedActors.Add(p.Key,p.Value);
            foreach(var p in staticRoots)n.staticRoots.Add(p);
            foreach(var p in pending)n.pending.Add(p.Key,p.Value);
            foreach(var p in preferred)n.preferred.Add(p.Key,p.Value);
            n.serial=serial;n.clock=clock;return n;
        }
        public PlacedPanel[] Snapshot() { var list=new List<PlacedPanel>(live.Values);list.Sort(delegate(PlacedPanel a,PlacedPanel b){return String.CompareOrdinal(a.Id,b.Id);});return list.ToArray(); }
        internal void ReconcileNetworkPreview(PlacedPanel[] panels){if(panels==null||panels.Length>256)throw new ArgumentException("network preview");live.Clear();foreach(var panel in panels)live.Add(panel.Id,panel);pending.Clear();preferred.Clear();staticRoots.Clear();Refresh(null);}
        private string Gate(CommandContext c,long? sequence,bool preparedDamage=false)
        {
            if(!c.IsAuthority || c.SessionId!=session || c.AuthorityEpoch!=epoch)return "authority";
            if(String.IsNullOrEmpty(c.ActorId)||String.IsNullOrEmpty(c.CommandId)||c.CommandId.Length>128||c.ActorId.Length>128||c.Tick<0)return "context";
            RuleResult allowed=authority.Validate(c);if(!allowed.Accepted)return allowed.Reason.Length==0?"authority":allowed.Reason;
            long last; if(lastTicks.TryGetValue(c.ActorId,out last)&&c.Tick<last)return "stale_tick";
            bool mode;
            if(!preparedDamage&&sequencedActors.TryGetValue(c.ActorId,out mode)&&mode!=sequence.HasValue)return "sequence_mode";
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
            if(!preparedDamage)sequencedActors[c.ActorId]=sequence.HasValue;
            if(sequence.HasValue)sequences[c.ActorId]=sequence.Value;
            lastTicks[c.ActorId]=c.Tick;
            return "";
        }
        private static CandidateDecision Decision(string reason,List<Panel> panels,int cost) { return new CandidateDecision(reason,panels,cost); }
        public CandidateDecision Preview(string actorId)
        {
            BuilderState state;
            if(!actors.TryRead(actorId,out state)||state==null||!state.Eligible||!state.Tracked)return Decision("input_ineligible",new List<Panel>(),0);
            if(!state.Head.IsFinite||!state.Origin.IsFinite||!state.Direction.IsFinite||CandidateGeometry.Length(state.Origin)>100000||CandidateGeometry.Length(state.Head)>100000)return Decision("pose",new List<Panel>(),0);
            if(state.Variant<0||state.Variant>3||!state.Expanded&&state.Variant!=0)return Decision("variant",new List<Panel>(),0);
            if(state.Tier<0||state.Tier>2||!state.Expanded&&state.Tier!=0)return Decision("tier",new List<Panel>(),0);
            string first="reach";
            List<Panel> candidates=CandidateGeometry.Intersections(state.Origin,state.Direction);
            Panel old;
            if(candidates.Count>0&&preferred.TryGetValue(actorId,out old))
            {
                Float3 ray=CandidateGeometry.Mul(state.Direction,1/CandidateGeometry.Length(state.Direction));
                float component=CandidateGeometry.C(ray,old.Axis);
                if(Math.Abs(component)>.000001f)
                {
                    float depth=(CandidateGeometry.C(old.Center,old.Axis)-CandidateGeometry.C(state.Origin,old.Axis))/component;
                    Float3 offset=CandidateGeometry.Sub(CandidateGeometry.Add(state.Origin,CandidateGeometry.Mul(ray,depth)),old.Center);
                    bool inside=true;for(int axis=0;axis<3;axis++)if(axis!=old.Axis&&Math.Abs(CandidateGeometry.C(offset,axis))>CandidateGeometry.Side/2+.08f)inside=false;
                    float minimum=old.Kind=="wall"?CandidateGeometry.WallMinimum:0;
                    if(inside&&depth>=minimum&&depth<=CandidateGeometry.Range&&Math.Abs(depth-candidates[0].Distance)<=.08f)
                        candidates.Insert(0,new Panel(old.Kind,old.Center,old.Axis,false,depth));
                }
            }
            foreach(Panel face in candidates)
            {
                Panel candidate=new Panel(face.Kind,face.Center,face.Axis,face.LowerSupport,face.Distance,state.Tier,face.Kind=="floor"?0:state.Variant);
                PlacedPanel existing=null;foreach(var row in live.Values)if(row.Shape.Slot==candidate.Slot){existing=row;break;}
                if(existing!=null)
                {
                    if(existing.Owner!=actorId||existing.Shape.Tier>=state.Tier||existing.Shape.Variant!=candidate.Variant)continue;
                    // Upgrades preserve geometry, so skip overlap/capacity checks, but retain placement reach and visibility.
                    if(CandidateGeometry.Length(CandidateGeometry.Sub(candidate.Center,state.Head))>CandidateGeometry.Range+CandidateGeometry.Side){first="reach";continue;}
                    if(!world.HasLineOfSight(actorId,state.Origin,candidate.Center)){first="occluded";continue;}
                    preferred[actorId]=candidate;return Decision("",new List<Panel>{candidate},InfiniteBuilds?0:candidate.Cost-existing.Shape.Cost);
                }
                var plan=new List<Panel>();plan.Add(candidate);
                if(!Supported(plan)) { Panel lower=CandidateGeometry.Lower(candidate);if(!Occupied(lower))plan.Insert(0,lower); }
                string reason=Validate(plan,state,actorId);
                if(reason.Length==0){preferred[actorId]=candidate;int cost=0;foreach(var piece in plan)cost+=piece.Cost;return Decision("",plan,InfiniteBuilds?0:cost);}
                if(first=="reach")first=reason;
            }
            return Decision(first,new List<Panel>(),0);
        }
        public CandidateDecision Place(CommandContext context)
        { return PlaceCore(context,null); }
        public CandidateDecision Place(ConstructionCommand command)
        { return PlaceCore(command.Context,command.Sequence); }
        private CandidateDecision PlaceCore(CommandContext context,long? sequence)
        {
            string rejected=Gate(context,sequence);if(rejected.Length>0)return Decision(rejected,new List<Panel>(),0);
            // Recompute from host pose and current collisions/support; never trust a preview.
            CandidateDecision d=Preview(context.ActorId);preferred.Remove(context.ActorId);if(!d.Accepted)return d;
            if(serial>long.MaxValue-d.Panels.Length)return Decision("serial_exhausted",new List<Panel>(),0);
            foreach(Panel p in d.Panels)
            {
                PlacedPanel existing=null;foreach(var row in live.Values)if(row.Shape.Slot==p.Slot){existing=row;break;}
                if(existing!=null)live[existing.Id]=new PlacedPanel(existing.Id,existing.Owner,p,existing.Health*p.MaximumHealth/existing.Shape.MaximumHealth);
                else {string id="build_"+epoch+"_"+(++serial);live.Add(id,new PlacedPanel(id,context.ActorId,p,p.MaximumHealth));}
            }

            Refresh(null);return d;
        }
        public void CancelPreview(string actorId) { if(actorId!=null)preferred.Remove(actorId); }
        private bool Occupied(Panel panel) { foreach(PlacedPanel p in live.Values)if(p.Shape.Slot==panel.Slot)return true;return false; }
        private string Validate(List<Panel> plan,BuilderState state,string actor)
        {
            if(live.Count+plan.Count>256)return "capacity";
            int owned=0;foreach(var piece in live.Values)if(piece.Owner==actor)owned++;
            if(state.Expanded&&owned+plan.Count>96)return "capacity_player";
            foreach(Panel p in plan)
            {
                if(CandidateGeometry.Length(CandidateGeometry.Sub(p.Center,state.Head))>CandidateGeometry.Range+CandidateGeometry.Side)return "reach";
                if(CandidateGeometry.Contains(p,state.Head,.20f))return "head_clearance";
                bool buried=true;foreach(var box in CandidateGeometry.Boxes(p))if(!world.IsBuried(box))buried=false;
                if(buried)return p.Kind=="wall"?"buried_wall":"buried_floor";
                foreach(var box in CandidateGeometry.Boxes(p)){string clearance=world.Clearance(box,actor);if(!String.IsNullOrEmpty(clearance))return clearance;}
                if(!world.HasLineOfSight(actor,state.Origin,p.Center))return "occluded";
            }
            return Supported(plan)?"":"unsupported";
        }
        private bool StaticContact(Panel panel){foreach(var box in CandidateGeometry.Boxes(panel))if(world.HasStaticContact(box,CandidateGeometry.ContactTolerance))return true;return false;}
        private HashSet<string> SupportedLive()
        {
            var supported=new HashSet<string>(StringComparer.Ordinal);
            foreach(PlacedPanel p in live.Values)if(StaticContact(p.Shape))supported.Add(p.Id);
            bool changed=true;
            while(changed) { changed=false;foreach(PlacedPanel p in live.Values)if(!supported.Contains(p.Id))foreach(PlacedPanel other in live.Values)if(supported.Contains(other.Id)&&CandidateGeometry.Connected(p.Shape,other.Shape)){supported.Add(p.Id);changed=true;break;} }
            return supported;
        }
        private bool Supported(List<Panel> plan)
        {
            HashSet<string> supported=SupportedLive();var accepted=new HashSet<int>();
            for(int i=0;i<plan.Count;i++)
            {
                if(StaticContact(plan[i]))accepted.Add(i);
                foreach(PlacedPanel p in live.Values)if(supported.Contains(p.Id)&&CandidateGeometry.Connected(plan[i],p.Shape))accepted.Add(i);
            }
            bool changed=true;while(changed){changed=false;for(int i=0;i<plan.Count;i++)if(!accepted.Contains(i))for(int j=0;j<plan.Count;j++)if(accepted.Contains(j)&&CandidateGeometry.Connected(plan[i],plan[j])){accepted.Add(i);changed=true;break;}}
            return accepted.Count==plan.Count;
        }
        public RuleResult Damage(CommandContext context,string pieceId,float validatedDamage)
        { return DamageCore(context,null,pieceId,validatedDamage); }
        public RuleResult Damage(ConstructionCommand command,string pieceId,float validatedDamage)
        { return DamageCore(command.Context,command.Sequence,pieceId,validatedDamage); }
        private RuleResult DamageCore(CommandContext context,long? sequence,string pieceId,float validatedDamage)
        {
            string reason=Gate(context,sequence,true);if(reason.Length>0)return RuleResult.Reject(reason);
            PlacedPanel p;if(pieceId==null||!live.TryGetValue(pieceId,out p))return RuleResult.Reject("missing_piece");
            if(float.IsNaN(validatedDamage)||float.IsInfinity(validatedDamage)||validatedDamage<=0)return RuleResult.Reject("damage");
            float hp=Math.Max(0,p.Health-validatedDamage);
            if(hp>0)live[pieceId]=new PlacedPanel(p.Id,p.Owner,p.Shape,hp);
            else { live.Remove(pieceId);pending.Remove(pieceId);Refresh(p.Shape); }
            return RuleResult.Accept();
        }
        // The owner requested editing: deliberately replace the old parity-only refusal.
        internal PlacedPanel DismantleTarget(string actor,BuilderState pose)
        {
            if(pose==null||!pose.Expanded||!pose.Eligible||!pose.Tracked||!pose.Head.IsFinite||!pose.Origin.IsFinite||!pose.Direction.IsFinite)return null;
            float length=CandidateGeometry.Length(pose.Direction);if(length<.001f||Single.IsInfinity(length))return null;
            var direction=CandidateGeometry.Mul(pose.Direction,1/length);PlacedPanel nearest=null;float distance=6;
            foreach(var p in live.Values){float d=Geometry.Ray(pose.Origin,direction,p.Shape,6,0);if(d<distance||d==distance&&(nearest==null||String.CompareOrdinal(p.Id,nearest.Id)<0)){distance=d;nearest=p;}}
            if(nearest==null||nearest.Owner!=actor||CandidateGeometry.Length(CandidateGeometry.Sub(pose.Head,pose.Origin))>3)return null;
            var hit=CandidateGeometry.Add(pose.Origin,CandidateGeometry.Mul(direction,Math.Max(0,distance-.05f)));
            return world.HasLineOfSight(actor,pose.Head,pose.Origin)&&world.HasLineOfSight(actor,pose.Origin,hit)?nearest:null;
        }
        public RuleResult Dismantle(ConstructionCommand command,string pieceId)
        {
            string reason=Gate(command.Context,command.Sequence);if(reason.Length>0)return RuleResult.Reject(reason);
            BuilderState pose;if(!actors.TryRead(command.Context.ActorId,out pose))return RuleResult.Reject("dismantle_pose");
            var target=DismantleTarget(command.Context.ActorId,pose);
            if(target==null||target.Id!=pieceId)return RuleResult.Reject("dismantle_target");
            live.Remove(target.Id);pending.Remove(target.Id);Refresh(target.Shape);return RuleResult.Accept();
        }
        private void Refresh(Panel severed)
        {
            HashSet<string> supported=SupportedLive();var distance=new Dictionary<string,int>();
            var frontier=new Queue<PlacedPanel>();
            if(severed!=null)foreach(PlacedPanel p in live.Values)if(!supported.Contains(p.Id)&&CandidateGeometry.Connected(severed,p.Shape)){distance[p.Id]=1;frontier.Enqueue(p);}
            foreach(PlacedPanel p in live.Values)if(staticRoots.Contains(p.Id)&&!supported.Contains(p.Id)&&!distance.ContainsKey(p.Id)){distance[p.Id]=1;frontier.Enqueue(p);}
            staticRoots=new HashSet<string>(StringComparer.Ordinal);
            foreach(PlacedPanel p in live.Values)if(StaticContact(p.Shape))staticRoots.Add(p.Id);
            while(frontier.Count>0)
            {
                PlacedPanel current=frontier.Dequeue();
                foreach(PlacedPanel p in live.Values)if(!supported.Contains(p.Id)&&!distance.ContainsKey(p.Id)&&CandidateGeometry.Connected(p.Shape,current.Shape))
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
