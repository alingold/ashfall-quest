using System;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    // Exact precision_rifle.gd dispatch over the existing native precision owner.
    // Native Apply owns identity/replay/clock, finite ammo and source recoil.
    public sealed partial class WeaponHandling
    {
        public bool AwpReloadGripInProgress {get{return mechanism==WeaponMechanism.PrecisionRifle&&(contact||phase==ReloadPhase.HeldMagazine);}}
        public bool AwpLastShotSupported {get;private set;}
        public long AwpCasingEvents {get;private set;}
        public long AwpMagazineEvents {get;private set;}
        public void SuspendAwpControls(){if(mechanism!=WeaponMechanism.PrecisionRifle||retired)return;Suspend();overrun=0;revision++;}
        public RuleResult ApplyAwp(CommandContext command,HandlingAction action,HandlingInput input)
        {
            if(mechanism!=WeaponMechanism.PrecisionRifle||FireReservation!=null||!Enum.IsDefined(typeof(HandlingAction),action))return RuleResult.Reject("AWP unavailable");
            long before=revision,shotBefore=shots;bool oldGrip=previousGrip;
            bool near=input.ForegripDistance<(supported?.45f:.25f)&&phase==ReloadPhase.Ready&&!(releaseGrip&&input.Grip),shotLatch=near&&latched;
            if(near&&GripToggle&&input.Grip&&!oldGrip)shotLatch=!shotLatch;
            bool shotSupport=input.SupportTracked&&!input.SupportReserved&&near&&(GripToggle?shotLatch:input.Grip);
            var sample=input;if(action==HandlingAction.ReloadStep&&input.Desktop)sample.Trigger=false;
            // Source AWP trigger is sampled BEFORE click/stick-down. Fire rather than
            // Sample suppresses only the generic offer/bolt gesture and primary flick.
            var result=Apply(command,HandlingAction.Fire,sample);
            if(revision==before||!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed)return result;
            if(shots>shotBefore)AwpLastShotSupported=shotSupport;
            if(action==HandlingAction.Eject){result=AwpRequestReload(input);}
            else if(action==HandlingAction.ReloadStep)result=input.Desktop?AwpDesktop(input):RuleResult.Reject("physical BACK/UP/BACK required");
            if(!input.Desktop&&input.SupportTracked&&!input.SupportReserved)AwpGesture(input,oldGrip);
            if(phase!=ReloadPhase.Ready){supported=false;latched=false;}
            return action==HandlingAction.Sample?RuleResult.Accept():result;
        }
        RuleResult AwpRequestReload(HandlingInput input)
        {
            if(input.SupportReserved||contact||phase!=ReloadPhase.Ready&&phase!=ReloadPhase.NeedsBolt)return RuleResult.Reject("AWP reload unavailable");
            pending=true;chambered=false;releaseTrigger=true;releaseGrip=input.Grip;boltTravel=0;
            var accepted=Eject();if(accepted.Accepted)cooldown=.12f;return accepted; // Request exposes bolt; no refund yet.
        }
        RuleResult AwpCycle()
        {
            if(spent){AwpCasingEvents++;spent=false;}
            var result=Cycle();if(result.Accepted&&phase==ReloadPhase.NeedsMagazine)AwpMagazineEvents++;return result;
        }
        RuleResult AwpDesktop(HandlingInput input)
        {
            if(input.SupportReserved)return RuleResult.Reject("support unavailable");
            if(phase==ReloadPhase.Ready){var r=AwpRequestReload(input);return r.Accepted?AwpCycle():r;}
            if(phase==ReloadPhase.NeedsMagazine){Grab(Ashfall.Integration.Playable.HandsWeapons.R24AwpDefinition.Offer);if(phase==ReloadPhase.HeldMagazine)Insert();return RuleResult.Accept();}
            if(phase==ReloadPhase.HeldMagazine){Insert();return RuleResult.Accept();}
            return phase==ReloadPhase.NeedsBolt?AwpCycle():RuleResult.Reject("AWP desktop stage");
        }
        void AwpGesture(HandlingInput input,bool oldGrip)
        {
            if(releaseGrip)return;bool fresh=input.Grip&&!oldGrip;var d=Ashfall.Integration.Playable.HandsWeapons.R24AwpDefinition.Offer;
            if(phase==ReloadPhase.NeedsMagazine&&fresh&&Reach(input.SupportLocal,d,.25f))Grab(input.SupportLocal);
            else if(phase==ReloadPhase.HeldMagazine){if(!input.Grip)SetStage(ReloadPhase.NeedsMagazine);else if(input.SupportLocal.Y-grab.Y>=.06f&&Reach(input.SupportLocal,Ashfall.Integration.Playable.HandsWeapons.R24AwpDefinition.Insert,.25f))Insert();}
            else if(phase==ReloadPhase.NeedsBolt)
            {
                if(fresh&&!contact&&AwpBoltReach(input.SupportLocal)){contact=true;boltGrab=input.SupportLocal;}
                if(!input.Grip){contact=false;boltTravel=0;}
                if(contact&&input.Grip){float pull=boltGrab.Z-input.SupportLocal.Z;boltTravel=Math.Max(0,Math.Min(.115f,pull));if(pull>=.115f)AwpCycle();}
            }
        }
        public static bool AwpBoltReach(Float3 point)
        {
            if(!point.IsFinite)return false;var c=Ashfall.Integration.Playable.HandsWeapons.R24AwpDefinition.Bolt;
            float x=point.X-c.X,y=point.Y-c.Y,z=point.Z-c.Z;if(x*x+y*y+z*z>.42f*.42f)return false;
            return x*x/(.24f*.24f)+y*y/(.20f*.20f)+z*z/(z>0?.30f*.30f:.22f*.22f)<=1;
        }
    }
}
