#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Climbing.Engine;
using UnityEngine;
namespace Ashfall.Integration.ClimbingMotor.Gates
{
    // Callable only; coordinator serializes installed-editor execution. No callbacks.
    // This checks Unity's geometry conversion against the staged trajectory, NOT
    // angular PhysX collision clearance, approval, rendering or gameplay feel.
    public static class ClimbingR6TrajectoryGates
    {
        public static void RunSourceTrajectoryFixture()
        {
            Mesh mesh=null;
            try
            {
                mesh=ExplicitConvexContext.CreateSourceMesh();
                if(!ExplicitConvexContext.SourceMesh(mesh)||mesh.vertexCount!=168)throw new Exception("source hull identity");
                var vertices=mesh.vertices;
                var trajectory=new SourceHeadTrajectory(new Float3(1,2,3),new ConvexHeadPose{Qw=1},new Float3(2,.1f,-1),.7f);
                for(int i=0;i<vertices.Length;i++)for(int j=0;j<=40;j++)
                {
                    float phase=j*.05f;
                    var expected=new Vector3(1,2,3)+new Vector3(2,.1f,-1)*Mathf.Min(1,phase)+
                        Quaternion.AngleAxis(Mathf.Max(0,phase-1)*.7f*Mathf.Rad2Deg,Vector3.up)*vertices[i];
                    var actual=trajectory.Vertex(i,Math.Min(2,(double)phase));
                    if((expected-new Vector3(actual.X,actual.Y,actual.Z)).magnitude>2e-6f)throw new Exception("Unity trajectory conversion");
                }
            }
            finally{if(mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}}
        }
    }
}
#endif
