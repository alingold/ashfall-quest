#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Value copy of the real adapter's last poses/cumulative decisions. It does
    // not carry transforms, mutable collision owners, or gameplay authority.
    [Serializable]public struct R24KnifeDiagnostics
    {
        public bool Usable,HadSweep,ProbeEnabled,ProbeConvex,ProbeTrigger,OwnerHistory,OwnerRetired;
        public int OwnerFrames,OwnerSuspensions;public string OwnerInstance,OwnerStatus,OwnerLastSuspension;
        public int CreatedQueryShapes,ReleasedQueryShapes,LiveQueryShapes,PeakQueryShapes;
        public int EmptyProbeBounds,SweptContacts,TouchingContacts,RetainedContacts,StepEntries;public Vector3 SweptProbeExtents,TouchingProbeExtents,RetainedProbeExtents;
        public int MeshBakes;public Vector3 ProbeScale,ProbeBoundsCentre,ProbeBoundsExtents;
        public int Queries,Broadphase,Ignored,Semantic,PenetrationTests,Penetrations,Surface,SurfaceFailures,HeadBlocked,BladeBlocked,Visible,Entries,Applied,HullBuilds,HullFailures,HullVertices,HullTriangles,InitialContacts;
        public Vector3 Base,Tip,Head,QueryCentre;public string CandidateId;public float Travel,BladeSpeed,HandSpeed;
    }
}
#endif
