#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Integration.Playable.JumpPads;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;

// Future coordinated native fixture. This file is staged outside Assets.
public static class R23HealthStationsSmoke
{
    sealed class Sight:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return true;}}
    static void Require(bool okay,string name){if(!okay)throw new InvalidOperationException("health station: "+name);}
    static Vector3 U(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
    public static IEnumerator Run()
    {
        var host=new GameObject("R23 health station fixture");var map=new GameObject("fixture native map root");var actor=new GameObject("fixture actor");GameObject wall=null;Mesh mesh=null;
        try
        {
            map.transform.position=new Vector3(10000,10000,10000);
            var health=new R23RoundState(new string[0],new Sight());health.Damage(40);
            var stations=host.AddComponent<R23HealthStations>();stations.Initialize("paintball",actor.transform,map.transform,delegate(float gain){return health.ApplyPlayerGain(gain,0);});
            float until=Time.realtimeSinceStartup+30;while(!stations.Ready&&Time.realtimeSinceStartup<until&&stations.Failure==null)yield return null;
            Require(stations.Ready&&stations.StationCount==17,"actual Paintball manifest loaded");
            var row=stations.AuthoredStations[0];Vector3 center=U(row.Center),feet=center+new Vector3(.5f,.1f,0);actor.transform.position=feet;
            var own=actor.AddComponent<BoxCollider>();own.center=Vector3.up*.1f;own.size=Vector3.one*.4f;
            Physics.SyncTransforms();Require(stations.Step(feet,.1f,true)&&health.Health==61,"physical open contact applies real HealthService +1");
            wall=new GameObject("fixture physical wall");wall.transform.position=center+new Vector3(.25f,.21f,0);var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(.08f,.5f,.5f);
            Physics.SyncTransforms();Require(!stations.Step(feet,.1f,true)&&health.Health==61&&stations.LastRate==0,"native wall blocks gain");
            wall.layer=19;Physics.SyncTransforms();Require(stations.Step(feet,.1f,true)&&health.Health==62,"source bullet-only geometry does not block physical mask 1");
            wall.layer=8;Physics.SyncTransforms();Require(!stations.Step(feet,.1f,true)&&health.Health==62,"constructed cover blocks contact");
            box.enabled=false;wall.layer=0;mesh=new Mesh();mesh.vertices=new[]{new Vector3(0,-1,-1),new Vector3(0,1,-1),new Vector3(0,0,1)};mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();wall.AddComponent<MeshCollider>().sharedMesh=mesh;
            Physics.SyncTransforms();Require(!stations.Step(feet,.1f,true)&&health.Health==62,"one-sided native triangle blocks from its back face");
            wall.SetActive(false);Require(!stations.Step(feet,.1f,false)&&health.Health==62&&stations.LastRequestedGain==0,"pause emits no gain");
            Require(!stations.Step(feet+Vector3.right*4,.1f,true)&&health.Health==62,"outside original radius silent");
            Require(stations.SetMap("alpha")&&stations.StationCount==98,"actual ALFA rows selected");
            Require(!stations.SetMap("range")&&!stations.Ready&&stations.StationCount==0,"no invented Range station");
            Debug.Log("PASS R23 native health stations: original rows, real HealthService, physical wall/backface/cover blocking, ballistic exclusion, pause and map reset.");
        }
        finally{if(wall!=null)UnityEngine.Object.Destroy(wall);if(mesh!=null)UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(map);UnityEngine.Object.Destroy(actor);}
    }
}
#endif
