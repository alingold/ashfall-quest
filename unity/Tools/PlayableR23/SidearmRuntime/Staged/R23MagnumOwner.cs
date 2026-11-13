using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    // Additive source adapter. Native Magnum loaded/reserve/stages remain authoritative.
    // No fields or duplicate ammunition counters are added to WeaponHandling.
    public sealed partial class WeaponHandling
    {
        public void SuspendR23MagnumControls(){if(mechanism==WeaponMechanism.MagnumRevolver&&!retired){Suspend();overrun=0;revision++;}}
        public bool R23MagnumReloadGripInProgress {get{return mechanism==WeaponMechanism.MagnumRevolver&&contact&&(phase==ReloadPhase.CylinderOpen||phase==ReloadPhase.CylinderLoaded);}}
        public RuleResult ApplyR23Magnum(CommandContext command,HandlingInput input,bool openRequested,bool desktopReload,bool triggerBlocksFlick)
        {
            if(mechanism!=WeaponMechanism.MagnumRevolver||FireReservation!=null)return RuleResult.Reject("Magnum unavailable");
            long before=revision;bool oldGrip=previousGrip;
            // Native Apply validates/time-steps/fire/debits once. Fire dispatch suppresses
            // generic unscaled reload geometry; source .84 fitted reach follows below.
            var result=Apply(command,HandlingAction.Fire,input);
            if(revision==before||!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed)return result;
            // R23 _poll_vr fires before processing its opening controls.
            if(openRequested&&phase==ReloadPhase.Ready)OpenCylinder();
            bool free=input.SupportTracked&&!input.SupportReserved;
            if(desktopReload){if(phase==ReloadPhase.Ready||free&&!releaseGrip)RevolverStep();}
            else if(!input.Desktop&&free)R23MagnumGesture(input,oldGrip);
            // Flick trigger threshold is >.2; firing/rearm uses >.65 independently.
            var flickInput=input;flickInput.Trigger=triggerBlocksFlick;PrimaryFlick(flickInput);
            if(phase!=ReloadPhase.Ready){supported=false;latched=false;}
            return RuleResult.Accept();
        }
        void R23MagnumGesture(HandlingInput input,bool oldGrip)
        {
            if(releaseGrip)return;bool fresh=input.Grip&&!oldGrip;
            if(!input.Grip)contact=false;
            else if(fresh&&!contact&&SourceRifleReach(input.SupportLocal,RevolverPoint(input.LeftHanded,phase==ReloadPhase.CylinderOpen),.18f,.84f)){
                if(phase==ReloadPhase.CylinderOpen||phase==ReloadPhase.CylinderLoaded){contact=true;grab=input.SupportLocal;}
            }
            if(!contact||!input.Grip)return;
            if(phase==ReloadPhase.CylinderOpen&&input.SupportLocal.Z-grab.Z>=.06f&&SourceRifleReach(input.SupportLocal,RevolverPoint(input.LeftHanded,false),.18f,.84f)){LoadCylinder();contact=false;}
            else if(phase==ReloadPhase.CylinderLoaded&&(input.SupportLocal.X-grab.X>=.05f||input.SupportLocal.Y-grab.Y>=.05f))CloseCylinder();
        }
    }
}
