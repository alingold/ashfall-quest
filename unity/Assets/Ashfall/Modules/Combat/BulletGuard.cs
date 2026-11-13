using System;
using Ashfall.Core;
namespace Ashfall.Modules.Combat
{
    // Server adapter supplies inverse-transformed endpoints in an orthonormal shield
    // frame: +Z points towards the attacking face in Unity's reflected coordinates.
    // Finite source rectangle; no ellipse, infinite plane, rear, melee or area block.
    public sealed class BulletGuard
    {
        private readonly Float3 centre,right,up,front;
        private readonly float halfWidth,halfHeight;
        public BulletGuard(Float3 centre,Float3 right,Float3 up,Float3 front,float halfWidth,float halfHeight)
        {
            if(!centre.IsFinite||!right.IsFinite||!up.IsFinite||!front.IsFinite||!Finite(halfWidth)||!Finite(halfHeight)||halfWidth<=0||halfHeight<=0||Math.Abs(Dot(right,right)-1)>.001||Math.Abs(Dot(up,up)-1)>.001||Math.Abs(Dot(front,front)-1)>.001||Math.Abs(Dot(right,up))>.001||Math.Abs(Dot(front,up))>.001||Math.Abs(Dot(front,right))>.001)throw new ArgumentException("orthonormal guard");
            this.centre=centre;this.right=right;this.up=up;this.front=front;this.halfWidth=halfWidth;this.halfHeight=halfHeight;
        }
        public bool Intersects(Float3 origin,Float3 endpoint,out float fraction)
        {
            fraction=0;if(!origin.IsFinite||!endpoint.IsFinite)return false;
            double divisor=DifferenceDot(endpoint,origin,front);if(divisor>=-.00001)return false;
            double t=DifferenceDot(centre,origin,front)/divisor;if(t<0||t>1)return false;
            double x=(double)origin.X+(endpoint.X-(double)origin.X)*t-centre.X;
            double y=(double)origin.Y+(endpoint.Y-(double)origin.Y)*t-centre.Y;
            double z=(double)origin.Z+(endpoint.Z-(double)origin.Z)*t-centre.Z;
            if(Math.Abs(x*right.X+y*right.Y+z*right.Z)>halfWidth||Math.Abs(x*up.X+y*up.Y+z*up.Z)>halfHeight)return false;
            fraction=(float)t;return true;
        }
        private static bool Finite(float f){return !Single.IsNaN(f)&&!Single.IsInfinity(f);}
        private static double DifferenceDot(Float3 a,Float3 b,Float3 n){return ((double)a.X-b.X)*n.X+((double)a.Y-b.Y)*n.Y+((double)a.Z-b.Z)*n.Z;}
        private static double Dot(Float3 a,Float3 b){return (double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;}
    }
}

