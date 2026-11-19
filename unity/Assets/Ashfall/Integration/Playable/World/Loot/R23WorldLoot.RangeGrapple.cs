#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.World.Loot
{
    public sealed partial class R23WorldLoot
    {
        string rangeGrapple;GameObject rangeGrappleTable;Material rangeTableMaterial;
        public string RangeGrappleActionId {get{return rangeGrapple;}}
        void ClearRangeGrapple()
        {
            rangeGrapple=null;
            if(rangeGrappleTable!=null)Destroy(rangeGrappleTable);
            if(rangeTableMaterial!=null)Destroy(rangeTableMaterial);
            rangeGrappleTable=null;rangeTableMaterial=null;
        }
        void EnsureRangeGrappleTable()
        {
            if(rangeGrappleTable!=null)return;
            rangeGrappleTable=new GameObject("Range grapple tool table");rangeGrappleTable.transform.SetParent(map.transform,false);
            rangeGrappleTable.transform.position=map.Spawn+new Vector3(-.8f,0,1.6f);
            rangeTableMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));rangeTableMaterial.SetColor("_BaseColor",new Color(.12f,.16f,.18f));
            foreach(Vector3 row in new[]{new Vector3(0,.55f,0),new Vector3(-.32f,.25f,0),new Vector3(.32f,.25f,0)})
            {
                var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name="Grapple table support";part.transform.SetParent(rangeGrappleTable.transform,false);part.transform.localPosition=row;
                part.transform.localScale=row.y>.5f?new Vector3(.9f,.1f,.65f):new Vector3(.08f,.5f,.5f);part.GetComponent<Renderer>().sharedMaterial=rangeTableMaterial;
            }
        }
        void RefreshRangeGrapple()
        {
            if(currentMap!="range"||!prepared||!enabledWorld)return;
            // One current display copy; the taken instance retains its normal custody.
            string instance;Vector3 center,at;Quaternion rotation;
            if(rangeGrapple!=null&&weapons.TryReadWorldWeapon(rangeGrapple,out instance,out center,out at,out rotation))return;
            if(rangeGrapple!=null){instances.Remove(rangeGrapple);baselineEager.Remove(rangeGrapple);}
            EnsureRangeGrappleTable();
            Vector3 point=map.Spawn+new Vector3(-.8f,.9f,1.6f);
            if(!weapons.SpawnWorldWeapon("grapple",2,point,Quaternion.Euler(0,90,0),out rangeGrapple,true))return;
            instances.Add(rangeGrapple);baselineEager.Add(rangeGrapple);SpawnedCount++;
        }
    }
}
#endif
