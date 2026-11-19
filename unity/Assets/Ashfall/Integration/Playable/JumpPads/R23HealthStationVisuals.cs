#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.JumpPads
{
    internal sealed class R23HealthStationVisuals:IDisposable
    {
        readonly Transform root,head;readonly Transform[] rings;readonly TextMesh[] labels;readonly Material[] materials;readonly Mesh mesh;readonly AudioSource voice;readonly Font font;
        public bool AudioReady {get{return voice!=null&&voice.clip!=null;}}
        public R23HealthStationVisuals(Transform owner,Transform actor,R23SourceHealthStation[] rows)
        {
            root=new GameObject("Original R18 station rings/countdowns").transform;root.SetParent(owner,false);
            var camera=actor.GetComponentInChildren<Camera>(true);head=camera==null?actor:camera.transform;
            font=Resources.Load<Font>("R23GodotUI");var shader=Resources.Load<Shader>("R23StationRing");var textShader=Shader.Find("Ashfall/R23GunFeedback");
            if(shader==null||font==null||textShader==null)throw new InvalidOperationException("Source R18 station render resources unavailable");
            rings=new Transform[rows.Length];labels=new TextMesh[rows.Length];materials=new Material[rows.Length*2];
            mesh=new Mesh{name="Source1.35m station QuadMesh"};mesh.vertices=new[]{new Vector3(-.675f,-.675f,0),new Vector3(.675f,-.675f,0),new Vector3(.675f,.675f,0),new Vector3(-.675f,.675f,0)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.uv=new[]{Vector2.up,Vector2.one,Vector2.right,Vector2.zero};mesh.RecalculateBounds();
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];var go=new GameObject("R18 station ring "+i);go.transform.SetParent(root,false);go.transform.position=new Vector3(row.Center.X,row.Center.Y+.86f,row.Center.Z);rings[i]=go.transform;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();var material=new Material(shader);materials[i*2]=material;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                var label=new GameObject("R18 station countdown "+i);label.transform.SetParent(root,false);label.transform.position=go.transform.position-Vector3.up*.64f;
                var text=label.AddComponent<TextMesh>();text.font=font;text.fontSize=36;text.characterSize=1;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.richText=false;text.color=new Color(1,217f/255,128f/255);labels[i]=text;
                var ink=new Material(textShader);ink.renderQueue=3201;ink.SetColor("_Color",new Color(1,217f/255,128f/255));ink.SetFloat("_Font",1);ink.SetFloat("_Cull",0);ink.SetFloat("_Depth",4);ink.SetTexture("_MainTex",font.material.mainTexture);materials[i*2+1]=ink;label.GetComponent<MeshRenderer>().sharedMaterial=ink;label.SetActive(false);
            }
            var audio=new GameObject("Original R18 station restore 3D SFX");audio.transform.SetParent(root,false);voice=audio.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;voice.minDistance=3;voice.maxDistance=25;voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.dopplerLevel=0;voice.clip=Resources.Load<AudioClip>("R23HealthStations/station_restore");
        }
        public void SetSfxVolume(float value){voice.volume=float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);}
        public void PlayRestore(Vector3 feet){if(voice.clip!=null){voice.transform.position=feet;voice.Play();}}
        public void Refresh(double now,R23HealthStationRecharge recharge)
        {RefreshShared(delegate(int i){return recharge.Remaining(i,now);});}
        public void RefreshShared(Func<int,double> remainingAt)
        {
            for(int i=0;i<rings.Length;i++)
            {
                float remaining=(float)remainingAt(i);rings[i].rotation=head.rotation;materials[i*2].SetFloat("_Recharge",remaining/15);
                var label=labels[i];bool show=remaining>0&&(label.transform.position-head.position).sqrMagnitude<3600;label.gameObject.SetActive(show);if(!show)continue;
                label.transform.rotation=head.rotation;label.text=Mathf.CeilToInt(remaining)+"s";
                font.RequestCharactersInTexture(label.text,36,FontStyle.Normal);materials[i*2+1].SetTexture("_MainTex",font.material.mainTexture);CharacterInfo cap;
                float size=font.GetCharacterInfo('H',out cap,36,FontStyle.Normal)?cap.glyphHeight*.003f:.078f;
                var bounds=label.GetComponent<MeshRenderer>().localBounds.size;if(bounds.y>.00001f)label.transform.localScale=Vector3.one*(size/bounds.y);
            }
        }
        public void Dispose(){if(root!=null){root.gameObject.SetActive(false);UnityEngine.Object.Destroy(root.gameObject);}if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(materials!=null)foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);}
    }
}
#endif
