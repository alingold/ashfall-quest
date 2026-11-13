using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Gallery;
class GalleryCases
{
 sealed class Sight:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return true;}}
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static HandlingInput Input(bool trigger){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=trigger,ForegripDistance=.5f};}
 static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
 static void Main(){
  var offset=R23GalleryRules.DockOffset(new Float3(0,0,1));Check(offset.X==0&&offset.Y==-.16f&&offset.Z==1.1f&&R23GalleryRules.UnityYawDegrees==-90,"source full-size gallery dock is1.1m ahead/.16below with reflected source90-degree yaw");
  var forward=new Float3(0,.5f,(float)Math.Sqrt(.75));offset=R23GalleryRules.DockOffset(forward);var up=R23GalleryRules.ViewUp(forward);
  Check(Math.Abs(Dot(offset,forward)-1.1f)<.00001f&&Math.Abs(Dot(offset,up)+.16f)<.00001f,"source upright gallery preserves view pitch and removes roll");
  Check(R23GalleryRules.CanGrab(.35f,-1,true,false,true)&&R23GalleryRules.CanGrab(2,.94f,true,false,true)&&!R23GalleryRules.CanGrab(2.01f,1,true,false,true)&&!R23GalleryRules.CanGrab(.36f,.939f,true,false,true),"source near-grip and two-metre remote-facing gallery reach limits");
  Check(!R23GalleryRules.CanGrab(.1f,1,false,false,true)&&!R23GalleryRules.CanGrab(.1f,1,true,true,true)&&!R23GalleryRules.CanGrab(.1f,1,true,false,false),"tracking, hand reservation and focus gate preview grab");
  var carried=new R23RoundState(new[]{"target/0"},new Sight(),true);int carriedAmmo=carried.Rifle.Snapshot.Loaded,rifleReserve=carried.ReadReserve("mk18"),sniperReserve=carried.ReadReserve("spr");
  var preview=new R23GallerySession();Check(preview.Show("mk18")&&preview.Grab()&&preview.Live&&preview.Loaded==18&&preview.Reserve==-1,"gallery097 creates independent live source MK18 with preview-only unlimited reserve");
  string first=preview.InstanceId;Check(!preview.Step(.12f,Input(true),false,true,false),"new gallery live instance requires trigger release before firing");preview.Step(0,Input(false),false,true,true);
  Check(preview.Step(.01f,Input(true),false,true,false),"gallery preview fires through actual native weapon owner");preview.Step(.08f,Input(false),false,true,true);preview.Step(.08f,Input(false),false,true,true);
  Check(preview.PreviewShots==3&&preview.Loaded==15,"MK18 inspection preserves committed three-round source burst");
  Check(preview.Show("mk18")&&preview.Loaded==15&&preview.InstanceId==first,"same viewed ID/status refresh cannot refill live preview");
  preview.Step(0,Input(false),true,true,true);preview.Step(0,Input(false),true,true,true);preview.Step(0,Input(false),true,true,true);
  Check(preview.Loaded==18&&preview.State.Value.Chambered&&preview.Reserve==-1,"gallery unlimited source reserve still uses real eject/insert/charge mechanics");
  Check(!preview.Step(.25f,Input(true),false,false,false)&&preview.Loaded==18,"blocked gallery cannot shoot or consume preview magazine");
  Check(preview.Release()&&!preview.Live&&!preview.State.HasValue&&preview.Grab()&&preview.Loaded==18&&preview.InstanceId!=first,"release retires live097; next grab creates fresh private instance, not carried refill");
  preview.Show("akm");preview.Grab();preview.Step(0,Input(false),false,true,true);for(int i=0;i<50;i++)preview.Step(.1f,Input(true),false,true,false);
  Check(preview.Loaded==0&&preview.State.Value.Phase==ReloadPhase.NeedsMagazine,"gallery AKM uses source automatic20-round magazine and dry/empty state");
  preview.Step(0,Input(false),true,true,true);preview.Step(0,Input(false),true,true,true);Check(preview.Loaded==20&&preview.Reserve==-1,"AKM preview-only reload does not need or touch carried rifle pool");
  preview.Show("spr");preview.Grab();preview.Step(0,Input(false),false,true,true);preview.Step(.16f,Input(true),false,true,false);int shots=preview.PreviewShots;
  for(int i=0;i<10;i++)preview.Step(.1f,Input(true),false,true,false);
  preview.Step(.1f,Input(false),false,true,false);preview.Step(.1f,Input(true),false,true,false);
  Check(preview.Loaded==3&&preview.PreviewShots==shots,"gallery SPR holds semi-auto latch through held and intermediate trigger travel");
  preview.Step(.1f,Input(false),false,true,true);Check(preview.Step(.1f,Input(true),false,true,false)&&preview.Loaded==2,"source SPR preview rearms after full release");
  preview.Hide();Check(!preview.Active&&!preview.Live&&!preview.Step(.25f,Input(true),false,true,false),"hidden gallery owns no live firing state");
  Check(carried.Rifle.Snapshot.Loaded==carriedAmmo&&carried.ReadReserve("mk18")==rifleReserve&&carried.ReadReserve("spr")==sniperReserve&&carried.ReadTarget("target/0").Health==100&&carried.ReadTarget("target/0").Shield==100,"gallery firing/reloading/releasing changes no carried ammunition, reserve or world target health");
 }
}
