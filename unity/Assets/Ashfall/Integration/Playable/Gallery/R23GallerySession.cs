using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Gallery
{
    public static class R23GalleryRules
    {
        public const float DockForward=1.10f,DockBelow=.16f,SourceYawDegrees=90,UnityYawDegrees=-90;
        public static bool CanGrab(float distance,float aimDot,bool tracked,bool reserved,bool focused) {
            return tracked&&!reserved&&focused&&!float.IsNaN(distance)&&!float.IsInfinity(distance)&&distance>=0&&distance<=2&&(distance<=.35f||!float.IsNaN(aimDot)&&aimDot>=.94f);
        }
        public static Float3 ViewUp(Float3 forward) {
            double size=Math.Sqrt((double)forward.X*forward.X+(double)forward.Y*forward.Y+(double)forward.Z*forward.Z);if(!forward.IsFinite||size<.000001)return new Float3(0,1,0);
            float x=(float)(forward.Z/size),z=(float)(-forward.X/size);float side=(float)Math.Sqrt(x*x+z*z);if(side<.001f){x=1;z=0;}else{x/=side;z/=side;}
            return new Float3((float)(forward.Y*z/size),(float)((forward.Z*x-forward.X*z)/size),(float)(-forward.Y*x/size));
        }
        public static Float3 DockOffset(Float3 forward) { var up=ViewUp(forward);return new Float3(forward.X*DockForward-up.X*DockBelow,forward.Y*DockForward-up.Y*DockBelow,forward.Z*DockForward-up.Z*DockBelow); }
    }
    // Source lobby_gallery_091 creates a new live097 instance on each grab. It has no
    // ammo_provider: reserve=-1 and take_reserve(requested)=requested, private to preview.
    public sealed class R23GallerySession
    {
        sealed class NoWorldDamage:IInteractionQuery { public bool HasLineOfSight(string actor,string owner,Float3 origin,Float3 point,float range){return false;} }
        sealed class PreviewReserve:IRevisionedAmmoReserve {
            long revision;public int Available{get{return -1;}}public long Revision{get{return revision;}}
            public int Take(int requested){if(requested<=0)return 0;revision++;return requested;}
            public int Refund(int rounds){if(rounds<=0)return 0;revision++;return rounds;}
        }
        readonly R23SemiTriggerLatch semi=new R23SemiTriggerLatch();
        readonly R23MagazineControls magazineControls=new R23MagazineControls();
        LocalRifleSandbox live;long generation;
        public bool Active { get; private set; }
        public SourceRifleDefinition Definition { get; private set; }
        public bool Live { get { return live!=null; } }
        public int PreviewShots { get; private set; }
        public int Loaded { get { return live==null?0:live.LoadedRounds; } }
        public int Reserve { get { return live==null?0:live.Reserve; } }
        public HandlingSnapshot? State { get { return live==null?(HandlingSnapshot?)null:live.Snapshot; } }
        public string InstanceId { get { return live==null?"":live.InstanceId; } }
        public bool Show(string id) { var definition=SourceRifleDefinition.ForId(id);if(definition==null)return false;if(Active&&Definition==definition)return true;Release();Definition=definition;Active=true;return true; }
        public bool Grab() { if(!Active||Live)return false;IRevisionedAmmoReserve privateReserve=Definition.Id=="px4"?(IRevisionedAmmoReserve)new R23RifleReserve(1200):new PreviewReserve();live=new LocalRifleSandbox(new string[0],new NoWorldDamage(),Definition,privateReserve,"lobby-live097/"+Definition.Id+"/"+(++generation));live.SuspendControls();semi.Suspend();magazineControls.Reset();return true; }
        public bool Release() { if(live==null)return false;live.Retire();live=null;semi.Suspend();magazineControls.Reset();return true; }
        public void Hide(){Release();Active=false;}
        public bool Step(float seconds,HandlingInput input,bool reload,bool focused,bool triggerFullyReleased) {
            if(!Active||live==null)return false;
            if(Definition.SourceManualControls||Definition.Id=="rfb")return StepSource(seconds,input,reload,0,0,focused,input.Desktop&&reload);
            if(Definition.VrOpeningOnly&&!input.Desktop&&live.Snapshot.Phase!=ReloadPhase.Ready)reload=false;
            if(Definition.Id=="spr"){semi.Sample(triggerFullyReleased);input.Trigger=semi.Allows(input.Trigger);}
            bool shot=live.Step(seconds,input,reload,!focused);
            if(shot){PreviewShots++;if(Definition.Id=="spr")semi.Shot();}return shot;
        }
        // Same source projector/owner as carried SMGs. Raw held click/stick enter
        // here; Eject stays distinct from the legacy desktop staged shortcut.
        public bool StepSource(float seconds,HandlingInput input,bool physicalRightClick,float stickX,float stickY,bool focused,bool desktopReload=false)
        {
            if(!Active||live==null)return false;
            bool eligible=focused&&input.Enabled&&!input.Paused&&input.PrimaryTracked&&!input.PrimaryClimbing&&!input.HeadObstructed;
            // catalog_firearm._poll_vr RFB override has click only, after firing.
            bool rfb=Definition.Id=="rfb";
            var action=magazineControls.Project(eligible,physicalRightClick,true,rfb?0:stickX,rfb?0:stickY,desktopReload);
            if(action==HandlingAction.ReloadStep)input.Desktop=true;
            if(rfb&&!input.Desktop&&action==HandlingAction.Eject)
            {
                bool fired=live.StepSource(seconds,input,HandlingAction.Sample,!focused);
                input.Trigger=false;live.StepSource(0,input,HandlingAction.Eject,!focused);if(fired)PreviewShots++;return fired;
            }
            bool shot=live.StepSource(seconds,input,action,!focused);if(shot)PreviewShots++;return shot;
        }
    }
}
