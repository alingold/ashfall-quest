# Dispatch template

Use [R24 working rules](HILL-CLIMB-ORCHESTRATION.md). Dispatch only when another worker can produce an independent deliverable.

Every brief fits these fields:
- **Outcome:** the model, visible fix or working feature to deliver.
- **Baseline/owner:** exact project and files; current Godot is `quest-r24/source` from Opus R23.
- **Implement:** the concrete change, not another proposal for somebody else.
- **Check:** one relevant runtime/visual/behavior check; state untested limits.
- **Return:** source/assets, evidence path, remaining concrete defects and ready-to-merge instructions.

Skins return editable source plus PCVR/LIGHT exports and useful reference views. Maps return a source-bound repair demonstrated in the actual game. Unity returns a runnable integrated slice. Reviewers return actionable defects; they do not spawn more reviewers. A missing broad test is a limitation, not automatically a merge veto.

Do not duplicate historical reports, create routine certificate tasks, or dispatch an implementation's own assertions as an independent review. Integrators fix small findings and decide merge/adapt/park promptly.
