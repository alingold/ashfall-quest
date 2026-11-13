#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
// Explicit callable fixture only. Root owns all engine-lane execution.
public static class CombatCheckpointBoundaryNativeFixture
{
 sealed class World : IHealthWorld,IAuthorityGate,IInteractionQuery
 {
  public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
  public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
  public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float z){return true;}
  public bool TryGetPose(string a,out RescuePose p){p=new RescuePose();return false;}
 }
 public static void Run()
 {
  var w=new World();var h=new HealthService("synthetic",1,0,false,w,w,w);h.Register(new CommandContext("synthetic","host","r",1,1,true),"v",-1,false);
  var receipt=h.ApplyDamage(new CommandContext("synthetic","host","d",2,1,true),"physical","v",1,10,0,false);
  using(var m=new MemoryStream()){using(var writer=new BinaryWriter(m,System.Text.Encoding.UTF8,true))MatchCheckpointRecords.WriteDamageRecord(writer,receipt);m.Position=0;using(var reader=new BinaryReader(m)){var cold=MatchCheckpointRecords.ReadDamageRecord(reader);if(cold.Snapshot.Health!=90||Object.ReferenceEquals(cold,receipt))throw new Exception("archive");}}
  UnityEngine.Debug.Log("Combat checkpoint boundary synthetic record fixture; no adoption");
 }
}
#endif
