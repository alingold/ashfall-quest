# Root integration hook: numeric XR pose diagnostics

The opt-in helper is scripts/xr_pose_diagnostics_r19.gd. It is a RefCounted object, never an autoload. No Main/renderdiagnostics hook has been edited. Root controls enablement and collection. It performs no file/network I/O and writes no transforms, settings or inputs. Returned dictionaries contain numbers only: active settings/tau, stick axes, dominance/support/transition flags, frame stamps, orientation error angles between production tracker/controller/proxy/gun/model/filter/optic stages. NO raw positions, origins, basis arrays, names, voice, account or map data.

Small hook root can add to its existing opt-in diagnostics owner (use its existing explicit debug flag):

```gdscript
var _xr_pose_probe: RefCounted
# Inside the existing explicit diagnostics-enable path, after selected rack setup:
_xr_pose_probe = preload("res://scripts/xr_pose_diagnostics_r19.gd").new()
_xr_pose_probe.setup(weapon_rack, 20.0, 128, 5.0)
```

After the selected gun has run its production pose/support/filter/poll sequence, from the existing diagnostics callback:

```gdscript
if _xr_pose_probe != null:
    var pose_sample: Dictionary = _xr_pose_probe.snapshot()
    if not pose_sample.is_empty():
        existing_diagnostics_samples.append(pose_sample) # Root's existing bounded collector.
    if _xr_pose_probe.is_complete():
        _xr_pose_probe = null
```

Substitute the root's actual rack/collector identifiers. Do not add an always-on call, automatic upload, independent timer or new default logging file. Disable by dropping the reference. Explicitly call setup again only for another user-authorized diagnostic capture. snapshot() returns {} when throttled, exhausted, timed out, or no valid selected gun exists.

Bounds: setup clamps duration1..30seconds, sample count1..256, regular rate1..15Hz. Defaults20seconds/128/5Hz. Grip/support transitions can bypass the regular5Hz interval but NEVER the hard15Hz cap or count/duration bounds. Brief transitions entirely within that cap may be coalesced. Snapshot records differences from the last emitted sample. No allocations/storage beyond one returned sample and fixed helper state. Root's collector must remain bounded too.

Interpretation: controller_gun_error_deg is corrected dominant grip vs visible gun; controller_filter_error_deg is grip vs stored aim filter; filter_gun_error_deg isolates any downstream pose mutation. Proxy/model and optic/head errors include authored grip offsets, leveling and optical image staleness; their changes around grip transitions matter more than their absolute angles. Bound tracker-node error uses XRPose.get_adjusted_transform vs node-local basis. No claim of headset acceptance: the smoke test exercised synthetic tracker paths, count/duration/rate gates and numeric-only result schema headless.

The earlier tests/r19_pose_probe.gd and synthetic raw-pose sample were deleted. They must not be integrated. Fresh numeric example is tests/receipts/r19/r19-numeric-probe-sample.json.
