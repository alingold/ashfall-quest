#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using UnityEngine;
namespace Ashfall.Modules.Climbing.Engine
{
    // Deliberately bounded native candidate. Exact source head versus actual box
    // obstacles; the head is never replaced by a box/sphere. Other obstacle types
    // (including convex cooked meshes) refuse until a certified provider exists.
    public sealed class UnityBoxHullDistance:ISourceHullDistance
    {
        readonly IUnityConvexContext context;
        public UnityBoxHullDistance(IUnityConvexContext context){if(context==null)throw new ArgumentNullException("context");this.context=context;}
        public bool Approved {get{return context.Approved&&context.Validate();}}
        public long Revision {get{return context.Validate()?context.Revision:-1;}}
        public bool TryLowerBound(SourceHeadTrajectory path,double phase,out double metres)
        {
            metres=0;if(path==null||!context.Validate())return false;long revision=context.Revision;
            double minimum=1000;
            var hull=new Float3[path.VertexCount];for(int i=0;i<hull.Length;i++)hull[i]=path.Vertex(i,phase);
            foreach(var go in context.QueryScene.GetRootGameObjects())foreach(var c in go.GetComponentsInChildren<Collider>(true))
            {
                if(!c.enabled||!c.gameObject.activeInHierarchy||c==context.HeadCollider)continue;
                ConvexBodyIdentity id;
                if(c.isTrigger||!context.Resolve(c,out id)||(c.gameObject.layer!=8&&c.gameObject.layer!=9))return false;
                var box=c as BoxCollider;if(box==null)return false;
                if(box.size.x<=0||box.size.y<=0||box.size.z<=0||box.size.magnitude>1000)return false;
                if(!Finite(box.center.x)||!Finite(box.center.y)||!Finite(box.center.z)||box.center.magnitude>1000)return false;
                var m=box.transform.localToWorldMatrix;
                if(Math.Abs(m.m03)>100000||Math.Abs(m.m13)>100000||Math.Abs(m.m23)>100000)return false;
                var points=new double[8,3];
                for(int i=0;i<8;i++)
                {
                    double x=(double)box.center.x+((i&1)==0?-.5:.5)*box.size.x;
                    double y=(double)box.center.y+((i&2)==0?-.5:.5)*box.size.y;
                    double z=(double)box.center.z+((i&4)==0?-.5:.5)*box.size.z;
                    for(int k=0;k<3;k++)
                    {double v=m[k,0]*x+m[k,1]*y+m[k,2]*z+m[k,3];if(Double.IsNaN(v)||Math.Abs(v)>100000)return false;points[i,k]=v;}
                }
                double best=0;
                // Any separating unit axis supplies a valid lower bound. Missing
                // SAT axes can only lose clearance, never falsely certify it.
                for(int a=0;a<6;a++)
                {
                    double x=a<3?(a==0?1:0):m[0,a-3],y=a<3?(a==1?1:0):m[1,a-3],z=a<3?(a==2?1:0):m[2,a-3];
                    double n=Math.Sqrt(x*x+y*y+z*z);if(n<.99||n>1.01)return false;x/=n;y/=n;z/=n;
                    double hmin=Double.MaxValue,hmax=-Double.MaxValue,bmin=Double.MaxValue,bmax=-Double.MaxValue;
                    foreach(var p in hull){double d=p.X*x+p.Y*y+p.Z*z;hmin=Math.Min(hmin,d);hmax=Math.Max(hmax,d);}
                    for(int i=0;i<8;i++){double d=points[i,0]*x+points[i,1]*y+points[i,2]*z;bmin=Math.Min(bmin,d);bmax=Math.Max(bmax,d);}
                    best=Math.Max(best,Math.Max(bmin-hmax,hmin-bmax));
                }
                // 10 cm reserve dominates float transform/projection error inside
                // the declared 100 km/1 km bounds, including native box arithmetic.
                // It sacrifices near-contact availability; it is not penetration recovery.
                minimum=Math.Min(minimum,Math.Max(0,best-.1));
            }
            if(!context.Validate()||context.Revision!=revision)return false;metres=minimum;return true;
        }
        static bool Finite(float f){return !Single.IsNaN(f)&&!Single.IsInfinity(f);}
    }
}
#endif
