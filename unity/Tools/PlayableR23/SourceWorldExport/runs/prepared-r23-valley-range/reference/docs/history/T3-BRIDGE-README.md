# Local T3 Code read/prompt bridge

The bridge is set up and tested against the running T3 Code desktop server.
Gemini 4 Argon (High) is available through the authenticated `antigravity`
provider. No sign-in, installation, upgrade, or restart was needed.

Requirements: keep T3 Code running; Node.js 22 or newer on PATH (this PC has
24.12.0). The bridge reads the current loopback endpoint from T3's own runtime
record, so it does not hard-code a port or need a stored API key.

Run these from this folder in PowerShell:

```powershell
.\T3-Bridge.cmd status
.\T3-Bridge.cmd list Gemini
.\T3-Bridge.cmd providers
.\T3-Bridge.cmd read 4c4c62db-c1d3-45dc-b1d2-b748c7adf4c2 --text
.\T3-Bridge.cmd wait 4c4c62db-c1d3-45dc-b1d2-b748c7adf4c2 --seconds 60
```

`list` shows thread/project metadata; an optional text filter matches the title
or model. `read THREAD_ID --text` returns messages and run status; `--full`
returns T3's complete thread projection. `providers` returns current model
slugs and readiness without printing account credentials.
`wait` checks the latest run for at most 60 seconds, then exits; it does not
interrupt or cancel the agent when the timeout expires.

To send a prompt, save its exact multiline text in a UTF-8 file, then run:

```powershell
.\T3-Bridge.cmd send THREAD_ID --file C:\absolute\path\prompt.txt
```

This starts a real agent turn in the chosen T3 chat. The bridge checks for
active work and refuses to interrupt a running turn. The provider can use its
existing runtime permissions, so the prompt should state its workspace and
intended task clearly. Sending a prompt is explicit; status/list/read never
send one. No watchers or background prompt jobs are installed.

To create a fresh isolated chat, first create its workspace directory, then:

```powershell
.\T3-Bridge.cmd create --workspace C:\absolute\new\workspace --title "My isolated task" --provider antigravity --model gemini-4-argon-high
```

The directory becomes a T3 project when missing. Existing projects are reused
only for that exact directory. The command returns the new thread ID. Default
runtime is `full-access`, matching the available Argon setup; pass
`--runtime approval-required` for manual tool approvals instead. No original
ASHFALL, Skyrim, or Gemini asset project is selected automatically.

Validation: an isolated Argon chat named `CYBER BIRD T3 bridge verification`
returned exactly `T3_ARGON_BRIDGE_OK`, then completed. Its prompt forbade tools
and file changes. See `VALIDATION.json`. A separate new modeling chat is used
for the requested original raven taxi variant; it does not share the original
Argon asset workspace. That chat completed and delivered a 4,836-triangle GLB,
reproducible source and CC0 credits. The adopted asset is
`CYBER-BIRD-R5-Source/assets/cyber_bird/traffic_r5/raven_taxi_argon.glb` alongside
its `ARGON-PROVENANCE.json` and `ARGON-INTEGRATION.json`. Final geometry audits
and native rendering passed. See `ARGON-TASK-RESULT.json` for the completed run;
there is no background agent work left from this setup.

Implementation: the installed CLI's supported `auth session issue` creates a
short-lived scoped session. Tokens stay in process memory, are never printed
or written to this package, and are revoked on exit. Read commands request
only `orchestration:read`; send adds `orchestration:operate`; creating a project
also needs `settings:write`. HTTP uses `x-t3-orchestration-protocol: 2` and
`/api/orchestration/shell`, `/api/orchestration/threads/:id`. Prompt dispatch
uses the local `/ws?orchestrationProtocol=2` API with an authenticated short-lived
WebSocket ticket and `orchestration.dispatchCommand`.

These formats were checked against this installed T3 Code build's bundled
contracts and tested live. This is a local adapter rather than an official
stable SDK; a future T3 protocol change may require an update. It never edits
T3's database directly or bypasses T3 authentication. Its only T3-side changes
are the explicitly created isolated project/chats, prompts, and temporary
sessions. The bridge is original code provided under CC0-1.0.
