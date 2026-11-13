using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Exact solo exercise; deliberately separate from network/player health.
    public sealed class R24RescueTrainingDummyState
    {
        public const string TargetId="r24-rescue-training-dummy";
        public const float ResetSeconds=12;
        public bool Downed{get;private set;}public float Health{get;private set;}public int ReviveCount{get;private set;}public float ResetRemaining{get;private set;}
        public string Readout{get{return Downed?"RESCUE TRAINING\nDOWNED":"RESCUE COMPLETE\nTraining resets in 12 seconds";}}
        public R24RescueTrainingDummyState(){ResetTraining();}
        public void ResetTraining(){Downed=true;Health=0;ResetRemaining=0;}
        public bool TryRevive(string id,bool visible)
        {if(id!=TargetId||!Downed||!visible)return false;Downed=false;Health=50;ReviveCount++;ResetRemaining=ResetSeconds;return true;}
        // main.paused is an input/gameplay flag, not SceneTree.paused. The source
        // dummy's own _process therefore continues during ordinary menus/hiddenness.
        public void Tick(float seconds,bool gameplayPaused=false)
        {if(Single.IsNaN(seconds)||Single.IsInfinity(seconds))throw new ArgumentOutOfRangeException("seconds");if(ResetRemaining>0){ResetRemaining=Math.Max(0,ResetRemaining-Math.Max(0,seconds));if(ResetRemaining==0)ResetTraining();}}
        public static Float3 CheckpointOffset(bool actualSandbox){return new Float3(-4,actualSandbox?.45f:-1,3);}
        public static Float3 SourceLeftContact{get{return new Float3(-.13f,.61f,.13f);}}
        public static Float3 SourceRightContact{get{return new Float3(.13f,.61f,.13f);}}
        public bool TryReadTarget(R23ShotPose unityFrame,bool visible,out R24ReviveTarget target)
        {target=null;if(!Downed||!visible||!unityFrame.Valid)return false;target=new R24ReviveTarget(TargetId,unityFrame,unityFrame.Point(new Float3(-.13f,.61f,-.13f)),unityFrame.Point(new Float3(.13f,.61f,-.13f)));return true;}
    }
}
