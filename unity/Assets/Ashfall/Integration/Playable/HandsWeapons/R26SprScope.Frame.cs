#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    internal sealed partial class R23SprScope
    {
        const float WindowScale=1.1f;
        readonly List<Mesh> ocularMeshes=new List<Mesh>();
        // Widen the rear opening without scaling the mount or front objective.
        // Outside the old aperture, halve the bezel measured from its new edge.
        static float OcularAxis(float value,float half)
        {
            float size=Mathf.Abs(value);return Mathf.Sign(value)*(size<=half?size*WindowScale:half*WindowScale+(size-half)*.5f);
        }
        void WidenOcular()
        {
            foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.name.StartsWith("ScopeHousing/")&&!filter.name.StartsWith("ScopeDarkInterior/")&&!filter.name.StartsWith("RearOcularGlass/"))continue;
                var mesh=filter.mesh;ocularMeshes.Add(mesh);var points=mesh.vertices;
                for(int i=0;i<points.Length;i++)
                {
                    var p=points[i];float weight=1-Mathf.InverseLerp(-.008f,.04f,p.z);
                    p.x=Mathf.Lerp(p.x,OcularAxis(p.x,R23SprScopeMath.Width*.5f),weight);
                    p.y=Mathf.Lerp(p.y,.103f+OcularAxis(p.y-.103f,R23SprScopeMath.Height*.5f),weight);points[i]=p;
                }
                mesh.vertices=points;mesh.RecalculateBounds();mesh.RecalculateNormals();
            }
        }
        void ReleaseOcularMeshes(){foreach(var mesh in ocularMeshes)if(mesh!=null)Destroy(mesh);ocularMeshes.Clear();}
    }
}
#endif
