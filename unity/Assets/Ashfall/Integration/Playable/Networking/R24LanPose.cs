using System;using System.IO;using System.Text;using Ashfall.Core;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public struct R24LanRotation
    {
        public readonly float X,Y,Z,W;
        public R24LanRotation(float x,float y,float z,float w){X=x;Y=y;Z=z;W=w;}
        public bool Unit{get{float n=X*X+Y*Y+Z*Z+W*W;return !Single.IsNaN(n)&&!Single.IsInfinity(n)&&Math.Abs(n-1)<.001f;}}
    }
    public struct R24LanTransform
    {
        public readonly Float3 Position;public readonly R24LanRotation Rotation;
        public R24LanTransform(Float3 position,R24LanRotation rotation){Position=position;Rotation=rotation;}
        public bool Valid{get{return Position.IsFinite&&Math.Abs(Position.X)<=100000&&Math.Abs(Position.Y)<=100000&&Math.Abs(Position.Z)<=100000&&Rotation.Unit;}}
    }
    // Render presence only. These packets never move the local rig, add damage
    // colliders, mark a member Loaded, or authorize any gameplay operation.
    public sealed class R24LanPose
    {
        public readonly string Scene;public readonly int Style;public readonly byte Flags;
        public readonly R24LanTransform Root,Head,Left,Right;public readonly Float3 Velocity;public readonly float LeftGrip,RightGrip;
        public bool Diving{get{return (Flags&128)!=0;}}public bool Riding{get{return (Flags&64)!=0;}}public bool Jetting{get{return (Flags&32)!=0;}}public bool Tracked{get{return (Flags&1)!=0;}}public bool Supported{get{return (Flags&8)!=0;}}public bool Gliding{get{return (Flags&16)!=0;}}
        public R24LanPose(string scene,int style,byte flags,R24LanTransform root,R24LanTransform head,R24LanTransform left,R24LanTransform right,Float3 velocity,float leftGrip,float rightGrip)
        {Scene=scene;Style=style;Flags=flags;Root=root;Head=head;Left=left;Right=right;Velocity=velocity;LeftGrip=leftGrip;RightGrip=rightGrip;}
        static float Distance2(Float3 a,Float3 b){float x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return x*x+y*y+z*z;}
        static bool Grip(float v){return !Single.IsNaN(v)&&v>=0&&v<=1;}
        public bool Valid{get{return (Scene=="menu"||Scene!=null&&Scene.StartsWith("map:",StringComparison.Ordinal)&&Wire.IsHash(Scene.Substring(4)))&&Style>=0&&Style<139&&!(Gliding&&Diving)&&Root.Valid&&Head.Valid&&Left.Valid&&Right.Valid&&Distance2(Root.Position,Head.Position)<=25&&Distance2(Root.Position,Left.Position)<=25&&Distance2(Root.Position,Right.Position)<=25&&Velocity.IsFinite&&Math.Abs(Velocity.X)<=500&&Math.Abs(Velocity.Y)<=500&&Math.Abs(Velocity.Z)<=500&&Grip(LeftGrip)&&Grip(RightGrip);}}
        static void Vector(BinaryWriter w,Float3 p){w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        static Float3 Vector(BinaryReader r){return new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
        static void Transform(BinaryWriter w,R24LanTransform t){Vector(w,t.Position);w.Write(t.Rotation.X);w.Write(t.Rotation.Y);w.Write(t.Rotation.Z);w.Write(t.Rotation.W);}
        static R24LanTransform Transform(BinaryReader r){return new R24LanTransform(Vector(r),new R24LanRotation(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()));}
        public byte[] Encode()
        {
            if(!Valid)throw new ArgumentException("Finite bounded LAN render pose required");
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,new UTF8Encoding(false,true))){byte[] scene=Encoding.ASCII.GetBytes(Scene);w.Write((byte)1);w.Write((byte)scene.Length);w.Write(scene);w.Write((byte)Style);w.Write(Flags);Transform(w,Root);Transform(w,Head);Transform(w,Left);Transform(w,Right);Vector(w,Velocity);w.Write(LeftGrip);w.Write(RightGrip);return s.ToArray();}
        }
        public static bool TryDecode(byte[] bytes,out R24LanPose pose)
        {
            pose=null;if(bytes==null||bytes.Length>240)return false;
            try{using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s,new UTF8Encoding(false,true))){if(r.ReadByte()!=1)return false;int length=r.ReadByte();if(length<4||length>68||length>s.Length-s.Position)return false;string scene=Encoding.ASCII.GetString(r.ReadBytes(length));int style=r.ReadByte();byte flags=r.ReadByte();var root=Transform(r);var head=Transform(r);var left=Transform(r);var right=Transform(r);var velocity=Vector(r);float lg=r.ReadSingle(),rg=r.ReadSingle();var value=new R24LanPose(scene,style,flags,root,head,left,right,velocity,lg,rg);if(s.Position!=s.Length||!value.Valid)return false;pose=value;return true;}}catch(IOException){return false;}catch(ArgumentException){return false;}
        }
    }
    public sealed class R24LanPoseSnapshot
    {
        public readonly string Actor;public readonly long Sequence;public readonly double ReceivedAt;public readonly R24LanPose Pose;internal readonly byte[] Frame;
        internal R24LanPoseSnapshot(string actor,long sequence,double now,R24LanPose pose,byte[] frame){Actor=actor;Sequence=sequence;ReceivedAt=now;Pose=pose;Frame=frame;}
    }
}
