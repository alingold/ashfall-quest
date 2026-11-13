namespace Ashfall.Integration.Playable.HandsWeapons {
 public static class R24M60Status {
  public static string Instruction(R24M60Owner owner){if(owner==null)return "GRIP TO INSPECT / RELEASE TO RETURN";switch(owner.Stage){case R24M60Stage.BoxRemoved:return "GRIP BOX / LIFT UP";case R24M60Stage.HoldingBox:return "LIFT BOX UP";case R24M60Stage.NeedsFeed:return "RELEASE / GRIP BELT / PULL ACROSS";case R24M60Stage.BoxSeated:return "RELEASE / GRIP COVER / PUSH DOWN";case R24M60Stage.NeedsCharge:return "RELEASE / GRIP HANDLE / PULL BACK";case R24M60Stage.CoverOpen:return "GRIP BOX / PULL DOWN";default:return "TRIGGER FIRES / RIGHT STICK CLICK: RELOAD";}}
 }
}
