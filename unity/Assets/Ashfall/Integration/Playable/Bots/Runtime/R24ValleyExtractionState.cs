using System;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Bots
{
    // Frozen world.gd start_extraction_arrival/_process. Coordinates cross Z once.
    public sealed class R24ValleyExtractionState
    {
        double elapsed;bool hoverBob;
        public double Elapsed {get{return elapsed;}}
        public bool Landed {get{return elapsed>=12;}}
        public float RollRadians {get{return (float)(Math.Sin(elapsed*1.4)*.012);}}
        public float LeftRotorRadians {get{return (float)(-elapsed*26%(Math.PI*2));}}
        public float RightRotorRadians {get{return -LeftRotorRadians;}}
        public float BeaconEnergy {get{return elapsed%1.3<.12?2:0;}}
        public Float3 Position
        {
            get
            {
                var start=new Float3(0,35,-80);var approach=new Float3(0,10.2f,-20);var hover=new Float3(0,5.7f,-15);
                if(elapsed<8)return Lerp(start,approach,Math.Sin(elapsed/8*Math.PI*.5));
                if(elapsed<12)return Lerp(approach,hover,(1-Math.Cos((elapsed-8)/4*Math.PI))*.5);
                return new Float3(0,5.7f+(hoverBob?(float)Math.Sin(elapsed*1.8)*.07f:0),-15);
            }
        }
        public void Step(float dt)
        {if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");hoverBob=Landed;elapsed+=dt;}
        static Float3 Lerp(Float3 a,Float3 b,double amount)
        {return new Float3((float)(a.X+(b.X-a.X)*amount),(float)(a.Y+(b.Y-a.Y)*amount),(float)(a.Z+(b.Z-a.Z)*amount));}
    }
}
