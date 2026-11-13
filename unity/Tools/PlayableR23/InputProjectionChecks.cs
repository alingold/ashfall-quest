using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
class InputProjectionChecks
{
    static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Main()
    {
        foreach(var direction in new[]{new Float3(1,0,0),new Float3(-1,0,0),new Float3(0,0,1),new Float3(0,0,-1)})
        {
            var input=new PlayerInput();var motor=new Locomotion(PlayerTuning.SkinRuntime());
            HandSample left=new HandSample{Tracked=true,Position=new Float3(-.2f,1.2f,0)},right=new HandSample{Tracked=true,Position=new Float3(.2f,1.2f,0)};
            long tick=0;MotionStep step=new MotionStep();
            for(int n=0;n<3;n++)motor.Step(input.Read(++tick,.04f,R23InputProjection.Sample(true,true,new Float3(0,1.7f,0),left,right,false)),new MotionEnvironment{IsXr=true,Grounded=true},.04f);
            left.StickX=direction.X;left.StickY=direction.Z;
            var frame=input.Read(++tick,.04f,R23InputProjection.Sample(true,true,new Float3(0,1.7f,0),left,right,false));
            step=motor.Step(frame,new MotionEnvironment{IsXr=true,Grounded=true},.04f);
            Need(step.OwnsMotion,"XR neutral rearm lost");
            Need(step.Displacement.X*direction.X+step.Displacement.Z*direction.Z>.01f,"Thumbstick failed to move actual locomotion in requested direction");
            right.StickX=1;
            step=motor.Step(input.Read(++tick,.04f,R23InputProjection.Sample(true,true,new Float3(0,1.7f,0),left,right,false)),new MotionEnvironment{IsXr=true,Grounded=true},.04f);
            Need(step.YawRadians>0,"Right stick failed to turn");
            var lost=input.Read(++tick,.04f,R23InputProjection.Sample(false,true,new Float3(0,1.7f,0),left,right,false));
            Need(!lost.Tracked&&lost.MoveX==0&&lost.MoveY==0,"Lost XR focus must disarm");
        }
        Console.WriteLine("XR stick bridge -> real PlayerInput/Locomotion: four directions, turning and focus loss PASS");
        foreach(int primary in new[]{0,1})
        {
            var input=new PlayerInput();var motor=new Locomotion(PlayerTuning.SkinRuntime());
            var left=new HandSample{Tracked=true,Position=new Float3(-.2f,1.2f,0)};var right=new HandSample{Tracked=true,Position=new Float3(.2f,1.2f,0)};long tick=0;
            for(int i=0;i<3;i++)motor.Step(input.Read(++tick,.04f,R23InputProjection.Sample(true,true,new Float3(0,1.7f,0),left,right,false,primary,true)),new MotionEnvironment{IsXr=true,Grounded=true},.04f);
            left.StickY=1;right.StickX=1;if(primary==0)left.Secondary=true;else right.Secondary=true;
            var sample=R23InputProjection.Sample(true,true,new Float3(0,1.7f,0),left,right,false,primary,true);
            var step=motor.Step(input.Read(++tick,.04f,sample),new MotionEnvironment{IsXr=true,Grounded=true},.04f);
            Need(step.YawRadians==0&&step.Displacement.Z>.01f,"Selecting a source mark rotated the player or consumed walking");
            Need(sample.Right.StickX==1&&sample.Left.StickY==1&&(primary==0?sample.Left.Secondary:sample.Right.Secondary),"Mark reservation overwrote raw controller samples");
            if(primary==0)left.Secondary=false;else right.Secondary=false;
            step=motor.Step(input.Read(++tick,.04f,R23InputProjection.Sample(true,true,new Float3(0,1.7f,0),left,right,false,primary,true)),new MotionEnvironment{IsXr=true,Grounded=true},.04f);
            Need(step.YawRadians>0,"Releasing the mark button failed to restore turning");
        }
        Console.WriteLine("Actual PlayerInput/Locomotion: both mark-button dominances reserve turn while preserving walking/raw sticks, release restores turn PASS");
    }
}
