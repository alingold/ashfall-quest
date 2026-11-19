#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.Explosives;
namespace Ashfall.Integration.Playable.Vehicles
{
    // r19 VEHICLES19: the overhead eject handle. Built around the rest point of the grab bar; pulling slides the bar down and stretches the cables.
    public sealed class R26VehicleEjectHandleView:MonoBehaviour
    {
        Transform bar,cableLeft,cableRight;Material barMaterial,cableMaterial;AudioSource source;static AudioClip click;
        static readonly Color Idle=new Color(1,.416f,.169f),Grabbed=new Color(1,.85f,.25f),Ready=new Color(.25f,1,.45f);
        public static R26VehicleEjectHandleView Create(Transform parent,string name)
        {
            var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);
            var view=go.AddComponent<R26VehicleEjectHandleView>();view.Build();return view;
        }
        void Build()
        {
            barMaterial=R25ItemArtGeometry.Surface(Idle,.1f,.4f,true);cableMaterial=R25ItemArtGeometry.Surface(new Color(.12f,.13f,.14f),.5f,.35f);
            const float half=.14f;
            var barMesh=new R25VehicleMesh();barMesh.Tube(new[]{new Vector3(-half,0,0),new Vector3(half,0,0)},.024f,10);
            barMesh.Tube(new[]{new Vector3(-half,-.02f,0),new Vector3(-half,.04f,0)},.034f,10);barMesh.Tube(new[]{new Vector3(half,-.02f,0),new Vector3(half,.04f,0)},.034f,10);
            bar=barMesh.Attach(transform,"Eject handle grab bar",barMaterial).transform;
            cableLeft=Cable(-half);cableRight=Cable(half);
            var plate=new R25VehicleMesh();plate.Plate(new Vector3(0,R26VehicleSeat.CableLength+.02f,0),new Vector3(.34f,.03f,.08f),Quaternion.identity);plate.Attach(transform,"Eject handle roof plate",cableMaterial);
            source=gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.minDistance=1;source.maxDistance=20;source.playOnAwake=false;
        }
        Transform Cable(float x)
        {
            var mesh=new R25VehicleMesh();mesh.Tube(new[]{new Vector3(0,-1,0),Vector3.zero},.008f,6);
            var t=mesh.Attach(transform,"Eject handle cable",cableMaterial).transform;t.localPosition=new Vector3(x,R26VehicleSeat.CableLength,0);return t;
        }
        // rest: world position of the bar at zero pull. forward: the seat's forward direction.
        public void Show(Vector3 rest,Vector3 forward,float pull,bool grabbed,bool visible)
        {
            if(gameObject.activeSelf!=visible)gameObject.SetActive(visible);if(!visible)return;
            if(forward.sqrMagnitude>.0001f)transform.SetPositionAndRotation(rest,Quaternion.LookRotation(forward,Vector3.up));else transform.position=rest;
            Apply(pull,grabbed);
        }
        // Parented to the rig: local rest point and rotation come from the rig.
        public void ShowLocal(Vector3 localRest,float pull,bool grabbed,bool visible)
        {
            if(gameObject.activeSelf!=visible)gameObject.SetActive(visible);if(!visible)return;
            transform.localPosition=localRest;transform.localRotation=Quaternion.identity;Apply(pull,grabbed);
        }
        void Apply(float pull,bool grabbed)
        {
            pull=Mathf.Clamp(pull,0,R26VehicleSeat.MaxPull);
            bar.localPosition=new Vector3(0,-pull,0);
            cableLeft.localScale=cableRight.localScale=new Vector3(1,R26VehicleSeat.CableLength+pull,1);
            bool ready=R26VehicleSeat.PullEjects(pull);
            barMaterial.SetColor("_BaseColor",ready?Ready:grabbed?Grabbed:Idle);barMaterial.SetColor("_EmissionColor",(ready?Ready:grabbed?Grabbed:Idle)*(grabbed||ready?1.6f:.9f));
        }
        public void Click(float pitch=1)
        {
            if(source==null||!gameObject.activeInHierarchy)return;
            if(click==null){
                const int rate=44100,n=rate/9;var data=new float[n];uint seed=7;
                for(int i=0;i<n;i++){seed=seed*1664525u+1013904223u;float noise=((seed>>9)/(float)(1<<23))*2-1;float t=i/(float)rate;float env=Mathf.Exp(-t*55);data[i]=(noise*.45f+Mathf.Sin(t*2*Mathf.PI*180)*.7f+Mathf.Sin(t*2*Mathf.PI*95)*.5f)*env*.8f;}
                click=AudioClip.Create("R26 eject handle clunk",n,1,rate,false);click.SetData(data,0);
            }
            source.pitch=pitch;source.PlayOneShot(click,1);
        }
        void OnDestroy(){if(barMaterial!=null)Destroy(barMaterial);if(cableMaterial!=null)Destroy(cableMaterial);foreach(var f in GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh!=null)Destroy(f.sharedMesh);}
    }
}
#endif
