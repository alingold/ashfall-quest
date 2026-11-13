using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Gallery;
class SmgGalleryCases
{
    static int count;
    sealed class Sight:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return true;}}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static HandlingInput Input(bool trigger=false,bool grip=false,Float3 point=new Float3(),bool left=false)
    {return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger,Grip=grip,SupportLocal=point,ForegripDistance=.9f,LeftHanded=left};}
    static bool Step(R23GallerySession session,float dt,HandlingInput input,bool click=false,bool focused=true,float x=0,float y=0)
    {return session.StepSource(dt,input,click,x,y,focused);}
    static void Main()
    {
        var carried=new R23RoundState(new[]{"actual-carried-target"},new Sight(),true);int rifle=carried.ReadReserve("cx4"),pistol=carried.ReadReserve("px4"),shells=carried.ReadReserve("m1014");
        string[] ids={"ump","p90","mp5","mp9"};int[] capacities={28,45,21,24};
        for(int n=0;n<ids.Length;n++)
        {
            string id=ids[n];var s=new R23GallerySession();var definition=SourceRifleDefinition.ForId(id);
            Check(definition!=null&&definition.Capacity==capacities[n]&&definition.AmmoFamily=="smg"&&definition.VrOpeningOnly,id+" actual source definition/capacity/live-rack family");
            Check(s.Show(id)&&s.Grab()&&s.Loaded==capacities[n]&&s.Reserve==-1,id+" private source live097 owner/unlimited preview pool");string first=s.InstanceId;
            Check(!Step(s,.1f,Input(true)),id+" held entry trigger blocked");Step(s,0,Input());
            Check(Step(s,.01f,Input(true))&&s.Loaded==capacities[n]-1,id+" real native first shot");
            Step(s,.1f,Input(true));Check(s.Loaded<capacities[n]-1,id+" actual automatic hold cadence");
            int loaded=s.Loaded;Check(!Step(s,.1f,Input(true),focused:false)&&s.Loaded==loaded,id+" unfocused preview cannot debit");
            Step(s,.01f,Input());Step(s,.01f,Input(),true);Check(s.State.Value.Phase==ReloadPhase.NeedsMagazine,id+" real source magazine eject");
            Step(s,.01f,Input(),true);Check(s.State.Value.Phase==ReloadPhase.NeedsMagazine,id+" repeated VR click cannot substitute physical reload");
            Step(s,.01f,Input());Step(s,.01f,Input(false,true,definition.Offer));Check(s.State.Value.Phase==ReloadPhase.HeldMagazine,id+" fresh actual offer grip");
            if(id=="p90")
            {Step(s,.01f,Input(false,true,new Float3(definition.Offer.X,definition.Offer.Y+.11f,definition.Offer.Z)));Check(s.State.Value.Phase==ReloadPhase.HeldMagazine,"P90 UP does not replace source FORWARD insertion");}
            Step(s,.01f,Input(false,true,definition.Insert));Check(s.State.Value.Phase==ReloadPhase.NeedsRack,id+" actual source insertion direction/seat");
            if(id=="p90")
            {
                Step(s,.01f,Input(),true);Check(s.State.Value.Phase==ReloadPhase.NeedsMagazine,"P90 click in NeedsRack ejects seated magazine instead of desktop auto-charge");
                Step(s,.01f,Input());Step(s,.01f,Input(false,true,definition.Offer));Step(s,.01f,Input(false,true,definition.Insert));
                Check(s.State.Value.Phase==ReloadPhase.NeedsRack,"P90 actual replacement magazine forward stroke after seated eject");
            }
            bool left=id=="p90";var charge=definition.Charging;if(left)charge=new Float3(-charge.X,charge.Y,charge.Z);
            Step(s,.01f,Input(left:left));Step(s,.01f,Input(false,true,charge,left));
            Step(s,.01f,Input(false,true,new Float3(charge.X,charge.Y,charge.Z-.08f),left));
            Check(s.State.Value.Phase==ReloadPhase.Ready&&s.Loaded==capacities[n]&&s.Reserve==-1,id+" actual BACK rack"+(left?" / source mirrored handle":""));
            Step(s,.01f,Input(),y:-1);Check(s.State.Value.Phase==ReloadPhase.NeedsMagazine,id+" source neutral-rearmed stick-down eject");
            s.Release();Check(!s.Live&&s.Grab()&&s.InstanceId!=first&&s.Loaded==capacities[n],id+" release retires owner; fresh private regrab");s.Hide();Check(!s.Active&&!s.Live,id+" hide cleanup");
        }
        Check(carried.ReadReserve("cx4")==rifle&&carried.ReadReserve("px4")==pistol&&carried.ReadReserve("m1014")==shells&&carried.ReadTarget("actual-carried-target").Health==100&&carried.ReadTarget("actual-carried-target").Shield==100,"four SMG previews cannot debit/grant carried families or damage world target");
        Console.WriteLine("PASS "+count+" real SMG preview owner/control/physical reload/isolation cases; no native render/device execution");
    }
}
