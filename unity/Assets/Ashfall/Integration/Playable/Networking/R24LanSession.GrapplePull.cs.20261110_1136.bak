#if UNITY_6000_0_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
using UnityEngine.XR;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        readonly Dictionary<string,LineRenderer> pullLines=new Dictionary<string,LineRenderer>();
        readonly Dictionary<string,long> shownPulls=new Dictionary<string,long>();
        Material pullMaterial;LineRenderer pullEdge;R24ItemAudio pullAudio;AudioSource pullVoice;Coroutine pullLoad;
        bool ownPullWas;long pullEpoch=-1;float pullEdgeAlpha;
        void SyncGrapplePull()
        {
            if(root==null||root.Rig==null)return;
            if(peer==null){ClearGrapplePullPresentation();return;}
            if(pullEpoch!=peer.Lobby.Epoch){ClearGrapplePullPresentation();pullEpoch=peer.Lobby.Epoch;}
            var rules=peer.Lobby.SelectedMap==null?null:peer.Lobby.SelectedMap.SourceRules;
            if(rules==null||!rules.GrapplePull||!rules.GrappleGun||PresenceScene=="menu"){if(root.Rig.BeingGrappled)peer.Lobby.PublishWeb(root.Rig.WebState,2);ClearGrapplePullPresentation();return;}
            if(pullMaterial==null){
                pullMaterial=new Material(Shader.Find("Sprites/Default"));
                pullAudio=new R24ItemAudio();pullVoice=pullAudio.Voice(transform,"Grapple tug",-17,15);pullLoad=StartCoroutine(LoadPullSound());
                pullEdge=PullLine("Pull edge cue",49,.045f);pullEdge.useWorldSpace=false;pullEdge.transform.SetParent(root.Rig.Head.transform,false);
                for(int k=0;k<49;k++){float a=k*2*Mathf.PI/48;pullEdge.SetPosition(k,new Vector3(Mathf.Cos(a)*.55f,Mathf.Sin(a)*.4f,.4f));}
                pullEdge.startColor=pullEdge.endColor=new Color(.03f,.09f,.12f,.16f);
            }
            bool own=false;var live=new HashSet<string>();double now=peer.Lobby.EstimatedHostTime;
            foreach(var row in peer.Lobby.GrapplePulls){
                if(now>=row.Start+1.5)continue;
                live.Add(row.Victim);LineRenderer line;
                if(!pullLines.TryGetValue(row.Victim,out line)){line=PullLine("Grapple player tether",2,.012f);pullLines[row.Victim]=line;}
                Vector3 from=Vector3.zero,to=Vector3.zero;bool visible=PullEndpoint(row.Shooter,row.Hand,true,out from)&&PullEndpoint(row.Victim,0,false,out to);
                line.enabled=visible;if(visible){line.SetPosition(0,from);line.SetPosition(1,to);}
                long shown;if(!shownPulls.TryGetValue(row.Victim,out shown)||shown<row.Revision){
                    shownPulls[row.Victim]=row.Revision;
                    if(row.Shooter==peer.Lobby.OwnActor){root.Rig.StopGrappleShot(row.Hand);PullHaptic(row.Hand,.45f);}
                    if(row.Victim==peer.Lobby.OwnActor){root.Rig.AdoptGrapplePull(row.Revision,row.Target,(float)System.Math.Max(0,now-row.Start));pullVoice.transform.position=root.Rig.Head.transform.position;pullAudio.Play(pullVoice,"tug");}
                }
                if(row.Victim==peer.Lobby.OwnActor)own=true;
            }
            foreach(var id in new List<string>(pullLines.Keys))if(!live.Contains(id)){Destroy(pullLines[id].gameObject);pullLines.Remove(id);}
            foreach(var id in new List<string>(shownPulls.Keys))if(!live.Contains(id))shownPulls.Remove(id);
            if(!own)root.Rig.ClearGrapplePull();
            // A blocked/locally cancelled victim may end its own pull, never anybody else's.
            if(ownPullWas&&own&&!root.Rig.BeingGrappled)peer.Lobby.PublishWeb(root.Rig.WebState,2);
            ownPullWas=own&&root.Rig.BeingGrappled;
            pullEdgeAlpha=Mathf.MoveTowards(pullEdgeAlpha,ownPullWas&&root.Rig.MotionVelocity.sqrMagnitude>.04f?.16f:0,Time.unscaledDeltaTime*.8f);
            pullEdge.startColor=pullEdge.endColor=new Color(.03f,.09f,.12f,pullEdgeAlpha);pullEdge.enabled=pullEdgeAlpha>.001f;
        }
        bool PullEndpoint(string actor,int hand,bool muzzle,out Vector3 point)
        {
            point=Vector3.zero;
            if(actor==peer.Lobby.OwnActor){
                if(!muzzle){point=root.Rig.Head.transform.position-Vector3.up*.45f;return true;}
                Quaternion rotation;return root.Gameplay.TryReadGrappleMuzzle(hand,out point,out rotation);
            }
            R24LanPoseSnapshot pose;
            if(!peer.Lobby.TryReadPresence(actor,out pose)||pose.Pose.Scene!=PresenceScene||Time.realtimeSinceStartupAsDouble-pose.ReceivedAt>1)return false;
            var value=muzzle?(hand==0?pose.Pose.Left.Position:pose.Pose.Right.Position):pose.Pose.Head.Position;
            point=new Vector3(value.X,value.Y,value.Z);if(!muzzle)point.y-=.45f;return true;
        }
        LineRenderer PullLine(string name,int count,float width)
        {var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=pullMaterial;line.positionCount=count;line.startWidth=line.endWidth=width;line.startColor=line.endColor=Color.cyan;line.useWorldSpace=true;line.enabled=false;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;}
        IEnumerator LoadPullSound(){yield return pullAudio.LoadOptional("tug","Expansion/Audio/grapple/tug.wav");}
        static void PullHaptic(int hand,float strength){var device=InputDevices.GetDeviceAtXRNode(hand==0?XRNode.LeftHand:XRNode.RightHand);if(device.isValid)device.SendHapticImpulse(0,strength,.06f);}
        void ClearGrapplePullPresentation()
        {
            if(root!=null&&root.Rig!=null)root.Rig.ClearGrapplePull(true);
            ownPullWas=false;pullEdgeAlpha=0;shownPulls.Clear();
            foreach(var line in pullLines.Values)if(line!=null)Destroy(line.gameObject);pullLines.Clear();
            if(pullEdge!=null)Destroy(pullEdge.gameObject);pullEdge=null;
            if(pullLoad!=null){StopCoroutine(pullLoad);pullLoad=null;}
            if(pullAudio!=null){pullAudio.Dispose();pullAudio=null;}
            if(pullMaterial!=null)Destroy(pullMaterial);pullMaterial=null;
        }
    }
}
#endif
