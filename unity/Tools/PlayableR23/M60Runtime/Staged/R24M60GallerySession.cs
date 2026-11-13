using System;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Gallery {
 public sealed class R24M60GallerySession {
  sealed class Lease:IAuthorityGate{internal bool Active=true;public RuleResult Validate(CommandContext c){return Active&&c.IsAuthority&&c.SessionId==Session&&c.ActorId=="preview"?RuleResult.Accept():RuleResult.Reject("private M60 preview lifetime");}}
  // Source live097 has no ammo_provider; this supply is private and never carried.
  sealed class Reserve:IRevisionedAmmoReserve{long rev;public int Available{get{return -1;}}public long Revision{get{return rev;}}public int Take(int n){if(n<=0)return 0;rev++;return n;}public int Refund(int n){if(n<=0)return 0;rev++;return n;}}
  const string Session="r24-gallery-m60";readonly R23MagazineControls controls=new R23MagazineControls();Lease lease;long generation,tick,sequence;
  public bool Active{get;private set;}public R24M60Owner Owner{get;private set;}public int PreviewShots{get;private set;}
  CommandContext C(){return new CommandContext(Session,"preview","preview/"+(++sequence),tick,0,true);}
  public void Show(){if(!Active){Release();Active=true;}}public bool Grab(){if(!Active||Owner!=null)return false;lease=new Lease();tick=sequence=0;PreviewShots=0;Owner=new R24M60Owner(C(),"lobby-live097/m60/"+(++generation),new Reserve(),lease);controls.Reset();return true;}
  public void Release(){if(Owner!=null)Owner.Retire();if(lease!=null)lease.Active=false;Owner=null;lease=null;controls.Reset();}public void Hide(){Release();Active=false;}
  public bool Step(float dt,HandlingInput input,bool physicalRightClick,float x,float y,bool focus,R23ShotPose pose,out R24M60Shot shot,bool desktop=false){shot=null;if(!Active||Owner==null)return false;if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");bool valid=focus&&input.Enabled&&!input.Paused&&input.PrimaryTracked&&!input.PrimaryClimbing&&!input.HeadObstructed;var action=controls.Project(valid,physicalRightClick,true,x,y,desktop);if(!valid){Owner.SuspendControls();return false;}input.Desktop=action==HandlingAction.ReloadStep;tick++;input.Sequence=sequence+1;input.DeltaSeconds=dt;Owner.Apply(C(),action,input,pose,out shot);if(shot!=null)PreviewShots++;return shot!=null;}
 }
}
