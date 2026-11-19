#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;using Ashfall.Modules.Flight;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  readonly System.Collections.Generic.HashSet<string> shownPods=new System.Collections.Generic.HashSet<string>();
  void ClearPodPresentation(){if(root!=null&&root.TeamLaunch!=null)foreach(string actor in shownPods)root.TeamLaunch.RemovePlayer(actor);shownPods.Clear();}
  void SyncPodPresentation(){if(peer==null||root==null)return;var current=new System.Collections.Generic.HashSet<string>();foreach(var member in peer.Lobby.Members){R24LanPodState state;if(peer.Lobby.TryReadPod(member.Actor,out state)){string key=member.Actor==peer.Lobby.OwnActor?"player":member.Actor;current.Add(key);root.ApplyLanPodPresentation(state);}}if(root.TeamLaunch!=null)foreach(string actor in shownPods)if(!current.Contains(actor))root.TeamLaunch.RemovePlayer(actor);shownPods.Clear();foreach(string actor in current)shownPods.Add(actor);}

  sealed class RootPodWorld:IR24LanPodWorld,IR26LanPlatformWorld {readonly R24LanSession owner;internal RootPodWorld(R24LanSession owner){this.owner=owner;}
   public bool OnPlatform(Float3 feet){var launch=owner.root.TeamLaunch;return launch!=null&&launch.Eligible&&launch.Runtime.Layout.PlatformAt(feet)>=0;}
   public bool TryPlan(string actor,int team,string pod,Float3 root,double clock,out CapsulePlan plan){plan=null;return owner.root!=null&&owner.root.PlanLanPod(actor,team,pod,root,clock,out plan);}
  }
 }
}
#endif
