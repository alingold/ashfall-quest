#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Diagnostics;using UnityEngine;
namespace Ashfall.Integration.Playable.World.Loot
{
    public sealed partial class R23WorldLoot
    {
        // Per frame and per pickup table: entries re-checked against viewer distance, and the
        // weapon visuals that may be built (a weapon visual is 15-60 renderers; the other
        // tables only toggle an existing view). 24 visits sweep 500 items in under 0.3 s at 72 Hz.
        const int CullVisits=24,CullBuilds=1;
        // In a LAN/online session the session's shared-state sync owns eligibility every frame.
        bool SessionOwnsEligibility{get{return sourceWorld!=null&&sourceWorld.LanSession!=null&&sourceWorld.LanSession.Lobby!=null;}}
        bool ExemptFromCulling(string id){return baselineEager.Contains(id);}
        void BindCulling(){weapons.WorldVisualExempt=ExemptFromCulling;if(explosives!=null)explosives.WorldViewExempt=ExemptFromCulling;}
        void StepCulling()
        {
            if(actor==null||weapons==null)return;R26PerfMarks.Begin(R26PerfMarks.Cull);
            R26PickupCulling.Viewer=actor.position;R26PickupCulling.HasViewer=true;
            weapons.CullWorldVisuals(CullVisits,CullBuilds);if(explosives!=null)explosives.CullWorldViews(CullVisits);if(foods!=null)foods.CullWorldViews(CullVisits);if(supplies!=null)supplies.CullWorldViews(CullVisits);
            R26PerfMarks.End(R26PerfMarks.Cull);
        }
    }
}
#endif
