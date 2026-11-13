using System;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Gallery {
 public sealed class R24AwpGallerySession {
  sealed class Lease:IAuthorityGate {internal bool Active=true;public RuleResult Validate(CommandContext c){return Active&&c.IsAuthority&&c.SessionId==Session&&c.ActorId=="preview"?RuleResult.Accept():RuleResult.Reject("private AWP preview lifetime");}}
  // Source lobby live097 has no ammo_provider: reserve=-1, take/refund=requested.
  // This exists only inside the independent gallery and cannot grant carried ammo.
  sealed class Reserve:IRevisionedAmmoReserve {long revision;public int Available{get{return -1;}}public long Revision{get{return revision;}}public int Take(int n){if(n<=0)return 0;revision++;return n;}public int Refund(int n){if(n<=0)return 0;revision++;return n;}}
  const string Session="r24-gallery-awp";readonly R23MagazineControls controls=new R23MagazineControls();Lease lease;long generation,tick,sequence;
  public bool Active{get;private set;}public R24AwpOwner Owner{get;private set;}public int PreviewShots{get;private set;}
  CommandContext C(){return new CommandContext(Session,"preview","preview/"+(++sequence),tick,0,true);}
  public void Show(){if(!Active){Release();Active=true;}}public bool Grab(){if(!Active||Owner!=null)return false;lease=new Lease();tick=sequence=0;PreviewShots=0;Owner=new R24AwpOwner(C(),"lobby-live097/awp/"+(++generation),new Reserve(),lease);controls.Reset();return true;}
  public void Release(){if(Owner!=null)Owner.Retire();if(lease!=null)lease.Active=false;Owner=null;lease=null;controls.Reset();}public void Hide(){Release();Active=false;}
  public bool Step(float dt,HandlingInput input,bool physicalRightClick,float stickX,float stickY,bool focused,R23ShotPose pose,out R24AwpShot shot,bool desktopReload=false){shot=null;if(!Active||Owner==null)return false;if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");bool valid=focused&&input.Enabled&&!input.Paused&&input.PrimaryTracked&&!input.PrimaryClimbing&&!input.HeadObstructed;var action=controls.Project(valid,physicalRightClick,true,stickX,stickY,desktopReload);if(!valid){Owner.SuspendControls();return false;}input.Desktop=action==HandlingAction.ReloadStep;tick++;input.Sequence=sequence+1;input.DeltaSeconds=dt;Owner.Apply(C(),action,input,pose,out shot);if(shot!=null)PreviewShots++;return shot!=null;}
 }
}
