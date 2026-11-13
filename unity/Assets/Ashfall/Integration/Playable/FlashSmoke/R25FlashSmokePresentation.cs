#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Explosives;
using Ashfall.Integration.Playable.Expansion;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;

namespace Ashfall.Integration.Playable.FlashSmoke
{
    // Local presentation only. Both the passive LAN snapshots and offline clock
    // feed this owner; no collider, damage callback or visibility rule is added.
    public sealed class R25FlashSmokePresentation : MonoBehaviour
    {
        sealed class Cloud
        {
            internal string Id;
            internal Vector3 At;
            internal float Age, Received;
            internal GameObject Node;
            internal Mesh Full, Reduced;
            internal MeshFilter Filter;
            internal Material Material;
            internal AudioSource Hiss;
        }
        readonly Dictionary<string,Cloud> clouds = new Dictionary<string,Cloud>();
        readonly HashSet<string> flashed = new HashSet<string>();
        readonly Queue<string> flashOrder = new Queue<string>();
        readonly R25FlashBlindness blind = new R25FlashBlindness();
        readonly R24ItemAudio audio = new R24ItemAudio();
        Transform eye;
        R23VRExplosives owner;
        Material overlayMaterial;
        Mesh overlayMesh;
        GameObject overlay;
        Renderer overlayRenderer;
        AudioSource bang, ring, pop, fade;
        float smokeAlpha,ringExposure;
        readonly Color[] veilColors={Color.white,Color.white,Color.white,Color.white,Color.white};
        public Action<float> WorldAudioGain;
        public Func<float> SfxGain;

