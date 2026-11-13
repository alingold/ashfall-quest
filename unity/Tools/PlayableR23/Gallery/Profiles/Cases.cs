using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Gallery;
class NewProfileCases
{
    static int count;
    sealed class Sight:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return true;}}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static HandlingInput Input(bool fire=false,bool grip=false,Float3 point=new Float3())
    {return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=fire,Grip=grip,SupportLocal=point,ForegripDistance=.8f};}
    static void Main()
    {
        var carried=new R23RoundState(new[]{"world-target"},new Sight(),true);carried.EnsureMagnumState("carried/magnum");int carriedRifle=carried.ReadReserve("mk18"),carriedPistol=carried.ReadReserve("magnum");
        var gallery=new R23GallerySession();Check(gallery.Show("cx4")&&gallery.Grab(),"CX4 creates actual source owner");
        Check(gallery.Definition.Id=="cx4"&&gallery.Loaded==25&&gallery.Reserve==-1,"CX4 actual25-round automatic preview, no carried pool");
        Check(!gallery.Step(.05f,Input(true),false,true,false),"CX4 grab held trigger blocked until neutral");gallery.Step(0,Input(),false,true,true);
        Check(gallery.Step(.01f,Input(true),false,true,false)&&gallery.Loaded==24,"CX4 fires actual native owner");
        int shots=gallery.PreviewShots;gallery.Step(.2f,Input(true),false,true,false);Check(gallery.PreviewShots>shots,"CX4 automatic hold uses native interval");
        gallery.Release();Check(!gallery.Live&&gallery.Grab()&&gallery.Loaded==25,"CX4 release retires private owner, regrab fresh source preview");
        Check(gallery.Show("px4")&&!gallery.Live&&gallery.Grab(),"PX4 switches actual private owner");Check(gallery.Loaded==16&&gallery.Reserve==1200,"PX4 finite private source1200 pistol reserve");
        gallery.Step(0,Input(),false,true,true);Check(gallery.Step(.01f,Input(true),false,true,false)&&gallery.Loaded==15,"PX4 native semi fires one round");
        gallery.Step(.25f,Input(true),false,true,false);Check(gallery.Loaded==15,"PX4 held trigger cannot repeat");
        gallery.Step(.25f,Input(false),false,true,false);Check(gallery.Step(.01f,Input(true),false,true,false)&&gallery.Loaded==14,"PX4 source below-fire-threshold release rearms without SPR-only .2 latch");
        gallery.Step(.25f,Input(),false,true,true);Check(gallery.Step(.01f,Input(true),false,true,false)&&gallery.Loaded==13,"PX4 full release rearms");
        gallery.Step(.25f,Input(),false,true,true);gallery.Step(0,Input(),true,true,true);Check(gallery.State.Value.Phase==ReloadPhase.NeedsMagazine,"PX4 real native eject stage");
        gallery.Step(0,Input(),true,true,true);Check(gallery.State.Value.Phase==ReloadPhase.NeedsMagazine,"PX4 VR click cannot bypass physical magazine reload");
        var definition=gallery.Definition;gallery.Step(0,Input(),false,true,true);gallery.Step(.01f,Input(false,true,definition.Offer),false,true,true);
        Check(gallery.State.Value.Phase==ReloadPhase.HeldMagazine,"PX4 actual offered magazine grip");
        gallery.Step(.01f,Input(false,true,definition.Insert),false,true,true);Check(gallery.State.Value.Phase==ReloadPhase.NeedsRack,"PX4 upward insertion uses production geometry");
        gallery.Step(.01f,Input(),false,true,true);gallery.Step(.01f,Input(false,true,definition.Charging),false,true,true);
        gallery.Step(.01f,Input(false,true,new Float3(definition.Charging.X,definition.Charging.Y,definition.Charging.Z-.08f)),false,true,true);
        Check(gallery.State.Value.Phase==ReloadPhase.Ready&&gallery.Loaded==16&&gallery.Reserve==1197,"PX4 real slide rack chambers, finite preview debits only fired rounds");
        Check(!gallery.Step(.25f,Input(true),false,false,false),"focus invalidation cannot fire private preview");gallery.Hide();Check(!gallery.Active&&!gallery.Live,"new profile hide cleans private live owner");
        Check(carried.ReadReserve("mk18")==carriedRifle&&carried.ReadReserve("magnum")==carriedPistol&&carried.ReadTarget("world-target").Health==100&&carried.ReadTarget("world-target").Shield==100,"new previews never mutate carried ammo or world targets");
        Console.WriteLine("PASS "+count+" actual CX4/PX4 private preview-owner cases");
    }
}
