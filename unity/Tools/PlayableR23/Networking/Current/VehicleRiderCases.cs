using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Vehicles;
using Ashfall.Integration.Playable.Networking;
partial class LobbyCases
{
    static void VehicleRiderHolds()
    {
        var mount=new R25BikeHold();for(int i=0;i<3;i++)Need(!mount.Step(true,.1f,.4f),"Mount waits for 400ms");
        Need(mount.Step(true,.1f,.4f),"Mount requests at 400ms");Need(!mount.Step(true,.1f,.4f),"Held mount sends once");
        mount.Step(false,.1f,.4f);Need(!mount.Step(true,.1f,.4f),"Release resets hold");
        var exit=new R25BikeHold();for(int i=0;i<4;i++)Need(!exit.Step(true,.1f,.5f),"Exit waits for 500ms");Need(exit.Step(true,.1f,.5f),"Exit at 500ms");Need(!exit.Step(true,.1f,.5f),"Exit sends once");
        var invalid=new R25BikeHold();Need(!invalid.Step(true,Single.NaN,.4f)&&!invalid.Step(true,1,.4f),"Invalid time ignored");
        var tap=new R25BikeHold();Need(!tap.Step(true,.1f,.5f)&&!tap.Step(false,.1f,.5f),"Jump tap does not dismount");
    }
    static void VehicleRiderMapping()
    {
        var bike=new R25BikeDynamics();var control=new R25BikeDynamics();
        for(int i=0;i<60;i++){
            R25BikeRider.Drive(bike,.02f,.3f,1,90,i==0,i==1,true,.55f,new Float3(0,1,0));
            control.Step(.02f,1,.3f,(float)Math.PI/2,i==0,i==1,true,.55f,new Float3(0,1,0));
            Need(Near(bike.Velocity.X,control.Velocity.X,.0001f)&&Near(bike.Velocity.Z,control.Velocity.Z,.0001f),"Stick axes and degree heading map to motor");
        }
        Need(bike.Velocity.X>0&&bike.Velocity.Z<0,"90 degree forward and right strafe");
        bike.Accept(new Float3(2,1,3));Need(bike.Velocity.Equals(new Float3(2,1,3)),"Accepted sweep momentum retained");
        Need(R25BikeRider.Allowed(true,true,false,false,false),"Ordinary rider allowed");
        Need(!R25BikeRider.Allowed(false,true,false,false,false)&&!R25BikeRider.Allowed(true,false,false,false,false)&&!R25BikeRider.Allowed(true,true,true,false,false)&&!R25BikeRider.Allowed(true,true,false,true,false)&&!R25BikeRider.Allowed(true,true,false,false,true),"Rule tracking menu/death ghost carrier interlocks");
    }
    static void VehicleRiderPose()
    {
        var root=new Float3(1,2,3);var velocity=new Float3(10,0,-2);
        var predicted=R25BikeRider.Remote(root,velocity,.1);Need(Near(predicted.X,2,.0001f)&&Near(predicted.Z,2.8f,.0001f),"Remote pose extrapolated by velocity");
        Need(Near(R25BikeRider.Remote(root,velocity,4).X,2.5f,.0001f),"Prediction bounded at 150ms");Need(R25BikeRider.Remote(root,velocity,-1).Equals(root),"Negative age clamped");
        Need(R25BikeRider.Remote(root,velocity,Double.NaN).Equals(root),"Nonfinite age safe");
        Need(Near(R25BikeRider.Yaw(0,(float)Math.Sin(Math.PI/4),0,(float)Math.Cos(Math.PI/4)),90,.001f),"Root quaternion body yaw");
        var t=new R24LanTransform(root,new R24LanRotation(0,0,0,1));R24LanPose read;
        var pose=new R24LanPose("menu",114,127,t,t,t,t,velocity,0,0);
        Need(pose.Riding&&pose.Jetting&&R24LanPose.TryDecode(pose.Encode(),out read)&&read.Riding,"Bit 64 accepted with prior flags");
        Need(!new R24LanPose("menu",0,128,t,t,t,t,velocity,0,0).Valid,"Bit 128 rejected");Need(!new R24LanPose("menu",139,64,t,t,t,t,velocity,0,0).Valid,"Style 139 excluded after optional legacy extension");
    }
}
