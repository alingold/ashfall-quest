#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Integration.Playable.Activities;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.ConstructionCandidate;
using UnityEngine;
using UnityEngine.Rendering;

// Staged outside Assets. Future coordinated native visual/candidate fixture only.
public static class R23ConstructionPreviewSmoke
{
    static void Need(bool okay,string message){if(!okay)throw new Exception("Construction preview: "+message);}
    public static IEnumerator Run(Action<R23VRActivities> inspectOrCapture=null)
    {
        float side=CandidateGeometry.Side;var site=new Vector3(side*1000+side*.5f,0,side*.5f);
        var map=new GameObject("Native preview fixture map");var actor=new GameObject("Native preview fixture actor");var owner=new GameObject("Native preview fixture activities");R23VRActivities activities=null;
        try
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(map.transform,false);floor.transform.position=site-Vector3.up*.1f;floor.transform.localScale=new Vector3(24,.2f,24);
            actor.transform.position=site;var body=actor.AddComponent<CharacterController>();body.height=1.7f;body.radius=.24f;body.center=Vector3.up*.85f;
            var head=new GameObject("Actual fixture head").transform;head.SetParent(actor.transform,false);head.localPosition=Vector3.up*1.7088f;
            var left=new GameObject("Actual fixture left").transform;left.SetParent(actor.transform,false);left.localPosition=new Vector3(-.3f,1.2f,.2f);
            var right=new GameObject("Actual fixture right").transform;right.SetParent(actor.transform,false);right.localPosition=new Vector3(.3f,1.2f,.2f);
            right.rotation=Quaternion.Inverse(R23TrackedGripPose.LocalRotation); // accepted corrected bore points +Z
            activities=owner.AddComponent<R23VRActivities>();activities.Initialize(actor.transform,head,left,right,body,map.transform);Physics.SyncTransforms();
            activities.TickConstruction(new InputFrame{Tick=1,Tracked=true},true,true,false,false);
            activities.TickConstruction(new InputFrame{Tick=2,Tracked=true,TriggerRight=true},true,true,false,false);
            Need(activities.PreviewPanelCount>0&&activities.PreviewLightningVisible,"actual accepted plan emits faces and lightning: "+activities.BuildStatus);
            Need(activities.PanelCount==0&&activities.Balance==999,"render preview cannot commit/debit");
            Need(activities.PreviewVisualRoot.GetComponentsInChildren<Collider>(true).Length==0,"no preview collision bodies");
            var renderers=activities.PreviewVisualRoot.GetComponentsInChildren<MeshRenderer>(true);int count=renderers.Length;
            foreach(var view in renderers)
            {
                Need(view.sharedMaterial.shader.isSupported,"native preview shader supported");
                Need(view.sharedMaterial.shader.name=="Ashfall/R23 Source Construction Preview"&&view.shadowCastingMode==ShadowCastingMode.Off,"source unlit/no-shadow material");
                if(view.name.StartsWith("R23 snapped"))Need(view.sharedMaterial.renderQueue==3000&&view.sharedMaterial.GetInt("_SrcBlend")==5&&view.sharedMaterial.GetInt("_DstBlend")==10&&view.sharedMaterial.GetInt("_ZWrite")==0,"explicit alpha/depth state");
            }
            yield return null;
            // Host can frame/capture PreviewVisualRoot here; no capture is claimed by this worker.
            if(inspectOrCapture!=null)inspectOrCapture(activities);
            for(int i=3;i<103;i++)activities.TickConstruction(new InputFrame{Tick=i,Tracked=true,TriggerRight=true},true,true,false,false);
            Need(activities.PreviewVisualRoot.GetComponentsInChildren<MeshRenderer>(true).Length==count&&activities.Balance==999,"held preview resources remain bounded; no charge");
            activities.TickConstruction(new InputFrame{Tick=103,Tracked=true},true,true,false,false);
            Need(activities.PanelCount>0&&activities.Balance==999-activities.PanelCount,"release still commits real CandidateService/charge owner");
            Need(activities.PreviewPanelCount==0&&!activities.PreviewLightningVisible,"release cleans up visuals");
            activities.TickConstruction(new InputFrame{Tick=104,Tracked=false,Paused=true},true,true,false,false);
            Need(activities.PreviewPanelCount==0&&!activities.PreviewLightningVisible,"pause/tracking cleanup");
            Debug.Log("PASS native construction preview: real candidate faces, source shader/depth, ten-ribbon cue, no collider/debit while held, pooled resources, actual release placement and cleanup. Visual stereo/depth capture requires host inspection.");
        }
        finally
        {
            if(activities!=null)activities.ResetRound();map.SetActive(false);actor.SetActive(false);owner.SetActive(false);UnityEngine.Object.Destroy(owner);UnityEngine.Object.Destroy(actor);UnityEngine.Object.Destroy(map);Physics.SyncTransforms();
        }
    }
}
#endif
