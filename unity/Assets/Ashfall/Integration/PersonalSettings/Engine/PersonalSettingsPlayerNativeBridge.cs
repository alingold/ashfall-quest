#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
namespace Ashfall.Integration.PersonalSettings.Engine
{
    public static class PersonalSettingsPlayerNativeBridge
    {
        // Driver must already have coordinator-bound lifecycle and settings authority.
        // Never assign its public Settings/ownership directly to bypass admission.
        public static RuleResult Publish(UnityPlayerDriver driver,CommandContext context,long generation,long revision,EffectiveSettings snapshot)
        {
            if(driver==null||snapshot==null)throw new ArgumentNullException();
            var port=new TypedPlayerSettingsPort(snapshot);
            return driver.PublishAuthorizedSettings(context,generation,revision,snapshot.CreatePlayerTuning(),port,
                snapshot.LeftHanded?DominantHand.Left:DominantHand.Right,
                snapshot.Text("sprint_binding")=="left_grip"?SprintBinding.LeftGrip:SprintBinding.LeftClick);
        }
    }
}
#endif
