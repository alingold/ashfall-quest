using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23RoundState
    {
        readonly Dictionary<string,R24SmawRuntime> smaws=new Dictionary<string,R24SmawRuntime>();
        public R24SmawRuntime Smaw{get{R24SmawRuntime owner;return smaws.TryGetValue(selectedRifle=="smaw"?selectedAction:"smaw",out owner)?owner:null;}}
        public R24SmawRuntime ReadSmawInstance(string actionId){R24SmawRuntime owner;return actionId!=null&&smaws.TryGetValue(actionId,out owner)?owner:null;}
        public void AdvanceSmawWorld(float dt,bool paused){if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");if(paused||Health<=0)return;now+=dt;AdvanceLegacyPracticeTargets(dt);}
        public SourceLaunchDescriptor StepSmaw(float dt,HandlingInput input,R23ShotPose pose,bool clear,bool desktopReload,bool paused){if(!sourceLoadout||selectedRifle!="smaw"||Smaw==null)return null;AdvanceSmawWorld(dt,paused);input.Paused|=paused||Health<=0;pendingShot=false;return Smaw.Step(dt,input,pose,clear,desktopReload);}
    }
}
