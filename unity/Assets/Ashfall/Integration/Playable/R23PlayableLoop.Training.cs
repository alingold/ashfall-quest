#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public R24RescueTrainingDummy RescueTraining{get;private set;}
        int trainingFrame=-1;
        void InitializeSourceTraining()
        {
            RescueTraining=new GameObject("Actual R6 solo rescue exercise").AddComponent<R24RescueTrainingDummy>();RescueTraining.transform.SetParent(transform,false);RescueTraining.Initialize(Rig.Head.transform);RescueTraining.SetWorldVisible(false);
        }
        void ResetSourceTrainingCheckpoint()
        {if(RescueTraining!=null)RescueTraining.SetCheckpoint(activeSpawn,Map.MapId=="range");}
        void TickSourceTraining()
        {
            // Visibility is a source-world transition, not a time sample. A
            // Start/return callback can run within the already sampled frame.
            if(RescueTraining!=null)RescueTraining.SetWorldVisible(Ready&&roundStarted&&!switchingMap);
            if(trainingFrame==Time.frameCount)return;trainingFrame=Time.frameCount;float dt=Mathf.Clamp(Time.deltaTime,0,.25f);
            if(Gameplay!=null)Gameplay.TickSourcePracticeTargets(dt);
            if(RescueTraining!=null)RescueTraining.Tick(dt);
        }
        R24ReviveTarget[] ReadActualTrainingAndAllies()
        {
            // Explicit caller override retains its existing ownership contract.
            if(ReviveTargetProvider!=null)return ReviveTargetProvider();
            if(LanSession!=null&&LanSession.Lobby!=null)return LanSession.ReviveTargets();
            var actual=new List<R24ReviveTarget>(Gameplay.ReadRevivableAllies());R24ReviveTarget training;
            if(RescueTraining!=null&&RescueTraining.TryReadTarget(out training))actual.Add(training);return actual.ToArray();
        }
        bool CompleteTrainingOrAllyRevive(string id)
        {
            if(CompleteRevive!=null)return CompleteRevive(id);
            if(id==R24RescueTrainingDummyState.TargetId)return RescueTraining!=null&&RescueTraining.TryRevive(id);
            if(LanSession!=null&&LanSession.Lobby!=null)return LanSession.CompleteSharedRevive(id);
            return Gameplay.TryCompleteActualRevive(id);
        }
    }
}
#endif
