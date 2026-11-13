using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Bots
{
    // Direct guard_routes.gd port. All points are map-local with one Z reflection.
    // The supplied ray is the only physics seam; no player/perception/attack owner.
    public sealed class R24GuardRoutes
    {
        public struct Hit {public readonly bool Found;public readonly Float3 Point,Normal;public Hit(Float3 point,Float3 normal){Found=true;Point=point;Normal=normal;}}
        public delegate Hit WorldRay(Float3 from,Float3 to);
        // enemy.gd::_prepare_route one-attempt-per-remembered-goal policy.
        public sealed class Intent
        {
            bool attempted;Float3 goal;
            public Float3 LastRequestedGoal {get{return goal;}}
            public bool TryBegin(int variant,bool hasLastKnown,bool hasSight,float lostTime,Float3 position,Float3 remembered)
            {
                if(variant==1||variant==3||!hasLastKnown)return false;Valid(position);Valid(remembered);
                if(attempted&&Length(Sub(goal,remembered))<=2)return false;
                if(Math.Abs(remembered.Y-position.Y)<1.25f&&(hasSight||lostTime<.4f||Length(Sub(position,remembered))<2.5f))return false;
                goal=remembered;attempted=true;return true;
            }
        }
        public sealed class Traversal
        {
            internal Float3[] points=new Float3[0];internal int index;internal float elapsed,budget,stalled,bestDistance=float.PositiveInfinity;
            public Float3 Destination {get;internal set;}
            public bool Finished {get;internal set;}
            public bool Aborted {get;internal set;}
            public bool Pending {get;internal set;}
            public string AbortReason {get;internal set;}
            public int PointCount {get{return points.Length;}}
            public float Budget {get{return budget;}}
            public Float3 PointAt(int at){return points[at];}
            // Unity motor adapter: after a rejected ledge probe, steer inward
            // to the existing accepted segment. Never advances a waypoint,
            // changes a goal or supplies support; the world bridge must validate
            // the correction's wall and floor before using this direction.
            public Float3 ReturnToSegment(Float3 position)
            {
                Valid(position);if(Finished||Aborted||Pending||index<=0||index>=points.Length)return new Float3();
                Float3 a=points[index-1],segment=Flat(Sub(points[index],a)),offset=Flat(Sub(position,a));float length=Squared(segment);
                if(length<=.000001f)return new Float3();float t=Math.Max(0,Math.Min(1,(offset.X*segment.X+offset.Z*segment.Z)/length));
                var inward=Flat(Sub(Add(a,Scale(segment,t)),position));return Length(inward)<.23f?new Float3():Unit(inward);
            }
            public Float3 Direction(Float3 position,float dt)
            {
                Valid(position);Delta(dt);if(Finished||Aborted||Pending)return new Float3();elapsed+=dt;
                if(elapsed>budget){Aborted=true;AbortReason="time_budget";return new Float3();}
                while(index<points.Length)
                {
                    Float3 flat=Flat(Sub(points[index],position));if(Length(flat)<.23f&&Math.Abs(points[index].Y-position.Y)<.55f){index++;bestDistance=float.PositiveInfinity;stalled=0;continue;}
                    float distance=Length(Sub(position,points[index]));if(distance<bestDistance-.05f){bestDistance=distance;stalled=0;}else stalled+=dt;
                    if(stalled>3){Aborted=true;AbortReason="blocked";return new Float3();}return Unit(flat);
                }
                Finished=true;return new Float3();
            }
        }
        sealed class Request {internal WeakReference Owner;internal Float3 From,Destination;internal float Speed,Difficulty;internal Traversal Traversal;}
        readonly WorldRay ray;readonly List<Float3> anchors=new List<Float3>();readonly List<List<int>> links=new List<List<int>>();readonly Dictionary<long,bool> staticEdges=new Dictionary<long,bool>();readonly List<Request> pending=new List<Request>();
        public int CompletedQueries {get;private set;}
        public int PendingCount {get{return pending.Count;}}
        public int AnchorCount {get{return anchors.Count;}}
        public R24GuardRoutes(WorldRay worldRay)
        {
            if(worldRay==null)throw new ArgumentNullException("worldRay");ray=worldRay;
            foreach(var p in new[]{new Float3(-29,0,27.5f),new Float3(-29,4,41.5f),new Float3(-29,4,43.7f),new Float3(-25.65f,4,43.7f),new Float3(-25.65f,8,31.5f),new Float3(-20.7f,8,30.4f),new Float3(-20.7f,8,38),new Float3(-18,8,38),new Float3(-18,8,43),new Float3(-11.4f,8,43),new Float3(-11.4f,8,47),new Float3(6,8,47),new Float3(6,12,60.5f),new Float3(6,12,62.7f),new Float3(2.5f,12,62.7f),new Float3(2.5f,16,50),new Float3(8,16,50),new Float3(8,16,62),new Float3(15,16,69),new Float3(20.5f,16,71),new Float3(20.5f,16,76),new Float3(9.5f,16,71),new Float3(9.5f,16,76)})AddPoint(p);
            for(int i=0;i<18;i++)Connect(i,i+1);Connect(18,19);Connect(19,20);Connect(18,21);Connect(21,22);
        }
        public Float3 AnchorAt(int index){return anchors[index];}
        public void InvalidateStaticCache(){staticEdges.Clear();}
        void AddPoint(Float3 p){anchors.Add(p);links.Add(new List<int>());}
        void Connect(int a,int b){if(!links[a].Contains(b))links[a].Add(b);if(!links[b].Contains(a))links[b].Add(a);}
        public bool RegisterCorridor(Float3[] points)
        {
            if(points==null||points.Length<2||points.Length>64)return false;var proposed=new List<Float3>(anchors);var ids=new List<int>();bool hasLink=false;
            for(int i=0;i<points.Length;i++)
            {
                Float3 point=points[i];if(!point.IsFinite||Length(point)>4096||i>0&&Length(Sub(point,points[i-1]))>40)return false;int id=-1;
                for(int candidate=0;candidate<proposed.Count;candidate++)if(Squared(Sub(proposed[candidate],point))<=.01f){id=candidate;break;}
                if(id<0){if(proposed.Count>=256)return false;id=proposed.Count;proposed.Add(point);}if(ids.Count>0&&ids[ids.Count-1]!=id)hasLink=true;ids.Add(id);
            }
            if(!hasLink)return false;for(int i=anchors.Count;i<proposed.Count;i++)AddPoint(proposed[i]);for(int i=0;i<ids.Count-1;i++)if(ids[i]!=ids[i+1])Connect(ids[i],ids[i+1]);InvalidateStaticCache();return true;
        }
        public Traversal RequestRoute(object owner,Float3 from,Float3 destination,float speed,float difficulty)
        {
            if(owner==null)throw new ArgumentNullException("owner");Inputs(from,destination,speed,difficulty);var traversal=new Traversal{Destination=destination,Pending=true};
            for(int i=pending.Count-1;i>=0;i--)if(ReferenceEquals(pending[i].Owner.Target,owner)){pending[i].Traversal.Aborted=true;pending[i].Traversal.AbortReason="superseded";pending.RemoveAt(i);}
            if(pending.Count>=32){traversal.Pending=false;traversal.Aborted=true;traversal.AbortReason="queue_full";return traversal;}
            pending.Add(new Request{Owner=new WeakReference(owner),From=from,Destination=destination,Speed=speed,Difficulty=difficulty,Traversal=traversal});return traversal;
        }
        public bool ProcessOne(Func<object,bool> available=null)
        {
            while(pending.Count>0)
            {
                var request=pending[0];pending.RemoveAt(0);object owner=request.Owner.Target;if(owner==null||available!=null&&!available(owner)||request.Traversal.Aborted)continue;
                var computed=BeginRoute(request.From,request.Destination,request.Speed,request.Difficulty);var result=request.Traversal;result.points=computed.points;result.budget=computed.budget;result.Aborted=computed.Aborted;result.AbortReason=computed.AbortReason;result.Pending=false;CompletedQueries++;return true;
            }
            return false;
        }
        public void CancelPending()
        {foreach(var request in pending){request.Traversal.Aborted=true;request.Traversal.Pending=false;request.Traversal.AbortReason="owner_unavailable";}pending.Clear();}
        public bool WalkableSegment(Float3 from,Float3 to)
        {
            Valid(from);Valid(to);int samples=Math.Max(2,(int)Math.Ceiling(Length(Sub(from,to))/.45f));Float3 flat=Unit(Flat(Sub(to,from))),side=new Float3(flat.Z*.58f,0,-flat.X*.58f);var offsets=from.Y>15&&to.Y>15?new[]{new Float3(),side,Scale(side,-1)}:new[]{new Float3()};
            for(int sample=0;sample<=samples;sample++)
            {
                var point=Lerp(from,to,sample/(float)samples);var floor=ray(Add(point,new Float3(0,.65f,0)),Add(point,new Float3(0,-.7f,0)));if(!floor.Found||floor.Normal.Y<.25f||Math.Abs(floor.Point.Y-point.Y)>.6f)return false;
                if(sample>0){var previous=Lerp(from,to,(sample-1)/(float)samples);foreach(float height in new[]{.7f,1.4f})foreach(var offset in offsets)if(ray(Add(Add(previous,offset),new Float3(0,height,0)),Add(Add(point,offset),new Float3(0,height,0))).Found)return false;}
            }
            return true;
        }
        public Float3[] Route(Float3 from,Float3 rememberedDestination)
        {
            Valid(from);Valid(rememberedDestination);var support=ray(Add(rememberedDestination,new Float3(0,.25f,0)),Add(rememberedDestination,new Float3(0,-2.2f,0)));if(!support.Found||support.Normal.Y<.25f)return new Float3[0];rememberedDestination=support.Point;
            if(Math.Abs(from.Y-rememberedDestination.Y)<.7f&&WalkableSegment(from,rememberedDestination))return new[]{rememberedDestination};
            var starts=Attachments(from,true);var ends=Attachments(rememberedDestination,false);Float3[] result=new Float3[0];float best=float.PositiveInfinity;
            foreach(int start in starts)foreach(int end in ends)
            {
                var ids=Path(start,end);if(ids.Length==0)continue;float length=Length(Sub(from,anchors[ids[0]]))+Length(Sub(anchors[ids[ids.Length-1]],rememberedDestination));for(int i=0;i<ids.Length-1;i++)length+=Length(Sub(anchors[ids[i]],anchors[ids[i+1]]));if(length>=best)continue;bool valid=true;
                for(int i=0;i<ids.Length-1;i++){long key=((long)ids[i]<<32)|(uint)ids[i+1];bool usable;if(!staticEdges.TryGetValue(key,out usable)){usable=WalkableSegment(anchors[ids[i]],anchors[ids[i+1]]);staticEdges.Add(key,usable);}if(!usable){valid=false;break;}}
                if(valid){best=length;result=new Float3[ids.Length+1];for(int i=0;i<ids.Length;i++)result[i]=anchors[ids[i]];result[ids.Length]=rememberedDestination;}
            }
            return result;
        }
        List<int> Attachments(Float3 point,bool start)
        {
            var candidates=new List<int>();for(int i=0;i<anchors.Count;i++)if(Math.Abs(anchors[i].Y-point.Y)<.8f&&Length(Sub(anchors[i],point))<22)candidates.Add(i);candidates.Sort((a,b)=>{int order=Length(Sub(anchors[a],point)).CompareTo(Length(Sub(anchors[b],point)));return order==0?a.CompareTo(b):order;});var result=new List<int>();foreach(int i in candidates){if(start?WalkableSegment(point,anchors[i]):WalkableSegment(anchors[i],point))result.Add(i);if(result.Count>=3)break;}return result;
        }
        // Default AStar3D cost/estimate is Euclidean distance; no proximity edges.
        int[] Path(int start,int end)
        {
            var open=new List<int>{start};var cost=new float[anchors.Count];var parent=new int[anchors.Count];var closed=new bool[anchors.Count];for(int i=0;i<cost.Length;i++){cost[i]=float.PositiveInfinity;parent[i]=-1;}cost[start]=0;
            while(open.Count>0)
            {
                int selected=0;float best=float.PositiveInfinity;for(int i=0;i<open.Count;i++){float score=cost[open[i]]+Length(Sub(anchors[open[i]],anchors[end]));if(score<best){selected=i;best=score;}}int at=open[selected];open.RemoveAt(selected);if(at==end){var path=new List<int>();for(int p=end;p>=0;p=parent[p])path.Add(p);path.Reverse();return path.ToArray();}closed[at]=true;
                foreach(int next in links[at]){if(closed[next])continue;float candidate=cost[at]+Length(Sub(anchors[at],anchors[next]));if(candidate>=cost[next])continue;cost[next]=candidate;parent[next]=at;if(!open.Contains(next))open.Add(next);}
            }
            return new int[0];
        }
        public Traversal BeginRoute(Float3 from,Float3 destination,float speed,float difficulty)
        {Inputs(from,destination,speed,difficulty);var points=Route(from,destination);float length=0;var previous=from;foreach(var point in points){length+=Length(Sub(previous,point));previous=point;}var traversal=new Traversal{points=points,Destination=destination,budget=Math.Min(length/Math.Max(speed,.5f)+5,28+27*Math.Max(0,Math.Min(1,difficulty)))};if(points.Length==0){traversal.Aborted=true;traversal.AbortReason="no_supported_route";}return traversal;}
        static void Inputs(Float3 from,Float3 to,float speed,float difficulty){Valid(from);Valid(to);if(float.IsNaN(speed)||float.IsInfinity(speed)||float.IsNaN(difficulty)||float.IsInfinity(difficulty))throw new ArgumentException("Route policy inputs");}
        static void Valid(Float3 p){if(!p.IsFinite)throw new ArgumentException("Route point");}
        static void Delta(float dt){if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");}
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}static Float3 Scale(Float3 a,float scale){return new Float3(a.X*scale,a.Y*scale,a.Z*scale);}static Float3 Flat(Float3 a){return new Float3(a.X,0,a.Z);}static float Squared(Float3 a){return a.X*a.X+a.Y*a.Y+a.Z*a.Z;}static float Length(Float3 a){return(float)Math.Sqrt(Squared(a));}static Float3 Unit(Float3 a){float length=Length(a);return length>0?Scale(a,1/length):new Float3();}static Float3 Lerp(Float3 a,Float3 b,float t){return Add(a,Scale(Sub(b,a),t));}
    }
}
