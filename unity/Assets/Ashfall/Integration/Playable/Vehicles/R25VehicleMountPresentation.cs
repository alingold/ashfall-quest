#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Vehicles
{
    public static class R25VehicleMountPresentation
    {
        public static Transform Create(Transform parent,string kind,Material glow)
        {
            var group=new GameObject("Boarding handles - empty hand grip 0.4 seconds").transform;
            group.SetParent(parent,false);
            foreach(int side in new[]{-1,1}){
                var p=R25BikeRider.MountOffset(kind,side);var at=new Vector3(p.X,p.Y,p.Z);
                var mesh=new R25VehicleMesh();mesh.Tube(new[]{at+Vector3.down*.13f,at+Vector3.left*.04f,at+Vector3.up*.13f},.035f);
                mesh.Attach(group,"Glowing boarding handle",glow);
                var text=new GameObject("HOLD GRIP prompt").AddComponent<TextMesh>();text.transform.SetParent(group,false);
                text.transform.localPosition=at+Vector3.up*.26f;text.transform.localRotation=Quaternion.Euler(0,-side*90,0);
                text.font=Resources.Load<Font>("R23InventoryUI");text.fontSize=48;text.characterSize=.018f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;
                if(text.font!=null)text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
                text.text="EMPTY HAND\nHOLD GRIP 0.4s";
            }
            return group;
        }
    }
}
#endif
