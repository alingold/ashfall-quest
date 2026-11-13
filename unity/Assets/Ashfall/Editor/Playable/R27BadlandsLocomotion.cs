#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Calibration;
using UnityEngine;

namespace Ashfall.Editor.Playable
{
    public static class R27BadlandsLocomotion
    {
        [Serializable] sealed class Sites { public List<Vector3> sites = new List<Vector3>(); public int faces, samples; }
        [Serializable] sealed class Result
        {
            public bool nativeExecuted = true, acceptancePassed;
            public string map, manifest, source;
            public int scenarios, frames, belowSupport, groundViews, visibleViolations, stuck, failedStand, fixtureInvalid, skippedPlacements, shallowCross, deepCross; public float maximumCross; public List<string> deepCrossScenarios = new List<string>();
            public float minimumFloorGap = 999, minimumEyeHeight = 999;
            public List<string> failures = new List<string>();
        }
        [Serializable] sealed class Frame
        {
            public string scenario, phase, corrections, advance, verified;
            public float moveY; public bool inputReady, gameplayBlocked, lastTracked, lastPaused, rightReady, menuLocomotion;
            public int frame;
            public Vector3 root, eye, origin, rawHead;
            public float crouch, standing, calibratedEye, stickX, stickY, supportY, gap, clearance, alpha, cross;
            public bool supported, focused, paused, below, visibleViolation;
        }
        static HandSample Hand(float x) { return new HandSample { Tracked = true, Position = new Float3(x, .8f, .3f) }; }

