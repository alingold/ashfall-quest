# Explicit Set Height: current native tracked posture

Changed ONLY live `Calibration/R23VerticalCalibration.cs`, `R23CalibrationState.cs` plus associated calibration tools. No Root/Rig/UI edits; art and historical staged calibration files untouched.

## Live behavior and binding

Existing parameterless `RequestRecalibration` UI Action preserved. It now takes the first valid actual tracked .1?4m pose, seated/standing/crouched, without boot .25s stability or PC focus/menu-pause gating. This directly invokes the semantics of frozen `player.initialize_vertical_combat_system`; explicit immediate action is the user's requested interaction (source's separate gaze UI is not being claimed). Boot retains its original .25s/.025m window. Target head top1.8288m, eye offset.120m, translation only, crouch metric length, above-baseline correction, ceiling/hull protection, persistence and disarm event remain unchanged.

On the button callback, native HMD verification can apply immediately if its full pose matches the current camera sample within5cm; otherwise request remains pending until the next valid host pose. The5cm check is only provenance for this fallback, not a stability window; a true host `Tick(tracked:true,...)` takes its first valid manual sample without that fallback check. A finite cached/default camera alone is never treated as tracked. Invalid raw height now reports `Tracked height outside source range (0.1-4 m)` rather than falsely saying tracking is missing.

New Root-callable production seam:

```csharp
Vector3 p; Quaternion q;
Tracking = R23VerticalCalibration.TryReadNativeTrackedHeadPose(out p,out q);
if(Tracking) { Head.transform.localPosition=p; Head.transform.localRotation=q; }
```

Replace Rig.TrackPoses's current legacy-isTracked-only HMD block with this if the root wants all actual native providers consistently. Helper checks, in order: legacy XR HMD full position+rotation with explicit tracked/state flags; native XRNode.Head `tracked` and actual full pose; enabled/added InputSystem XRHMD `isTracked`, Position|Rotation tracking bits, actual full pose. Finite position/unit-near quaternion required. No display-running/PC/OpenXR-session-focus inference, no fake untracked pose, no origin-mode/height fabrication, and it never writes a camera/controller itself. Calibration's pending manual path reuses this verified native fallback only when it matches current tracked camera pose.

Root is still sole camera/hand tracking writer. UI event binding can remain exactly `UI.RecalibrationRequested += Calibration.RequestRecalibration`. Runtime Tick signature unchanged.

If real Quest rawY is0 because the native provider is in eye-level rather than floor stage, that remains source-invalid (source explicitly rejects <.1). Root must resolve actual supported Floor tracking origin and actual current pose; do not substitute standing1.65m. Existing577b64 on-device raw flags/heights were not available to this worker, so no claim that exact provider defect was reproduced on hardware.

## Checks

`python unity/Tools/PlayableR23/Calibration/check_manual.py`: **233 PASS**, seated/tall first-sample captures, changing/paused/unfocused explicit action, source bounds/untracked/nonfinite refusal, no accumulated lift,10cm metric crouch, upper clamp, original boot stability/motion/focus, native6DoF pose validity and ceiling formula. Pure checks use actual live state file. Pinned actual frozen R6 player/vertical-combat scripts against SOURCE-MANIFEST SHA b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111; hashes `manual-source-pins.json`.

Installed Unity6000.6.8f1 Runtime API compile PASS after final change; full actual source compiler existing checker, isolated Tools output. Root owns final multi-target check and hardware verification. No engine/device/build/native execution by this worker. Historical `StagedRuntime`/old staging check is not the current implementation or this task's check command.
