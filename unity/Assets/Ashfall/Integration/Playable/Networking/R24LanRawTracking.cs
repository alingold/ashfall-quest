using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    // Raw tracking is distinct from R24LanPose's rendered glove frames. This is
    // source pose evidence, NOT a spawn/life/weapon lease or permission to damage.
    public sealed class R24LanRawTracking
    {
        public readonly string MapHash;public readonly int SpawnRevision;
        public readonly byte Flags; // tracked body, left controller, right controller, revive paddles active (source pose.paddles)
        public bool Paddles{get{return (Flags&8)!=0;}}
        public readonly R24LanTransform Root,Head,Left,Right;
        public readonly float LeftGrip,RightGrip;
        public R24LanRawTracking(string mapHash,byte flags,R24LanTransform root,R24LanTransform head,R24LanTransform left,R24LanTransform right,float leftGrip,float rightGrip,int spawnRevision=0)
        {SpawnRevision=spawnRevision;MapHash=mapHash;Flags=flags;Root=root;Head=head;Left=left;Right=right;LeftGrip=leftGrip;RightGrip=rightGrip;}
        internal static double Distance2(Float3 a,Float3 b)
        {double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return x*x+y*y+z*z;}
        public bool BodyValid{get{return Wire.IsHash(MapHash)&&SpawnRevision>=0&&(Flags&~15)==0&&(Flags&1)!=0&&Root.Valid&&Head.Valid&&Distance2(Root.Position,Head.Position)<=25;}}
        static bool GripValid(float value){return !Single.IsNaN(value)&&value>=0&&value<=1;}
        static Float3 Rotate(R24LanRotation q,Float3 v)
        {
            float tx=2*(q.Y*v.Z-q.Z*v.Y),ty=2*(q.Z*v.X-q.X*v.Z),tz=2*(q.X*v.Y-q.Y*v.X);
            return new Float3(v.X+q.W*tx+q.Y*tz-q.Z*ty,v.Y+q.W*ty+q.Z*tx-q.X*tz,v.Z+q.W*tz+q.X*ty-q.Y*tx);
        }
        R24LanTransform NeutralHand(int hand)
        {
            // Godot (-.3/+ .3,-.35,-.25), reflected once into Unity world Z.
            Float3 offset=Rotate(Root.Rotation,new Float3(hand==0?-.3f:.3f,-.35f,.25f));
            return new R24LanTransform(new Float3(Head.Position.X+offset.X,Head.Position.Y+offset.Y,Head.Position.Z+offset.Z),Root.Rotation);
        }
        internal R24LanRawTracking Sanitize(out int count)
        {
            count=0;byte flags=Flags;var left=Left;var right=Right;float lg=LeftGrip,rg=RightGrip;
            if((flags&2)==0||!left.Valid||Distance2(Root.Position,left.Position)>25||!GripValid(lg)){left=NeutralHand(0);lg=0;flags=(byte)(flags&~2);count++;}
            if((flags&4)==0||!right.Valid||Distance2(Root.Position,right.Position)>25||!GripValid(rg)){right=NeutralHand(1);rg=0;flags=(byte)(flags&~4);count++;}
            if(count>0)flags=(byte)(flags&~8); // source: a neutralized hand also clears paddles
            return count==0?this:new R24LanRawTracking(MapHash,flags,Root,Head,left,right,lg,rg,SpawnRevision);
        }
        static void Write(BinaryWriter w,R24LanTransform t){w.Write(t.Position.X);w.Write(t.Position.Y);w.Write(t.Position.Z);w.Write(t.Rotation.X);w.Write(t.Rotation.Y);w.Write(t.Rotation.Z);w.Write(t.Rotation.W);}
        static R24LanTransform Read(BinaryReader r){return new R24LanTransform(new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),new R24LanRotation(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()));}
        public byte[] Encode()
        {
            if(!BodyValid)throw new ArgumentException("Tracked finite raw body/map required");
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write((byte)2);w.Write(System.Text.Encoding.ASCII.GetBytes(MapHash));w.Write(SpawnRevision);w.Write(Flags);Write(w,Root);Write(w,Head);Write(w,Left);Write(w,Right);w.Write(LeftGrip);w.Write(RightGrip);return s.ToArray();}
        }
        public static bool TryDecode(byte[] bytes,out R24LanRawTracking value)
        {if(!TryReadEnvelope(bytes,out value))return false;if(value.BodyValid)return true;value=null;return false;}
        internal static bool TryReadEnvelope(byte[] bytes,out R24LanRawTracking value)
        {
            value=null;if(bytes==null||bytes.Length!=190)return false;
            try{using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s)){if(r.ReadByte()!=2)return false;string hash=System.Text.Encoding.ASCII.GetString(r.ReadBytes(64));int revision=r.ReadInt32();byte flags=r.ReadByte();var root=Read(r);var head=Read(r);var left=Read(r);var right=Read(r);float lg=r.ReadSingle(),rg=r.ReadSingle();var pose=new R24LanRawTracking(hash,flags,root,head,left,right,lg,rg,revision);if(!Wire.IsHash(hash)||revision<0||s.Position!=s.Length)return false;value=pose;return true;}}catch(IOException){return false;}catch(ArgumentException){return false;}
        }
    }
    public sealed class R24LanRawTrackingSample
    {
        public readonly long Sequence;public readonly double Time;public readonly R24LanRawTracking Pose;
        internal R24LanRawTrackingSample(long sequence,double time,R24LanRawTracking pose){Sequence=sequence;Time=time;Pose=pose;}
    }
    // One bounded source-style 20-sample history per authenticated current member.
    // Receive time is host-owned. The lobby binds each sample to actual current
    // Session placement. Life and owned-weapon combat authority remain separate.
    public sealed class R24LanRawTrackingHistory
    {
        readonly R24LanRawTrackingSample[] samples=new R24LanRawTrackingSample[20];int next,count,budget;double budgetAt=-1;
        internal double OldestTime{get{return count==0?0:samples[(next-count+20)%20].Time;}}
        public int Count{get{return count;}}public int SanitizedHands{get;private set;}public string Rejection{get;private set;}
        public R24LanRawTrackingSample Latest{get{return count==0?null:samples[(next+19)%20];}}
        static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
        bool Reject(string reason){Rejection=reason;return false;}
        public bool Accept(long sequence,double now,R24LanRawTracking pose,bool match,float motionMultiplier,Func<Float3,Float3,bool> worldClear,float motionSlack=0)
        {
            Rejection=null;var previous=Latest;
            if(!Finite(now)||now<0||previous!=null&&now<previous.Time)return Reject("clock");
            if(budgetAt<0||now-budgetAt>1){budgetAt=now;budget=0;}budget=Math.Min(46,budget+1);if(budget>45)return Reject("rate");
            if(sequence<=0||previous!=null&&sequence<=previous.Sequence)return Reject("sequence");
            if(pose==null||!pose.BodyValid||!Finite(motionMultiplier)||motionMultiplier<1||!Finite(motionSlack)||motionSlack<0)return Reject("body");
            if(previous!=null&&previous.Pose.MapHash!=pose.MapHash)return Reject("map");
            if(previous!=null&&previous.Pose.SpawnRevision!=pose.SpawnRevision)return Reject("spawn-revision");
            if(previous!=null&&match){
                double dt=Math.Max(.001,Math.Min(.5,now-previous.Time)),floor=3+motionSlack,allowance=Math.Max(floor,55*dt*motionMultiplier);
                Float3 a=previous.Pose.Root.Position,b=pose.Root.Position;double x=(double)b.X-a.X,y=(double)b.Y-a.Y,z=(double)b.Z-a.Z;
                if(Math.Sqrt(x*x+z*z)>allowance||y>Math.Max(allowance,120*dt)||-y>Math.Max(floor,360*dt))return Reject("speed");
                if(R24LanRawTracking.Distance2(previous.Pose.Head.Position,pose.Head.Position)>=.0001&&(worldClear==null||!worldClear(previous.Pose.Head.Position,pose.Head.Position)))return Reject("world-sweep");
            }
            int sanitized;var accepted=pose.Sanitize(out sanitized);SanitizedHands+=sanitized;samples[next]=new R24LanRawTrackingSample(sequence,now,accepted);next=(next+1)%20;count=Math.Min(20,count+1);return true;
        }
        public bool HeadAt(double time,out Float3 head)
        {
            head=new Float3();if(count==0)return false;
            var previous=samples[(next-count+20)%20];if(time<previous.Time-.000001)return false;
            for(int i=1;i<count;i++){
                var following=samples[(next-count+i+20)%20];
                if(time<following.Time){
                    float t=(float)((time-previous.Time)/(following.Time-previous.Time));
                    var a=previous.Pose.Head.Position;var b=following.Pose.Head.Position;
                    head=new Float3(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);return true;
                }
                previous=following;
            }
            head=previous.Pose.Head.Position;return true;
        }
        public bool TryNearest(double time,out R24LanRawTrackingSample sample)
        {
            sample=null;if(!Finite(time))return false;double distance=Double.PositiveInfinity;
            for(int i=0;i<count;i++){var item=samples[(next-count+i+20)%20];double delta=Math.Abs(item.Time-time);if(delta<distance){distance=delta;sample=item;}}
            return sample!=null;
        }
    }
}
