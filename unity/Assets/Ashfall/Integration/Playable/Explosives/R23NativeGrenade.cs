#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using UnityEngine;
namespace Ashfall.Integration.Playable.Explosives
{
    internal sealed class R23NativeGrenade:MonoBehaviour
    {
        R23VRExplosives owner;R23GrenadeLaunch launch;R23GrenadeClock clock;Rigidbody body;AudioSource warning;GameObject zone;bool paused,groundContact,wasGrounded;Vector3 normal=Vector3.up,lastRoll,savedVelocity,savedSpin;float roll,sfx=1;
        public SphereCollider Shape {get;private set;}
        public void Initialize(R23VRExplosives service,Transform actor,R23GrenadeLaunch spec,AudioClip warningClip)
        {
            owner=service;launch=spec;clock=new R23GrenadeClock(spec.ItemId,spec.Kind);Shape=gameObject.AddComponent<SphereCollider>();Shape.radius=.035f;
            body=gameObject.AddComponent<Rigidbody>();body.mass=.35f;body.useGravity=false;body.linearDamping=0;body.angularDamping=0;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearVelocity=spec.Velocity;
            foreach(var collider in actor.GetComponentsInChildren<Collider>(true))Physics.IgnoreCollision(Shape,collider);
            warning=gameObject.AddComponent<AudioSource>();warning.playOnAwake=false;warning.clip=warningClip;warning.spatialBlend=1;warning.maxDistance=60;warning.minDistance=1;warning.volume=.4466836f;warning.dopplerLevel=0;
            if(spec.Kind=="zone"){zone=new GameObject("Source shrinking zone mist");zone.layer=2;zone.transform.SetParent(transform,false);zone.AddComponent<MeshFilter>().sharedMesh=owner.ZoneMesh;zone.AddComponent<MeshRenderer>().sharedMaterial=owner.ZoneMaterial;zone.SetActive(false);}
            SetSfx(1);
        }
        void FixedUpdate()
        {
            if(owner==null){Destroy(gameObject);return;}SetPaused(!owner.SimulationEnabled);if(paused)return;float dt=Time.fixedDeltaTime;
            if(!clock.Stuck)
            {
                body.linearVelocity+=Vector3.down*(R23GrenadeMath.Gravity*dt);
                if((launch.Kind=="frag"||launch.Kind=="flash")&&groundContact)
                {
                    if(wasGrounded)roll+=Vector3.Distance(body.position,lastRoll);lastRoll=body.position;wasGrounded=true;
                    body.linearVelocity=R23VRExplosives.V(R23GrenadeMath.GroundRollVelocity(R23VRExplosives.F(body.linearVelocity),R23VRExplosives.F(normal),dt,roll));body.angularVelocity=Vector3.MoveTowards(body.angularVelocity,Vector3.zero,8*dt);
                }
                else wasGrounded=false;groundContact=false;
            }
            var frame=clock.Advance(dt,true,R23VRExplosives.F(body.position),owner.Deliver);
            if(launch.Kind=="flash"&&frame.Detonated)owner.FlashSmoke.Flash(body.position);
            if(launch.Kind=="smoke"&&clock.Active){if(!clock.Stuck){clock.Contact();body.linearVelocity=Vector3.zero;body.isKinematic=true;Shape.enabled=false;}owner.FlashSmoke.Smoke(launch.ItemId,body.position,clock.CloudAge);}
            if(frame.Warning)warning.Play();if(frame.Activated&&launch.Kind=="zone"){warning.Stop();zone.SetActive(true);owner.Play("zone_bloom",body.position,.6309573f,100,1,true);}
            if(zone!=null&&clock.Active)zone.transform.localScale=Vector3.one*frame.Radius;
            if(frame.Detonated&&launch.Kind=="frag"){warning.Stop();owner.Play("frag_blast",body.position,.6309573f,100,1,true);}
            if(frame.Ended){owner.Finished(this);Destroy(gameObject);}
        }
        void OnCollisionEnter(Collision collision){Contact(collision);}void OnCollisionStay(Collision collision){Contact(collision);}
        void Contact(Collision collision)
        {
            if(paused||clock==null||clock.Ended)return;
            foreach(var c in collision.contacts)if(c.normal.y>.45f){groundContact=true;normal=c.normal.normalized;break;}
            if(!clock.Contact())return;var contact=collision.GetContact(0);body.linearVelocity=body.angularVelocity=Vector3.zero;body.isKinematic=true;Shape.enabled=false;
            Vector3 point=contact.point,n=contact.normal.normalized;RaycastHit hit;if(owner.Ray(body.position+Vector3.up*.12f,body.position-Vector3.up*.22f,out hit)){point=hit.point;n=hit.normal;}
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward,n).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.ProjectOnPlane(Vector3.forward,n).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.ProjectOnPlane(Vector3.right,n).normalized;transform.rotation=Quaternion.LookRotation(forward,n);
            float minimum=float.PositiveInfinity;foreach(var filter in GetComponentsInChildren<MeshFilter>())if(zone==null||filter.gameObject!=zone){var b=filter.sharedMesh.bounds;for(int k=0;k<8;k++){Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));minimum=Mathf.Min(minimum,Vector3.Dot(filter.transform.TransformPoint(p)-transform.position,n));}}
            if(!float.IsInfinity(minimum))body.position=point+n*(.004f-minimum);if(launch.Kind=="zone")owner.Play("zone_arm",body.position,1,35);
        }
        public void SetPaused(bool value)
        {
            if(body==null||paused==value)return;paused=value;
            if(value){savedVelocity=body.linearVelocity;savedSpin=body.angularVelocity;body.isKinematic=true;warning.Pause();}
            else{body.isKinematic=clock.Stuck;if(!clock.Stuck){body.linearVelocity=savedVelocity;body.angularVelocity=savedSpin;}warning.UnPause();}
        }
        public void SetSfx(float value){sfx=value;if(warning!=null)warning.volume=.4466836f*sfx;}
        public void Cancel(){SetPaused(true);if(warning!=null)warning.Stop();if(Shape!=null)Shape.enabled=false;owner=null;enabled=false;gameObject.SetActive(false);}
        public R23ProjectileReadout Read(){return new R23ProjectileReadout{ItemId=launch.ItemId,Kind=launch.Kind,Position=body.position,Age=clock.Age,ZoneAge=clock.ZoneAge,Radius=clock.Radius,ZoneTicks=clock.ZoneTicks,Stuck=clock.Stuck,Active=clock.Active,GroundRoll=roll};}
        void OnDestroy(){if(warning!=null)warning.Stop();if(owner!=null)owner.Finished(this);}
    }
}
#endif
