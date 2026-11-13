# Source head observation connection binding

SessionNetworkCoordinator.GetConnectionIdentity(actor) returns the actual Session opaque connection-incarnation object or null. It neither creates authority nor exposes a writable member. SessionSourceObservations.BindPlayer captures that identity alongside the real PlayerFrameOwner generation, Health life/connection generations, canonical actor and spawn revision. Capture requires the same Session identity on every observation.

Same-name reconnect without an intermediate observation therefore hides the old head source even if Health has not yet updated and the old owner publishes another valid tick. Only a new trusted BindPlayer call can attest it for the replacement. This uses the existing Session incarnation implementation required by POSE-LIFECYCLE-v1; historical Session assemblies lacking that deliberate port are unsupported. No wire/schema change or feet-to-head inference is introduced.
