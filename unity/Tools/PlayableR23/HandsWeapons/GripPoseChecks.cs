using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Ashfall.Integration.Playable.HandsWeapons;
class GripPoseChecks
{
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{
            var path=Path.Combine(args[0],new AssemblyName(eventArgs.Name).Name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
        Check();
    }
    static double[] Rotate(double[] q,double[] p)
    {
        double x=q[0],y=q[1],z=q[2],w=q[3];
        return new[]{(1-2*y*y-2*z*z)*p[0]+(2*x*y-2*z*w)*p[1]+(2*x*z+2*y*w)*p[2],
            (2*x*y+2*z*w)*p[0]+(1-2*x*x-2*z*z)*p[1]+(2*y*z-2*x*w)*p[2],
            (2*x*z-2*y*w)*p[0]+(2*y*z+2*x*w)*p[1]+(1-2*x*x-2*y*y)*p[2]};
    }
    [MethodImpl(MethodImplOptions.NoInlining)]static void Check()
    {
        var random=new System.Random(2384);int cases=0;
        for(int sample=0;sample<256;sample++)
        {
            var q=new double[4];double length=0;for(int i=0;i<4;i++){q[i]=random.NextDouble()*2-1;length+=q[i]*q[i];}length=Math.Sqrt(length);for(int i=0;i<4;i++)q[i]/=length;
            var rawUnity=new Quaternion((float)-q[0],(float)-q[1],(float)q[2],(float)q[3]);
            for(int axis=0;axis<3;axis++)
            {
                var source=new double[3];source[axis]=1;
                // Independently apply Godot's actual Rx(-PI/2), then the raw pose and Z reflection.
                var expected=Rotate(q,new[]{source[0],source[2],-source[1]});expected[2]*=-1;
                var actual=(rawUnity*R23TrackedGripPose.LocalRotation)*new Vector3((float)source[0],(float)source[1],(float)-source[2]);
                if(Math.Abs(actual.x-expected[0])+Math.Abs(actual.y-expected[1])+Math.Abs(actual.z-expected[2])>.000002)throw new Exception("Source grip conjugation mismatch at "+sample+" axis "+axis);
                cases++;
            }
        }
        foreach(float scale in new[]{.92f,.945f,1.32f})
        {
            var grip=new Vector3(0,-.057f,-.025f);var actual=R23TrackedGripPose.GripCenteredOffset(grip,scale)+R23TrackedGripPose.LocalRotation*(grip*scale);
            if(actual.sqrMagnitude>.0000000001f)throw new Exception("Correction detached model grip contact");cases++;
        }
        for(int hand=0;hand<2;hand++)
        {
            // Independently measured from original glTF Wrist/Middle_Proximal/
            // Thumb_Proximal global bones, after the existing Z-roll/reflection bake.
            float mirror=hand==0?1:-1;
            var finger=new Vector3(-.018913137f*mirror,.166124628f,.985923375f);
            var thumb=new Vector3(.907455805f*mirror,-.411109263f,.086678348f);
            var rotation=R23TrackedGripPose.GloveLocalRotation(hand);
            if((rotation*finger-R23TrackedGripPose.LocalRotation*Vector3.forward).sqrMagnitude>.00000001f)throw new Exception("Glove fingers disagree with accepted gun direction");
            if((rotation*thumb-R23TrackedGripPose.LocalRotation*Vector3.up).sqrMagnitude>.00000001f)throw new Exception("Glove thumb-side axis disagrees with gun up");
            cases+=2;
            // Independent visible-model axes, both dominant-hand choices and
            // arbitrary recoil/world rotations. Supporting art must follow the
            // displayed bore without inheriting the player's controller roll.
            for(int sample=0;sample<128;sample++)
            {
                var q=new double[4];double length=0;for(int i=0;i<4;i++){q[i]=random.NextDouble()*2-1;length+=q[i]*q[i];}length=Math.Sqrt(length);for(int i=0;i<4;i++)q[i]/=length;
                var model=new Quaternion((float)q[0],(float)q[1],(float)q[2],(float)q[3]);
                var expectedForward=Rotate(q,new double[]{0,0,1});var expectedUp=Rotate(q,new double[]{0,1,0});
                var actual=R23TrackedGripPose.SupportArtRotation(model,hand);
                var f=actual*finger;var u=actual*thumb;
                if(Math.Abs(f.x-expectedForward[0])+Math.Abs(f.y-expectedForward[1])+Math.Abs(f.z-expectedForward[2])>.000003)throw new Exception("Support glove fingers detached from displayed bore");
                if(Math.Abs(u.x-expectedUp[0])+Math.Abs(u.y-expectedUp[1])+Math.Abs(u.z-expectedUp[2])>.000003)throw new Exception("Support glove thumb axis detached from displayed model");
                cases+=2;
            }
        }
        Console.WriteLine("PASS "+cases+" actual Unity quaternion/source reflected grip and physical contact observations; no engine loaded");
    }
}
