#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Vehicles;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Explosives;
using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    // R5 (playtest 4): right-hand aiming for the tank (with a landing-point
    // reticle) and mounted machine guns on the helicopter and bike
    // (aim dot, tracer, impact spark, m60 shot sound). The host owns damage.
    public sealed partial class R24LanSession
    {
        const int AimMask=(1<<0)|(1<<8);
        GameObject reticle;Material reticleMaterial,tracerMaterial;
        readonly List<GameObject> gunEffects=new List<GameObject>();readonly List<float> gunEffectEnds=new List<float>();
        R23Audio vehicleGunAudio;Transform vehicleGunMuzzle;bool vehicleGunAudioLoading;
        float nextTankAimSend;float sentAimYaw=999,sentAimPitch=999,sentHull=999;
        void BindVehicleAim()
        {
            root.Rig.FireTank=delegate(string id){return peer!=null?peer.Lobby.RequestTankFireAimed(id,root.Rig.TankAimYaw,root.Rig.TankAimPitch,root.Rig.VehicleHullYaw):FireLocalTank(id);};
            root.Rig.FireVehicleGun=FireVehicleGunLocal;
        }
        bool FireVehicleGunLocal(string id,Vector3 origin,Vector3 direction)
        {
            if(peer!=null&&!peer.Lobby.RequestVehicleGun(id,V(origin),V(direction)))return false;
            RaycastHit hit;Vector3 end=origin+direction*R24LanLobby.VehicleGunRange;bool struck=Physics.Raycast(origin,direction,out hit,R24LanLobby.VehicleGunRange,AimMask,QueryTriggerInteraction.Ignore);if(struck)end=hit.point;
            GunEffect(Line(origin,end,.025f),.06f);if(struck)GunEffect(Spark(hit.point),.09f);
            PlayVehicleGun(origin);return true;
        }
        void PlayVehicleGun(Vector3 at)
        {
            if(vehicleGunAudio==null){vehicleGunMuzzle=new GameObject("R5 vehicle gun muzzle").transform;vehicleGunMuzzle.SetParent(transform,false);vehicleGunAudio=new R23Audio(vehicleGunMuzzle,vehicleGunMuzzle);}
            if(!vehicleGunAudioLoading){vehicleGunAudioLoading=true;StartCoroutine(vehicleGunAudio.Load("m60"));}
            vehicleGunMuzzle.position=at;vehicleGunAudio.Play("m60","shot",true);
        }
        Material Unlit(Color color){return R25ItemArtGeometry.Surface(color,0,.2f,true);}
        GameObject Line(Vector3 a,Vector3 b,float width)
        {
            if(tracerMaterial==null)tracerMaterial=Unlit(new Color(1,.85f,.35f));
            var go=new GameObject("R5 vehicle gun tracer");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=tracerMaterial;line.positionCount=2;line.SetPosition(0,a);line.SetPosition(1,b);line.startWidth=width;line.endWidth=width*.5f;line.useWorldSpace=true;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return go;
        }
        GameObject Spark(Vector3 at)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="R5 vehicle gun impact";DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(transform,false);go.transform.position=at;go.transform.localScale=Vector3.one*.18f;
            if(tracerMaterial==null)tracerMaterial=Unlit(new Color(1,.85f,.35f));go.GetComponent<MeshRenderer>().sharedMaterial=tracerMaterial;return go;
        }
        void GunEffect(GameObject go,float seconds){gunEffects.Add(go);gunEffectEnds.Add(Time.unscaledTime+seconds);}
        void TickVehicleAim()
        {
            for(int i=gunEffects.Count-1;i>=0;i--)if(Time.unscaledTime>=gunEffectEnds[i]){if(gunEffects[i]!=null)Destroy(gunEffects[i]);gunEffects.RemoveAt(i);gunEffectEnds.RemoveAt(i);}
            string kind=root!=null&&root.Rig!=null?root.Rig.RidingKind:null;
            if(kind==null){ShowReticle(false,Vector3.zero);return;}
            if(kind=="tank")
            {
                float yaw=root.Rig.TankAimYaw,pitch=root.Rig.TankAimPitch,hull=root.Rig.VehicleHullYaw;
                if(peer!=null&&Time.unscaledTime>=nextTankAimSend&&(Mathf.Abs(Mathf.DeltaAngle(yaw,sentAimYaw))>.5f||Mathf.Abs(pitch-sentAimPitch)>.5f||Mathf.Abs(Mathf.DeltaAngle(hull,sentHull))>.5f))
                {
                    string id=root.Rig.RidingVehicleId;
                    if(id!=null&&peer.Lobby.RequestTankAim(id,yaw,pitch,hull)){sentAimYaw=yaw;sentAimPitch=pitch;sentHull=hull;nextTankAimSend=Time.unscaledTime+.1f;}
                }
                // The shell leaves along the turret (which slews toward the hand) at the hand's pitch.
                float turret=yaw;foreach(var row in bikeRows)if(row.Id==root.Rig.RidingVehicleId)turret=row.TurretYaw;
                var direction=R24LanLobby.TankShellDirection(turret,pitch);Vector3 d=new Vector3(direction.X,direction.Y,direction.Z);
                Vector3 at=root.Rig.transform.position+Vector3.up*.65f+Quaternion.Euler(0,turret,0)*Vector3.forward*3.6f+Vector3.up*d.y*3.6f,velocity=d*45;
                Vector3 landing=at;bool landed=false;
                for(int i=0;i<160&&!landed;i++)
                {
                    Vector3 next=at+velocity*.05f;velocity+=Vector3.down*Ashfall.Integration.Playable.Explosives.R23GrenadeMath.Gravity*.05f;RaycastHit hit;
                    if(Physics.Linecast(at,next,out hit,AimMask,QueryTriggerInteraction.Ignore)){landing=hit.point;landed=true;}else{at=next;landing=next;}
                }
                ShowReticle(true,landing);
                return;
            }
            var ray=root.Rig.VehicleGunRay;RaycastHit aim;Vector3 dot=ray.origin+ray.direction*40;
            if(Physics.Raycast(ray.origin,ray.direction,out aim,R24LanLobby.VehicleGunRange,AimMask,QueryTriggerInteraction.Ignore))dot=aim.point;
            ShowReticle(true,dot);
        }
        void ShowReticle(bool show,Vector3 at)
        {
            if(!show){if(reticle!=null)reticle.SetActive(false);return;}
            if(reticleMaterial==null)reticleMaterial=Unlit(new Color(1,.25f,.15f));
            if(reticle==null)
            {
                reticle=new GameObject("R5 vehicle aim reticle");reticle.transform.SetParent(transform,false);
                var core=GameObject.CreatePrimitive(PrimitiveType.Sphere);core.name="R5 reticle dot";DestroyImmediate(core.GetComponent<Collider>());core.transform.SetParent(reticle.transform,false);core.transform.localScale=Vector3.one*.12f;var dot=core.GetComponent<MeshRenderer>();dot.sharedMaterial=reticleMaterial;dot.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            reticle.SetActive(true);reticle.transform.position=at;
            float distance=root!=null&&root.Rig!=null&&root.Rig.Head!=null?Vector3.Distance(root.Rig.Head.transform.position,at):10;
            reticle.transform.localScale=Vector3.one*Mathf.Clamp(distance/12f,.35f,6f);
        }
        void ClearVehicleAim()
        {
            foreach(var go in gunEffects)if(go!=null)Destroy(go);gunEffects.Clear();gunEffectEnds.Clear();
            if(reticle!=null)Destroy(reticle);reticle=null;
            sentAimYaw=sentAimPitch=sentHull=999;
        }
    }
}
#endif
