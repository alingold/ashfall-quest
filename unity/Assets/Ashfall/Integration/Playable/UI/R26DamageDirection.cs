#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;using Ashfall.Integration.Playable.Networking;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.UI
{
    // Godot incoming_fire.gd: four open low-view chevron chains, one-second fade.
    [DefaultExecutionOrder(1500)]
    public sealed class R26DamageDirection:MonoBehaviour
    {
        readonly R26DamageDirectionState state=new R26DamageDirectionState();readonly Transform[] chains=new Transform[4];readonly Material[] materials=new Material[4];
        R23PlayableLoop root;Mesh mesh;Vector3 lastForward=Vector3.forward;long epoch=-1,life=-1;string map;bool initialized;
        public int Notifications{get;private set;}public int VisibleChains{get;private set;}
        public void Initialize(R23PlayableLoop loop)
        {
            root=loop;mesh=BuildMesh();var shader=Resources.Load<Shader>("R23GunFeedback");
            for(int i=0;i<chains.Length;i++){
                var node=new GameObject("Incoming fire chevrons "+i);node.layer=18;node.transform.SetParent(transform,false);chains[i]=node.transform;node.AddComponent<MeshFilter>().sharedMesh=mesh;
                var material=new Material(shader);materials[i]=material;material.SetColor("_Color",new Color(.98f,.045f,.035f,1));material.SetFloat("_Depth",8);material.SetFloat("_Cull",0);material.renderQueue=3125;
                var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;node.SetActive(false);
            }
            initialized=true;Application.onBeforeRender+=Present;
        }
        public void NotifyDamage(Vector3 source){if(!initialized||!root.IncomingDamageVisible)return;var eye=root.Rig.Head.transform.position;state.Notify(F(source),F(eye),Time.unscaledTimeAsDouble);Notifications++;Present();}
        public void Clear(){state.Clear();VisibleChains=0;foreach(var chain in chains)if(chain!=null)chain.gameObject.SetActive(false);}
        void LateUpdate()
        {
            if(!initialized)return;var lobby=root.LanSession==null?null:root.LanSession.Lobby;string currentMap=root.Map==null?null:root.Map.MapId;long currentEpoch=lobby==null?-1:lobby.Epoch;R24LanHealthState health;long currentLife=lobby!=null&&lobby.TryReadHealth(lobby.OwnActor,out health)?health.LifeGeneration:-1;
            if(map!=currentMap||epoch!=currentEpoch||life!=currentLife){Clear();map=currentMap;epoch=currentEpoch;life=currentLife;}
            if(lobby!=null){R26DamageDirectionReceipt cue;int budget=32;while(budget-->0&&lobby.TryDequeueDamageDirection(out cue))if(root.IncomingDamageVisible&&lobby.TryReadHealth(lobby.OwnActor,out health)&&cue.Spawn==health.SpawnRevision&&cue.Life==health.LifeGeneration)NotifyDamage(U(cue.Source));}
            if(!root.IncomingDamageVisible){Clear();return;}Present();
        }
        void Present()
        {
            if(!initialized||root==null||!root.IncomingDamageVisible)return;var head=root.Rig.Head.transform;Vector3 forward=head.forward;forward.y=0;if(forward.sqrMagnitude>.0001f)lastForward=forward.normalized;forward=lastForward;
            Vector3 right=Vector3.Cross(Vector3.up,forward);var upright=Quaternion.LookRotation(forward,Vector3.up);VisibleChains=0;
            for(int i=0;i<chains.Length;i++){
                float alpha=state.Remaining(i,Time.unscaledTimeAsDouble);bool show=alpha>0;chains[i].gameObject.SetActive(show);if(!show)continue;VisibleChains++;
                float bearing=R26DamageDirectionState.Bearing(F(head.position),F(forward),state.Source(i));
                chains[i].SetPositionAndRotation(head.position+forward*2.2f+right*(Mathf.Sin(bearing)*.36f)+Vector3.up*(-1.08f-Mathf.Cos(bearing)*.18f),upright*Quaternion.Euler(0,0,-bearing*Mathf.Rad2Deg));chains[i].localScale=Vector3.one*2;
                materials[i].SetColor("_Color",new Color(.98f,.045f,.035f,alpha));
            }
        }
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}static Vector3 U(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
        static Mesh BuildMesh()
        {
            var v=new Vector3[24];var t=new int[48];float[] widths={.23f,.18f,.135f,.095f},heights={.085f,.073f,.061f,.05f},ys={0,.105f,.193f,.265f};int[] order={0,1,4,0,4,5,1,2,3,1,3,4};
            for(int i=0;i<4;i++){float w=widths[i],h=heights[i],y=ys[i];int n=i*6;v[n]=new Vector3(-w*.5f,y-h*.35f,0);v[n+1]=new Vector3(0,y+h*.5f,0);v[n+2]=new Vector3(w*.5f,y-h*.35f,0);v[n+3]=new Vector3(w*.5f-.025f,y-h*.65f,0);v[n+4]=new Vector3(0,y+h*.03f,0);v[n+5]=new Vector3(-w*.5f+.025f,y-h*.65f,0);for(int k=0;k<12;k++)t[i*12+k]=n+order[k];}
            var mesh=new Mesh();mesh.name="R26 Godot incoming fire chevrons";mesh.vertices=v;mesh.triangles=t;mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy(){Application.onBeforeRender-=Present;if(root!=null&&root.Gameplay!=null)root.Gameplay.BindIncomingDirection(null);if(mesh!=null)Destroy(mesh);foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
#endif
