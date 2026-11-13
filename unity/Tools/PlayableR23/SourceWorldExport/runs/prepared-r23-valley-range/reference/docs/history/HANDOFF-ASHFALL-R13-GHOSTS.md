# ASHFALL R13: moving team ghosts

[Launch VR](./ASHFALL-R13-Ghosts/Launch-VR.cmd) · [Desktop](./ASHFALL-R13-Ghosts/Launch-Desktop.cmd) · [Three-peer ghost test](./ASHFALL-R13-Ghosts/Self-Test-Ghosts.cmd) · [Source](./ASHFALL-R13-Ghosts-Source/README-R13.md)

Ghosts keep the current skin, can move with normal world collision, and appear translucent/grayscale through walls only to teammates. Friendly names/health tracks remain; opponents see no body/tag/weapon/shadow. Equipment is disabled until alive. Compact death notice, validated revival, timed deathmatch respawn and state restoration included.

[Through-wall teammate view](./ASHFALL-R13-Ghosts/screenshots/R13-07-GHOST-THROUGH-WALL.png) · [Opposing view](./ASHFALL-R13-Ghosts/screenshots/R13-09-OPPONENT-BEHIND-WALL.png) · [HUD notice](./ASHFALL-R13-Ghosts/screenshots/R13-06-COMPACT-DEATH-NOTICE.png)

Offline: 984 ghost checks across both model tiers/all seven skins; three actual Main peers exercise ghost walking, ally/enemy visibility, validated revival and timed respawn; 170 R12 deployment checks, 604 audio routes, 135 character checks, finite paired-ammo and session/reload fences. [Validation](./ASHFALL-R13-Ghosts/VALIDATION-R13.json) · [Porting manifest](./ASHFALL-R13-Ghosts/PORTING-MANIFEST-R13.json) · [Feedback register](./ASHFALL-R13-Ghosts/FEEDBACK-REGISTER-R13.md)

All 276 existing WAVs and 430 model/texture files are unchanged. Original/R10 map packs are unchanged; R12's full-library evidence is inherited, not a new R13 sweep. Own settings are seeded unchanged from R12; Initialize remains manual. No VR or another game was launched/stopped. Physical headset/performance and real Internet acceptance remain.

Source branch: `standalone/r13-team-ghosts`  
Checkpoint: `1122846d2b14d55736f4b972c95c84e56193c56a`  
Runtime ID: `e9390219bdf23f9297d7ed9250ab641f230c5fb62d691d347e46387098bcf1fd`  
Executable SHA256: `12abeed99e63092a2f95f5f19f1d3c408b82783222c29890cf736aa4f7fdb333`
