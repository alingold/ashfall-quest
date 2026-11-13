#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.HealthStates;
// Callable by the root engine lane. No startup attributes or live object adoption.
public static class HealthArchiveNativeFixture
{
 sealed class Local : IHealthWorld,IAuthorityGate,IInteractionQuery
 {
  public double Now {get{return 0;}} public bool InMatch {get{return true;}} public bool TeamChangesAllowed {get{return false;}}
  public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
  public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
  public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}
 }
 public static void Run()
 {
  var f=new Local();var h=new HealthService("archive-local-synthetic",7,0,false,f,f,f);
  var c=new CommandContext("archive-local-synthetic","host","register",1,7,true);
  if(!h.Register(c,"a",-1,false).Accepted)throw new Exception("register");
  var before=h.Read("a");var t=h.DrainTransitions();
  var binding=new CheckpointBinding("root-frozen-build","synthetic-no-content-approval",c.SessionId,"host",7,1,1,1);
  byte[] bytes=MatchCheckpointRecords.Capture(binding,new[]{before},t);HealthSnapshot[] s;HealthTransition[] restored;
  MatchCheckpointRecords.Restore(bytes,binding,out s,out restored);
  if(Object.ReferenceEquals(before,s[0])||!Object.ReferenceEquals(s[0],restored[0].After)||h.Read("a").Health!=100)throw new Exception("archive identity");
  UnityEngine.Debug.Log("HealthArchiveNativeFixture synthetic PASS; no authority adoption");
 }
}
#endif
