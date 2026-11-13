#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;
using Ashfall.Modules.Player;using Ashfall.Modules.WeaponHandling;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.Input;using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;using UnityEditor;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static void GalleryTracerFrame98254(R23PlayableLoop loop,int primary,float grip,float trigger,bool dual)
  {
   var l=new HandSample{Tracked=true,Grip=primary==0?grip:dual?grip:0,Trigger=primary==0?trigger:dual?trigger:0};
   var r=new HandSample{Tracked=true,Grip=primary==1?grip:dual?grip:0,Trigger=primary==1?trigger:dual?trigger:0};
   loop.Rig.SmokeTrackedHands(l,r);loop.StepItemInput(.02f,l,r,true,true,false,true);
  }
  static object GalleryProperty(object value,string name){var p=value.GetType().GetProperty(name,Fields);Need(p!=null,"Actual gallery property "+name);return p.GetValue(value,null);}
  static void GalleryFeedbackParts(R23PlayableLoop loop,string profile,int primary,out Transform model,out Transform muzzle,out Transform anchor,out object feedback)
  {
   var gallery=loop.Gallery;
   if(profile=="uzis"||profile=="matadors"){
    var presentation=(R24DualPresentation)((Array)Read(gallery,"dualGallery")).GetValue(profile=="uzis"?0:1);var view=presentation.View;int side=0;
    model=((Transform[])Read(view,"models"))[side];muzzle=((Transform[])Read(view,"muzzles"))[side];anchor=((Transform[])Read(view,"flashAnchors"))[side];feedback=((Array)Read(view,"hud")).GetValue(side);return;
   }
   if(profile=="dt11"||profile=="awp"||profile=="m60"){
    model=(Transform)Read(gallery,profile+"Model");var view=Read(gallery,profile+"View");muzzle=(Transform)GalleryProperty(view,"Muzzle");anchor=profile=="m60"?(Transform)GalleryProperty(view,"Flash"):muzzle;feedback=Read(gallery,profile=="dt11"?"dt11Feedback":profile+"GunFeedback");return;
   }
   if(profile=="magnum"||profile=="m1014"){
    var view=Read(gallery,profile+"Model");model=(Transform)GalleryProperty(view,"HandArtModel");muzzle=(Transform)GalleryProperty(view,"Muzzle");
   }else{var view=((IDictionary)Read(gallery,"models"))[profile];model=(Transform)Read(view,"Model");muzzle=(Transform)Read(view,"Muzzle");}
   anchor=muzzle;feedback=Read(gallery,"previewGunFeedback");
  }
  static void GalleryPublishedPose(R23PlayableLoop loop,string profile,int primary,Transform model,Transform muzzle)
  {
   if(profile=="uzis"||profile=="matadors")return;
   Vector3 source;Transform neutral;R23WeaponFeedbackState feedback=null;float scale=1;
   if(profile=="magnum"){var view=Read(loop.Gallery,"magnumModel");source=Vector(R23MagnumRuntime.SourceMuzzle);scale=R23MagnumRuntime.ModelScale;neutral=(Transform)Read(view,"Root");feedback=(R23WeaponFeedbackState)Read(view,"feedback");}
   else if(profile=="m1014"){source=Vector(R23M1014Definition.Muzzle);neutral=(Transform)Read(Read(loop.Gallery,"m1014Model"),"Root");}
   else if(profile=="dt11"){source=Vector(R24Dt11Definition.Muzzle);scale=R24Dt11Definition.Scale;neutral=(Transform)Read(loop.Gallery,"dt11Root");feedback=(R23WeaponFeedbackState)Read(loop.Gallery,"dt11Recoil");}
   else if(profile=="awp"){source=Vector(R24AwpDefinition.Muzzle);neutral=(Transform)Read(loop.Gallery,"awpRoot");feedback=(R23WeaponFeedbackState)Read(loop.Gallery,"awpFeedback");}
   else if(profile=="m60"){source=Vector(R24M60Definition.Muzzle);neutral=(Transform)Read(loop.Gallery,"m60Root");feedback=(R23WeaponFeedbackState)Read(loop.Gallery,"m60Feedback");}
   else{var view=((IDictionary)Read(loop.Gallery,"models"))[profile];source=(Vector3)Read(view,"sourceMuzzle");neutral=(Transform)Read(view,"Root");scale=((SourceRifleDefinition)Read(view,"Definition")).ModelScale;feedback=(R23WeaponFeedbackState)Read(view,"profileFeedback");}
   Need(Vector3.Distance(muzzle.position,model.TransformPoint(source))<.0001f,"Published muzzle is on actual source barrel, including recoil/scale "+profile);
   var boreRotation=model.rotation*(profile=="awp"?Quaternion.Euler(2.5f,0,0):Quaternion.identity);Need(Quaternion.Angle(muzzle.rotation,boreRotation)<.05f,"Published muzzle follows source barrel orientation "+profile);
   if(feedback!=null){R23ShotPose expected,shot;var q=neutral.rotation;R23WeaponPoseMath.Resolve(new R23ShotPose(Float(neutral.position),Float(q*Vector3.right),Float(q*Vector3.up),Float(q*Vector3.forward)),Float(source),scale,feedback.Distance,feedback.PitchRadians,feedback.YawRadians,R23WeaponPoseMath.UprightPulse(profile),out expected,out shot);Need(Vector3.Distance(model.position,Vector(expected.Origin))<.0001f&&Quaternion.Angle(model.rotation,Quaternion.LookRotation(Vector(expected.Forward),Vector(expected.Up)))<.05f,"Actual carried recoil resolver drives private preview "+profile);Need(feedback.AcceptedShots>0&&feedback.Distance>0,"Actual accepted shot reaches render feedback "+profile);}
  }
  static Ashfall.Core.Float3 Float(Vector3 v){return new Ashfall.Core.Float3(v.x,v.y,v.z);}static Vector3 Vector(Ashfall.Core.Float3 v){return new Vector3(v.X,v.Y,v.Z);}
  static IEnumerator GalleryFeedbackC1(R23PlayableLoop loop,string profile,Action<string> note,int onlyHand=-1)
  {
   string[] profiles={"mk18","akm","spr","cx4","px4","ump","p90","mp5","mp9","1911","tec9","rfb","fal","s85","magnum","m1014","dt11","awp","m60","uzis","matadors"};Need(Array.IndexOf(profiles,profile)>=0,"Declared firearm feedback profile");
   note("GALLERYFEEDBACK C1 actual Root entry "+profile);var settings=loop.UI.Settings;bool dual=profile=="uzis"||profile=="matadors";
   try{
    loop.ReturnToLobby();
    if(onlyHand>=0){loop.UI.OpenWeapons();Need(loop.UI.BrowseWeapon(profile),"Actual pre-cycle browse");loop.UI.ToggleMenu();note("Actual UI departure Page="+loop.UI.Page+" active="+loop.Gallery.Active+" UIviewed="+(loop.UI.CurrentViewedWeaponId??"<null>")+" GalleryViewed="+(loop.Gallery.ViewedWeaponId??"<null>"));Need(!loop.Gallery.Active&&loop.UI.CurrentViewedWeaponId==null,"Actual UI departure clears viewed-profile publication");}
    foreach(int primary in onlyHand>=0?new[]{onlyHand}:new[]{0,1}){
     loop.UI.SetSettings(settings.With(dominantHand:primary));Call(loop,"ApplySettings",loop.UI.Settings);loop.Gameplay.HolsterCurrent();loop.Gameplay.SetPreferredHandWhenHolstered(primary);loop.UI.OpenWeapons();Need(loop.UI.BrowseWeapon(profile),"Actual selector browse "+profile);
     float end=Time.realtimeSinceStartup+60;while(!loop.Gallery.IsWeaponReady(profile)&&loop.Gallery.WeaponFailure(profile)==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Gallery.IsWeaponReady(profile),"Actual source gallery ready "+loop.Gallery.WeaponFailure(profile));
     string inventory=String.Join("|",loop.Gameplay.OwnedWeaponActionIds);var carried=new Dictionary<string,int>();var reserves=new Dictionary<string,int>();foreach(string id in loop.Gameplay.OwnedWeaponActionIds){carried[id]=loop.Gameplay.ReadWeaponState(id).Loaded;reserves[id]=loop.Gameplay.ReadReserve(id);}float hp=loop.Gameplay.Health,shield=loop.Gameplay.Shield;
     var hand=primary==0?loop.Rig.Left:loop.Rig.Right;var other=primary==0?loop.Rig.Right:loop.Rig.Left;var dock=(Vector3)Read(loop.Gallery,"dockPosition");hand.SetPositionAndRotation(dock,Quaternion.Inverse(R23TrackedGripPose.LocalRotation));other.SetPositionAndRotation(dock+Vector3.right*.7f,Quaternion.Inverse(R23TrackedGripPose.LocalRotation));
     for(int i=0;i<4;i++)GalleryTracerFrame98254(loop,primary,0,0,dual);
     note("Before fresh grip primary="+primary+" actualGameplayHand="+loop.Gameplay.PrimaryHand+" holder="+loop.Gallery.HoldingHand+" ready="+loop.Gallery.IsWeaponReady(profile)+" dockDistance="+Vector3.Distance(dock,hand.position));
     GalleryTracerFrame98254(loop,primary,1,0,dual);note("After fresh grip profile="+profile+" primary="+primary+" active="+loop.Gallery.Active+" UIviewed="+loop.UI.CurrentViewedWeaponId+" galleryViewed="+loop.Gallery.ViewedWeaponId+" holder="+loop.Gallery.HoldingHand+" live="+loop.Gallery.ViewState.Live+" previousGrip="+String.Join(",",(bool[])Read(loop.Gallery,"previousGrip")));Need(loop.Gallery.HoldingHand==primary&&loop.Gallery.ViewState.Live,"Actual fresh grip owns preview "+profile+"/"+primary);for(int i=0;i<8;i++)GalleryTracerFrame98254(loop,primary,1,0,dual);
     int shots=loop.Gallery.ViewState.PreviewShots;for(int i=0;i<8&&loop.Gallery.ViewState.PreviewShots==shots;i++)GalleryTracerFrame98254(loop,primary,1,1,dual);Need(loop.Gallery.ViewState.PreviewShots>shots,"Actual private owner accepts shot "+profile+"/"+primary);
     loop.Gallery.RefreshTracerPresentation();Transform model,muzzle,anchor;object feedback;GalleryFeedbackParts(loop,profile,primary,out model,out muzzle,out anchor,out feedback);var flash=(Transform)Read(feedback,"flash");Need((bool)GalleryProperty(feedback,"FlashVisible")&&flash.gameObject.activeInHierarchy,"Actual carried muzzle-flash renderer active "+profile);
     int accepted=loop.Gallery.ViewState.PreviewShots,ammo=loop.Gallery.ViewState.Ammo;var bindings=(IDictionary)Read(loop.Gallery,"galleryTracerBindings");var far=new Dictionary<LineRenderer,Vector3>();foreach(DictionaryEntry e in bindings){var line=(LineRenderer)e.Key;if(line.enabled)far.Add(line,line.GetPosition(1));}Need(far.Count>0,"Accepted visible tracers");float maxMove=0;
     for(int pose=0;pose<3;pose++){
      Vector3 old=muzzle.position;loop.Rig.transform.position+=new Vector3(.08f,0,.02f);hand.position+=new Vector3(.035f,.015f,0);hand.rotation=Quaternion.Euler(4,7,-3)*hand.rotation;other.position+=new Vector3(-.02f,.01f,.025f);other.rotation=Quaternion.Euler(-3,-5,2)*other.rotation;Vector3 raw=hand.position,otherRaw=other.position;Quaternion q=hand.rotation,oq=other.rotation;
      loop.Gallery.RefreshTracerPresentation();GalleryPublishedPose(loop,profile,primary,model,muzzle);maxMove=Mathf.Max(maxMove,Vector3.Distance(old,muzzle.position));
      foreach(DictionaryEntry e in bindings){var line=(LineRenderer)e.Key;if(!far.ContainsKey(line))continue;Vector3 at;Need(line.enabled&&loop.Gallery.TryReadDisplayedTracerMuzzle((int)Read(e.Value,"PhysicalHand"),out at)&&Vector3.Distance(line.GetPosition(0),at)<.0001f,"Late trace follows actual published recoil muzzle "+profile);Need(line.GetPosition(1)==far[line],"Accepted endpoint unchanged");}
      Need(flash.parent==anchor&&Vector3.Distance(flash.position,anchor.position)<.0001f&&Quaternion.Angle(flash.rotation,anchor.rotation)<.05f,"Flash follows same displayed source anchor");Need(raw==hand.position&&otherRaw==other.position&&q==hand.rotation&&oq==other.rotation,"No raw tracked pose writes");Need(accepted==loop.Gallery.ViewState.PreviewShots&&ammo==loop.Gallery.ViewState.Ammo,"Late renderer never ticks owner/ammo");
     }
     Need(maxMove>.01f,"Actual late tracked emitter moved");foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material!=null)foreach(var msg in ShaderUtil.GetShaderMessages(material.shader))Need(msg.severity.ToString()!="Error",msg.message);foreach(var msg in ShaderUtil.GetShaderMessages(flash.GetComponent<Renderer>().sharedMaterial.shader))Need(msg.severity.ToString()!="Error",msg.message);
     Need(inventory==String.Join("|",loop.Gameplay.OwnedWeaponActionIds)&&hp==loop.Gameplay.Health&&shield==loop.Gameplay.Shield,"Private preview preserves actual carried inventory/Health");foreach(string id in carried.Keys)Need(loop.Gameplay.ReadWeaponState(id).Loaded==carried[id]&&loop.Gameplay.ReadReserve(id)==reserves[id],"Actual carried ammo/reserve unchanged");
     for(int i=0;i<6;i++)GalleryTracerFrame98254(loop,primary,1,0,dual);yield return new WaitForSecondsRealtime(.09f);loop.Gallery.RefreshTracerPresentation();Need(!(bool)GalleryProperty(feedback,"FlashVisible"),"Actual flash expires");foreach(var line in far.Keys)Need(!line.enabled,"Actual map-duration trace expires");
     GalleryTracerFrame98254(loop,primary,0,0,dual);loop.Gallery.RefreshTracerPresentation();Need(!(bool)GalleryProperty(feedback,"FlashVisible"),"Release hides flash");foreach(var line in far.Keys)Need(!line.enabled,"Release hides traces");loop.UI.ToggleMenu();note("Actual UI departure Page="+loop.UI.Page+" active="+loop.Gallery.Active+" UIviewed="+(loop.UI.CurrentViewedWeaponId??"<null>")+" GalleryViewed="+(loop.Gallery.ViewedWeaponId??"<null>"));Need(!loop.Gallery.Active&&loop.UI.CurrentViewedWeaponId==null,"Actual UI closes preview between hand cases");note("Actual "+profile+" hand="+primary+" privateShot/flash/recoil/3lateposes/expiry/release PASS; carried IDs-ammo-Health/rawtracking retained; desktop only");
    }
   }finally{loop.Gallery.Hide();loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.ReturnToLobby();}
  }
  static IEnumerator GalleryTracer98254(R23PlayableLoop loop,string profile,Action<string> note)
  {
   Need(Array.IndexOf(new[]{"mk18","cx4","magnum","m1014","dt11","awp","m60","uzis","matadors"},profile)>=0,"Declared gallery tracer profile");
   note("GALLERYTRACER98254 actual Root gallery profile="+profile+" entered");var settings=loop.UI.Settings;bool dual=profile=="uzis"||profile=="matadors";
   try{
    loop.ReturnToLobby();
    foreach(int primary in new[]{0,1}){
     loop.UI.SetSettings(settings.With(dominantHand:primary));Call(loop,"ApplySettings",loop.UI.Settings);loop.Gameplay.HolsterCurrent();loop.Gameplay.SetPreferredHandWhenHolstered(primary);loop.UI.OpenWeapons();Need(loop.UI.BrowseWeapon(profile),"Actual selector browse "+profile);
     float end=Time.realtimeSinceStartup+60;while(!loop.Gallery.IsWeaponReady(profile)&&loop.Gallery.WeaponFailure(profile)==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Gallery.IsWeaponReady(profile),"Actual source gallery ready "+loop.Gallery.WeaponFailure(profile));
     string inventory=String.Join("|",loop.Gameplay.OwnedWeaponActionIds);var carried=new Dictionary<string,int>();var reserves=new Dictionary<string,int>();foreach(string id in loop.Gameplay.OwnedWeaponActionIds){carried[id]=loop.Gameplay.ReadWeaponState(id).Loaded;reserves[id]=loop.Gameplay.ReadReserve(id);}float hp=loop.Gameplay.Health,shield=loop.Gameplay.Shield;
     var hand=primary==0?loop.Rig.Left:loop.Rig.Right;var other=primary==0?loop.Rig.Right:loop.Rig.Left;var dock=(Vector3)Read(loop.Gallery,"dockPosition");hand.SetPositionAndRotation(dock,Quaternion.Inverse(R23TrackedGripPose.LocalRotation));other.SetPositionAndRotation(dock+Vector3.right*.7f,Quaternion.Inverse(R23TrackedGripPose.LocalRotation));
     GalleryTracerFrame98254(loop,primary,0,0,dual);GalleryTracerFrame98254(loop,primary,1,0,dual);Need(loop.Gallery.HoldingHand==primary&&loop.Gallery.ViewState.Live,"Actual fresh grip owns preview");for(int i=0;i<8;i++)GalleryTracerFrame98254(loop,primary,1,0,dual);
     int shots=loop.Gallery.ViewState.PreviewShots,ammo=loop.Gallery.ViewState.Ammo;note("Before actual fire hand="+primary+" live="+loop.Gallery.ViewState.Live+" phase="+(loop.Gallery.ViewState.NativeState.HasValue?loop.Gallery.ViewState.NativeState.Value.Phase.ToString():"dual")+" ammo="+ammo+" shots="+shots);
     for(int i=0;i<8&&loop.Gallery.ViewState.PreviewShots==shots;i++)GalleryTracerFrame98254(loop,primary,1,1,dual);
     Need(loop.Gallery.ViewState.PreviewShots>shots,"Actual private owner accepted a shot "+profile+"/"+primary);
     var bindings=(IDictionary)Read(loop.Gallery,"galleryTracerBindings");Need(bindings.Count>0,"Actual accepted shot bound real trace renderers");loop.Gallery.RefreshTracerPresentation();var far=new Dictionary<LineRenderer,Vector3>();float largestPreRefresh=0;
     foreach(DictionaryEntry entry in bindings){var line=(LineRenderer)entry.Key;if(!line.enabled)continue;int physical=(int)Read(entry.Value,"PhysicalHand");Vector3 muzzle=Vector3.zero;Need(loop.Gallery.TryReadDisplayedTracerMuzzle(physical,out muzzle)&&Vector3.Distance(line.GetPosition(0),muzzle)<.0001f,"Post-recoil trace starts at actual rendered emitter");far.Add(line,line.GetPosition(1));foreach(var msg in ShaderUtil.GetShaderMessages(line.sharedMaterial.shader))Need(msg.severity.ToString()!="Error",msg.message);}
     Need(far.Count>0,"At least one actual visible accepted tracer");
     for(int pose=0;pose<3;pose++){
      loop.Rig.transform.position+=new Vector3(.08f,0,.02f);hand.position+=new Vector3(.035f,.015f,0);hand.rotation=Quaternion.Euler(4,7,-3)*hand.rotation;other.position+=new Vector3(-.02f,.01f,.025f);other.rotation=Quaternion.Euler(-3,-5,2)*other.rotation;Physics.SyncTransforms();
      Vector3 raw=hand.position,otherRaw=other.position;Quaternion q=hand.rotation,oq=other.rotation;
      var oldStarts=new Dictionary<LineRenderer,Vector3>();foreach(var line in far.Keys)oldStarts.Add(line,line.GetPosition(0));
      loop.Gallery.RefreshTracerPresentation();foreach(DictionaryEntry entry in bindings){var line=(LineRenderer)entry.Key;if(!far.ContainsKey(line))continue;Vector3 muzzle=Vector3.zero;Need(line.enabled&&loop.Gallery.TryReadDisplayedTracerMuzzle((int)Read(entry.Value,"PhysicalHand"),out muzzle)&&Vector3.Distance(line.GetPosition(0),muzzle)<.0001f,"Late tracked movement anchors actual trace start");largestPreRefresh=Mathf.Max(largestPreRefresh,Vector3.Distance(oldStarts[line],muzzle));Need(line.GetPosition(1)==far[line],"Accepted far endpoint is byte-identical");}
      Need(raw==hand.position&&otherRaw==other.position&&q==hand.rotation&&oq==other.rotation,"Presentation does not write raw tracked hands");
     }
     Need(largestPreRefresh>.01f,"Actual translated/rotated emitter exposes old world-fixed start");Need(inventory==String.Join("|",loop.Gameplay.OwnedWeaponActionIds)&&hp==loop.Gameplay.Health&&shield==loop.Gameplay.Shield,"Private preview leaves carried inventory and Health intact");foreach(string id in carried.Keys)Need(loop.Gameplay.ReadWeaponState(id).Loaded==carried[id]&&loop.Gameplay.ReadReserve(id)==reserves[id],"Private shot leaves actual carried ammo/reserve intact");
     note("Actual "+profile+" primary="+primary+" shotDelta="+(loop.Gallery.ViewState.PreviewShots-shots)+" traces="+far.Count+" 3lateposes oldOriginResidual="+largestPreRefresh+"m -><.1mm; acceptedends/tracking/carriedHealth-ammo retained.");
     if(profile=="mk18"&&primary==0){yield return new WaitForSecondsRealtime(.09f);loop.Gallery.RefreshTracerPresentation();foreach(var line in far.Keys)Need(!line.enabled,"Original short tracer lifetime expires");}
     GalleryTracerFrame98254(loop,primary,0,0,dual);loop.Gallery.RefreshTracerPresentation();foreach(var line in far.Keys)Need(!line.enabled,"Actual release hides old owned traces");loop.Gallery.Hide();
    }
   }finally{loop.Gallery.Hide();loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.ReturnToLobby();}
  }
 }
}
#endif
