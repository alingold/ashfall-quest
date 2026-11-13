# Versioned Ping owner recovery

Accepted Modes/2 PingService and CommandLedger bytes stay immutable. A new separately reviewed owner in Modules/ModesRecovery may provide RecoverablePingService and its own private ledger, derived explicitly from the exact accepted source with source-trace/differential evidence. It is not accepted Modes/2 and cannot silently replace current Director pings.

Preserve actual authority, actor/team visibility, warmup, cooldown, clock, limits, map transforms, replay/order and journal capacity semantics. Its public gameplay API should match the accepted service where possible using existing public Modes types. Its codec captures all actual private replay/cooldown/marks/clock/epoch/map/team state with strict bounded bytes and trusted external CheckpointBinding/digest. Restore into fresh quarantined objects bound to actual restored roster/gate; never replay commands or infer private state from VisiblePings. Preserve unknown unsupported original states by explicit refusal, not resetting them. Original accepted service cannot be exported through reflection or a fixture success callback.

Cross-owner Director adoption remains root-owned after independent review and coherent full checkpoint/producer closure. This addition grants no production authority, new network protocol, global retirement or M1 approval.
