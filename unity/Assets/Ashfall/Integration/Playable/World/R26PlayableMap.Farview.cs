using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.World
{
    public sealed partial class R23PlayableMap
    {
        // Opt-in per-map detail limits. Structures, players and all other maps keep their existing ranges.
        struct FarviewPolicy
        {
            internal string Map;internal float Distance,MaxDiameter,SourceRange;
            internal FarviewPolicy(string map,float distance,float diameter,float sourceRange){Map=map;Distance=distance;MaxDiameter=diameter;SourceRange=sourceRange;}
        }
        static readonly FarviewPolicy[] farviewPolicies={new FarviewPolicy("cyber_bird",300,0,170),new FarviewPolicy("valley",200,3,0)};
        readonly List<Renderer> farviewDetails=new List<Renderer>();GameObject farviewGeometry;float farviewDistance;
        internal bool FarviewEvidenceOverride=false;
        void PrepareFarview()
        {
            if(farviewGeometry==geometryRoot)return;farviewDetails.Clear();farviewGeometry=geometryRoot;farviewDistance=0;
            if(geometryRoot==null||manifest==null)return;FarviewPolicy policy=new FarviewPolicy();
            for(int i=0;i<farviewPolicies.Length;i++)if(farviewPolicies[i].Map==MapId){policy=farviewPolicies[i];break;}
            if(policy.Distance<=0)return;farviewDistance=policy.Distance;
            foreach(var renderer in geometryRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Only original static map chunks. No loot, avatars, destructible owners or generated fixtures.
                if(renderer.transform.parent!=geometryRoot.transform||!renderer.name.StartsWith("R23 chunk ",System.StringComparison.Ordinal))continue;
                int chunk;if(!System.Int32.TryParse(renderer.name.Substring(10),out chunk)||chunk<0||chunk>=manifest.chunks)continue;
                if(policy.SourceRange>0){if(manifest.chunkVisibilityRanges==null||manifest.chunkVisibilityRanges[chunk]!=policy.SourceRange||renderer.GetComponent<Collider>()!=null)continue;}
                else if(renderer.bounds.size.sqrMagnitude>policy.MaxDiameter*policy.MaxDiameter)continue;
                farviewDetails.Add(renderer);
            }
        }
        void LateUpdate()
        {
            if(!IsLoaded||!presentationEnabled||viewCamera==null||FarviewEvidenceOverride)return;
            ApplyFarview(viewCamera.transform.position,true);
        }
        internal void ApplyFarview(Vector3 eye,bool apply)
        {
            PrepareFarview();float distance=farviewDistance*farviewDistance;
            for(int i=0;i<farviewDetails.Count;i++)
            {
                var renderer=farviewDetails[i];if(renderer==null)continue;
                // Closest bounds point preserves the whole batch whenever any part is within range.
                renderer.forceRenderingOff=apply&&renderer.bounds.SqrDistance(eye)>distance;
            }
        }
    }
}
