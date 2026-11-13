# Runtime contracts

Read the active source together with these boundary specifications. Versioned
contracts are additive historical baselines, not interchangeable project versions.
`CONTRACTS-v3.md` preserves the unit, source-ID and authority rules from v1/v2.
Do not delete earlier versions while later contracts refer to them.

- `CONTRACTS-v1.md`, `CONTRACTS-v2.md`, `CONTRACTS-v3.md`: shared boundaries,
  installed-editor compatibility and prepared transaction rules.
- `DAMAGE-POLICY-v4.md`, `HEALTH-TRANSACTIONS-v4.md`,
  `MATCH-TRANSACTIONS-v4.md`, `NETWORK-LIFECYCLE-v4.md`: gameplay authority,
  transaction and connection policy.
- `OWNER-ASSEMBLY-BOUNDARIES-v2.md`, `RECOVERY-ASSEMBLY-BOUNDARY-v1.md`,
  `MATCH-RECOVERY-v1.md`, `HEALTH-EPOCH-REVOCATION-v1.md`: ownership/recovery.
- The remaining named specifications cover focused input, pose, motion, audio,
  collision, settings and reservation boundaries.

The current durable Health path adds a fallible storage barrier before Session
publication. Earlier descriptions of purely in-memory no-fail commits do not
imply that storage I/O cannot fail. See the current
[LAN/storage guide](../Tools/PlayableR23/Networking/README.md) for quotas, exact
outcomes, manifest recovery and the fresh-room restart policy.

Start development at [PLAYABLE-R23.md](../PLAYABLE-R23.md); run source/CPU checks
through [Tools/check.py](../Tools/check.py). Engine/build/device checks remain
subject to the shared lane and current playtest hold.
