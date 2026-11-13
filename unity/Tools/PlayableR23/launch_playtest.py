"""Explicit user playtest: acquire shared lanes, launch, release on game exit."""
import datetime
import json
import pathlib
import queue
import subprocess
import sys
import threading
from playtest_state import PlaytestState

root = pathlib.Path(__file__).resolve().parents[3]
evidence = root / "docs/agent-runs/20261104-three-chats/unity/playable-r23"
stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
executable = (root / json.loads((evidence / 'current-build.json').read_text(encoding='utf-8-sig'))['build']).resolve()
if not executable.is_relative_to(root / 'unity/Builds') or not executable.is_file():
    raise SystemExit('Current verified executable missing or outside Unity Builds')
player_log=evidence/("player-"+stamp+".log")
command = [sys.executable, "-u", str(root / "campaign-staging/20261104-three-chats/world/engine_lane.py"),
           "--", sys.executable, "-u", str(root / "skins_pipeline/tools/gpu_worker.py"),
           "--owner", "unity-r23-user-playtest", "--", str(executable),
           "-logFile", str(player_log)]
with (evidence / ("launch-" + stamp + ".log")).open("w", encoding="utf-8") as log:
    child = subprocess.Popen(command, cwd=executable.parent, stdout=subprocess.PIPE,
                             stderr=subprocess.STDOUT, text=True,
                             creationflags=subprocess.CREATE_NO_WINDOW)
    log.write("lane_wrapper_pid=" + str(child.pid) + "\n"); log.flush()
    messages=queue.Queue()
    def collect():
        for line in child.stdout:messages.put(line)
    threading.Thread(target=collect,daemon=True).start()
    state=PlaytestState();position=0;last=None;pending_player=b''
    def report(terminal=None):
        global last
        snapshot=state.snapshot();snapshot.update(executable=str(executable),playerLog=str(player_log),laneWrapperPid=child.pid)
        if terminal is not None:snapshot.update(phase='exited',exitCode=terminal)
        if snapshot==last:return
        last=snapshot
        # Monitoring/receipt I/O must never terminate a human runtime.
        try:(evidence/("launch-state-"+stamp+".json")).write_text(json.dumps(snapshot,indent=2)+'\n')
        except OSError as error:print('PLAYTEST_STATUS_IO '+str(error),flush=True)
        print('PLAYTEST_STATE '+json.dumps(snapshot),flush=True)
    report()
    while child.poll() is None:
        try:
            line=messages.get(timeout=.5);log.write(line);log.flush()
            if "GPU_QUEUE_WAIT" in line:
                # Refuse GPU queuing: this tree has not launched the game yet.
                subprocess.run(["taskkill.exe","/PID",str(child.pid),"/T","/F"],stdout=log,stderr=log,check=False)
                break
        except queue.Empty:pass
        try:
            if player_log.exists():
                with player_log.open('rb') as player:
                    player.seek(position);fresh=player.read();position=player.tell()
                pending_player+=fresh
                while b'\n' in pending_player:
                    line,pending_player=pending_player.split(b'\n',1)
                    state.observe(line.decode('utf-8',errors='replace'))
                report()
        except OSError:pass
    code = child.wait()
    while not messages.empty():log.write(messages.get_nowait())
    log.write("exit=" + str(code) + "\n")
    report(code)
