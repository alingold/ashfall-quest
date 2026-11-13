using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Gallery;
static class CatalogGalleryCases
{
    static int count;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
    static HandlingInput Input(bool trigger=false,bool grip=false,Float3 point=new Float3())
    {return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger,Grip=grip,SupportLocal=point,ForegripDistance=.9f};}
    static bool Step(R23GallerySession s,float dt,HandlingInput input,bool click=false,float y=0,bool focus=true)
    {return s.StepSource(dt,input,click,0,y,focus);}
    static void Rack(R23GallerySession s)
    {
        var d=s.Definition;Step(s,0,Input());Step(s,.01f,Input(false,true,d.Charging));
        Step(s,.01f,Input(false,true,new Float3(d.Charging.X,d.Charging.Y,d.Charging.Z-d.HandlePull-.001f)));
    }
    static void Main()
    {
        foreach(string id in new[]{"1911","tec9","rfb","fal","s85"})
        {
            var d=SourceRifleDefinition.ForId(id);var s=new R23GallerySession();
            Check(d!=null&&!d.Pistol&&d.SupportPoint.Y==-.025f&&d.SupportPoint.Z==.36f,id+" actual catalog base support, no PX4 shortcut");
            Check(s.Show(id)&&s.Grab()&&s.Loaded==d.Capacity&&s.Reserve==-1,id+" private source live097 owner, no carried provider");
            string first=s.InstanceId;Check(!Step(s,.1f,Input(true)),id+" held entry cannot fire");Step(s,0,Input());
            Check(Step(s,.01f,Input(true)),id+" real owner first accepted discharge");
            if(id=="s85")
            {
                Check(s.State.Value.Loaded==0&&s.State.Value.Seated==4&&s.Loaded==4&&s.State.Value.Phase==ReloadPhase.NeedsRack,"S85 nonfinal discharge stores seated rounds and requires rack");
                Check(!Step(s,.25f,Input(true)),"S85 held trigger cannot substitute bolt cycle");Rack(s);
                Check(s.Loaded==4&&s.State.Value.Phase==ReloadPhase.Ready,"S85 physical BACK rack restores actual retained four rounds");
            }
            else if(d.SemiAutomatic)
            {int prior=s.Loaded;Check(!Step(s,.25f,Input(true))&&s.Loaded==prior,id+" held semi trigger stays disarmed");}
            else
            {int prior=s.Loaded;Step(s,.25f,Input(true));Check(s.Loaded<prior,id+" actual automatic held cadence");}
            int ammo=s.Loaded;Check(!Step(s,.1f,Input(true),focus:false)&&s.Loaded==ammo,id+" focus loss cannot debit");
            Step(s,0,Input());Step(s,.01f,Input(),true);Check(s.State.Value.Phase==ReloadPhase.NeedsMagazine,id+" actual click eject");
            Step(s,.01f,Input(),true);Check(s.State.Value.Phase==ReloadPhase.NeedsMagazine,id+" held click cannot insert or rack");
            Step(s,0,Input());Step(s,.01f,Input(false,true,d.Offer));Check(s.State.Value.Phase==ReloadPhase.HeldMagazine,id+" actual offered magazine grip");
            if(id=="s85")
            {
                Step(s,.01f,Input(false,true,d.Insert));Check(s.State.Value.Phase==ReloadPhase.HeldMagazine,"S85 old low well cannot replace raised offer-relative zone");
                Step(s,.01f,Input(false,true,new Float3(d.Offer.X,d.Offer.Y+.10f,d.Offer.Z)));
            }
            else Step(s,.01f,Input(false,true,d.Insert));
            Check(s.State.Value.Phase==ReloadPhase.NeedsRack,id+" actual source insertion zone");Rack(s);
            Check(s.State.Value.Phase==ReloadPhase.Ready&&s.Loaded==d.Capacity&&s.Reserve==-1,id+" physical reload fills private owner only");
            s.Release();Check(s.Grab()&&s.InstanceId!=first&&s.Loaded==d.Capacity,id+" released owner retired, fresh isolated regrab");s.Hide();Check(!s.Live&&!s.Active,id+" menu hide cleanup");
        }
        var rfb=new R23GallerySession();rfb.Show("rfb");rfb.Grab();Step(rfb,0,Input());
        Check(Step(rfb,.01f,Input(true),true)&&rfb.PreviewShots==1&&rfb.State.Value.Phase==ReloadPhase.NeedsMagazine,"RFB simultaneous trigger+fresh click fires then ejects, source order");
        rfb.Release();rfb.Grab();Step(rfb,0,Input());Step(rfb,.01f,Input(),y:-1);
        Check(rfb.State.Value.Phase==ReloadPhase.Ready,"RFB custom VR path has no generic stick-down eject");rfb.Hide();
        Console.WriteLine("PASS "+count+" actual private Catalog5 owner/control/physical reload cases; no engine/render/device run");
    }
}
