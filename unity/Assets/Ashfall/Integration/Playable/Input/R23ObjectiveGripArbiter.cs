using System;
namespace Ashfall.Integration.Playable.Input
{
    public enum R23GripObjectiveKind { None,Weapon,Consumable,Open,Explosive,Supply,Station }
    public struct R23GripObjective
    {
        public readonly R23GripObjectiveKind Kind;public readonly string Id;public readonly int Hand;public readonly float Distance;
        public R23GripObjective(R23GripObjectiveKind kind,string id,int hand,float distance){Kind=kind;Id=id;Hand=hand;Distance=distance;}
        public bool Valid {get{return Kind!=R23GripObjectiveKind.None&&!String.IsNullOrEmpty(Id)&&(Hand==0||Hand==1)&&!Single.IsNaN(Distance)&&!Single.IsInfinity(Distance)&&Distance>=0&&Distance<=(Kind==R23GripObjectiveKind.Open?.35f:2);}}
    }
    // Original objective_grip_arbiter.gd chooses one closest eligible objective
    // across both fresh hands. Providers own aim, LOS, custody and target validity.
    public sealed class R23ObjectiveGripArbiter
    {
        readonly bool[] down={true,true},claimed={false,false};
        public bool Reserved(int hand){if(hand<0||hand>1)throw new ArgumentOutOfRangeException("hand");return claimed[hand];}
        public void Reset(){for(int i=0;i<2;i++){down[i]=true;claimed[i]=false;}}
        public R23GripObjective Step(bool enabled,bool leftReady,bool rightReady,float leftGrip,float rightGrip,R23GripObjective[] targets)
        {
            var best=default(R23GripObjective);float nearest=float.PositiveInfinity;
            for(int hand=0;hand<2;hand++)
            {
                bool ready=enabled&&(hand==0?leftReady:rightReady);float grip=hand==0?leftGrip:rightGrip;
                if(!ready||Single.IsNaN(grip)||Single.IsInfinity(grip)){down[hand]=true;claimed[hand]=false;continue;}
                if(grip<=.2f){down[hand]=false;claimed[hand]=false;continue;}
                if(grip<.65f||down[hand])continue;down[hand]=true;
                if(targets==null)continue;
                foreach(var target in targets)
                {
                    if(!target.Valid||target.Hand!=hand)continue;claimed[hand]=true;
                    if(target.Distance<nearest){nearest=target.Distance;best=target;}
                }
            }
            return best;
        }
    }
}
