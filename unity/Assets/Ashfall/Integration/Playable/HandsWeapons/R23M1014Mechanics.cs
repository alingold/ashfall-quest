using System;
using Ashfall.Core;

namespace Ashfall.Modules.WeaponHandling
{
    // Disjoint source-specific extension. The existing native owner retains ammo,
    // authority/replay, reserve transfers, support and lifetime counters.
    public sealed partial class WeaponHandling
    {
        // Trusted presentation correction only; callers authenticate a current host
        // receipt first. This never creates a lifetime or consumes a reserve grant.
        internal bool ReconcileNetworkMagazine(int rounds,int localReloads=-1){if(retired||FireReservation!=null||rounds<0||rounds>(sourceMechanics==null?capacity:1)||revision==long.MaxValue)return false;
            if(sourceMechanics!=null){if(localReloads<0)return false;if(sourceMechanics.Loaded==rounds&&sourceMechanics.Reloads==localReloads)return true;sourceMechanics=sourceMechanics.Copy();sourceMechanics.Loaded=rounds;sourceMechanics.Reloads=localReloads;sourceMechanics.Held=false;sourceMechanics.Release=true;revision++;return true;}
            int current=phase==ReloadPhase.NeedsRack?seated:loaded;if(current==rounds)return true;
            Suspend();loaded=rounds;seated=0;chambered=rounds>0;spent=false;phase=rounds>0?ReloadPhase.Ready:mechanism==WeaponMechanism.MagnumRevolver?ReloadPhase.CylinderOpen:ReloadPhase.NeedsMagazine;pending=false;boltTravel=0;overrun=0;releaseTrigger=true;
            if(dt11Mode){boltTravel=rounds>0?0:.72f;dt11Live=(1<<rounds)-1;dt11Spent=0;dt11Swipe=false;dt11Flick.Reset();}
            if(m60){m60Stage=Ashfall.Integration.Playable.HandsWeapons.R24M60Stage.Ready;m60MotionStage=m60Stage;m60BeltFed=rounds>0;m60Contact=false;m60Progress=0;m60NeedsNeutral=true;seated=rounds;}
            revision++;return true;
        }
        internal WeaponHandling(string session,string actor,long epoch,IAmmoReserve reserve,IAuthorityGate authority,bool m1014)
            :this(session,actor,epoch,WeaponMechanism.ServiceRifle,reserve,authority)
        {if(!m1014)throw new ArgumentException("M1014 definition");capacity=6;interval=Ashfall.Integration.Playable.HandsWeapons.R23M1014Definition.Interval;releaseTrigger=true;}
        internal bool IsM1014 {get{return mechanism==WeaponMechanism.ServiceRifle&&capacity==6&&interval==Ashfall.Integration.Playable.HandsWeapons.R23M1014Definition.Interval;}}
        public bool R23M1014ReloadGripInProgress {get{return IsM1014&&contact;}}
        public void SuspendM1014Controls(){if(!IsM1014||retired)return;Suspend();overrun=0;revision++;}
        public RuleResult ApplyM1014(CommandContext command,HandlingAction action,HandlingInput input,bool ejectAfterShot=false)
        {
            if(!IsM1014||FireReservation!=null||!Enum.IsDefined(typeof(HandlingAction),action))return RuleResult.Reject("M1014 unavailable");
            long before=revision,shotBefore=shots;bool oldGrip=previousGrip,newTick=command.Tick!=tick;
            float oldRecoil=recoil,oldPitch=pitch,oldIdle=idle;int oldPattern=pattern;
            bool near=input.ForegripDistance<(supported?.45f:.25f)&&phase==ReloadPhase.Ready&&!(releaseGrip&&input.Grip);
            bool shotLatch=near&&latched;if(near&&GripToggle&&input.Grip&&!oldGrip)shotLatch=!shotLatch;
            bool shotSupported=input.SupportTracked&&!input.SupportReserved&&near&&(GripToggle?shotLatch:input.Grip);
            var native=input;if(action==HandlingAction.ReloadStep||action==HandlingAction.Eject)native.Trigger=false;
            // Calling Fire instead of Sample avoids generic rifle gesture/flick dispatch.
            var result=Apply(command,HandlingAction.Fire,native);
            if(revision==before||!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed)return result;
            float dt=newTick?input.DeltaSeconds:0,decay=(float)Math.Exp(-dt*9),recovered=oldRecoil*decay;
            float sourceIdle=oldIdle+dt;
            if(shots>shotBefore)
            {
                recoil=Math.Min(.13f,recovered+.075f*(shotSupported?.38f:1));pitch=(float)((shotSupported?2:3.5)*Math.PI/180);yaw=0;
                pattern=(sourceIdle>=.8f?0:oldPattern)+1;idle=0;
            }
            else
            {
                pitch=sourceIdle>.35f?oldPitch*decay:oldPitch;yaw=0;
                if(sourceIdle>=.8f){pattern=0;recoil=pitch=0;}
            }
            if(action==HandlingAction.Eject)result=Eject();
            else if(action==HandlingAction.ReloadStep)result=input.Desktop?DesktopStep():RuleResult.Reject("physical UP/BACK required");
            if(ejectAfterShot)Eject(); // source stick-down is evaluated after its held-trigger fire.
            if(!input.Desktop&&input.SupportTracked&&!input.SupportReserved)M1014Gesture(input,oldGrip);
            if(phase!=ReloadPhase.Ready){supported=false;latched=false;}
            return action==HandlingAction.Sample?RuleResult.Accept():result;
        }
        void M1014Gesture(HandlingInput input,bool oldGrip)
        {
            if(releaseGrip)return;
            var offer=new Float3(0,-.25f,.14f);var socket=new Float3(0,-.10f,.14f);var handle=new Float3(input.LeftHanded?-.06f:.06f,.045f,.10f);
            if(!input.Grip){contact=false;boltTravel=0;return;}
            if(!contact&&!oldGrip&&Reach(input.SupportLocal,phase==ReloadPhase.NeedsRack?handle:offer,.115f))
            {
                contact=phase==ReloadPhase.NeedsMagazine||phase==ReloadPhase.NeedsRack;
                if(phase==ReloadPhase.NeedsMagazine)grab=input.SupportLocal;else boltGrab=input.SupportLocal;
            }
            if(contact&&phase==ReloadPhase.NeedsMagazine&&input.SupportLocal.Y-grab.Y>=.10f&&Reach(input.SupportLocal,socket,.11f))
            {Grab(input.SupportLocal);if(phase==ReloadPhase.HeldMagazine)Insert();contact=false;}
            else if(contact&&phase==ReloadPhase.NeedsRack)
            {
                float pull=boltGrab.Z-input.SupportLocal.Z;boltTravel=Math.Max(0,Math.Min(.10f,pull));
                if(pull>=.085f){Cycle();contact=false;}
            }
        }
    }
    public sealed partial class WeaponLifetimes
    {
        public RuleResult CreateM1014(CommandContext command,string instanceId,IAmmoReserve reserve,IAuthorityGate authority,out WeaponHandling weapon)
        {
            weapon=null;if(!Valid(command,instanceId)||entries.ContainsKey(instanceId)||reserve==null||authority==null)return RuleResult.Reject("M1014 lifetime");
            var result=authority.Validate(command);if(!result.Accepted)return result;
            result=approval.AuthorizeNew(command,instanceId,WeaponMechanism.ServiceRifle,6);if(!result.Accepted)return result;
            weapon=new WeaponHandling(session,command.ActorId,command.AuthorityEpoch,reserve,authority,true);weapon.InitializeMagazine(6);weapon.RecordEnvelope(command);
            entries.Add(instanceId,new Entry{Active=weapon,Actor=command.ActorId,Epoch=command.AuthorityEpoch});return RuleResult.Accept();
        }
    }
}
