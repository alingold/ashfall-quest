#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Bots
{
 public static class R24RagdollRules
 {
  public const int ColliderLayer=26,GameplayQueryMask=~(1<<ColliderLayer),WorldMask=(1<<0)|(1<<8),MaximumCorpses=6,MaximumBodies=48;
  public const float Lifetime=12,MaximumDistance=40,MaximumLinearSpeed=12,MaximumAngularSpeed=9;
 }
 // A synchronous death-pose snapshot plus a lease on the EXISTING shared
 // source geometry/material/texture owner. No texture copies or BakeMesh path.
 internal sealed class R24RagdollSource:IDisposable
 {
  internal R24EnemySourceGeometry Geometry;internal int Variant;internal Transform[] Frames,Bones;internal Transform Model;internal Material[] Materials;internal Dictionary<string,Renderer> Renderers;internal Action Release;
  public void Dispose(){var release=Release;Release=null;if(release!=null)release();}
 }
 internal sealed class R24RagdollWorld
 {
  readonly List<R24RagdollCorpse> corpses=new List<R24RagdollCorpse>(6);bool paused;
  internal string Failure {get;private set;}
  internal int Count{get{Prune();return corpses.Count;}}
  internal int BodyCount{get{int result=0;Prune();foreach(var corpse in corpses)result+=corpse.BodyCount;return result;}}
  internal R24RagdollCorpse At(int index){Prune();return corpses[index];}
  void Prune(){corpses.RemoveAll(x=>x==null||x.Retired);}
  internal bool Spawn(Transform parent,Vector3 position,Quaternion rotation,Vector3 sourceEnemyImpulse,R24RagdollSource source)
  {
   if(parent==null||source==null)return false;Prune();while(corpses.Count>=6||BodyCount+source.Geometry.ForVariant(source.Variant).Source.bodies.Length>48){corpses[0].Retire();corpses.RemoveAt(0);}
   GameObject node=null;R24RagdollCorpse corpse=null;
   try{node=new GameObject("Original source corpse variant"+source.Variant);node.transform.SetParent(parent,false);node.transform.SetPositionAndRotation(position,rotation);corpse=node.AddComponent<R24RagdollCorpse>();corpse.Initialize(source,position,sourceEnemyImpulse);corpse.SetPaused(paused);corpses.Add(corpse);return true;}
   catch(Exception error){Failure=error.ToString();if(corpse!=null)corpse.Retire();else{source.Dispose();if(node!=null)UnityEngine.Object.Destroy(node);}Debug.LogError("Source ragdoll unavailable; authored death fallback retained: "+error);return false;}
  }
  internal void SetPaused(bool value){paused=value;Prune();foreach(var corpse in corpses)corpse.SetPaused(value);}
  internal void Step(float dt){Prune();foreach(var corpse in corpses)corpse.Step(dt);Prune();}
  internal void Clear(){foreach(var corpse in corpses)if(corpse!=null)corpse.Retire();corpses.Clear();paused=false;Failure=null;}
 }
}
#endif
