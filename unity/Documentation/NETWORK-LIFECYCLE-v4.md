# Network lifecycle v4

Central Unity contract clarification, 2026-11-04. v3 did not authorize clients to persist command numbers across authenticated reconnects. Source-compatible remapped clients may restart numbering. NetworkingPeerR4 and SessionsR4 remain unreviewed candidates; no automatic acceptance follows.

Ingress sequence and raw client command IDs are scoped to one authenticated connection generation. Trusted canonical actor identity, semantic damage/event replay, retained logical transaction identities and authority epoch remain distinct. An old lease, queued callback, frame or receipt must never act on a replacement connection. Never erase canonical logical-event history or reset global damage replay to admit a reconnect.

The authorized repair may implement explicit per-lease ingress watermarks and generation-bound command normalization, or a trusted server-side translation with equivalent injectivity and replay protection. No client-chosen generation or authentication bypass. Authenticate first; reject stale generations before mapping or dispatch. Fresh and remapped connections starting at sequence1 must work; in-generation duplicates and packets from the retired connection must remain rejected. No arbitrary persistent-client-numbering requirement.

Prepared peer admission must detect retained replay-history mutations and keep active capacity separate from bounded canonical history. Domain rejection before final publication must not spend fresh identity history. Exhaustion fails closed without evicting replay tombstones. Prepare/validate/no-fail-commit/abort share the trusted host lane; external ports must be pure/stable/nonthrowing, and unsupported rollback stays explicit.

Epoch, lifecycle and source-ID contracts remain coordinator-owned. All implementation changes need fresh independent review and actual composed pressure/reconnect gates. Native Transport, authentication provisioning, WAN and hardware are separate open gates.

