# Development checks

Run from the ASHFALL repository root. Use a new output directory outside `unity/`
for each invocation; results never replace imported compiler outputs.

```powershell
python unity/Tools/check.py all --output-root maintenance/checks-20261109
```

Choose `lan`, `modules`, `project` or `api` instead of `all` to run one layer.

| Layer | What it checks |
| --- | --- |
| `lan` | Production Session/LAN/Health source with simulated transport, authority, custody, lifecycle and adversarial durable-storage cases. Crash workers are CPU-only child processes. |
| `modules` | Six retained Health, pickup and TeamLaunch suites against current source. |
| `project` | All Assets C# source using the last imported Unity response files, adding not-yet-imported source to the correct runtime/editor unit. |
| `api` | Runtime, EditorRuntime and BuildAndSmoke source closures against installed native API references. BuildAndSmoke is compiled, never invoked. |

C# warnings remain errors. Source coverage, field-drift assertions, movement/world
validation and replay checks remain enabled. A failing layer stops the runner.
LAN storage fixtures and partial-write/corruption evidence stay inside that run's
`lan/storage-fixtures/` directory.

`project` defaults to the shared cache response directory
`unity/Library/Bee/artifacts/1300b0aE.dag`. Supply `--response-root <directory>` after
an authorized import changes it. `api` uses the installed Unity 6000.6.8f1 compiler
and preserved editor-probe package assemblies. These checks read the cache;
they do not launch Unity, mutate Library, open sockets or access a device.

`PlayableR23/Networking/Current/` is the maintained multiplayer test suite. Its
Map/Presence/Round/Spawn fixtures still live one directory above and are included
by the current harness. Those are active tests, despite their older location.
Obsolete tracking/1 fixtures and hardcoded one-off staging/receipt scripts were
removed in the November 9 cleanup. Historical receipts remain in the campaign
and agent-run directories. One-shot source rewrite scripts were also removed: they replayed already-published
implementation edits rather than checking behavior, and some could replace Unity
GUIDs or duplicate declarations. Their final test cases and runnable checks remain.
Edit the maintained source directly; use the checks to validate changes.

Other feature tools are focused conversion/checking utilities; they are not
implicitly part of `all`.

Native UTP, Physics, UI, frame pacing and headset acceptance require separately
authorized engine/peer/device tests. Keep the existing playtest hold until released.
