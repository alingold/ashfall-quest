#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Vehicles;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Explosives;
using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        const int AimMask=(1<<0)|(1<<8);
        GameObject reticle;MeshRenderer reticleRenderer;Material reticleMaterial,limitedReticleMaterial;
        R23Audio vehicleGunAudio;Transform vehicleGunMuzzle;bool vehicleGunAudioLoading;
        string sentAimVehicle;float nextVehicleAimSend;float sentAimYaw=999,sentAimPitch=999,sentHull=999;
        void BindVehicleAim()
        {
            root.Rig.FireTank=delegate(string id){
                if(peer==null)return FireLocalTank(id);
                PublishRawTracking();
                return peer.Lobby.RequestTankFireAimed(id,root.Rig.TankAimYaw,root.Rig.TankAimPitch,root.Rig.VehicleHullYaw);
            };
            root.Rig.FireVehicleGun=FireVehicleGunLocal;
        }
        bool FireVehicleGunLocal(string id,Vector3 origin,Vector3 direction)
        {
            if(peer!=null){
                PublishRawTracking();
                if(!peer.Lobby.RequestVehicleGun(id,V(origin),V(direction)))return false;
            }
            PresentMountedBullet(id,origin,direction);
            PlayVehicleGun(origin);return true;
        }
        void PlayVehicleGun(Vector3 at)
        {
            if(vehicleGunAudio==null){vehicleGunMuzzle=new GameObject("R5 vehicle gun muzzle").transform;vehicleGunMuzzle.SetParent(transform,false);vehicleGunAudio=new R23Audio(vehicleGunMuzzle,vehicleGunMuzzle);}
            if(!vehicleGunAudioLoading){vehicleGunAudioLoading=true;StartCoroutine(vehicleGunAudio.Load("m60"));}
            vehicleGunMuzzle.position=at;vehicleGunAudio.Play("m60","shot",true);
        }
        void TickVehicleAim()
        {
            var rig=root==null?null:root.Rig;
            string id=rig==null?null:rig.RidingVehicleId;
            if(id==null||rig.VehicleBoarding||!rig.VehicleAimReady){sentAimVehicle=null;ShowReticle(false,Vector3.zero);return;}
            bool tank=rig.RidingKind=="tank";
            if(peer!=null&&Time.unscaledTime>=nextVehicleAimSend){
                float yaw=tank?rig.TankAimYaw:rig.VehicleGunYaw,pitch=tank?rig.TankAimPitch:rig.VehicleGunPitch,hull=tank?rig.VehicleHullYaw:0;
                if(id!=sentAimVehicle||Mathf.Abs(Mathf.DeltaAngle(yaw,sentAimYaw))>.5f||Mathf.Abs(pitch-sentAimPitch)>.5f||Mathf.Abs(Mathf.DeltaAngle(hull,sentHull))>.5f){
                    if(peer.Lobby.RequestVehicleAim(id,yaw,pitch,hull)){sentAimVehicle=id;sentAimYaw=yaw;sentAimPitch=pitch;sentHull=hull;nextVehicleAimSend=Time.unscaledTime+.1f;}
                }
            }
            Ray ray=rig.VehicleGunRay;
            if(tank){
                // Linecast successive segments from the muzzle to mark the shell's landing point.
                Vector3 at=ray.origin,velocity=ray.direction*45;
                for(int i=0;i<160;i++){
                    const float dt=.05f;
                    Vector3 gravity=Vector3.down*R23GrenadeMath.Gravity;
                    Vector3 next=at+(velocity+gravity*(dt*.5f))*dt;velocity+=gravity*dt;
                    RaycastHit hit;if(Physics.Linecast(at,next,out hit,AimMask,QueryTriggerInteraction.Ignore)){at=hit.point;break;}
                    at=next;
                }
                ShowReticle(true,at,rig.VehicleAimLimited);return;
            }
            RaycastHit aim;
            Vector3 dot=Physics.Raycast(ray,out aim,R24LanLobby.VehicleGunRange,AimMask,QueryTriggerInteraction.Ignore)
                ?aim.point:ray.GetPoint(R24LanLobby.VehicleGunRange);
            ShowReticle(true,dot,rig.VehicleAimLimited);
        }
        static Material ReticleSurface(Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",color);return material;
        }
        void ShowReticle(bool show,Vector3 at,bool limited=false)
        {
            if(!show){if(reticle!=null)reticle.SetActive(false);return;}
            if(reticleMaterial==null)reticleMaterial=ReticleSurface(new Color(1,.19f,.12f));
            if(limited&&limitedReticleMaterial==null)limitedReticleMaterial=ReticleSurface(new Color(1,.73f,.12f));
            if(reticle==null)
            {
                reticle=new GameObject("R5 vehicle aim reticle");reticle.transform.SetParent(transform,false);
                var core=GameObject.CreatePrimitive(PrimitiveType.Sphere);core.name="R5 reticle dot";DestroyImmediate(core.GetComponent<Collider>());core.transform.SetParent(reticle.transform,false);core.transform.localScale=Vector3.one*.12f;reticleRenderer=core.GetComponent<MeshRenderer>();reticleRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            reticle.SetActive(true);reticle.transform.position=at;reticleRenderer.sharedMaterial=limited?limitedReticleMaterial:reticleMaterial;
            float distance=root!=null&&root.Rig!=null&&root.Rig.Head!=null?Vector3.Distance(root.Rig.Head.transform.position,at):10;
            reticle.transform.localScale=Vector3.one*Mathf.Clamp(distance/12f,.35f,6f);
        }
        void ClearVehicleAim()
        {
            if(reticle!=null)Destroy(reticle);reticle=null;reticleRenderer=null;
            sentAimVehicle=null;nextVehicleAimSend=0;
        }
    }
}
#endif
