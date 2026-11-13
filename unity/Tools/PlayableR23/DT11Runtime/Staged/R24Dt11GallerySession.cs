using System;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Gallery
{
    public sealed class R24Dt11GallerySession
    {
        sealed class Lease:IAuthorityGate{internal bool Active=true;public RuleResult Validate(CommandContext c){return Active&&c.IsAuthority&&c.SessionId==Session&&c.ActorId=="preview"?RuleResult.Accept():RuleResult.Reject("private DT11 preview lifetime");}}
        sealed class Reserve:IRevisionedAmmoReserve{long revision;public int Available{get{return -1;}}public long Revision{get{return revision;}}public int Take(int amount){if(amount<=0)return 0;revision++;return amount;}public int Refund(int amount){if(amount<=0)return 0;revision++;return amount;}}
        const string Session="r24-gallery-dt11";readonly R23MagazineControls controls=new R23MagazineControls();Lease lease;long generation,tick,sequence;public bool Active{get;private set;}public R24Dt11Owner Owner{get;private set;}public int PreviewShots{get;private set;}
        CommandContext C(){return new CommandContext(Session,"preview","preview/"+(++sequence),tick,0,true);}
        public void Show(){if(!Active){Release();Active=true;}}public bool Grab(){if(!Active||Owner!=null)return false;lease=new Lease();tick=sequence=0;PreviewShots=0;Owner=new R24Dt11Owner(C(),"lobby-live097/dt11/"+(++generation),new Reserve(),lease);Owner.SuspendControls();controls.Reset();return true;}
        public void Release(){if(Owner!=null)Owner.Retire();if(lease!=null)lease.Active=false;Owner=null;lease=null;controls.Reset();}public void Hide(){Release();Active=false;}
        public bool Step(float dt,HandlingInput input,bool primaryClick,bool focus,R23M1014Pose pose,out R24Dt11Volley volley,bool triggerBlocksFlick=false,bool desktopReload=false){volley=null;if(!Active||Owner==null)return false;if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");if(!focus||!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed){Owner.SuspendControls();controls.Reset();return false;}var action=controls.Project(true,primaryClick,true,0,0,desktopReload);input.Desktop=action==HandlingAction.ReloadStep;tick++;input.Sequence=sequence+1;input.DeltaSeconds=dt;Owner.Apply(C(),action,input,pose,out volley,triggerBlocksFlick);if(volley!=null)PreviewShots++;return volley!=null;}
    }
}
