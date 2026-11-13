"""Summarize actual player-log observations; not a substitute for process ownership."""
class PlaytestState:
    def __init__(self):
        self.world_ready=False
        self.headset_focused=False
        self.controllers_tracked=False
        self.startup_unavailable=False
        self.ever_headset_ready=False
        self.phase='starting'

    def observe(self,line):
        if 'XR_ERROR_FORM_FACTOR_UNAVAILABLE' in line:
            self.startup_unavailable=True
        if 'R23 VR map loop ready' in line:
            self.world_ready=True
        if 'R23 XR input ' in line:
            self.headset_focused='XR focused' in line
            self.controllers_tracked='L tracked' in line and '| R tracked' in line
        ready=self.world_ready and self.headset_focused
        self.ever_headset_ready|=ready
        self.phase=('headset-ready' if ready else 'headset-unfocused' if self.ever_headset_ready
                    else 'waiting-for-headset' if self.startup_unavailable
                    else 'waiting-for-xr-focus' if self.world_ready else 'starting')

    def snapshot(self):
        return dict(phase=self.phase,worldReady=self.world_ready,
                    headsetFocused=self.headset_focused,controllersTracked=self.controllers_tracked,
                    startupHeadsetUnavailable=self.startup_unavailable,
                    everHeadsetReady=self.ever_headset_ready)
