using System;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Bots
{
    // Frozen R6 main.gd::_configure_missions and world.gd, normal campaign.
    // Source positions cross the Godot->Unity Z boundary exactly once here.
    // Opt-in dam-crown/spillway expansion has separate geometry/progress and
    // must not be silently substituted for the ordinary four-mission campaign.
    public sealed class R24ValleyMissionState
    {
        public const int MissionCount=4;
        static readonly string[] ids={"relay","turbine","uplink","extraction","complete"};
        static readonly string[] titles={"01  /  DEAD AIR","02  /  THE LONG FALL","03  /  LAST LIGHT","04  /  DOWNDRAFT","CAMPAIGN COMPLETE"};
        static readonly string[] orders={"Activate relay","Activate turbine bypass","Activate uplink","Clear extraction pad","CAMPAIGN COMPLETE"};
        static readonly Float3[] objectives={new Float3(0,2,16),new Float3(-18,9,44),new Float3(15,17,70),new Float3(0,1,-15)};
        static readonly Float3[][] waves={
            new[]{new Float3(-8,.2f,13),new Float3(8,.2f,23),new Float3(-3,1.2f,15),new Float3(3,1.2f,17)},
            new[]{new Float3(-20,8.2f,43),new Float3(-15,8.2f,43),new Float3(-23,8.2f,48),new Float3(-13,8.2f,40),new Float3(-8,8.2f,47),new Float3(-18,8.2f,38.5f)},
            new[]{new Float3(11,16.2f,65),new Float3(17,16.2f,65),new Float3(21,16.2f,69),new Float3(9,16.2f,72),new Float3(20,16.2f,75),new Float3(13,16.2f,70),new Float3(17,16.2f,70),new Float3(8,16.2f,58)},
            new[]{new Float3(12,.2f,-12),new Float3(10,.2f,0),new Float3(-14,.2f,0),new Float3(-14,.2f,-12),new Float3(7,.2f,2),new Float3(-6,.2f,1)}
        };
        public int Chapter {get;private set;}
        public int Kills {get;private set;}
        public double PlaySeconds {get;private set;}
        public bool Complete {get{return Chapter==MissionCount;}}
        public string MissionId {get{return ids[Chapter];}}
        public string Title {get{return titles[Chapter];}}
        public string Order {get{return orders[Chapter];}}
        public bool BeginsAlerted {get{return Chapter!=0&&!Complete;}}
        public int EnemyCount {get{return Complete?0:waves[Chapter].Length;}}
        public Float3 Objective {get{return ObjectiveAt(Math.Min(Chapter,MissionCount-1));}}
        public Float3 Screen {get{return R24KestrelBrain.Add(Objective,new Float3(0,.65f,-.37f));}}
        public Float3 Checkpoint {get{return Chapter==0?new Float3(0,1,-8):R24KestrelBrain.Add(ObjectiveAt(Math.Min(Chapter-1,3)),new Float3(0,1,-3));}}
        public Float3 GuardApproach {get{return Chapter==0?new Float3(0,1,-8):ObjectiveAt(Math.Min(Chapter-1,3));}}
        public Float3 SpawnAt(int slot){if(Complete||slot<0||slot>=EnemyCount)throw new ArgumentOutOfRangeException("source encounter slot");return waves[Chapter][slot];}
        public int VariantAt(int slot){SpawnAt(slot);return Chapter==3&&slot==EnemyCount-1?3:slot%3;}
        public static Float3 ObjectiveAt(int chapter){if(chapter<0||chapter>=MissionCount)throw new ArgumentOutOfRangeException("source mission");return objectives[chapter];}
        public void Restore(int chapter,int kills,double seconds)
        {if(chapter<0||chapter>4||kills<0||kills>1000000||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0||seconds>31536000)throw new ArgumentException("Source campaign checkpoint");Chapter=chapter;Kills=kills;PlaySeconds=seconds;}
        public void Step(float dt,bool simulate)
        {if(!R24KestrelBrain.Finite(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");if(simulate&&!Complete)PlaySeconds=Math.Min(31536000,PlaySeconds+dt);}
        public void EnemyDefeated(){if(!Complete)Kills=Math.Min(1000000,Kills+1);}
        public bool Advance(int actualLiving,bool eligible)
        {if(Complete||!eligible||actualLiving!=0)return false;Chapter++;return true;}
    }
}
