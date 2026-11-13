using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Player;
using Ashfall.Modules.Presentation;
namespace Ashfall.Integration.PersonalSettings
{
    // Explicit trusted host fanout. Owns no Health outbox, authority, clock or storage
    // callbacks. Root supplies every committed transition and separately attributes
    // outgoing receipts after its real Combat authorization.
    public sealed class CommittedPresentationBinding : IPresentationStateSource
    {
        readonly int thread=Thread.CurrentThread.ManagedThreadId;
        readonly PresentationStateBinding state;
        readonly Locomotion motor;
        readonly Dictionary<string,TargetFeedbackBinding> targets=new Dictionary<string,TargetFeedbackBinding>(StringComparer.Ordinal);
        readonly IPersonalAudioPort audio;
        public CommittedPresentationBinding(PresentationStateBinding state,Locomotion motor,IPersonalAudioPort audio)
        {if(state==null||motor==null||audio==null)throw new ArgumentNullException();this.state=state;this.motor=motor;this.audio=audio;}
        void Lane(){if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Presentation host lane");}
        public void BindTarget(string canonicalTarget,TargetFeedbackBinding feedback)
        {Lane();if(String.IsNullOrEmpty(canonicalTarget)||feedback==null)throw new ArgumentException("target binding");targets.Add(canonicalTarget,feedback);}
        public void ObserveCommitted(HealthTransition transition)
        {
            Lane();if(transition==null||transition.After==null)return;
            state.OnHealthTransition(transition);
            TargetFeedbackBinding target;
            if(targets.TryGetValue(transition.After.Id,out target))target.ObserveTransition(transition);
        }
        public void AuthorizeLocalOutgoing(HealthTransition exactCommittedReceipt)
        {
            Lane();if(exactCommittedReceipt==null||exactCommittedReceipt.After==null)return;
            TargetFeedbackBinding target;
            if(targets.TryGetValue(exactCommittedReceipt.After.Id,out target))target.OnAuthorizedOutgoingTransition(exactCommittedReceipt);
        }
        // Explicit replay only after durable settings commit/context publication.
        // Failure remains visible to caller; never a live preview or storage callback.
        public void PublishAudio(EffectiveSettings committed){Lane();PersonalAudioPublication.Apply(committed,audio);}
        public HudInput Capture()
        {Lane();state.Stamina=motor.SprintStamina;state.StaminaMaximum=motor.StaminaMaximum;return state.Capture();}
    }
}
