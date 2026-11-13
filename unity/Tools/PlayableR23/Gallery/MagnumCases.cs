using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.Gallery;
using Ashfall.Integration.Playable.HandsWeapons;
class MagnumGalleryCases
{
    static int cases;
    sealed class Sight:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return true;}}
    static void Need(bool value,string label){cases++;if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static HandlingInput Pose(bool trigger=false,bool grip=false,Float3 point=new Float3())
    {return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger,Grip=grip,SupportLocal=point,ForegripDistance=1,HasPrimaryPose=true,PrimaryForward=new Float3(0,0,1),PrimaryUp=new Float3(0,1,0)};}
    static bool Step(R23MagnumGallerySession s,float dt=.02f,bool trigger=false,bool grip=false,Float3 point=new Float3(),bool click=false,bool focused=true)
    {return s.Step(dt,new R23MagnumInput(Pose(trigger,grip,point),primaryButton:click,triggerBlocksFlick:trigger),focused);}
    static bool Fire(R23MagnumGallerySession s){Step(s,.25f);return Step(s,.084f,true);}
    static void Main()
    {
        var carried=new R23RoundState(new[]{"target/0"},new Sight(),true);carried.EnsureMagnumState("carried/magnum");
        int carriedAmmo=carried.Magnum.Loaded,carriedReserve=carried.Magnum.Reserve,carriedMask=carried.Magnum.ChamberMask;
        var session=new R23MagnumGallerySession();Need(!session.Grab()&&!session.Live,"Hidden Magnum gallery has no owner/grant");
        session.Show();Need(session.Active&&session.Grab()&&session.Live,"Showing/gripping creates private actual Magnum owner");
        var first=session.Owner;string id=first.InstanceId;Need(first.Loaded==7&&first.Reserve==1200&&first.ChamberMask==127,"Private source7 chamber/finite1200 preview pool");
        Need(!Step(session,trigger:true)&&first.Loaded==7,"Held entry trigger cannot fire without neutral");
        Need(Fire(session)&&first.Loaded==6&&first.Reserve==1200&&first.ChamberMask==126&&session.PreviewShots==1,"Actual shot debits chamber once; reserve unchanged");
        Need(!Step(session,.25f,true)&&first.Loaded==6,"Held semi trigger cannot repeat");
        for(int i=0;i<6;i++)Need(Fire(session),"Actual remaining chamber fire "+i);
        Need(first.Loaded==0&&first.Snapshot.Phase==ReloadPhase.CylinderOpen&&first.OfferedRoundCount==7,"Empty private cylinder opens genuine source round offer");
        Step(session,point:R23MagnumRuntime.Offer(false));Step(session,grip:true,point:R23MagnumRuntime.Offer(false));Step(session,grip:true,point:R23MagnumRuntime.OpenPoint(false));
        Need(first.Loaded==7&&first.Reserve==1193&&first.Snapshot.Phase==ReloadPhase.CylinderLoaded,"Physical forward loading stroke uses real finite preview reserve");
        var point=R23MagnumRuntime.OpenPoint(false);Step(session,point:point);Step(session,grip:true,point:point);Step(session,grip:true,point:new Float3(point.X,point.Y+.051f,point.Z));
        Need(first.Snapshot.Phase==ReloadPhase.Ready&&first.ChamberMask==127,"Fresh support grip/upstroke closes source cylinder");
        Need(!Step(session,.25f,true,focused:false)&&first.Loaded==7,"Lost focus suspends firing without debit");
        session.Release();Need(!session.Live&&!first.Step(.2f,new R23MagnumInput(Pose(true))),"Release retires private native owner");
        Need(session.Grab()&&session.Owner.InstanceId!=id&&session.Owner.Loaded==7&&session.Owner.Reserve==1200,"Next source live097 grab is fresh private owner/pool");
        session.Hide();Need(!session.Active&&!session.Live&&!Step(session,.1f,true),"Hide retires all live inspection mechanics");
        Need(carried.Magnum.Loaded==carriedAmmo&&carried.Magnum.Reserve==carriedReserve&&carried.Magnum.ChamberMask==carriedMask&&carried.ReadTarget("target/0").Health==100,"Preview changes no carried Magnum ammo/reserve/chambers or world health");
        Console.WriteLine(cases+" Magnum gallery production cases passed");
    }
}
