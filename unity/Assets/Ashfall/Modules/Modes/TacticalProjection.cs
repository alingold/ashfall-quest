using System;
using Ashfall.Core;

namespace Ashfall.Modules.Modes
{
    public struct MapPoint
    {
        public readonly double X,Y;
        public MapPoint(double x,double y) { X=x; Y=y; }
    }
    public sealed class TacticalProjection
    {
        private readonly Float3 unityOrigin;
        private readonly double minX,minGodotZ,width,depth;
        // Bounds are immutable Godot X/Z map metadata; origin is ALREADY reflected Unity SI.
        public TacticalProjection(Float3 unityOrigin,double minX,double minGodotZ,double maxX,double maxGodotZ)
        {
            if(!unityOrigin.IsFinite||!Finite(minX)||!Finite(minGodotZ)||!Finite(maxX)||!Finite(maxGodotZ)||maxX<=minX||maxGodotZ<=minGodotZ||!Finite(maxX-minX)||!Finite(maxGodotZ-minGodotZ)) throw new ArgumentException("map bounds");
            this.unityOrigin=unityOrigin; this.minX=minX; this.minGodotZ=minGodotZ; width=maxX-minX; depth=maxGodotZ-minGodotZ;
        }
        public MapPoint Project(Float3 unityWorld,bool clamp)
        {
            if(!unityWorld.IsFinite) throw new ArgumentException("world point");
            double x=((double)unityWorld.X-unityOrigin.X-minX)/width;
            double y=(-((double)unityWorld.Z-unityOrigin.Z)-minGodotZ)/depth;
            return new MapPoint(clamp?Clamp(x):x,clamp?Clamp(y):y);
        }
        public MapPoint Pixel(Float3 unityWorld)
        { MapPoint uv=Project(unityWorld,true); return new MapPoint(310+650*uv.X,100+650*uv.Y); }
        public static MapPoint Heading(Float3 unityForward)
        {
            if(!unityForward.IsFinite) throw new ArgumentException("forward");
            double length=Math.Sqrt((double)unityForward.X*unityForward.X+(double)unityForward.Z*unityForward.Z);
            return length<.00001 ? new MapPoint(0,0) : new MapPoint(unityForward.X/length,-unityForward.Z/length);
        }
        // Preserves source draw policy: alive opponents visible; downed opponents hidden.
        public static bool MarkerVisible(ModeActor viewer,ModeActor target,int teams,bool hasPose)
        { return !target.Spectator&&(target.Id==viewer.Id||hasPose)&&(target.Life=="alive"||RoundController.Allied(viewer,target,teams)); }
        private static double Clamp(double n) { return Math.Max(0,Math.Min(1,n)); }
        private static bool Finite(double n) { return !Double.IsNaN(n)&&!Double.IsInfinity(n); }
    }
}
