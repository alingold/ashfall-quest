#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        R26RemoteShotPresentation remoteShots;public int RemoteShotsPresented{get{return remoteShots==null?0:remoteShots.Volleys;}}
        public int RemoteSoundsPresented{get{return remoteShots==null?0:remoteShots.Sounds;}}
        Ashfall.Modules.HealthStates.LifeState RemotePresentationLife(string actor)
        {
            R24LanHealthState health;if(peer.Lobby.TryReadHealth(actor,out health))return health.Life;string life;
            if(peer.Lobby.TryReadSurvivalLife(actor,out life))return life=="downed"?Ashfall.Modules.HealthStates.LifeState.Downed:life=="eliminated"?Ashfall.Modules.HealthStates.LifeState.Dead:life=="spectator"?Ashfall.Modules.HealthStates.LifeState.Spectator:Ashfall.Modules.HealthStates.LifeState.Alive;
            return Ashfall.Modules.HealthStates.LifeState.Alive;
        }
        public event System.Action<R26RemoteVolley> RemoteGunshot;
        Ashfall.Integration.Playable.Input.R23TeamMark sentMark;
        void SyncRemoteShots()
        {
            var mark=root.TeamMarks==null?null:root.TeamMarks.Current;
            if(mark!=sentMark){sentMark=mark;if(mark!=null&&root.MatchActive)peer.Lobby.PublishTeamPing(V(mark.Position));}
            R26RemoteVolley shot;int budget=32;while(budget-->0&&peer.Lobby.TryDequeueRemoteVolley(out shot)){
                R24LanRemoteAvatar avatar;if(root.MatchActive&&RemoteGunshot!=null)RemoteGunshot(shot);
                if(!root.Ready||PresenceScene=="menu")continue;remoteAvatars.TryGetValue(shot.Actor,out avatar);
                if(remoteShots==null){remoteShots=new GameObject("Pooled remote shot feedback").AddComponent<R26RemoteShotPresentation>();remoteShots.transform.SetParent(transform,false);remoteShots.Initialize(root.Gameplay);}
                remoteShots.Present(avatar,shot,peer.Lobby.EstimatedHostTime-shot.Time);
            }
            R26BulletContact c;while(peer.Lobby.TryDequeueBulletContact(out c)){
                if(!root.Ready||PresenceScene=="menu")continue;
                if(c.Pellet==255){root.Gameplay.StopFlight(c);continue;}
                Collider surface=root.RangeLane==null?null:root.RangeLane.ContactCollider(c.RangeTarget);
                if(surface==null&&c.MapSurface){RaycastHit hit;Vector3 normal=BulletPoint(c.Normal);if(Physics.Raycast(BulletPoint(c.Point)+normal*.02f,-normal,out hit,.04f,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))surface=hit.collider;}
                bool own=c.Actor==peer.Lobby.OwnActor;root.Gameplay.AcceptBulletContact(c,surface,own);
                if(c.MapSurface&&c.RangeTarget==0&&surface!=null&&root.Map!=null)root.Map.TryDamageCollider(surface,c.Damage);
                if(own&&c.RangeTarget!=0&&root.RangeLane!=null)root.RangeLane.ConfirmRangeDamage(surface,c.Damage);
            }
            SyncAwpAudio();
        }
    }
    // Global per-session limits, irrespective of actor count. No allocation, raycast,
    // lights, particles or material clones per shot after the streak pool warms up.
    [DefaultExecutionOrder(1000)]
    public sealed class R26RemoteShotPresentation:MonoBehaviour
    {
        public const int SoundCapacity=R24LanLobby.Capacity*2,TracerCapacity=R24LanLobby.Capacity*8,FlashCapacity=R24LanLobby.Capacity;
        readonly AudioSource[] voices=new AudioSource[SoundCapacity];readonly float[] flashUntil=new float[FlashCapacity];
        readonly Transform[] flashes=new Transform[FlashCapacity];readonly R24LanRemoteAvatar[] flashActors=new R24LanRemoteAvatar[FlashCapacity];readonly int[] flashHands=new int[FlashCapacity];
        int voiceCursor,flashCursor;R23HandsWeapons source;Mesh flashMesh;Material flashMaterial;public int Volleys{get;private set;}public int Sounds{get;private set;}
        public void Initialize(R23HandsWeapons gameplay)
        {
            source=gameplay;flashMesh=FlashMesh();flashMaterial=new Material(Resources.Load<Shader>("R23GunFeedback"));flashMaterial.SetColor("_Color",new Color(3,1.95f,.48f));flashMaterial.SetFloat("_Depth",4);flashMaterial.SetFloat("_Cull",0);flashMaterial.renderQueue=3020;
            for(int i=0;i<SoundCapacity;i++){var node=new GameObject("Remote shot voice "+i);node.transform.SetParent(transform,false);var voice=node.AddComponent<AudioSource>();voices[i]=voice;voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.minDistance=2;voice.maxDistance=85;voice.dopplerLevel=0;}
            for(int i=0;i<FlashCapacity;i++){var node=new GameObject("Remote muzzle flash "+i);node.transform.SetParent(transform,false);flashes[i]=node.transform;node.AddComponent<MeshFilter>().sharedMesh=flashMesh;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=flashMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;node.SetActive(false);}
        }
        public void Present(R24LanRemoteAvatar avatar,R26RemoteVolley shot,double age)
        {
            Vector3 origin=Vector3.zero;Quaternion rotation=Quaternion.identity;bool anchored=avatar!=null&&avatar.Visible&&avatar.HeldProfile==shot.Profile&&avatar.TryRemoteMuzzle(shot.Hand,out origin,out rotation);if(!anchored){origin=V(shot.Origin);Vector3 d=shot.Directions.Length==0?Vector3.forward:V(shot.Directions[0]);rotation=d.sqrMagnitude>.000001f?Quaternion.LookRotation(d):Quaternion.identity;}
            var voice=voices[voiceCursor++%SoundCapacity];voice.transform.position=origin;if(source.PlayRemoteShot(shot.Profile,voice,shot.Actor))Sounds++;
            float now=Time.unscaledTime;R24LanBallisticProfile gun;if(R24LanBallisticProfile.TryRead(shot.Profile,out gun))for(int i=0;i<shot.Directions.Length;i++)source.ShowFlight(V(shot.Origin),V(shot.Directions[i]),gun,age,shot.Actor,shot.Id,i);
            int f=flashCursor++%FlashCapacity;flashes[f].SetPositionAndRotation(origin,rotation);flashes[f].gameObject.SetActive(true);flashUntil[f]=now+R23WeaponFeedbackState.FlashSeconds;flashActors[f]=anchored?avatar:null;flashHands[f]=shot.Hand;Volleys++;
        }
        void LateUpdate()
        {
            float now=Time.unscaledTime;Vector3 point;Quaternion rotation;
            for(int i=0;i<FlashCapacity;i++){if(!flashes[i].gameObject.activeSelf)continue;if(now>=flashUntil[i]){flashes[i].gameObject.SetActive(false);flashActors[i]=null;}else if(flashActors[i]!=null&&flashActors[i].TryRemoteMuzzle(flashHands[i],out point,out rotation))flashes[i].SetPositionAndRotation(point,rotation);}
        }
        static Vector3 V(Ashfall.Core.Float3 point){return new Vector3(point.X,point.Y,point.Z);}
        static Mesh FlashMesh(){var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;int start=v.Count;v.Add(new Vector3(Mathf.Cos(a)*.018f,Mathf.Sin(a)*.018f,0));v.Add(new Vector3(Mathf.Cos(b)*.018f,Mathf.Sin(b)*.018f,0));v.Add(new Vector3(Mathf.Cos(b)*.018f,Mathf.Sin(b)*.018f,.07f));v.Add(new Vector3(Mathf.Cos(a)*.018f,Mathf.Sin(a)*.018f,.07f));foreach(int n in new[]{0,2,1,0,3,2})t.Add(start+n);}var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateBounds();return mesh;}
        void OnDestroy(){foreach(var voice in voices)if(voice!=null)voice.Stop();if(flashMesh!=null)Destroy(flashMesh);if(flashMaterial!=null)Destroy(flashMaterial);}
    }
}
#endif
