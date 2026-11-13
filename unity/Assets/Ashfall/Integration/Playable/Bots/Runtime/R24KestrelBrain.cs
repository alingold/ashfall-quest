using System;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Bots
{
    public enum R24KestrelState { Patrol, Pursue, Windup, Recover, Dead }

    // Live KESTREL policy, transcribed from frozen R6 enemy.gd. Coordinates
    // are already Unity-local (one Z reflection at the source encounter boundary).
    // Physics, illumination, real visibility and damage belong to the squad bridge.
    public sealed class R24KestrelBrain
    {
        readonly Random random;
        public readonly int Variant,Slot;
        public readonly Float3 Home;
        public readonly float Phase,GuardYaw;public float Difficulty {get;private set;}
        public R24KestrelState State {get;private set;}
        public float Health {get;private set;}
        public float MaximumHealth {get{return Variant==3?350:Variant==1?60:Variant==2?150:85;}}
        public float SightRange {get{return Variant==3?(wardenAlert>0?200:120):Variant==1?80:Variant==2?64:72;}}
        public float EngageRange {get{return Variant==3?100:Variant==1?58:Variant==2?48:54;}}
        public float Age {get;private set;}
        public float StateTime {get;private set;}
        public float Detection {get;private set;}
        public float LostTime {get;private set;}
        public float AlertMemory {get;private set;}
        public float Stagger {get;private set;}
        public bool HasSight {get;private set;}
        public bool HasLastKnown {get;private set;}
        public Float3 LastFeet {get;private set;}
        public Float3 LastAim {get;private set;}
        public Float3 LockedAim {get;private set;}
        public bool ShotRequested {get;private set;}
        public float Recoil {get;private set;}
        public float RadioCooldown {get;private set;}
        public int BossPhase {get;private set;}
        public bool AreaAttack {get;private set;}
        public bool NeedsGroundMark {get;private set;}
        public Float3 ImpactPoint {get;private set;}
        public bool CoreOpen {get{return Variant==3&&State!=R24KestrelState.Dead&&(State==R24KestrelState.Recover||State==R24KestrelState.Windup&&StateTime>=(AreaAttack?1.55f:1.05f));}}
        public float LastDamageMultiplier {get;private set;}
        public float AttackWindup {get{return Variant==3?(AreaAttack?1.85f:1.3f):WindupSeconds;}}
        public float Pressure {get{return Difficulty<=.5f?Lerp(.65f,1,Difficulty*2):Lerp(1,2.35f,(Difficulty-.5f)*2);}}
        public float WindupSeconds {get{return Difficulty<=.5f?Lerp(1.34f,1.04f,Difficulty*2):Lerp(1.04f,.56f,(Difficulty-.5f)*2);}}
        public float FixedDodgeSeconds {get{return Lerp(.42f,.28f,Math.Max(0,(Difficulty-.5f)*2));}}
        public float MoveSpeed {get{return Variant==3?(BossPhase==2?3.2f:2):(Variant==2?1.4f:2.2f)*(Difficulty<=.5f?Lerp(.85f,1,Difficulty*2):Lerp(1,1.45f,(Difficulty-.5f)*2))*(Stagger>0?.3f:1);}}
        public float DamageScale {get{return Difficulty>.66f?3:1;}}
        float cooldown,sensorTimer,sensorElapsed,cueTime,motionTime,wardenAlert;bool nextArea;
        Float3 cuePosition,lastSensed,motionPosition,motionVelocity,aimError;
        bool hasSensed,motionValid;

        public R24KestrelBrain(int variant,int slot,Float3 home,Float3 approach,float difficulty,int seed)
        {
            if(variant<0||variant>3||slot<0||!home.IsFinite||!approach.IsFinite||!Finite(difficulty))throw new ArgumentException("KESTREL encounter inputs");
            BossPhase=1;LastDamageMultiplier=1;Variant=variant;Slot=slot;Home=home;Difficulty=Clamp(difficulty,0,1);random=new Random(seed);
            Phase=Next()*(float)(Math.PI*2);cooldown=1.5f+Next()*1.5f;Health=MaximumHealth;
            var d=Sub(approach,home);GuardYaw=(float)Math.Atan2(d.X,d.Z);
        }

        public void SetDifficulty(float value){Difficulty=Finite(value)?Clamp(value,0,1):.5f;}

        public bool SensorDue(float dt,out float elapsed)
        {
            ValidateDelta(dt);sensorTimer-=dt;sensorElapsed+=dt;elapsed=0;
            if(State==R24KestrelState.Dead||sensorTimer>0)return false;
            elapsed=sensorElapsed;sensorElapsed=0;sensorTimer=Lerp(.14f,.07f,Math.Max(0,(Difficulty-.5f)*2))+Next()*.04f;return true;
        }
        public bool InFieldOfView(Float3 position,Float3 forward,Float3 aim)
        {
            var relative=Sub(aim,Add(position,new Float3(0,1.4f,0)));float distance=Length(relative);
            if(distance<=4||cueTime>0&&Length(Sub(aim,cuePosition))<=3)return true;
            var horizontal=Flat(relative);var f=Unit(Flat(forward));
            if(Length(horizontal)>.01f&&Dot(f,Unit(horizontal))<Math.Cos((Variant==3?80:Variant==1?75:60)*Math.PI/180))return false;
            return Math.Abs(Math.Atan2(relative.Y,Math.Max(Length(horizontal),.001f)))<=(Variant==1||Variant==3?60:50)*Math.PI/180;
        }
        public void Observe(float elapsed,Float3 position,Float3 feet,Float3 aim,float actualSpeed,float exposure,bool clear)
        {
            if(State==R24KestrelState.Dead)return;
            if(!Finite(elapsed)||elapsed<=0||!position.IsFinite||!feet.IsFinite||!aim.IsFinite||!Finite(actualSpeed)||!Finite(exposure))throw new ArgumentException("KESTREL actual observation");
            if(clear){
                if(motionValid){var observed=Limit(Scale(Flat(Sub(aim,motionPosition)),1/elapsed),6);motionVelocity=Lerp(motionVelocity,observed,1-(float)Math.Exp(-elapsed/.12f));}
                else motionVelocity=new Float3();
                motionPosition=aim;motionTime=Age;motionValid=true;
            }else{motionValid=false;motionVelocity=new Float3();}
            float speed=Math.Max(0,actualSpeed);if(hasSensed)speed=Math.Max(speed,Length(Sub(aim,lastSensed))/Math.Max(elapsed,.01f));lastSensed=aim;hasSensed=true;
            if(clear){
                if(AlertMemory>0)Detection=1;
                else{
                    float distance=Length(Sub(aim,Add(position,new Float3(0,1.4f,0))));
                    float motion=Clamp((speed-.15f)/4.35f,0,1),proximity=Clamp((SightRange-distance)/Math.Max(SightRange-3,1),.25f,1);
                    float rate=1.8f*Contrast(exposure)*proximity*(1+.9f*motion)*Pressure;if(distance<=4)rate=Math.Max(rate,2.5f);
                    Detection=Math.Min(1,Detection+rate*elapsed);
                }
                HasSight=Detection>=1;if(HasSight){AlertMemory=6;LastFeet=feet;LastAim=aim;HasLastKnown=true;}
            }else{HasSight=false;Detection=Math.Max(0,Detection-elapsed*.35f);AlertMemory=Math.Max(0,AlertMemory-elapsed);}
        }
        public Float3 Tick(float dt,Float3 position){return Tick(dt,position,null,null,null);}
        // Source _prepare_route/_route_active/_follow_route hooks. The brain
        // still owns perception, attack priority and search/Patrol transitions.
        public Float3 Tick(float dt,Float3 position,Action prepareRoute,Func<bool> routeActive,Func<float,Float3> followRoute)
        {
            ValidateDelta(dt);if(!position.IsFinite)throw new ArgumentException("position");ShotRequested=false;
            if(State==R24KestrelState.Dead)return new Float3();
            Age+=dt;StateTime+=dt;cooldown-=dt;wardenAlert=Math.Max(0,wardenAlert-dt);Stagger=Math.Max(0,Stagger-dt);cueTime=Math.Max(0,cueTime-dt);RadioCooldown=Math.Max(0,RadioCooldown-dt);Recoil=Math.Max(0,Recoil-dt*3);
            LostTime=HasSight?0:LostTime+dt;Float3 move=new Float3();
            if(State==R24KestrelState.Patrol){if(HasSight)Change(R24KestrelState.Pursue);}
            else if(State==R24KestrelState.Pursue){
                float distance=HasLastKnown?Length(Sub(LastAim,Add(position,new Float3(0,1.4f,0)))):float.PositiveInfinity;
                if(prepareRoute!=null)prepareRoute();bool routed=routeActive!=null&&routeActive();
                if(LostTime>Lerp(5,11,Difficulty)&&!routed)Change(R24KestrelState.Patrol);
                else if(HasSight&&cooldown<=0&&distance<EngageRange&&(Stagger<=0||Variant==3)){
                    LockedAim=LastAim;float angle=Next()*(float)(Math.PI*2),r=Math.Max(0,.5f-Difficulty)*1.6f*(float)Math.Sqrt(Next());
                    aimError=new Float3((float)Math.Cos(angle)*r,(float)Math.Sin(angle)*r*.4f,0);LockedAim=Add(LockedAim,aimError);Change(R24KestrelState.Windup);if(Variant==3){AreaAttack=nextArea;nextArea=!nextArea;NeedsGroundMark=AreaAttack;}
                }else if(HasLastKnown){
                    var direction=Unit(Flat(Sub(LastAim,position)));
                    if(routed&&followRoute!=null)move=followRoute(dt);
                    else if(!HasSight){var goal=LastFeet;if(Difficulty>.55f&&Length(Flat(Sub(goal,position)))<6){float b=GuardYaw+SearchSide*(.8f+LostTime*.45f);goal=Add(goal,new Float3((float)Math.Sin(b)*3,0,(float)Math.Cos(b)*3));}move=Unit(Flat(Sub(goal,position)));}
                    else if(distance>14)move=direction;
                    else if(distance<5)move=Scale(direction,-1);
                    else move=Scale(Right(direction),(float)Math.Sin(Age*.7f+Phase));
                    if(Variant==1&&HasSight&&Difficulty>.55f&&distance>=5&&distance<=20)move=Unit(Add(Scale(direction,.25f),Scale(Right(direction),SearchSide)));
                    if(Variant==3&&HasSight&&distance>=5&&distance<=20)move=Scale(Right(direction),(float)Math.Sin(Age*(BossPhase==2?1.35f:.6f)+Phase));
                }
            }else if(State==R24KestrelState.Windup){
                if(!AreaAttack&&StateTime<(Variant==3?.62f:AttackWindup-FixedDodgeSeconds)&&HasSight){float horizon=Clamp(AttackWindup-StateTime+Math.Max(0,Age-motionTime),0,.65f);LockedAim=Add(Add(LastAim,Variant!=3&&motionValid?Limit(Scale(motionVelocity,horizon*Lerp(.82f,.94f,Difficulty)),2):new Float3()),aimError);}
                if(StateTime>AttackWindup)ShotRequested=true;
            }else if(State==R24KestrelState.Recover&&StateTime>(Variant==3?1.45f:.45f))Change(R24KestrelState.Pursue);
            if((Variant==0||Variant==2)&&State==R24KestrelState.Pursue&&HasLastKnown&&HasSight&&(routeActive==null||!routeActive())){var to=Flat(Sub(TacticalDestination(),position));move=Length(to)>.65f?Unit(to):new Float3();}
            return move;
        }
        public void ResolveRouteTraversal(bool finished,bool aborted)
        {if(finished&&aborted)throw new ArgumentException("Traversal terminal state");if(State!=R24KestrelState.Pursue)return;if(finished)LostTime=0;else if(aborted)Change(R24KestrelState.Patrol);}
        public Float3 TacticalDestination()
        {
            var center=HasLastKnown?LastFeet:Home;var approach=Flat(Sub(Home,center));if(Length(approach)<.1f)approach=new Float3(0,0,-1);approach=Unit(approach);
            return Add(Add(center,Scale(approach,7+(Slot/8)*2)),Scale(Right(approach),(Slot%2==0?1:-1)*(4+((Slot/2)%4)*2)));
        }
        public Float3 LookPoint(Float3 position)
        {
            if(State==R24KestrelState.Windup)return AreaAttack?ImpactPoint:LockedAim;
            if(State==R24KestrelState.Pursue&&HasLastKnown)return LastAim;
            float yaw=GuardYaw+(float)Math.Sin(Age*(Math.PI*2/12)+Phase)*(float)(Math.PI/4);
            return Add(position,new Float3((float)Math.Sin(yaw)*10,1.4f,(float)Math.Cos(yaw)*10));
        }
        public void CompleteShot(bool inRange)
        {if(!ShotRequested||State!=R24KestrelState.Windup)return;ShotRequested=false;if(inRange){Recoil=.4f;cooldown=(Variant==3?(BossPhase==2?2.9f:3.6f):((Variant==2?3.1f:2.2f)+Next()*.7f))/Pressure;}else cooldown=.5f;Change(R24KestrelState.Recover);}
        // Bridge supplies the actual world ray result before the marked windup
        // advances. An unsupported airborne mark falls back to direct fire.
        public void ResolveGroundMark(bool supported,Float3 point)
        {if(!NeedsGroundMark)return;if(supported&&!point.IsFinite)throw new ArgumentException("Actual Warden ground mark");NeedsGroundMark=false;if(supported)ImpactPoint=point;else AreaAttack=false;}
        public bool TakeDamage(float amount,Float3 attackerFeet,Float3 attackerHead)
        {
            if(State==R24KestrelState.Dead||!Finite(amount)||amount<=0||!attackerFeet.IsFinite||!attackerHead.IsFinite)return false;
            LastDamageMultiplier=Variant==3&&!CoreOpen?.25f:1;Health=Math.Max(0,Health-amount*LastDamageMultiplier);LastFeet=attackerFeet;LastAim=attackerHead;HasLastKnown=true;Detection=1;AlertMemory=6;cueTime=1;cuePosition=attackerHead;LostTime=0;Stagger=.22f;
            if(Variant==3){wardenAlert=12;if(State==R24KestrelState.Patrol||State==R24KestrelState.Pursue)cooldown=Math.Min(cooldown,.45f);if(Health<=MaximumHealth*.5f)BossPhase=2;Recoil=.15f;}
            if(Health<=0)Change(R24KestrelState.Dead);else if(State==R24KestrelState.Patrol)Change(R24KestrelState.Pursue);return true;
        }
        public void HearShot(Float3 origin,Float3 position,float loudness)
        {if(State==R24KestrelState.Dead||!origin.IsFinite||!Finite(loudness)||loudness<=0||Length(Sub(origin,position))>Clamp(80*loudness,0,120))return;LastFeet=LastAim=cuePosition=origin;HasLastKnown=true;cueTime=1;LostTime=0;AlertMemory=6;Detection=Math.Max(Detection,.55f);if(State==R24KestrelState.Patrol)Change(R24KestrelState.Pursue);}
        public void ReceiveRadio(Float3 feet,Float3 aim)
        {if(State==R24KestrelState.Dead||HasSight)return;LastFeet=feet;LastAim=aim;HasLastKnown=true;LostTime=0;if(State==R24KestrelState.Patrol)Change(R24KestrelState.Pursue);}
        public bool TryRadio(){if(Difficulty<.65f||!HasSight||RadioCooldown>0)return false;RadioCooldown=2;return true;}
        float SearchSide {get{return Math.Sin(Phase)<0?-1:1;}}
        void Change(R24KestrelState state){State=state;StateTime=0;}
        float Next(){return (float)random.NextDouble();}
        static void ValidateDelta(float dt){if(!Finite(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");}
        public static bool Finite(float x){return !float.IsNaN(x)&&!float.IsInfinity(x);}
        public static float Contrast(float exposure){float x=Clamp(exposure,0,1),sun=.18f+.82f*.84f;return x>=.84f?Lerp(sun,1,(x-.84f)/.16f):sun*(.06f+.94f*(float)Math.Pow(x/.84f,1.35));}
        public static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        public static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        public static Float3 Scale(Float3 a,float s){return new Float3(a.X*s,a.Y*s,a.Z*s);}
        public static Float3 Flat(Float3 a){return new Float3(a.X,0,a.Z);}
        public static Float3 Right(Float3 a){return new Float3(a.Z,0,-a.X);}
        public static float Length(Float3 a){return (float)Math.Sqrt(Dot(a,a));}
        public static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        public static Float3 Unit(Float3 a){float n=Length(a);return n>.000001f?Scale(a,1/n):new Float3();}
        public static Float3 Limit(Float3 a,float max){float n=Length(a);return n>max?Scale(a,max/n):a;}
        public static Float3 Lerp(Float3 a,Float3 b,float t){return Add(a,Scale(Sub(b,a),t));}
        public static float Clamp(float x,float lo,float hi){return Math.Max(lo,Math.Min(hi,x));}
        static float Lerp(float a,float b,float t){return a+(b-a)*t;}
    }
}
