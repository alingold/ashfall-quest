#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Player;
using UnityEngine;
namespace Ashfall.Integration.AvatarPose
{
    // Explicit root installation only. No static activation or automatic rig discovery.
    // Decorates the actual motor; LateUpdate samples physical transforms after Update and feedback.
    public sealed class NativePoseScheduler : MonoBehaviour, IDisposable
    {
        UnityPlayerDriver driver;
        NativePoseBinding.Provider provider;
        IExclusivePlayerMotor original;
        ObservedMotor observed;
        bool terminal;
        public bool FirstPerson=true;
        public void Install(UnityPlayerDriver source,NativePoseBinding.Provider binding)
        {
            if(terminal||observed!=null||!isActiveAndEnabled||source==null||binding==null||source.Motor==null||!binding.MatchesDriver(source))
                throw new InvalidOperationException("Explicit active source/motor/provider required");
            driver=source;provider=binding;original=source.Motor;
            observed=new ObservedMotor(original,provider);driver.Motor=observed;
            driver.PoseSourceInvalidated+=Dispose;
        }
        // Invoke synchronously after actual Session membership publication, even when no motor runs.
        public bool MembershipPublished(){return !terminal&&provider!=null&&provider.RefreshLifetime();}
        void LateUpdate()
        {
            if(terminal||provider==null)return;
            if(driver==null||!System.Object.ReferenceEquals(driver.Motor,observed)||!provider.RefreshLifetime()){Dispose();return;}
            // Every skipped/failed movement frame hides. Pause alone retains live physical poses.
            if(observed.Frame==Time.frameCount?observed.Success:driver.Paused)
                provider.Pump(Time.unscaledDeltaTime,FirstPerson);
            else provider.Hide();
        }
        public void Dispose()
        {
            if(terminal)return;terminal=true;
            if(driver!=null){driver.PoseSourceInvalidated-=Dispose;if(System.Object.ReferenceEquals(driver.Motor,observed))driver.Motor=original;}
            if(provider!=null)provider.Dispose();
        }
        void OnDisable(){Dispose();}
        void OnDestroy(){Dispose();}
        sealed class ObservedMotor:IExclusivePlayerMotor
        {
            readonly IExclusivePlayerMotor actual;readonly NativePoseBinding.Provider binding;
            public int Frame=-1;public bool Success;
            public ObservedMotor(IExclusivePlayerMotor actual,NativePoseBinding.Provider binding){this.actual=actual;this.binding=binding;}
            public PlayerMotorResult Apply(RootMotionProposal proposal,float seconds)
            {
                Frame=Time.frameCount;Success=false;binding.Hide();
                var result=actual.Apply(proposal,seconds);
                Success=result.Complete&&result.Velocity.IsFinite&&result.AppliedDisplacement.IsFinite&&
                    (!result.HasPhaseFeedback||(result.AppliedPhysicalTranslation.IsFinite&&result.AppliedPivotCorrection.IsFinite));
                return result;
            }
        }
    }
}
#endif
