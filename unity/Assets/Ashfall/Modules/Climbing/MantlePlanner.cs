using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    // player.gd try_start_release_mantle/_support_hit, release_mantle.gd plan_grazing.
    // Pure preflight: refusal never translates the root or collider.
    public static class MantlePlanner
    {
        public static bool TryPlan(IClimbCollision world, Float3 root, Float3 head, Float3 anchor,
            float standingEyeHeight, float floorNormalY, out Float3[] route)
        {
            route = null;
            if (world==null || !V.WorldPoint(root) || !V.WorldPoint(head) || !anchor.IsFinite || !V.Within(head,root,4) ||
                !V.Finite(standingEyeHeight) || standingEyeHeight<.55f || standingEyeHeight>2.2f ||
                !V.Finite(floorNormalY) || floorNormalY<0 || floorNormalY>1) return false;
            SurfaceHit support;
            bool supported=Support(world,root,head,floorNormalY,out support);
            if(supported && head.Y-support.Point.Y<=.065f)
                return Standing(world,root,head,support,standingEyeHeight,out route);
            if(TryGrazing(world,root,head,anchor,standingEyeHeight,floorNormalY,out route)) return true;
            // The 65mm band gates optional recovery, never the original valid support.
            return supported && Standing(world,root,head,support,standingEyeHeight,out route);
        }
        private static bool TryGrazing(IClimbCollision world,Float3 root,Float3 head,Float3 anchor,
            float standingEyeHeight,float floorNormalY,out Float3[] route)
        {
            route=null; SurfaceHit support;
            if (!V.Within(anchor,head,1)) return false;
            Float3 toward = new Float3(anchor.X-head.X,0,anchor.Z-head.Z);
            if (V.Length(toward)<.001f) toward=new Float3(0,0,1);
            toward=V.Mul(toward,1/V.Length(toward));
            float[] reaches={.0125f,.025f,.0375f,.05f,.0625f,.075f,.0875f,.1f,.112f,.115f};
            foreach(float reach in reaches)
            for(int sample=0;sample<32;sample++)
            {
                int step=(sample+1)/2;
                double angle=step*Math.PI/16*(sample%2==1 ? 1 : -1);
                Float3 direction=new Float3((float)(toward.X*Math.Cos(angle)+toward.Z*Math.Sin(angle)),0,
                    (float)(toward.Z*Math.Cos(angle)-toward.X*Math.Sin(angle)));
                Float3 inward=V.Mul(direction,Math.Min(reach+.003f,.115f));
                Float3 point=V.Add(head,inward);
                SurfaceHit hit;
                if (!world.Raycast(V.Add(point,V.Up(.018f)),V.Add(point,V.Up(-.08f)),QueryKind.Structure,out hit) ||
                    !Valid(hit,floorNormalY) || hit.Point.Y>head.Y+.0025f || head.Y-hit.Point.Y>.065f) continue;
                Float3 lift=V.Up(Math.Max(.006f,hit.Point.Y-head.Y+.006f));
                if (!world.HeadMotionClear(head,lift) || !world.HeadMotionClear(V.Add(head,lift),inward)) continue;
                Float3 offset=V.Add(lift,inward), movedRoot=V.Add(root,offset), movedHead=V.Add(head,offset);
                Float3[] standing;
                if (!Support(world,movedRoot,movedHead,floorNormalY,out support) ||
                    !Standing(world,movedRoot,movedHead,support,standingEyeHeight,out standing)) continue;
                route=new Float3[]{lift,inward,standing[0]};
                return true;
            }
            return false;
        }
        private static bool Valid(SurfaceHit h,float floor) { return h.Point.IsFinite && h.Normal.IsFinite && h.Normal.Y>=floor; }
        private static bool Support(IClimbCollision w,Float3 root,Float3 head,float floor,out SurfaceHit hit)
        {
            return w.Raycast(V.Add(head,V.Up(.015f)),new Float3(head.X,Math.Min(root.Y-3,head.Y-3),head.Z),QueryKind.Support,out hit)
                && Valid(hit,floor) && hit.Point.Y<=head.Y+.0025f;
        }
        // Current world eye/root separation, not remembered standing calibration.
        public static float MeasuredEyeHeight(Float3 root, Float3 head)
        { return Math.Max(.55f,Math.Min(2.2f,head.Y-root.Y)); }
        private static bool Standing(IClimbCollision w,Float3 root,Float3 head,SurfaceHit hit,float height,out Float3[] route)
        {
            route=null;
            float rise=hit.Point.Y-root.Y;
            if(rise<.02f || rise>MeasuredEyeHeight(root,head)+.0025f+.005f || !w.HeadMotionClear(head,V.Up(rise))) return false;
            route=new Float3[]{V.Up(rise)};
            return true;
        }
    }
}
