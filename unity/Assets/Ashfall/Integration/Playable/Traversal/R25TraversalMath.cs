using System;
using System.IO;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Traversal
{
    public static class R25TraversalVector
    {
        public static Float3 Add(Float3 a, Float3 b) { return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        public static Float3 Sub(Float3 a, Float3 b) { return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        public static Float3 Mul(Float3 a, float n) { return new Float3(a.X*n,a.Y*n,a.Z*n); }
        public static float Dot(Float3 a, Float3 b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
        public static float Length(Float3 a) { return (float)Math.Sqrt(Dot(a,a)); }
        public static Float3 Limit(Float3 a,float cap) { float n=Length(a);return !a.IsFinite?new Float3():n>cap?Mul(a,cap/n):a; }
        public static Float3 Unit(Float3 a) { float n=Length(a);return n>.00001f?Mul(a,1/n):new Float3(); }
    }
    public static class R25TraversalGestures
    {
        // Head-yaw local space: reserve hips/torso and the backpack behind the shoulders.
        public static bool ReservedZone(Float3 hand)
        {
            return !hand.IsFinite || Math.Abs(hand.X)<.45f && hand.Y<-.25f && hand.Y>-1.25f && hand.Z<.25f && hand.Z>-.45f
                || Math.Abs(hand.X)<.6f && hand.Y<.2f && hand.Y>-.6f && hand.Z<-.12f && hand.Z>-.65f;
        }
    }
    public sealed class R25JetPack
    {
        public float Fuel=1;
        public float Power { get; private set; }
        float airborne,rest,phase;
        public void Cancel() { Power=0;airborne=0;rest=0; }
        public Float3 Step(float dt,bool allowed,bool supported,bool climbing,bool held,bool glide,Float3 velocity,Float3 direction,float glideSpeed)
        {
            Power=0;
            if(float.IsNaN(dt)||dt<=0||dt>.1f||!velocity.IsFinite||!direction.IsFinite)return new Float3();
            phase+=dt;
            if(supported||climbing){airborne=0;rest+=dt;if(rest>.8f)Fuel=Math.Min(1,Fuel+.5f*Math.Min(dt,rest-.8f));}
            else {rest=0;airborne+=dt;}
            if(!allowed||supported||climbing||!held||airborne<.12f||Fuel<=0)return velocity;
            Power=Fuel<.08f&&((int)(phase*20)%2)==0?.4f:1;
            float consumed=Math.Min(Fuel,dt/3.2f);Power*=consumed/(dt/3.2f);Fuel=Math.Max(0,Fuel-consumed);
            if(glide){Float3 f=R25TraversalVector.Unit(new Float3(direction.X,0,direction.Z));return new Float3(f.X*glideSpeed*1.6f,-glideSpeed/7,f.Z*glideSpeed*1.6f);}
            Float3 h=new Float3(velocity.X,0,velocity.Z),target=R25TraversalVector.Mul(R25TraversalVector.Limit(new Float3(direction.X,0,direction.Z),1),13);
            h=R25TraversalVector.Add(h,R25TraversalVector.Limit(R25TraversalVector.Sub(target,h),(R25TraversalVector.Length(direction)>.01f?14:2)*dt*Power));h=R25TraversalVector.Limit(h,13);
            return new Float3(h.X,Math.Min(9,velocity.Y+(26*Power-16.2f)*dt),h.Z);
        }
    }
    public sealed class R25WebRope
    {
        public bool Attached { get; private set; }
        public Float3 Anchor { get; private set; }
        public float Length { get; private set; }
        public float FlightRemaining { get; private set; }
        public bool Zipped { get; private set; }
        float life,blocked,cooldown,zipCooldown;
        public bool Fire(Float3 wrist,Float3 anchor)
        {
            float d=R25TraversalVector.Length(R25TraversalVector.Sub(anchor,wrist));
            if(Attached||cooldown>0||!wrist.IsFinite||!anchor.IsFinite||d<2.5f||d>45)return false;
            Anchor=anchor;Length=Math.Max(2,d);FlightRemaining=d/120;life=blocked=0;Attached=true;return true;
        }
        public void Detach() { if(Attached)cooldown=.35f;Attached=false;FlightRemaining=0; }
        public void Tick(float dt,bool held,bool anchorAlive,bool obstructed)
        {
            Zipped=false;if(float.IsNaN(dt)||dt<=0||dt>.1f){Detach();return;}
            cooldown=Math.Max(0,cooldown-dt);zipCooldown=Math.Max(0,zipCooldown-dt);
            if(!Attached)return;life+=dt;FlightRemaining=Math.Max(0,FlightRemaining-dt);blocked=obstructed?blocked+dt:0;
            if(!held||!anchorAlive||life>=12||blocked>.3f)Detach();
        }
        public Float3 Forces(Float3 p,Float3 velocity,float trigger,float tug,float dt)
        {
            if(!velocity.IsFinite||!p.IsFinite||float.IsNaN(trigger)||float.IsInfinity(trigger)||float.IsNaN(tug)||float.IsInfinity(tug)||float.IsNaN(dt)||dt<=0||dt>.1f)return new Float3();
            if(!Attached||FlightRemaining>0)return velocity;
            trigger=Math.Max(0,Math.Min(1,trigger));Length=Math.Max(2,Length-9*trigger*dt);
            Float3 delta=R25TraversalVector.Sub(Anchor,p);float distance=R25TraversalVector.Length(delta);Float3 n=R25TraversalVector.Unit(delta);
            if(distance>=Length-.05f)velocity=R25TraversalVector.Add(velocity,R25TraversalVector.Mul(n,22*trigger*dt));
            if(tug>2.2f&&zipCooldown<=0){velocity=R25TraversalVector.Add(velocity,R25TraversalVector.Mul(n,6));zipCooldown=.6f;Zipped=true;}
            return R25TraversalVector.Limit(velocity,32);
        }
        public Float3 Constrain(Float3 p,ref Float3 velocity)
        {
            if(!p.IsFinite||!velocity.IsFinite){velocity=new Float3();return new Float3();}
            if(!Attached||FlightRemaining>0)return p;
            Float3 d=R25TraversalVector.Sub(p,Anchor);float distance=R25TraversalVector.Length(d);
            if(distance<=Length)return p;Float3 n=R25TraversalVector.Unit(d);float outward=R25TraversalVector.Dot(velocity,n);
            if(outward>0)velocity=R25TraversalVector.Sub(velocity,R25TraversalVector.Mul(n,outward));
            return R25TraversalVector.Add(Anchor,R25TraversalVector.Mul(n,Length));
        }
        // Corrections are never launch energy: only accepted physical displacement enters here.
        public static Float3 Accept(Float3 physical,float dt) { return dt>0&&dt<=.1f?R25TraversalVector.Limit(R25TraversalVector.Mul(physical,1/dt),32):new Float3(); }
    }
    public sealed class R25WebState
    {
        public readonly bool LeftAttached,RightAttached;
        public readonly Float3 LeftAnchor,RightAnchor;
        public R25WebState(bool left,Float3 a,bool right,Float3 b){LeftAttached=left;LeftAnchor=a;RightAttached=right;RightAnchor=b;}
        public bool Valid {get{return LeftAnchor.IsFinite&&RightAnchor.IsFinite&&R25TraversalVector.Length(LeftAnchor)<=173205&&R25TraversalVector.Length(RightAnchor)<=173205;}}
        public bool Near(Float3 head){return Valid&&head.IsFinite&&(!LeftAttached||R25TraversalVector.Length(R25TraversalVector.Sub(LeftAnchor,head))<=60)&&(!RightAttached||R25TraversalVector.Length(R25TraversalVector.Sub(RightAnchor,head))<=60);}
        public byte[] Encode(){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write((byte)((LeftAttached?1:0)|(RightAttached?2:0)));w.Write(LeftAnchor.X);w.Write(LeftAnchor.Y);w.Write(LeftAnchor.Z);w.Write(RightAnchor.X);w.Write(RightAnchor.Y);w.Write(RightAnchor.Z);return s.ToArray();}}
        public static bool Decode(byte[] bytes,out R25WebState state){state=null;if(bytes==null||bytes.Length!=25||bytes[0]>3)return false;using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s)){int bits=r.ReadByte();var a=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var b=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var v=new R25WebState((bits&1)!=0,a,(bits&2)!=0,b);if(!v.Valid)return false;state=v;return true;}}
        public bool Same(R25WebState b){if(b==null)return false;byte[] x=Encode(),y=b.Encode();for(int i=0;i<x.Length;i++)if(x[i]!=y[i])return false;return true;}
    }
}
