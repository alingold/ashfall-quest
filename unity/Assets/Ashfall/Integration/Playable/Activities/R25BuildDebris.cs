#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.Playable.Activities
{
    // Purely cosmetic pool: no colliders or bodies, and layer 26 is query-excluded.
    internal sealed class R25BuildDebris:IDisposable
    {
        sealed class Shard
        {
            internal GameObject Object;
            internal Vector3 Start,Velocity,Scale;
            internal float Born,Life;
            internal bool Fold;
        }
        readonly Shard[] pool=new Shard[96];
        readonly GameObject root;
        readonly Material material;
        int cursor;
        internal R25BuildDebris(Transform parent,Material finish)
        {
            root=new GameObject("Build removal cosmetics");root.layer=26;root.transform.SetParent(parent,false);
            material=finish;
        }
        Shard Next()
        {
            int index=cursor;cursor=(cursor+1)%pool.Length;
            if(pool[index]==null){
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Pooled build shard";go.layer=26;
                go.transform.SetParent(root.transform,false);var collider=go.GetComponent<Collider>();collider.enabled=false;UnityEngine.Object.Destroy(collider);
                go.GetComponent<Renderer>().sharedMaterial=material;pool[index]=new Shard{Object=go};
            }return pool[index];
        }
        internal void Emit(GameObject piece,bool fold)
        {
            var renderers=piece.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0)return;
            int count=fold?renderers.Length:8;
            for(int i=0;i<count;i++){
                var source=renderers[i%renderers.Length];var bounds=source.bounds;var shard=Next();
                shard.Fold=fold;shard.Born=Time.unscaledTime;shard.Life=fold?.28f:1.5f;
                shard.Start=fold?source.transform.position:bounds.center+new Vector3(((i*37)%11/10f-.5f)*bounds.size.x,((i*17)%11/10f-.5f)*bounds.size.y,((i*7)%11/10f-.5f)*bounds.size.z);
                shard.Velocity=new Vector3((i%3-1)*1.2f,1+(i%4)*.35f,(i%2==0?-1:1)*.8f);
                shard.Scale=fold?source.transform.lossyScale:new Vector3(.11f,.16f,.06f);
                shard.Object.transform.SetPositionAndRotation(shard.Start,Quaternion.identity);shard.Object.transform.localScale=shard.Scale;
                var properties=new MaterialPropertyBlock();source.GetPropertyBlock(properties);shard.Object.GetComponent<Renderer>().SetPropertyBlock(properties);shard.Object.SetActive(true);
            }
        }
        internal void Tick()
        {
            foreach(var shard in pool){
                if(shard==null||!shard.Object.activeSelf)continue;
                float age=Time.unscaledTime-shard.Born,t=Mathf.Clamp01(age/shard.Life);
                if(t>=1){shard.Object.SetActive(false);continue;}
                float scale=shard.Fold?1-t*t:Mathf.Clamp01((1-t)*6);
                shard.Object.transform.localScale=shard.Scale*scale;
                if(!shard.Fold){shard.Object.transform.position=shard.Start+shard.Velocity*age+Vector3.down*(3*age*age);shard.Object.transform.rotation=Quaternion.Euler(age*140,age*95,age*60);}
            }
        }
        internal void Clear(){foreach(var shard in pool)if(shard!=null)shard.Object.SetActive(false);}
        public void Dispose(){UnityEngine.Object.Destroy(root);}
    }
}
#endif
