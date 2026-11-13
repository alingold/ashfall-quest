using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Flight;

namespace Ashfall.Integration.Playable.TeamLaunch
{
    public struct R24LaunchMap
    {
        // World-space Unity X/Z bounds, already reflected once by the map owner.
        public string Id;public Float3 Minimum,Maximum;public float OriginY,Top;
        public bool BuiltIn{get{return Id!=null&&!Id.StartsWith("archive:",StringComparison.Ordinal);}}
        public float Span{get{return Math.Min(Maximum.X-Minimum.X,Maximum.Z-Minimum.Z);}}
    }
    public struct R24LaunchRules{public bool Live;public string Mode,Stage;public int Teams;public long Epoch;}
    public struct R24LaunchPlayer{public string Id;public int Team;public bool Alive,OnDeck;public Float3 Position;}
    public struct R24LaunchFrame
    {
        public Float3 Origin,Right,Forward;
        public Float3 Point(Float3 local){return R24LaunchMath.Add(Origin,R24LaunchMath.Add(R24LaunchMath.Scale(Right,local.X),new Float3(Forward.X*local.Z,local.Y,Forward.Z*local.Z)));}
        public Float3 Local(Float3 world){var d=R24LaunchMath.Sub(world,Origin);return new Float3(R24LaunchMath.Dot(d,Right),d.Y,R24LaunchMath.Dot(d,Forward));}
    }
    public sealed class R24LaunchAssignment
    {
        public readonly string PlayerId,PodId;public readonly int Team,Slot;public readonly Float3 Spawn;
        public R24LaunchAssignment(string player,string pod,int team,int slot,Float3 spawn){PlayerId=player;PodId=pod;Team=team;Slot=slot;Spawn=spawn;}
    }
    public sealed class R24LaunchLayout
    {
        public const int Slots=12;public readonly R24LaunchMap Map;public readonly R24LaunchRules Rules;readonly R24LaunchFrame[] stations;
        public int TeamCount{get{return stations.Length;}}public float Ceiling{get{return Map.Top+65+TeamCount*18+20;}}
        public static bool UsesSkyPlatform(string id){return !Ashfall.Integration.Playable.Match.R24ArchiveRoundPolicy.GroundTeams(id);}
        public static bool Eligible(R24LaunchMap map,R24LaunchRules rules){return rules.Live&&UsesSkyPlatform(map.Id)&&rules.Mode=="revive"&&rules.Teams>0&&rules.Teams<=8&&(rules.Stage=="lobby"||rules.Stage=="countdown"||rules.Stage=="match");}
        public R24LaunchLayout(R24LaunchMap map,R24LaunchRules rules)
        {
            if(!Eligible(map,rules)||String.IsNullOrEmpty(map.Id)||!map.Minimum.IsFinite||!map.Maximum.IsFinite||map.Span<=0||!R24LaunchMath.Finite(map.Top)||!R24LaunchMath.Finite(map.OriginY))throw new ArgumentException("Actual eligible launch map/rules required");
            Map=map;Rules=rules;stations=new R24LaunchFrame[rules.Teams];
            float[,] corner={{0,1},{1,0},{1,1},{0,0},{.5f,1},{.5f,0},{0,.5f},{1,.5f}};
            var directions=new[]{new Float3(0,0,-1),new Float3(0,0,1),new Float3(-1,0,0),new Float3(1,0,0),new Float3(0,0,-1),new Float3(0,0,1),new Float3(1,0,0),new Float3(-1,0,0)};
            for(int team=0;team<stations.Length;team++)
            {
                var d=directions[team];float mx=Math.Min(Math.Abs(d.Z)>.5?25.5f:10,(map.Maximum.X-map.Minimum.X)*.5f),mz=Math.Min(Math.Abs(d.Z)>.5?10:25.5f,(map.Maximum.Z-map.Minimum.Z)*.5f);
                var at=new Float3(map.Minimum.X+mx+(map.Maximum.X-map.Minimum.X-2*mx)*corner[team,0],map.Top+65+(map.Span<100?team*18:0),map.Minimum.Z+mz+(map.Maximum.Z-map.Minimum.Z-2*mz)*corner[team,1]);
                stations[team]=new R24LaunchFrame{Origin=at,Forward=d,Right=new Float3(d.Z,0,-d.X)};
            }
        }
        public string PodId(int team,int slot){return Rules.Epoch+":"+team+":"+slot;}
        public R24LaunchFrame Station(int team){if(team<0||team>=TeamCount)throw new ArgumentOutOfRangeException("team");return stations[team];}
        public R24LaunchFrame Pod(int team,int slot){if(slot<0||slot>=Slots)throw new ArgumentOutOfRangeException("slot");var f=Station(team);f.Origin=f.Point(new Float3((slot-5.5f)*3.6f,.14f,6));return f;}
        public R24LaunchAssignment Assign(string player,int team,IEnumerable<R24LaunchAssignment> roster)
        {
            if(String.IsNullOrEmpty(player))throw new ArgumentException("actual player");team=Math.Max(0,Math.Min(TeamCount-1,team));var used=new bool[Slots];
            if(roster!=null)foreach(var other in roster)if(other!=null&&other.PlayerId!=player&&other.Team==team&&other.Slot>=0&&other.Slot<Slots)used[other.Slot]=true;
            int slot=0;while(used[slot]&&slot<Slots-1)slot++;
            return new R24LaunchAssignment(player,PodId(team,slot),team,slot,Station(team).Point(new Float3((slot-5.5f)*3.6f,.12f,-3)));
        }
        public bool TryCabin(R24LaunchPlayer player,out int slot,out R24LaunchFrame pod)
        {
            slot=-1;pod=new R24LaunchFrame();if(player.Team<0||player.Team>=TeamCount||!player.Position.IsFinite)return false;
            for(int i=0;i<Slots;i++){var f=Pod(player.Team,i);var p=f.Local(player.Position);if(Math.Abs(p.X)<.95f*R24TeamLaunchRuntime.PodScale&&Math.Abs(p.Z)<1.3f*R24TeamLaunchRuntime.PodScale&&Math.Abs(p.Y)<.7f){slot=i;pod=f;return true;}}return false;
        }
        public bool Staging(Float3 point){if(!point.IsFinite)return false;foreach(var f in stations){var p=f.Local(point);if(p.X>=-25&&p.X<25&&p.Y>=-8.5f&&p.Y<19.5f&&p.Z>=-10&&p.Z<10)return true;}return false;}
    }
    public struct R24LaunchFloor{public Float3 Point;public float NormalY;}
    public interface IR24LaunchWorld
    {
        bool RaycastDown(Float3 from,float endY,out R24LaunchFloor floor);
        bool HasSafeSpawn{get;}bool SafeSpawn(Float3 point);
    }
    public enum R24LaunchRideState{Flying,Released,Landed}
    public sealed class R24LaunchRide
    {
        public readonly string PlayerId;public readonly CapsulePlan Plan;public readonly R24LaunchFrame Frame;public readonly double Started;
        // Local presentation only; never serialized or used by flight authority.
        public bool HasControl;public Float3 Control,ControlForward;
        public R24LaunchRideState State{get;internal set;}public Float3 LastAccepted{get;internal set;}public bool HasAccepted{get;internal set;}
        internal Float3 TerminalTarget;internal bool TerminalAccepted;internal R24LaunchRide(string actor,CapsulePlan plan,R24LaunchFrame frame,double started){PlayerId=actor;Plan=plan;Frame=frame;Started=started;State=R24LaunchRideState.Flying;}
        public Float3 Position(double clock){return Plan.Position(clock-Started);}
    }
    public struct R24LaunchCarrier
    {
        public readonly string PlayerId;public readonly R24LaunchRideState State;public readonly Float3 Target,Displacement;public readonly bool Carried;
        internal R24LaunchCarrier(string player,R24LaunchRideState state,Float3 target,Float3 actual){PlayerId=player;State=state;Target=target;Displacement=R24LaunchMath.Sub(target,actual);Carried=state==R24LaunchRideState.Flying;}
    }
    public enum R24LaunchIntentKind{None,Launch,Eject}
    public struct R24LaunchIntent{public R24LaunchIntentKind Kind;public string PlayerId,PodId;}
    public struct R24LaunchHand{public bool Tracked;public Float3 Position,Forward;public float Trigger,Grip;}
    public struct R24LaunchInput{public bool Enabled,XR,DesktopE,MenuOpen,SessionOverlay,HeadTracked;public Float3 Head,HeadForward;public R24LaunchHand Left,Right;}
    public sealed class R24TeamLaunchRuntime
    {
        public const float PodScale=.8f;
        // Human reach correction: button and aim target move together.
        // The sign and cabin frame retain their existing positions.
        public static Float3 ControlLocalPoint {get{return new Float3(0,1.12f,.8f);}}
        // Central, symmetric reach rule: independent of root, room offset and eye height.
        public static Float3 RiderControl(Float3 head,Float3 forward)
        {
            float length=(float)Math.Sqrt(forward.X*forward.X+forward.Z*forward.Z);
            float x=length>.001f?forward.X/length:0,z=length>.001f?forward.Z/length:1;
            return new Float3(head.X+x*.40f,head.Y-.32f,head.Z+z*.40f);
        }
        readonly Dictionary<string,R24LaunchRide> rides=new Dictionary<string,R24LaunchRide>(StringComparer.Ordinal);readonly Random random;
        public R24LaunchLayout Layout{get;private set;}public R24LaunchRules Rules{get;private set;}public string Failure{get;private set;}
        bool controlHeld=true;string boardKey="";
        public R24TeamLaunchRuntime(int? seed=null){random=seed.HasValue?new Random(seed.Value):new Random();}
        public bool Configure(R24LaunchMap map,R24LaunchRules rules)
        {
            Rules=rules;if(!R24LaunchLayout.Eligible(map,rules)){Clear();return false;}
            if(Layout!=null&&Layout.Map.Id==map.Id&&Layout.Rules.Epoch==rules.Epoch&&Layout.TeamCount==rules.Teams&&Layout.Map.Minimum.Equals(map.Minimum)&&Layout.Map.Maximum.Equals(map.Maximum)&&Layout.Map.Top==map.Top&&Layout.Map.OriginY==map.OriginY)return true;
            Clear();Layout=new R24LaunchLayout(map,rules);return true;
        }
        public void Clear(){rides.Clear();Layout=null;controlHeld=true;boardKey="";Failure=null;}
        public R24LaunchRide[] ReadRides(){var copy=new R24LaunchRide[rides.Count];rides.Values.CopyTo(copy,0);return copy;}
        public R24LaunchRide ReadRide(string player){R24LaunchRide value;return player!=null&&rides.TryGetValue(player,out value)?value:null;}
        // Presentation adoption only. The LAN lobby retains movement authority;
        // this feeds the existing cabin controls, colliders, audio and visuals.
        public bool AdoptHostRide(string actor,CapsulePlan plan,double started,int phase,Float3 terminal){if(Layout==null||String.IsNullOrEmpty(actor)||plan==null||plan.Team<0||plan.Team>=Layout.TeamCount||plan.Slot<0||plan.Slot>=12||!R24LaunchMath.Finite(started)||started<0||phase<0||phase>2||!terminal.IsFinite)return false;R24LaunchRide ride;if(!rides.TryGetValue(actor,out ride)||ride.Plan.PodId!=plan.PodId||ride.Started!=started){ride=new R24LaunchRide(actor,plan,Layout.Pod(plan.Team,plan.Slot),started);rides[actor]=ride;}ride.State=(R24LaunchRideState)phase;ride.TerminalTarget=terminal;return true;}
        public bool Used(string pod){foreach(var ride in rides.Values)if(ride.Plan.PodId==pod)return true;return false;}
        public void RemovePlayer(string id){if(id!=null)rides.Remove(id);}
        bool Refuse(string why){Failure=why;return false;}
        public bool TryLaunch(R24LaunchPlayer player,string pod,double clock,IList<Float3> spawns,IList<Float3> loot,Float3 arenaFallback,IR24LaunchWorld world,out R24LaunchRide ride)
        {
            ride=null;Failure=null;if(Layout==null||Rules.Stage!="match"||!player.Alive||!player.OnDeck||String.IsNullOrEmpty(player.Id)||rides.ContainsKey(player.Id)||world==null||!R24LaunchMath.Finite(clock)||clock<0)return Refuse("source match/living/deck/copy/world required");
            int slot;R24LaunchFrame frame;if(!Layout.TryCabin(player,out slot,out frame)||pod!=Layout.PodId(player.Team,slot)||Math.Abs(frame.Local(player.Position).Y)>.65f||Used(pod))return Refuse("actual own-team cabin/key unused and within source bounds required");
            var candidates=new List<Float3>();if(spawns!=null)foreach(var p in spawns)if(p.IsFinite)candidates.Add(p);if(loot!=null)foreach(var p in loot)if(p.IsFinite)candidates.Add(R24LaunchMath.Add(p,new Float3(0,-.48f,0)));if(candidates.Count==0&&arenaFallback.IsFinite)candidates.Add(arenaFallback);
            for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var p=candidates[i];candidates[i]=candidates[j];candidates[j]=p;}
            var map=Layout.Map;float inset=Math.Min(4,map.Span*.1f),minX=map.Minimum.X+inset,maxX=map.Maximum.X-inset,minZ=map.Minimum.Z+inset,maxZ=map.Maximum.Z-inset;
            if(map.BuiltIn)for(int z=0;z<9;z++)for(int x=0;x<9;x++){var at=new Float3(minX+(maxX-minX)*(x+.5f)/9,map.Top+5,maxZ-(maxZ-minZ)*(z+.5f)/9);R24LaunchFloor floor;if(world.RaycastDown(at,map.OriginY-1000,out floor)&&floor.NormalY>.7f)candidates.Add(R24LaunchMath.Add(floor.Point,new Float3(0,.12f,0)));}
            Float3 target=new Float3();bool found=false;
            foreach(var at in candidates)
            {
                if(at.X<minX||at.X>=maxX||at.Z<=minZ||at.Z>maxZ)continue;
                var delta=R24LaunchMath.Sub(at,frame.Origin);if(R24LaunchMath.Dot(delta,frame.Forward)<Math.Min(15,map.Span*.15f)||world.HasSafeSpawn&&!world.SafeSpawn(at))continue;
                float horizontal=(float)Math.Sqrt(delta.X*delta.X+delta.Z*delta.Z);if(horizontal<8&&candidates.Count>1)continue;if(!found){target=at;found=true;}if(horizontal>=Math.Min(60,map.Span*.25f)){target=at;break;}
            }
            if(!found)return Refuse("no genuine candidate satisfies source inset/forward/safe-spawn floor policy");
            var start=R24LaunchMath.Add(frame.Origin,new Float3(0,.12f,0));var clear=R24LaunchMath.Add(start,R24LaunchMath.Add(R24LaunchMath.Scale(frame.Forward,10),new Float3(0,5,0)));var cruise=new Float3(target.X,clear.Y,target.Z);R24LaunchFloor landingFloor;
            if(!world.RaycastDown(R24LaunchMath.Add(cruise,new Float3(0,3,0)),map.OriginY-2000,out landingFloor)||landingFloor.NormalY<.7f)return Refuse("actual landing ray has no source floor");
            var landing=R24LaunchMath.Add(landingFloor.Point,new Float3(0,.12f,0));var exit=R24LaunchMath.Sub(landing,R24LaunchMath.Scale(frame.Forward,2));R24LaunchFloor exitFloor;
            if(world.RaycastDown(R24LaunchMath.Add(exit,new Float3(0,3,0)),exit.Y-5,out exitFloor)&&exitFloor.NormalY>.7f&&Math.Abs(exitFloor.Point.Y-landing.Y)<1)exit=R24LaunchMath.Add(exitFloor.Point,new Float3(0,.12f,0));else exit=R24LaunchMath.Add(landing,new Float3(0,.3f,0));
            double duration=Math.Max(1.5,Math.Min(120,(R24LaunchMath.Distance(start,clear)+R24LaunchMath.Distance(clear,cruise)+R24LaunchMath.Distance(cruise,landing))/35));
            var plan=new CapsulePlan(pod,player.Team,slot,start,clear,cruise,landing,exit,R24LaunchMath.Scale(frame.Forward,-1),duration);ride=new R24LaunchRide(player.Id,plan,frame,clock);rides.Add(player.Id,ride);return true;
        }
        public bool TryCarrier(R24LaunchPlayer player,double clock,bool eject,out R24LaunchCarrier carrier)
        {
            carrier=new R24LaunchCarrier();var ride=ReadRide(player.Id);if(ride==null||Rules.Stage!="match"||!R24LaunchMath.Finite(clock)||!player.Position.IsFinite)return false;
            Float3 at=ride.Position(clock);
            if(ride.State==R24LaunchRideState.Flying){if(eject||!player.Alive){ride.State=R24LaunchRideState.Released;ride.TerminalTarget=R24LaunchMath.Add(at,R24LaunchMath.Scale(ride.Plan.RearDirection,2));}else if(clock>=ride.Started+ride.Plan.Duration){ride.State=R24LaunchRideState.Landed;ride.TerminalTarget=ride.Plan.ExitTarget;}}
            if(ride.State!=R24LaunchRideState.Flying){if(ride.TerminalAccepted)return false;at=ride.TerminalTarget;}
            carrier=new R24LaunchCarrier(player.Id,ride.State,at,player.Position);return true;
        }
        public void AcceptCarrierMotion(string player,Float3 actualPosition)
        {var ride=ReadRide(player);if(ride==null||!actualPosition.IsFinite)return;if(ride.State!=R24LaunchRideState.Flying&&R24LaunchMath.Distance(actualPosition,ride.TerminalTarget)<.05f)ride.TerminalAccepted=true;ride.HasAccepted=true;ride.LastAccepted=actualPosition;}
        public bool AuthorizedFlight(Float3 point,double clock){foreach(var ride in rides.Values)if(ride.State==R24LaunchRideState.Flying&&R24LaunchMath.Distance(point,ride.Position(clock))<3)return true;return false;}
        public R24LaunchIntent SampleInput(R24LaunchPlayer player,double clock,R24LaunchInput input)
        {
            var none=new R24LaunchIntent();if(Layout==null||!player.Alive){controlHeld=true;return none;}
            var ride=ReadRide(player.Id);bool flying=ride!=null&&ride.State==R24LaunchRideState.Flying;int slot;R24LaunchFrame frame;bool inside=Layout.TryCabin(player,out slot,out frame);
            string board=inside&&!flying?Layout.PodId(player.Team,slot):"";if(board!=""&&board!=boardKey)controlHeld=true;boardKey=board;
            if(flying){frame=ride.Frame;frame.Origin=R24LaunchMath.Add(ride.Position(clock),new Float3(0,-.12f,0));}
            var control=frame.Point(ControlLocalPoint);
            if(flying&&input.HeadTracked&&input.Head.IsFinite&&input.HeadForward.IsFinite){ride.Control=RiderControl(input.Head,input.HeadForward);var d=R24LaunchMath.Sub(ride.Control,input.Head);ride.ControlForward=new Float3(d.X,0,d.Z);ride.HasControl=true;}
            if(flying&&ride.HasControl)control=ride.Control;
            if(!input.Enabled){controlHeld=true;return none;}
            bool pressed=input.XR?Aims(input.Left,control)||Aims(input.Right,control):input.DesktopE;
            bool fresh=pressed&&!controlHeld;controlHeld=pressed;if(!fresh||input.MenuOpen||input.SessionOverlay)return none;
            if(flying)return new R24LaunchIntent{Kind=R24LaunchIntentKind.Eject,PlayerId=player.Id,PodId=ride.Plan.PodId};
            return inside&&player.OnDeck?new R24LaunchIntent{Kind=R24LaunchIntentKind.Launch,PlayerId=player.Id,PodId=board}:none;
        }
        static bool Aims(R24LaunchHand hand,Float3 control){if(!hand.Tracked||!hand.Position.IsFinite||!hand.Forward.IsFinite||!(hand.Trigger>.65f||hand.Grip>.65f))return false;var ray=R24LaunchMath.Sub(control,hand.Position);float distance=R24LaunchMath.Length(ray);return distance<.45f||distance<2&&R24LaunchMath.Dot(hand.Forward,R24LaunchMath.Scale(ray,1/distance))>.97f;}
    }
    public static class R24LaunchMath
    {
        public static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}public static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}public static Float3 Scale(Float3 a,float n){return new Float3(a.X*n,a.Y*n,a.Z*n);}public static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}public static float Length(Float3 a){return(float)Math.Sqrt(Dot(a,a));}public static float Distance(Float3 a,Float3 b){return Length(Sub(a,b));}public static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
    }
}
