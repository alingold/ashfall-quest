#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using UnityEngine;
namespace Ashfall.Integration.Playable.World
{
    // RANGE lane flight timing (WPN-BULLET-SPEED). Shots and impacts come from the live weapon
    // events (AcceptedBallisticVolley / SurfaceImpact), so the readout measures what the
    // simulation actually does: a same-frame hitscan impact reads 0.000 s, a travelling
    // projectile reads its real flight time. Nothing here changes the shot.
    public struct R26RangeShotMatch
    {
        public string Weapon;public Vector3 Origin;public double ShotTime;public int ShotFrame;public float Distance,Offset;public bool Exact;
    }
    public sealed class R26RangeShotLog
    {
        public const int Capacity=16,MaxRays=8;public const double MaximumAge=10;public const float ExactOffset=.02f;
        readonly double[] times=new double[Capacity];readonly int[] frames=new int[Capacity],rayCounts=new int[Capacity];readonly string[] weapons=new string[Capacity];
        readonly Vector3[] origins=new Vector3[Capacity],rays=new Vector3[Capacity*MaxRays];readonly bool[] used=new bool[Capacity*MaxRays];int next,count;
        public int Count{get{return count;}}
        public void Clear(){next=count=0;for(int i=0;i<Capacity;i++)weapons[i]=null;}
        // Preallocated ring: no per-shot allocation (the caller's direction array is only read).
        public bool Record(string weapon,Float3 origin,Float3[] directions,double time,int frame)
        {
            if(directions==null||directions.Length==0||!origin.IsFinite||Double.IsNaN(time)||Double.IsInfinity(time))return false;
            int slot=next,n=0;
            for(int i=0;i<directions.Length&&n<MaxRays;i++){var d=new Vector3(directions[i].X,directions[i].Y,directions[i].Z);float m=d.magnitude;if(!(m>1e-6f)||Single.IsInfinity(m))continue;rays[slot*MaxRays+n]=d/m;used[slot*MaxRays+n]=false;n++;}
            if(n==0)return false;
            times[slot]=time;frames[slot]=frame;weapons[slot]=weapon;origins[slot]=new Vector3(origin.X,origin.Y,origin.Z);rayCounts[slot]=n;next=(slot+1)%Capacity;if(count<Capacity)count++;return true;
        }
        // Hitscan signature first: a ray recorded this frame that passes within 2 cm of the impact.
        // Otherwise the unused ray with the smallest miss distance (tolerance grows with range for
        // drop), oldest first on ties. Each matched ray is consumed once.
        public bool TryMatch(Vector3 point,double now,int frame,out R26RangeShotMatch match)
        {
            match=new R26RangeShotMatch();int bestSlot=-1,bestRay=-1;float bestOffset=Single.PositiveInfinity;bool bestExact=false;
            for(int k=0;k<count;k++)
            {
                int slot=((next-1-k)%Capacity+Capacity)%Capacity;double age=now-times[slot];if(age<0||age>MaximumAge)continue;
                var delta=point-origins[slot];bool sameFrame=frames[slot]==frame;
                for(int r=0;r<rayCounts[slot];r++)
                {
                    int ray=slot*MaxRays+r;if(used[ray])continue;var d=rays[ray];float along=Vector3.Dot(delta,d);if(along<-.5f)continue;
                    float offset=(delta-d*along).magnitude;if(offset>Tolerance(along))continue;bool exact=sameFrame&&offset<=ExactOffset;
                    if(bestExact&&!exact)continue;
                    if((exact&&!bestExact)||offset<=bestOffset){bestSlot=slot;bestRay=ray;bestOffset=offset;bestExact=exact;}
                }
            }
            if(bestSlot<0)return false;used[bestRay]=true;
            match.Weapon=weapons[bestSlot];match.Origin=origins[bestSlot];match.ShotTime=times[bestSlot];match.ShotFrame=frames[bestSlot];match.Distance=Vector3.Distance(origins[bestSlot],point);match.Offset=bestOffset;match.Exact=bestExact;return true;
        }
        public static float Tolerance(float along){return .35f+.03f*Mathf.Max(0,along);}
    }
    // Real-world reference muzzle velocities used by the range readout and by
    // ~/ashfall/campaign/bullet-speed-audit.md. These are references, not game values.
    public static class R26RangeBallistics
    {
        public static bool TryReference(string weapon,out float metresPerSecond,out string cartridge)
        {
            metresPerSecond=0;cartridge=null;
            switch(weapon)
            {
                case "mk18":metresPerSecond=880;cartridge="5.56x45 M855, 14.5 in (M4A1)";return true;
                case "akm":metresPerSecond=715;cartridge="7.62x39, 16.3 in";return true;
                case "spr":metresPerSecond=840;cartridge="5.56x45 Mk262, 18 in";return true;
                case "magnum":metresPerSecond=440;cartridge=".357/.44 Magnum, 6 in";return true;
                case "cx4":metresPerSecond=420;cartridge="9x19, 16.6 in carbine";return true;
                case "px4":metresPerSecond=360;cartridge="9x19, 4 in";return true;
                case "m1014":metresPerSecond=400;cartridge="12 ga 00 buck";return true;
                case "ump":metresPerSecond=285;cartridge=".45 ACP, 8 in";return true;
                case "p90":metresPerSecond=715;cartridge="5.7x28 SS190";return true;
                case "mp5":metresPerSecond=400;cartridge="9x19, 8.9 in";return true;
                case "mp9":metresPerSecond=380;cartridge="9x19, 5.1 in";return true;
                case "1911":metresPerSecond=255;cartridge=".45 ACP, 5 in";return true;
                case "tec9":metresPerSecond=360;cartridge="9x19, 5 in";return true;
                case "rfb":metresPerSecond=810;cartridge="7.62x51, 18 in";return true;
                case "fal":metresPerSecond=840;cartridge="7.62x51, 21 in";return true;
                case "s85":metresPerSecond=600;cartridge=".30 Carbine (M1 Carbine)";return true;
                case "awp":metresPerSecond=850;cartridge=".308 Win, 26 in";return true;
                case "m60":metresPerSecond=850;cartridge="7.62x51, 22 in";return true;
                case "dt11":metresPerSecond=400;cartridge="12 ga target load";return true;
                case "uzis":metresPerSecond=400;cartridge="9x19, 10.2 in";return true;
                case "matadors":metresPerSecond=380;cartridge="12 ga buck, short barrel";return true;
                case "smaw":metresPerSecond=220;cartridge="Mk 153 SMAW HEDP rocket";return true;
                default:return false;
            }
        }
    }
}
#endif
