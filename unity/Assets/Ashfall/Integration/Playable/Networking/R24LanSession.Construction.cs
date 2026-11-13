#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  long appliedConstruction=-2,constructionEpoch=-1;
  void BindConstructionPresentation(){root.Activities.SharedDismantledProvider=delegate(string id){return peer!=null&&peer.Lobby.WasPanelDismantled(id);};root.Activities.SharedPanelsProvider=delegate{return peer==null?new Ashfall.Modules.Construction.PlacedPanel[0]:peer.Lobby.SharedPanels;};root.Activities.SharedBuildRequested=delegate(int hand){if(peer==null)return false;PublishRawTracking();return peer.Lobby.RequestBuild(hand,root.Activities.SelectedBuildTier,root.Activities.SelectedBuildVariant,root.Activities.SelectedDismantle);};}
  void SyncConstruction(){if(peer==null||root==null||root.Activities==null)return;var lobby=peer.Lobby;if(constructionEpoch!=lobby.Epoch||appliedConstruction!=lobby.ConstructionRevision){constructionEpoch=lobby.Epoch;appliedConstruction=lobby.ConstructionRevision;root.Activities.ReconcileNetworkConstruction();}}
 }
}
#endif
