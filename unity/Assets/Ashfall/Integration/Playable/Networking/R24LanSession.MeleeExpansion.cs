#if UNITY_6000_0_OR_NEWER
using System.Collections;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
using UnityEngine.XR;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        Coroutine meleeLoad;R24ItemAudio meleeAudio;AudioSource[] meleeVoices;LineRenderer[] meleeSparks;Material meleeMaterial;
        readonly float[] sparkUntil=new float[8];int meleeSlot;
        void SyncMeleeExpansion()
        {
            if(root==null||root.Rig==null)return;
            if(meleeAudio==null){meleeAudio=new R24ItemAudio();meleeVoices=new AudioSource[8];meleeSparks=new LineRenderer[8];meleeMaterial=new Material(Shader.Find("Sprites/Default"));
                for(int i=0;i<8;i++){meleeVoices[i]=meleeAudio.Voice(transform,"Melee contact",-12,18);var go=new GameObject("Melee impact spark");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=meleeMaterial;line.useWorldSpace=true;line.positionCount=6;line.startWidth=.008f;line.endWidth=.002f;line.enabled=false;meleeSparks[i]=line;}meleeLoad=StartCoroutine(LoadMeleeAudio());}
            for(int i=0;i<8;i++)meleeSparks[i].enabled=Time.unscaledTime<sparkUntil[i];
            R25MeleeHit hit;while(peer.Lobby.TryDequeueMeleeHit(out hit)){
                bool attacker=hit.Attacker==peer.Lobby.OwnActor;float stagger=attacker?hit.Stagger:hit.TargetStagger;if(stagger>0&&root.Gameplay!=null)root.Gameplay.NetworkMeleeStaggerUntil=Time.unscaledTime+stagger;var point=new Vector3(hit.Point.X,hit.Point.Y,hit.Point.Z);int slot=meleeSlot++%8;
                meleeVoices[slot].transform.position=point;meleeAudio.Play(meleeVoices[slot],hit.Kind=="parry"?(hit.Stagger>0||hit.TargetStagger>0?"parry":"clash"):"flesh-hit");
                int hand=attacker?hit.Hand:Vector3.Dot(point-root.Rig.Head.transform.position,root.Rig.Head.transform.right)<0?0:1;
                if(hit.Kind=="parry"&&!attacker)hand=hit.TargetHand;
                if(root.Rig.SessionFocused){var device=InputDevices.GetDeviceAtXRNode(hand==0?XRNode.LeftHand:XRNode.RightHand);HapticCapabilities cap;if(device.isValid&&device.TryGetHapticCapabilities(out cap)&&cap.supportsImpulse)device.SendHapticImpulse(0,hit.Kind=="parry"?.75f:Mathf.Clamp(.15f+hit.Damage/100,.15f,.8f),.065f);}
                var line=meleeSparks[slot];line.startColor=Color.white;line.endColor=hit.Kind=="parry"?Color.cyan:Color.yellow;
                for(int k=0;k<6;k++)line.SetPosition(k,point+(k%2==0?Vector3.zero:new Vector3(Mathf.Sin(k*2.4f),Mathf.Cos(k*2.4f),.4f)*.065f));sparkUntil[slot]=Time.unscaledTime+.09f;line.enabled=true;
            }
        }
        IEnumerator LoadMeleeAudio(){foreach(string name in new[]{"swish-light","swish-heavy","clash","parry","flesh-hit","block"})yield return meleeAudio.LoadOptional(name,"Expansion/Audio/melee/"+name+".wav");}
        void ClearMeleePresentation(){if(meleeLoad!=null){StopCoroutine(meleeLoad);meleeLoad=null;}if(meleeAudio!=null){meleeAudio.Dispose();meleeAudio=null;}if(meleeSparks!=null)foreach(var line in meleeSparks)if(line!=null)Destroy(line.gameObject);if(meleeMaterial!=null)Destroy(meleeMaterial);}
    }
}
#endif
