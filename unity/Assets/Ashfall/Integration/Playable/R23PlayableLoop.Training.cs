#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.World;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public R26RangeLane RangeLane{get;private set;}
        int trainingFrame=-1;
        void InitializeSourceTraining()
        {
            RangeLane=new GameObject("R26 SANDBOX RANGE shooting lane").AddComponent<R26RangeLane>();RangeLane.transform.SetParent(transform,false);RangeLane.Initialize(this);
        }
        void ResetSourceTrainingCheckpoint()
        {if(RangeLane!=null)RangeLane.ResetLane();}
        void TickSourceTraining()
        {
            // Visibility is a source-world transition, not a time sample. A
            // Start/return callback can run within the already sampled frame.
            if(RangeLane!=null)RangeLane.SetVisible(Ready&&roundStarted&&!switchingMap);
            if(trainingFrame==Time.frameCount)return;trainingFrame=Time.frameCount;float dt=Mathf.Clamp(Time.deltaTime,0,.25f);
            if(Gameplay!=null)Gameplay.TickSourcePracticeTargets(dt);
            if(RangeLane!=null)RangeLane.Tick(dt);
        }
        R24ReviveTarget[] ReadActualTrainingAndAllies()
        {
            // Explicit caller override retains its existing ownership contract.
            if(ReviveTargetProvider!=null)return ReviveTargetProvider();
            if(LanSession!=null&&LanSession.Lobby!=null)return LanSession.ReviveTargets();
            return Gameplay.ReadRevivableAllies();
        }
        bool CompleteTrainingOrAllyRevive(string id)
        {
            if(CompleteRevive!=null)return CompleteRevive(id);
            if(LanSession!=null&&LanSession.Lobby!=null)return LanSession.CompleteSharedRevive(id);
            return Gameplay.TryCompleteActualRevive(id);
        }
    }
}
#endif
