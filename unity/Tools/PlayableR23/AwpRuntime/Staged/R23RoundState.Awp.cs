using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23RoundState {
  readonly Dictionary<string,R24AwpOwner> awps=new Dictionary<string,R24AwpOwner>();
  public R24AwpOwner Awp{get{R24AwpOwner result;awps.TryGetValue(selectedRifle=="awp"?selectedAction:"awp",out result);return result;}}
  internal R24AwpOwner ReadAwpInstance(string action){R24AwpOwner owner;return action!=null&&awps.TryGetValue(action,out owner)?owner:null;}
  public R24AwpShot LastAwpShot{get;private set;}
  public bool StepAwp(float dt,HandlingInput input,HandlingAction action,R23ShotPose pose,bool paused){if(!sourceLoadout||selectedRifle!="awp"||Awp==null)return false;if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");bool suspended=paused||Health<=0;if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}input.Paused|=suspended;input.DeltaSeconds=dt;var command=Command();input.Sequence=command.Tick;R24AwpShot shot;Awp.Apply(command,action,input,pose,out shot);LastAwpShot=shot;pendingShot=shot!=null;return pendingShot;}
 }
}
