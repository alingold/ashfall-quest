using System;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // MP-RETICLE: non-physics aim volumes for collider-free remote avatars. These are the
    // three spheres the host's ApplyShot tests against the tracked head (head .17 m; chest
    // .29 m at -.45 m; hip .27 m at -.92 m), so the reticle settles where host hits land.
    // Pure, allocation-free arithmetic; callers bound the number of avatars by Capacity.
    public static class R26RemoteAimVolumes
    {
        public const int Capacity=16;
        // Nearest entry distance along a unit ray, or +Infinity. A ray starting inside a
        // sphere does not hit it (matches the host SphereRay).
        public static float Nearest(float ox,float oy,float oz,float dx,float dy,float dz,float hx,float hy,float hz)
        {
            float best=SphereEntry(ox,oy,oz,dx,dy,dz,hx,hy,hz,.17f);
            best=Math.Min(best,SphereEntry(ox,oy,oz,dx,dy,dz,hx,hy-.45f,hz,.29f));
            return Math.Min(best,SphereEntry(ox,oy,oz,dx,dy,dz,hx,hy-.92f,hz,.27f));
        }
        public static float SphereEntry(float ox,float oy,float oz,float dx,float dy,float dz,float cx,float cy,float cz,float radius)
        {
            // Measure the perpendicular miss directly so km-long rays do not subtract two nearly equal squared distances.
            double x=(double)cx-ox,y=(double)cy-oy,z=(double)cz-oz;
            double u=dx,v=dy,w=dz,lengthSquared=u*u+v*v+w*w;
            if(!(lengthSquared>0))return Single.PositiveInfinity;
            double projection=(x*u+y*v+z*w)/lengthSquared;
            double px=x-u*projection,py=y-v*projection,pz=z-w*projection;
            double inside=(double)radius*radius-px*px-py*py-pz*pz;
            if(!(inside>=0))return Single.PositiveInfinity;
            double entry=projection*Math.Sqrt(lengthSquared)-Math.Sqrt(inside);
            return entry>=0?(float)entry:Single.PositiveInfinity;
        }
    }
}