        public static IEnumerator Run(R23PlayableLoop loop, string output)
        {
            var rig = loop.Rig;
            var geometry = new R25HeadclipGeometry();
            foreach (var c in loop.Map.GetComponentsInChildren<MeshCollider>(false))
                if (c.gameObject.layer != 19 && c.gameObject.layer != 22) geometry.Add(c);
            geometry.Build();
            var discovered = new Sites();
            discovered.sites = geometry.OverhangSites(1f, out discovered.faces, out discovered.samples, true);
            File.WriteAllText(Path.Combine(output, "badlands-scan.json"), JsonUtility.ToJson(discovered, true));
            var sites = new List<Vector3> {
                new Vector3(73.41f,42.7788f,66), new Vector3(86.32f,42.7788f,36.32f),
                new Vector3(85.95f,42.7788f,53.95f), new Vector3(90.38f,42.7788f,37.78f),
                new Vector3(85.793f,42.359f,53.476f), new Vector3(64,42.7788f,63)
            };
            bool pilot = R23PlayableBuild.Argument("-ashfallLocoPilot") == "true";
            bool one = R23PlayableBuild.Argument("-ashfallLocoOne") == "true";
            if (one) sites.RemoveRange(1, sites.Count - 1);
            // The scan finds ~270k overhang samples; test an even spread of N of them.
            int scan; if (!int.TryParse(R23PlayableBuild.Argument("-ashfallLocoScan"), out scan)) scan = 0;
            for (int k = 0; k < scan && discovered.sites.Count > 0; k++)
                sites.Add(discovered.sites[(int)((long)k * discovered.sites.Count / scan)]);
            R23VRRig.FeetFloorRule = R23PlayableBuild.Argument("-ashfallLocoNoFeetRule") != "true";
            rig.CaptureAdvanceTrace = true;
            var result = new Result { map = loop.Map.MapId, manifest = loop.Map.ManifestSha256, source = loop.Map.SourceSha256 };
            var left = Hand(-.2f); var right = Hand(.2f); const float dt = 1f / 72;
            var writes = new StringBuilder();
            rig.LocomotionWrite = delegate(string owner, Vector3 root, Vector3 origin) {
                writes.Append(owner).Append(':').Append(root.ToString("F6")).Append('/').Append(origin.ToString("F6")).Append(';');
            };
            var calibration = rig.gameObject.AddComponent<R23VerticalCalibration>(); calibration.SuppressPersistence = true;
            calibration.Initialize(rig.TrackingOrigin, rig.Head, rig.Body); rig.Calibration = calibration;
            // The owner's 1.18 m physical eye is calibrated to the 1.7088 m game eye.
            rig.SetSpawn(new Vector3(23.09f,75.66f,18.36f), new Vector3(23.09f,75.66f,19.36f));
            for (int i = 0; i < 36; i++) { rig.FixtureTick(dt); rig.LocomotionProbeSample(left,right,dt,new Vector3(0,1.18f,0)); }
            if (!calibration.Calibrated || Mathf.Abs(calibration.Receipt.TrackedStandingEye - 1.18f) > .001f)
                throw new InvalidOperationException("Owner calibration receipt missing");
            using (var frames = new StreamWriter(Path.Combine(output,"frames.jsonl")))
            for (int s = 0; s < sites.Count; s++)
            foreach (float speed in pilot ? new[] { 11f } : new[] { 3f, 11f })
            foreach (float yaw in one ? new[] { 0f } : pilot ? new[] { 0f, 90f, 180f, 270f } : new[] { 0f, 30f, 60f, 90f, 180f, 225f, 270f })
            {
                Vector3 heading = Quaternion.Euler(0,yaw,0) * Vector3.forward;
                Vector3 start = sites[s] - heading * 2.5f; R25HeadclipGeometry.Triangle face; Vector3 floor;
                if (!geometry.Floor(start,5,out face,out floor)) { result.failures.Add("unsupported placement " + s + "/" + yaw); continue; }
                start.y = floor.y;
                // A start whose standing body already crosses a surface (head through a
                // ramp above the floor) is not a reachable player pose: skip it.
                R25HeadclipGeometry.Triangle over; Vector3 overFloor;
                if (geometry.Floor(start + Vector3.up * 1.5f, 2, out over, out overFloor) && overFloor.y > start.y + .05f)
                { result.failures.Add("blocked placement " + s + "/" + yaw); result.skippedPlacements++; continue; }
                rig.SetSpawn(start,start+heading); rig.Head.transform.localRotation=Quaternion.identity;
                rig.HeadclipProbeOwnsTracking = true; rig.GameplayBlocked=false; rig.FixtureRunsRecovery=true;
                rig.ApplySettings(loop.UI.Settings.With(walk:speed/PlayerTuning.WalkSpeed));
                string id = (s<6?"logged-":"scan-")+s+"-"+speed+"-"+yaw;
                bool failed = false; Vector3 retreatStart=Vector3.zero, priorEye=Vector3.zero, approachStart=Vector3.zero; float approached=0;
                // Every scenario includes focus loss under contact, a physical
                // duck/stand and walking out, then focus/pause in the open.
                for (int i=0;i<540;i++)
                {
                    string phase=i<36?"settle":i<180?"approach":i<216?"menu-contact":i<252?"duck-stand":i<396?"retreat":i<432?"menu-open":i<468?"pause":"neutral";
                    rig.FixtureUnfocused=i>=180&&i<216||i>=396&&i<432;
                    rig.LocomotionProbePaused=i>=432&&i<468;
                    var stick=left; stick.StickY=i>=36&&i<180?1:i>=252&&i<396?-1:0;
                    Vector3 raw=new Vector3(0,1.18f,0);
                    if(i>=216&&i<252)raw.y-=.2f*Mathf.Sin(Mathf.PI*(i-216)/35);
                    // The real loop rewrites GameplayBlocked every Update (round/menu state);
                    // the probe owns motion here, so unblock it before every sample.
                    rig.GameplayBlocked=false;
                    // Do NOT re-enable the rig between samples: LocomotionProbeSample disables it,
                    // and every enable/disable cycle runs OnDisable -> input.Disarm(), which kept
                    // the thumbstick from ever arming (the first fixture never walked).
                    writes.Length=0;rig.FixtureTick(dt);rig.LocomotionProbeSample(stick,right,dt,raw);
                    if(i==36)approachStart=rig.transform.position;
                    if(i==179)approached=Vector3.Distance(approachStart,rig.transform.position);
                    if(i==251)retreatStart=rig.transform.position;
                    int index=i;Vector3 sampledRaw=raw;string sampledPhase=phase;
                    yield return new R25HeadclipNative.Audit { Run=delegate {
                        Vector3 root=rig.transform.position,eye=rig.Head.transform.position;
                        R25HeadclipGeometry.Triangle under;Vector3 support;
                        bool floorFound=geometry.Floor(eye-Vector3.up*.2f,4,out under,out support);
                        float gap=floorFound?root.y-support.y:999;
                        bool below=floorFound&&gap<-.015f;
                        // Body crossing in the FEET column: a surface between the feet and the chin.
                        R25HeadclipGeometry.Triangle feetFace;Vector3 feetFloor;float cross=0;
                        if(geometry.Floor(new Vector3(root.x,Mathf.Max(root.y+.05f,eye.y-.25f),root.z),Mathf.Max(.1f,eye.y-.25f-root.y),out feetFace,out feetFloor)&&feetFloor.y>root.y+.02f)cross=feetFloor.y-root.y;
                        if(cross>=.45f){result.deepCross++;if(!result.deepCrossScenarios.Contains(id))result.deepCrossScenarios.Add(id);}else if(cross>0)result.shallowCross++;result.maximumCross=Mathf.Max(result.maximumCross,cross);
                        var nearest=geometry.Eye(eye);
                        bool violation=rig.ViewFade.Alpha<.999f&&(nearest.distance<.07f||index>0&&geometry.Crosses(priorEye,eye));
                        // The owner symptom: standing (real head up) yet the view sits near the floor under it.
                        bool ground=floorFound&&eye.y-support.y<.9f&&sampledRaw.y>=1.17f&&sampledPhase!="duck-stand";
                        if(below)result.belowSupport++;if(ground)result.groundViews++;if(violation)result.visibleViolations++;
                        result.minimumFloorGap=Mathf.Min(result.minimumFloorGap,gap);result.minimumEyeHeight=Mathf.Min(result.minimumEyeHeight,eye.y-root.y);
                        failed|=below||ground||violation;
                        frames.WriteLine(JsonUtility.ToJson(new Frame {scenario=id,phase=sampledPhase,frame=index,root=root,eye=eye,origin=rig.TrackingOrigin.localPosition,rawHead=sampledRaw,
                            crouch=rig.RampCrouchOffset,standing=calibration.Receipt.TrackedStandingEye,calibratedEye=calibration.Receipt.TargetEye,stickX=stick.StickX,stickY=stick.StickY,
                            supportY=floorFound?support.y:float.MinValue,gap=gap,clearance=nearest.distance,alpha=rig.ViewFade.Alpha,supported=rig.Supported,focused=!rig.FixtureUnfocused,
                            paused=rig.LocomotionProbePaused,below=below,visibleViolation=violation,cross=cross,corrections=writes.ToString(),
                            advance=rig.AdvanceTrace,verified=rig.VerifiedPath,moveY=rig.LastFrame.MoveY,inputReady=rig.LeftInputReady,gameplayBlocked=rig.GameplayBlocked,
                            lastTracked=rig.LastFrame.Tracked,lastPaused=rig.LastFrame.Paused,rightReady=rig.RightInputReady,menuLocomotion=rig.MenuLocomotion}));
                        if(s<6&&yaw==0&&(index==36||index==179||index==539))R25RampCrouchVideo.Render(rig.Head,Path.Combine(output,id+"-"+index+".png"));
                        priorEye=eye;result.frames++;
                    }};
                }
                // A fixture that never walks proves nothing: report it, do not count it as stuck.
                if(approached<.05f){result.fixtureInvalid++;failed=true;result.failures.Add(id+" approach did not move ("+approached.ToString("F3")+" m)");}
                else if(Vector3.Distance(retreatStart,rig.transform.position)<1){result.stuck++;failed=true;}
                if(rig.RampCrouchOffset>.005f){result.failedStand++;failed=true;}
                result.scenarios++;if(failed)result.failures.Add(id);
                File.WriteAllText(Path.Combine(output,"partial-result.json"),JsonUtility.ToJson(result,true));
            }
            rig.LocomotionWrite=null;rig.FixtureUnfocused=false;rig.LocomotionProbePaused=false;
            result.acceptancePassed=result.belowSupport==0&&result.groundViews==0&&result.visibleViolations==0&&result.stuck==0&&result.failedStand==0&&result.fixtureInvalid==0; // skipped placements are listed, not failures
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));
        }
    }
}
#endif
