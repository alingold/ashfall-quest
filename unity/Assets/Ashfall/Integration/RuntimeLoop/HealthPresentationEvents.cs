using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Audio;
using Ashfall.Modules.Presentation;
namespace Ashfall.Integration.RuntimeLoop
{
    // Coordinator supplies approved cue/position and local HUD context. Never derived from client input.
    public interface IHealthPresentationMap
    { RuleResult Map(HealthTransition transition,out HealthPresentation presentation); }
    public sealed class HealthPresentation
    {
        public string Cue,Subject;public int Variant;public Float3 Position;public bool Spatial,Weapon;
        public float Gain=1,Pitch=1,Damage;public HudInput Hud;public bool Paused;
    }
    // Retained bounded delivery receipts. Exhaustion fails closed; never evict unacknowledged IDs.
    // External playback must be trusted/nonthrowing. Crash durability/listening/rendering remain open.
    public sealed class HealthPresentationEvents : IRuntimeEvents
    {
        sealed class Receipt {internal bool Audio,Hud;internal HealthPresentation Plan;}
        readonly AudioRouter audio;readonly HudModel hud;readonly IHealthPresentationMap map;readonly int capacity;
        readonly Dictionary<string,Receipt> receipts=new Dictionary<string,Receipt>(StringComparer.Ordinal);
        public HealthPresentationEvents(AudioRouter audio,HudModel hud,IHealthPresentationMap map,int capacity)
        {if(audio==null||hud==null||map==null||capacity<1)throw new ArgumentException("presentation ports");this.audio=audio;this.hud=hud;this.map=map;this.capacity=capacity;}
        public RuleResult Deliver(HealthTransition e)
        {
            if(e==null||e.After==null||String.IsNullOrEmpty(e.SessionId)||e.Sequence<1)return RuleResult.Reject("event identity");
            string key=e.SessionId.Length.ToString(CultureInfo.InvariantCulture)+":"+e.SessionId+":"+e.AuthorityEpoch.ToString(CultureInfo.InvariantCulture)+":"+e.Sequence.ToString(CultureInfo.InvariantCulture);
            Receipt r;if(!receipts.TryGetValue(key,out r)) {
                if(receipts.Count>=capacity)return RuleResult.Reject("presentation receipt capacity");
                HealthPresentation p;var result=map.Map(e,out p);if(!result.Accepted)return result;
                if(p==null||!p.Position.IsFinite||float.IsNaN(p.Gain)||p.Gain<0||p.Gain>1||float.IsNaN(p.Pitch)||p.Pitch<.1f||p.Pitch>3||float.IsNaN(p.Damage)||float.IsInfinity(p.Damage)||p.Damage<0)return RuleResult.Reject("presentation plan");
                r=new Receipt{Plan=p};receipts.Add(key,r);
            }
            if(!r.Audio){var p=r.Plan;if(p.Cue!=null&&!audio.Once(key,p.Cue,p.Subject,p.Weapon,p.Variant,p.Position,p.Spatial,p.Gain,p.Pitch))return RuleResult.Reject("audio paused or externally consumed identity");r.Audio=true;}
            if(!r.Hud){var p=r.Plan;if(p.Hud!=null){hud.Update(p.Hud,p.Paused,0);hud.Damage(p.Damage);}r.Hud=true;}
            return RuleResult.Accept();
        }
    }
}
