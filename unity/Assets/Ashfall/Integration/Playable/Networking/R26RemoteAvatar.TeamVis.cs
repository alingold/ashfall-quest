#if UNITY_6000_0_OR_NEWER
using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanRemoteAvatar
    {
        bool teamNameAllowed,teamBarsAllowed;Transform teamBars;Mesh teamBarsMesh;Material teamBarsMaterial;
        readonly Vector3[] teamBarVertices=new Vector3[16];readonly Color[] teamBarColors=new Color[16];
        static readonly int[] TeamBarIndices={0,2,1,0,3,2,4,6,5,4,7,6,8,10,9,8,11,10,12,14,13,12,15,14};
        float teamHealth=-1,teamShield=-1;
        public bool TeamNameVisible{get{return nameTag!=null&&nameTag.gameObject.activeInHierarchy;}}
        public bool TeamBarsVisible{get{return teamBars!=null&&teamBars.gameObject.activeInHierarchy;}}
        public float TeamHealth{get{return teamHealth;}}public float TeamShield{get{return teamShield;}}
        internal void ReceiveTeamVisibility(bool showName,bool showBars,R24LanHealthState health)
        {
            teamNameAllowed=showName;teamBarsAllowed=showName&&showBars&&health!=null&&health.Connected;
            if(!teamBarsAllowed)return;
            if(teamBars==null)
            {
                teamBars=new GameObject("Teammate health and shield").transform;teamBars.SetParent(nameTag,false);teamBars.gameObject.layer=18;
                teamBarsMesh=new Mesh{name="Two compact teammate vitals"};teamBarsMesh.MarkDynamic();teamBars.gameObject.AddComponent<MeshFilter>().sharedMesh=teamBarsMesh;
                teamBarsMaterial=new Material(Resources.Load<Shader>("R26TeamVisBars"));var renderer=teamBars.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=teamBarsMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            if(teamHealth==health.Health&&teamShield==health.Shield)return;teamHealth=health.Health;teamShield=health.Shield;
            TeamBarQuad(0,-.12f,.44f,new Color(.08f,.10f,.13f),0);TeamBarQuad(4,-.16f,.44f,new Color(.08f,.10f,.13f),0);
            TeamBarQuad(8,-.12f,.44f*Mathf.Clamp01(teamHealth/100),new Color(.25f,1,.45f),-.001f);TeamBarQuad(12,-.16f,.44f*Mathf.Clamp01(teamShield/100),new Color(.25f,.7f,1),-.001f);
            teamBarsMesh.vertices=teamBarVertices;teamBarsMesh.colors=teamBarColors;teamBarsMesh.triangles=TeamBarIndices;teamBarsMesh.RecalculateBounds();
        }
        void TeamBarQuad(int n,float y,float width,Color tint,float z)
        {
            teamBarVertices[n]=new Vector3(-.22f,y,z);teamBarVertices[n+1]=new Vector3(-.22f,y-.025f,z);teamBarVertices[n+2]=new Vector3(-.22f+width,y-.025f,z);teamBarVertices[n+3]=new Vector3(-.22f+width,y,z);for(int i=0;i<4;i++)teamBarColors[n+i]=tint;
        }
        void PresentTeamTag()
        {
            bool show=Visible&&target.Tracked&&teamNameAllowed;if(nameTag.gameObject.activeSelf!=show)nameTag.gameObject.SetActive(show);
            if(teamBars!=null&&teamBars.gameObject.activeSelf!=teamBarsAllowed)teamBars.gameObject.SetActive(teamBarsAllowed);
            nameTag.position=head.position+Vector3.up*.35f;
            if(viewer!=null){float distance=Vector3.Distance(nameTag.position,viewer.transform.position);nameTag.rotation=Quaternion.LookRotation(nameTag.position-viewer.transform.position,Vector3.up);nameTag.localScale=Vector3.one*Mathf.Clamp(distance/12,1,3);}
        }
        void DisposeTeamTag(){if(teamBarsMesh!=null)Destroy(teamBarsMesh);if(teamBarsMaterial!=null)Destroy(teamBarsMaterial);}
    }
}
#endif
