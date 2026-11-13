using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed partial class R23RoundState {
  readonly Dictionary<string,R24M60Owner> m60s=new Dictionary<string,R24M60Owner>();
  public R24M60Owner M60{get{R24M60Owner result;m60s.TryGetValue(selectedRifle=="m60"?selectedAction:"m60",out result);return result;}}
  internal R24M60Owner ReadM60Instance(string action){R24M60Owner owner;return action!=null&&m60s.TryGetValue(action,out owner)?owner:null;}
  public R24M60Shot LastM60Shot{get;private set;}
  public bool StepM60(float dt,HandlingInput input,HandlingAction action,R23ShotPose pose,bool paused){if(!sourceLoadout||selectedRifle!="m60"||M60==null)return false;if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");bool suspended=paused||Health<=0;if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}input.Paused|=suspended;input.DeltaSeconds=dt;var command=Command();input.Sequence=command.Tick;R24M60Shot shot;M60.Apply(command,action,input,pose,out shot);LastM60Shot=shot;pendingShot=shot!=null;return pendingShot;}
 }
}
