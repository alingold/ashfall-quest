#if UNITY_6000_0_OR_NEWER
using System.Collections;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Integration.Playable.Traversal
{
    // Collider-free procedural art. Optional clips share the existing WAV bank.
    public sealed class R25TraversalPresentation:MonoBehaviour
    {
        readonly LineRenderer[] lines=new LineRenderer[2],rings=new LineRenderer[2],wrists=new LineRenderer[2];
        readonly Transform[] flames=new Transform[2];
        readonly Mesh[] flameMeshes=new Mesh[2];
        Material material;LineRenderer fuelArc;R24ItemAudio audioBank;
        readonly AudioSource[] zipVoices=new AudioSource[2];AudioSource jetVoice,cueVoice,creakVoice;bool ready,jetWas,lowWas;float nextCreak;
        public void Initialize()
        {
            if(ready)return;ready=true;
            material=new Material(Shader.Find("Sprites/Default"));material.color=Color.white;
            for(int i=0;i<2;i++){
                lines[i]=Line("Web line",new Color(.7f,.95f,1),.012f,17);
                rings[i]=Line("Web anchor ring",Color.cyan,.008f,25);
                wrists[i]=Line("Web wrist glow",Color.cyan,.015f,25);
                flames[i]=new GameObject("Jet flame cone").transform;flames[i].SetParent(transform,false);
                var mesh=new Mesh();flameMeshes[i]=mesh;var vertices=new Vector3[14];var triangles=new int[36];
                vertices[0]=new Vector3(0,-.55f,0);vertices[13]=Vector3.zero;
                for(int k=0;k<12;k++){float angle=k*Mathf.PI/6;vertices[k+1]=new Vector3(Mathf.Cos(angle)*.085f,0,Mathf.Sin(angle)*.085f);triangles[k*3]=0;triangles[k*3+1]=k+1;triangles[k*3+2]=(k+1)%12+1;}
                mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();flames[i].gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var light=flames[i].gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=Color.cyan;light.range=1;light.intensity=.5f;light.shadows=LightShadows.None;
                var renderer=flames[i].gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(.2f,.7f,1));renderer.SetPropertyBlock(block);
            }
            fuelArc=Line("Jet fuel arc",Color.cyan,.009f,33);
            audioBank=new R24ItemAudio();jetVoice=audioBank.Voice(transform,"Jet loop",-16,35,true);cueVoice=audioBank.Voice(transform,"Traversal cue",-12,35);creakVoice=audioBank.Voice(transform,"Web creak",-22,20);
            for(int i=0;i<2;i++)zipVoices[i]=audioBank.Voice(transform,"Web zip loop",-22,25,true);
            StartCoroutine(Load());Hide();
        }
        IEnumerator Load(){string[] jets={"ignite","thrust-loop","sputter","low-fuel"},web={"shoot","attach","zip-loop","creak","release"};foreach(string name in jets)yield return audioBank.LoadOptional(name,"Expansion/Audio/jetpack/"+name+".wav");foreach(string name in web)yield return audioBank.LoadOptional(name,"Expansion/Audio/web/"+name+".wav");}
        LineRenderer Line(string name,Color color,float width,int count){var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=count;line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;}
        static void Ring(LineRenderer line,Vector3 center,Vector3 right,Vector3 up,float radius){for(int k=0;k<line.positionCount;k++){float a=k*2*Mathf.PI/(line.positionCount-1);line.SetPosition(k,center+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius);}}
        public void Cue(string name,Vector3 at){if(!ready)return;cueVoice.transform.position=at;audioBank.Play(cueVoice,name);}
        public void Web(int hand,Vector3 wrist,Vector3 anchor,bool attached,float flight,float length,bool aiming,Vector3 candidate,float pull=0)
        {
            if(!ready)return;var zip=zipVoices[hand];zip.transform.position=wrist;if(attached&&flight<=0&&pull>.1f){if(!zip.isPlaying)audioBank.Play(zip,"zip-loop");zip.pitch=.8f+.4f*pull;}else zip.Stop();var line=lines[hand];var ring=rings[hand];var glow=wrists[hand];glow.enabled=false;line.enabled=ring.enabled=attached||aiming;if(!line.enabled)return;
            Vector3 end=attached?anchor:candidate;
            if(attached&&flight>0)end=Vector3.Lerp(anchor,wrist,Mathf.Clamp01(flight*120/Mathf.Max(2,Vector3.Distance(wrist,anchor))));
            float slack=attached?Mathf.Clamp(length-Vector3.Distance(wrist,anchor),0,.8f):.1f;
            for(int k=0;k<line.positionCount;k++){float t=(float)k/(line.positionCount-1);line.SetPosition(k,Vector3.Lerp(wrist,end,t)+Vector3.down*(4*t*(1-t)*slack*.25f));}
            Vector3 normal=(wrist-end).normalized,right=Vector3.Cross(normal,Vector3.up).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;
            Ring(ring,end,right,Vector3.Cross(normal,right),aiming?.1f:.065f);Ring(glow,wrist,Vector3.right,Vector3.forward,.045f);
            if(attached&&flight<=0&&slack<.05f&&Time.unscaledTime>nextCreak){nextCreak=Time.unscaledTime+1.4f;creakVoice.transform.position=wrist;audioBank.Play(creakVoice,"creak");}
        }
        public void Jet(Vector3 head,Quaternion rotation,bool active,float power,float fuel,Transform offhand)
        {
            if(!ready)return;
            for(int i=0;i<2;i++){flames[i].gameObject.SetActive(active);flames[i].position=head+rotation*new Vector3(i==0?-.17f:.17f,-.65f,-.24f);flames[i].rotation=rotation;flames[i].localScale=new Vector3(1,Mathf.Max(.2f,power),1);}
            jetVoice.transform.position=head;
            if(active&&!jetWas){Cue("ignite",head);audioBank.Play(jetVoice,"thrust-loop");}
            if(active&&jetVoice.clip==null)audioBank.Play(jetVoice,"thrust-loop");
            if(!active)jetVoice.Stop();jetVoice.pitch=.7f+.3f*power;jetVoice.volume=.12f*power;
            if(active&&power<.5f&&jetWas)Cue("sputter",head);
            if(fuel<=.2f&&!lowWas)Cue("low-fuel",head);lowWas=fuel<=.2f;jetWas=active;
            fuelArc.enabled=false; // R26: no permanent offhand fuel ring; low-fuel audio remains.
            if(fuelArc.enabled&&offhand!=null)for(int k=0;k<33;k++){float a=k/32f*Mathf.PI*1.8f*fuel;fuelArc.SetPosition(k,offhand.position+offhand.up*.065f+(offhand.right*Mathf.Cos(a)+offhand.forward*Mathf.Sin(a))*.07f);}
        }
        public void Hide(){if(!ready)return;foreach(var line in lines)line.enabled=false;foreach(var ring in rings)ring.enabled=false;foreach(var glow in wrists)glow.enabled=false;foreach(var flame in flames)flame.gameObject.SetActive(false);fuelArc.enabled=false;audioBank.Stop();jetWas=false;}
        void OnDisable(){Hide();}
        void OnDestroy(){StopAllCoroutines();if(audioBank!=null)audioBank.Dispose();if(material!=null)Destroy(material);foreach(var mesh in flameMeshes)if(mesh!=null)Destroy(mesh);}
    }
}
#endif
