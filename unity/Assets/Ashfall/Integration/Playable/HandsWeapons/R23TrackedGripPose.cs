#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public static class R23TrackedGripPose
    {
        // weapon.gd::corrected_grip_transform is raw * Rx(-PI/2) in Godot.
        // Conjugating by the single Z reflection gives raw * Rx(+PI/2) in Unity.
        // This applies to held weapons/reload props, never to tracked poses or menu aim.
        const float HalfSqrtTwo=.7071067811865475f;
        public static Quaternion LocalRotation {get{return new Quaternion(HalfSqrtTwo,0,0,HalfSqrtTwo);}}
        public static Vector3 Forward(Transform trackedHand){return trackedHand.rotation*LocalRotation*Vector3.forward;}
        // Original glove mesh wrist→middle-knuckle and wrist→thumb axes after
        // the existing asset conversion. Map fingers to the accepted gun bore
        // and the thumb-side axis to gun-up; never rotate the tracked hand.
        public static Quaternion GloveLocalRotation(int hand)
        {
            return hand==0?new Quaternion(.40611717f,-.54012005f,.64663062f,.35384745f):
                new Quaternion(-.40611717f,-.54012005f,.64663062f,-.35384745f);
        }
        public static Vector3 GripCenteredOffset(Vector3 modelGrip,float scale){return -(LocalRotation*(modelGrip*scale));}
        // weapon.gd/pistol.gd support art: unit visible-model basis times the
        // glove's rest basis relative to corrected grip. Tracking is never written.
        public static Quaternion SupportArtRotation(Quaternion modelRotation,int hand)
        {
            return modelRotation*new Quaternion(-HalfSqrtTwo,0,0,HalfSqrtTwo)*GloveLocalRotation(hand);
        }
    }
}
#endif
