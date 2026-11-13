# Float radians endpoints

DesktopLook publishes float radians by rounding a double-clamped ±Math.PI. The inclusive endpoint is therefore ±(float)Math.PI. Locomotion validates that same float interval, including SnapRadians supplied by PlayerSettingsPublication. The immediately adjacent exterior floats still reject, as do nonfinite values. This is an input-domain alignment, not a tolerance added to dt, heading, collision or authority checks.

Rejected yaw/tuning frames retain same-tick retry through detached calculation. Root phase20 covers both swipe directions, finite extreme pixel events, adjacent outside floats, invalid values and retry. This additive implementation correction changes no Core types, wire protocol or source sensitivity.
