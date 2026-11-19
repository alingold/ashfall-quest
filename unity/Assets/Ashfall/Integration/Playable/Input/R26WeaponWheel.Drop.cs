#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.XR;
namespace Ashfall.Integration.Playable.Input
{
    public sealed partial class R23WeaponWheel
    {
        const int DropSegments=32;
        readonly Vector3[] dropVertices=new Vector3[DropSegments*4];
        Mesh dropMesh;Transform dropRing;Func<string,int,bool> tryDrop;Func<string,bool> dropConfirmed;
        string pendingDropId;int pendingDropHand,dropTickSlot=-1,dropTicks;float pendingDropUntil;
        public void ConfigureDrop(Func<string,int,bool> request,Func<string,bool> confirmed){tryDrop=request;dropConfirmed=confirmed;}
        void InitializeDropRing()
        {
            var triangles=new int[DropSegments*6];for(int i=0;i<DropSegments;i++){int v=i*4,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;}
            dropMesh=Mesh(dropVertices,triangles,null);dropMesh.name="Reusable source drop ring";dropMesh.MarkDynamic();dropMesh.bounds=new Bounds(Vector3.zero,new Vector3(.148f,.148f,.001f));
            var material=Material(Hex("ffbd58"));material.renderQueue=4126;dropRing=Shape("Drop progress ring",dropMesh,material,root);dropRing.gameObject.SetActive(false);
        }
        void HideDropRing(){if(dropRing!=null)dropRing.gameObject.SetActive(false);dropTickSlot=-1;dropTicks=0;}
        void UpdateDropProgress(int primaryHand)
        {
            int slot=state.DropSlot;float fraction=state.DropFraction;
            if(!Active||slot<0||fraction<=0||tryDrop==null){HideDropRing();return;}
            dropRing.localPosition=At(R23WeaponWheelRules.SectorForSlot(slot),.215f)+Vector3.back*.029f;dropRing.gameObject.SetActive(true);
            // Fixed topology, reused vertices. Unfilled segments collapse to the arc end.
            // Godot radii .067/.074, 32 segments, clockwise from twelve o'clock.
            for(int i=0;i<DropSegments;i++){
                float a=Mathf.PI*.5f-Mathf.Min(i/(float)DropSegments,fraction)*Mathf.PI*2,b=Mathf.PI*.5f-Mathf.Min((i+1)/(float)DropSegments,fraction)*Mathf.PI*2;
                Vector3 start=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),end=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0);int v=i*4;
                dropVertices[v]=start*.067f;dropVertices[v+1]=start*.074f;dropVertices[v+2]=end*.074f;dropVertices[v+3]=end*.067f;
            }
            dropMesh.vertices=dropVertices;
            if(dropTickSlot!=slot){dropTickSlot=slot;dropTicks=0;}
            int ticks=(int)(fraction*R23WeaponWheelState.DropHoldSeconds/.5f);if(ticks>dropTicks){dropTicks=ticks;DropHaptic(primaryHand,.16f,.025f);}
        }
        bool RequestDrop(R23WeaponWheelRequest request)
        {
            if(tryDrop==null||!tryDrop(request.Id,request.Hand))return false;
            pendingDropId=request.Id;pendingDropHand=request.Hand;pendingDropUntil=Time.unscaledTime+3.5f;
            PollDropConfirmation(true,request.Hand);return true;
        }
        void PollDropConfirmation(bool valid,int primaryHand)
        {
            if(pendingDropId==null)return;
            if(!valid||primaryHand!=pendingDropHand||Time.unscaledTime>pendingDropUntil){pendingDropId=null;return;}
            // A queued guest request is not success: wait for the existing host receipt
            // path to remove ownership AND create the local world weapon.
            if(dropConfirmed!=null&&dropConfirmed(pendingDropId)){pendingDropId=null;DropHaptic(pendingDropHand,.7f,.10f);}
        }
        static void DropHaptic(int hand,float amplitude,float duration)
        {
            var device=InputDevices.GetDeviceAtXRNode(hand==0?XRNode.LeftHand:XRNode.RightHand);HapticCapabilities cap;
            if(device.isValid&&device.TryGetHapticCapabilities(out cap)&&cap.supportsImpulse&&cap.numChannels>0)device.SendHapticImpulse(0,amplitude,duration);
        }
    }
}
#endif
