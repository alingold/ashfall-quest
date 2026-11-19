using System;using System.Collections.Generic;
namespace Ashfall.Integration.Playable.Networking {
 // Immutable host rows carry independent revisions: a late row with a smaller global
 // sequence must still be applied. Never use a guest-wide high-water mark for admission.
 internal sealed class R26LanItemSync<T> {
  readonly Dictionary<string,long> revisions=new Dictionary<string,long>(4096,StringComparer.Ordinal);
  readonly Func<T,long> revision;internal readonly List<T> Rows=new List<T>(4096);
  object owner;long epoch=-1,hostSequence=-1;double nextSweep;int localCount=-1;internal bool Full,Discover;
  internal R26LanItemSync(Func<T,long> revision){this.revision=revision;}
  internal void Reset(){revisions.Clear();Rows.Clear();owner=null;epoch=-1;hostSequence=-1;nextSweep=0;localCount=-1;Full=Discover=false;}
  internal void Begin(object lobby,long currentEpoch,double now,int count){if(!ReferenceEquals(owner,lobby)||epoch!=currentEpoch){Reset();owner=lobby;epoch=currentEpoch;}
   Full=now>=nextSweep;Discover=Full||count!=localCount;localCount=count;if(Full)nextSweep=now+.5;}
  internal void Collect(Dictionary<string,T> states,bool hosting,long sequence){Rows.Clear();if(!Full&&hosting&&hostSequence==sequence)return;hostSequence=sequence;
   foreach(var pair in states){long next=revision(pair.Value),prior;if(!Full&&revisions.TryGetValue(pair.Key,out prior)&&prior==next)continue;revisions[pair.Key]=next;Rows.Add(pair.Value);}}
 }
 public sealed partial class R24LanLobby {
  // Direct dictionary enumeration avoids allocating public snapshot arrays. Host tables
 // can skip even revision comparisons when their publish sequence has not advanced.
  internal void ChangedLoot(R26LanItemSync<R24LanLootState> sync){Check();sync.Collect(lootStates,State==R24LanLobbyState.Hosting,lootSequence);}
  internal void ChangedSupplies(R26LanItemSync<R24LanSupplyState> sync){Check();sync.Collect(sharedSupplies,State==R24LanLobbyState.Hosting,supplySequence);}
  internal void ChangedFood(R26LanItemSync<R24LanFoodState> sync){Check();sync.Collect(foods,State==R24LanLobbyState.Hosting,foodSequence);}
  internal void ChangedGrenades(R26LanItemSync<R24LanGrenadeState> sync){Check();sync.Collect(grenades,State==R24LanLobbyState.Hosting,grenadeSequence);}
 }
}
