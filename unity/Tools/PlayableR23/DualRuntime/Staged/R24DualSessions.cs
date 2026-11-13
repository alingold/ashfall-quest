using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed class R24DualCarriedSession {
  readonly Dictionary<string,R24DualOwner> instances=new Dictionary<string,R24DualOwner>(StringComparer.Ordinal);readonly Dictionary<string,R24DualFeedback[]> feedback=new Dictionary<string,R24DualFeedback[]>(StringComparer.Ordinal);readonly Func<string,IRevisionedAmmoReserve> family;readonly IAuthorityGate gate;
  public R24DualOwner Selected{get;private set;}public R24DualCarriedSession(Func<string,IRevisionedAmmoReserve> actualFamily,IAuthorityGate actualGate){if(actualFamily==null||actualGate==null)throw new ArgumentNullException();family=actualFamily;gate=actualGate;}
  public bool Grant(CommandContext creation,string actualInstanceId,string profile,int stars=1){if(instances.ContainsKey(actualInstanceId))return false;var definition=new R24DualDefinition(profile,stars);var owner=new R24DualOwner(creation,actualInstanceId,definition,family(definition.AmmoFamily),gate);instances.Add(actualInstanceId,owner);feedback.Add(actualInstanceId,new[]{new R24DualFeedback(profile),new R24DualFeedback(profile)});return true;}
  public R24DualOwner Read(string id){R24DualOwner value;return id!=null&&instances.TryGetValue(id,out value)?value:null;}public R24DualFeedback Feedback(R24DualOwner owner,int side){return feedback[owner.InstanceId][side];}
  public bool Equip(string id,bool leftFree,bool rightFree,int dominant){var next=Read(id);if(next==null)return false;if(next==Selected&&next.Equipped)return true;if(!next.TryEquip(leftFree,rightFree,dominant))return false;if(Selected!=null)Selected.Holster();Selected=next;return true;}
  // Drop/holster retain this exact owner and finite ammunition; only explicit
  // round reset retires it. No map-change, equip or regrip entitlement grants.
  public void Holster(){if(Selected!=null)Selected.Holster();Selected=null;}public void Drop(){Holster();}public void Suspend(){if(Selected!=null){Selected.SuspendControls();foreach(var f in feedback[Selected.InstanceId])f.Suspend();}}
  public void AdvancePresentation(float dt,bool enabled){if(Selected==null||!enabled)return;foreach(var f in feedback[Selected.InstanceId])f.Advance(dt);}
  public R23ShotPose ResolvePose(int side,R23ShotPose neutral){if(Selected==null)throw new InvalidOperationException("Actual dual instance not equipped");var f=Feedback(Selected,side);R23ShotPose model,shot;R23WeaponPoseMath.Resolve(neutral,Selected.Definition.Muzzle,Selected.Definition.Scale,f.Distance,f.Pitch,f.Yaw,!Selected.Definition.Automatic,out model,out shot);return shot;}
  public RuleResult Apply(CommandContext c,R24DualFrame frame,out R24DualStep step){step=null;if(Selected==null)return RuleResult.Reject("No equipped dual instance");var r=Selected.Apply(c,frame,out step);if(r.Accepted&&step!=null){if(step.Dominant!=null)Feedback(Selected,0).Accepted(step.Dominant);if(step.Other!=null)Feedback(Selected,1).Accepted(step.Other);}return r;}
  public void ResetRound(){foreach(var owner in instances.Values)owner.Retire();instances.Clear();feedback.Clear();Selected=null;}
 }
}
namespace Ashfall.Integration.Playable.Gallery {
 using Ashfall.Integration.Playable.HandsWeapons;
 public sealed class R24DualGallerySession {
  sealed class Lease:IAuthorityGate{internal bool Active=true;public RuleResult Validate(CommandContext c){return Active&&c.IsAuthority&&c.SessionId=="r24-dual-gallery"&&c.ActorId=="preview"?RuleResult.Accept():RuleResult.Reject("Retired private dual preview");}}
  sealed class Supply:IRevisionedAmmoReserve{long revision;public int Available{get{return -1;}}public long Revision{get{return revision;}}public int Take(int n){if(n<=0)return 0;revision++;return n;}public int Refund(int n){if(n<=0)return 0;revision++;return n;}}
  Lease lease;long sequence,generation;string profile;R24DualCarriedSession session;public bool Active{get;private set;}public R24DualOwner Owner{get{return session==null?null:session.Selected;}}public int PreviewVolleys{get;private set;}
  CommandContext C(){return new CommandContext("r24-dual-gallery","preview","dual-preview/"+(++sequence),sequence,0,true);}
  public void Show(string id){new R24DualDefinition(id);if(Active&&profile==id)return;Hide();profile=id;Active=true;}
  public bool Grab(bool leftFree=true,bool rightFree=true,int dominant=1){if(!Active||Owner!=null||!leftFree||!rightFree)return false;lease=new Lease();var supply=new Supply();session=new R24DualCarriedSession(delegate{return supply;},lease);string instance="live097/"+profile+"/"+(++generation);session.Grant(C(),instance,profile);session.Equip(instance,true,true,dominant);PreviewVolleys=0;return true;}
  public bool Step(R24DualFrame frame,R23ShotPose leftNeutral,R23ShotPose rightNeutral,out R24DualStep step){step=null;if(Owner==null)return false;frame.Sequence=sequence+1;session.AdvancePresentation(frame.DeltaSeconds,frame.Enabled&&!frame.Paused&&frame.Focused);var left=frame.Left;var right=frame.Right;int leftSide=frame.DominantHand==0?0:1;left.FiringPose=session.ResolvePose(leftSide,leftNeutral);right.FiringPose=session.ResolvePose(1-leftSide,rightNeutral);frame.Left=left;frame.Right=right;var r=session.Apply(C(),frame,out step);if(step!=null)PreviewVolleys+=(step.Dominant==null?0:1)+(step.Other==null?0:1);return r.Accepted;}
  public void AdvancePresentation(float dt,bool enabled){if(session!=null)session.AdvancePresentation(dt,enabled);}
  public bool ApplyProjected(R24DualFrame frame,out R24DualStep step){step=null;if(Owner==null)return false;frame.Sequence=sequence+1;var r=session.Apply(C(),frame,out step);if(step!=null)PreviewVolleys+=(step.Dominant==null?0:1)+(step.Other==null?0:1);return r.Accepted;}
  public R24DualFeedback Feedback(int side){return session==null||Owner==null?null:session.Feedback(Owner,side);}public void Release(){if(session!=null)session.ResetRound();if(lease!=null)lease.Active=false;session=null;lease=null;}public void Hide(){Release();Active=false;}
 }
}