        public void Initialize(R23VRExplosives service, Transform head)
        {
            owner=service;eye=head;
            var shader=Resources.Load<Shader>("R23VrUi");
            if(shader!=null)
            {
                overlayMaterial=new Material(shader);overlayMaterial.renderQueue=4049;
                overlay=new GameObject("R25 flash and inside smoke veil");overlay.layer=18;
                overlay.transform.SetParent(eye,false);
                var camera=eye.GetComponent<Camera>();
                overlay.transform.localPosition=new Vector3(0,0,camera==null?.06f:Mathf.Max(.06f,camera.nearClipPlane*1.5f));
                overlayMesh=new Mesh();overlayMesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0),Vector3.zero};
                overlayMesh.triangles=new[]{0,4,1,1,4,2,2,4,3,3,4,0};overlayMesh.colors=new[]{Color.white,Color.white,Color.white,Color.white,Color.white};
                overlayMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,new Vector2(.5f,.5f)};overlayMesh.RecalculateBounds();
                overlay.AddComponent<MeshFilter>().sharedMesh=overlayMesh;
                overlayRenderer=overlay.AddComponent<MeshRenderer>();overlayRenderer.sharedMaterial=overlayMaterial;overlayRenderer.enabled=false;
            }
            bang=audio.Voice(transform,"Flash bang",-3,100);
            ring=audio.Voice(transform,"Flash ring",-9,1);ring.spatialBlend=0;
            pop=audio.Voice(transform,"Smoke pop",-6,45);
            fade=audio.Voice(transform,"Smoke fade",-12,30);
            StartCoroutine(Load());
        }
        IEnumerator Load()
        {
            foreach(string path in new[]{"flash/bang","flash/ring","smoke/pop","smoke/hiss-loop","smoke/fade"})
                yield return audio.LoadOptional(path,"Expansion/Audio/"+path+".wav");
        }
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        public void Flash(Vector3 at)
        {
            if(!owner.SimulationEnabled)return;
            if(owner.KindAllowed!=null&&!owner.KindAllowed("flash"))return;
            bang.transform.position=at;audio.Play(bang,"flash/bang");
            RaycastHit hit;bool clear=!owner.Ray(eye.position,at,out hit);
            float e=R25FlashExposure.Exposure(F(eye.position),F(eye.forward),F(at),clear);
            if(blind.Trigger(e)){ringExposure=e;ring.volume=e*.3548f*(SfxGain==null?1:SfxGain());audio.Play(ring,"flash/ring");}
        }
        public void Shared(R24LanProjectileState row)
        {
            if(row.Kind=="flash"&&row.Ended&&flashed.Add(row.Id))
            {
                flashOrder.Enqueue(row.Id);while(flashOrder.Count>128)flashed.Remove(flashOrder.Dequeue());
                Flash(R23VRExplosives.V(row.Position));
            }
            if(row.Kind=="smoke"&&row.Active&&!row.Ended&&row.EmitAt>=0)
                Smoke(row.Id,R23VRExplosives.V(row.Position),row.Age-row.EmitAt);
            if(row.Kind=="smoke"&&row.Ended)Remove(row.Id,true);
        }
        public void Smoke(string id,Vector3 at,float age)
        {
            if(owner.KindAllowed!=null&&!owner.KindAllowed("smoke"))return;
            Cloud c;
            if(!clouds.TryGetValue(id,out c))
            {
                if(clouds.Count>=64||age>=R25SmokeCloud.Life)return;
                var shader=Resources.Load<Shader>("R25SmokeCoverage");if(shader==null)return;
                c=new Cloud{Id=id,At=at,Node=new GameObject("R25 smoke "+id),Material=new Material(shader)};
                c.Node.layer=2;c.Node.transform.SetParent(transform,false);
                c.Full=Puffs(id,40);c.Reduced=Puffs(id,12);
                c.Filter=c.Node.AddComponent<MeshFilter>();c.Filter.sharedMesh=c.Full;
                var renderer=c.Node.AddComponent<MeshRenderer>();renderer.sharedMaterial=c.Material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                c.Hiss=audio.Voice(c.Node.transform,"Smoke hiss",-18,25,true);
                audio.Play(c.Hiss,"smoke/hiss-loop",age);clouds.Add(id,c);
                if(age<.2f){pop.transform.position=at;audio.Play(pop,"smoke/pop");}
            }
            c.At=at;c.Age=age;c.Received=Time.time;
        }
        // A single combined low-poly mesh, no transparent billboard stacks.
        static Mesh Puffs(string id,int count)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();uint seed=R25SmokeCloud.Seed(id);
            for(int p=0;p<count;p++)
            {
                Float3 offset;float size,alpha;R25SmokeCloud.Puff(seed,p,out offset,out size,out alpha);
                int start=vertices.Count;
                for(int y=0;y<=5;y++)for(int x=0;x<=8;x++)
                {
                    float lat=Mathf.PI*y/5,lon=2*Mathf.PI*x/8;
                    Vector3 vertex=R23VRExplosives.V(offset)+new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon))*size;
                    vertices.Add(Vector3.ClampMagnitude(vertex,1));
                }
                for(int y=0;y<5;y++)for(int x=0;x<8;x++)
                {
                    int a=start+y*9+x,b=a+9;
                    indices.Add(a);indices.Add(a+1);indices.Add(b);indices.Add(a+1);indices.Add(b+1);indices.Add(b);
                }
            }
            var mesh=new Mesh();mesh.vertices=vertices.ToArray();mesh.triangles=indices.ToArray();mesh.RecalculateBounds();return mesh;
        }
        void LateUpdate()
        {
            if(eye==null)return;
            if(!owner.SimulationEnabled){Release();foreach(var c in clouds.Values)c.Node.SetActive(false);return;}
            if(owner.KindAllowed!=null&&!owner.KindAllowed("flash"))blind.Release();
            blind.Step(Time.unscaledDeltaTime);
            if(WorldAudioGain!=null)WorldAudioGain(blind.DuckGain);
            float sfx=SfxGain==null?1:SfxGain();audio.SetVolume(sfx*blind.DuckGain);ring.volume=ringExposure*.3548f*sfx;
            if(!blind.Active&&ring.isPlaying)ring.Stop();
            float inside=0;
            var sorted=new List<Cloud>(clouds.Values);
            sorted.Sort(delegate(Cloud a,Cloud b){return (a.At-eye.position).sqrMagnitude.CompareTo((b.At-eye.position).sqrMagnitude);});
            for(int i=0;i<sorted.Count;i++)
            {
                Cloud c=sorted[i];c.Node.SetActive(true);float age=c.Age+Mathf.Max(0,Time.time-c.Received);
                if(R25SmokeCloud.Expired(age)||owner.KindAllowed!=null&&!owner.KindAllowed("smoke")){Remove(c.Id,true);continue;}
                if(!c.Hiss.isPlaying)audio.Play(c.Hiss,"smoke/hiss-loop",age);
                float r=R25SmokeCloud.Radius(age);
                c.Node.transform.position=R23VRExplosives.V(R25SmokeCloud.Center(F(c.At),r));c.Node.transform.localScale=Vector3.one*r;
                c.Filter.sharedMesh=i<4&&(c.At-eye.position).sqrMagnitude<1600?c.Full:c.Reduced;
                c.Material.SetFloat("_Density",R25SmokeCloud.Density(age));
                inside=Mathf.Max(inside,R25SmokeCloud.ViewAmount(F(eye.position),F(c.At),age));
            }
            smokeAlpha=Mathf.MoveTowards(smokeAlpha,inside,Mathf.Min(.1f,Time.unscaledDeltaTime)/.25f);
            Present();
        }
        void Present()
        {
            if(overlayMaterial==null)return;
            float a=blind.Alpha,s=smokeAlpha;
            Color flash=R25Features.FlashDarkMode?new Color(.025f,.025f,.025f):new Color(.87f,.85f,.81f);
            Color color=Color.Lerp(new Color(.48f,.49f,.5f),flash,a/Mathf.Max(.001f,a+s*(1-a)));
            color.a=a+s*(1-a);
            float center=R25Features.FlashDarkMode?(s*(1-a)+a*.15f)/Mathf.Max(.001f,color.a):1;
            veilColors[4]=new Color(1,1,1,center);overlayMesh.colors=veilColors;overlayMaterial.SetColor("_Color",color);overlayRenderer.enabled=color.a>.001f;
        }
        public void Release(){blind.Reset();smokeAlpha=0;if(WorldAudioGain!=null)WorldAudioGain(1);audio.Stop();Present();}
        void Remove(string id,bool sound)
        {
            Cloud c;if(!clouds.TryGetValue(id,out c))return;
            if(sound){fade.transform.position=c.At;audio.Play(fade,"smoke/fade");}
            if(c.Hiss!=null)audio.ReleaseVoice(c.Hiss);
            Destroy(c.Node);Destroy(c.Full);Destroy(c.Reduced);Destroy(c.Material);clouds.Remove(id);
        }
        public void Clear(){Release();foreach(string id in new List<string>(clouds.Keys))Remove(id,false);flashed.Clear();flashOrder.Clear();}
        void OnDisable(){Clear();}
        void OnDestroy(){Clear();audio.Dispose();if(overlay!=null)Destroy(overlay);if(overlayMesh!=null)Destroy(overlayMesh);if(overlayMaterial!=null)Destroy(overlayMaterial);}
    }
}
#endif
