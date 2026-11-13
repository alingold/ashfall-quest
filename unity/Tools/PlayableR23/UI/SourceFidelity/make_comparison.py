"""Embed unchanged existing screenshots side-by-side; no render/image editing or engine job."""
from pathlib import Path
import base64
import html
ROOT=Path(__file__).resolve().parents[5]
OUT=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/UI/SOURCE-COMPARISON.html'
OLD=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/run-20261104-193930'
NATIVE=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/floor-20261104-210248'
HIST=Path('C:/Users/aling/Documents/Codex/2026-10-24/build-a-large-beautiful-playable-map/outputs')
R21=Path('C:/Users/aling/Documents/Codex/2026-11-02/ashfall-r21-interactions/evidence')
def picture(path):
    data='data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode()
    return '<a href="'+data+'"><img src="'+data+'"></a><small>'+html.escape(str(path))+'</small>'
rows=[
('Lobby menu',ROOT/'docs/agent-runs/20261104-three-chats/world/r12-world-engine-batch/r8-map-runtime/game/incumbent/zero-request-no-application-lobby.png',OLD/'smoke-menu.png',NATIVE/'smoke-menu-source.png','Transparent 2.20 × 1.375 m source layout; yellow READY; cyan MAP SELECT; source menu anchor. Root owns the actual lobby/world transition. The desktop projections differ; preserve the source physical board dimensions pending an actual XR comparison.'),
('Character selection',HIST/'ASHFALL-R9-Evidence/MENU-R9-INITIAL.png',OLD/'smoke-appearance-menu.png',NATIVE/'smoke-characters-source.png','The historical single-podium reference predates the R23 left-side panel adjustment; actual R23 front_menu1000.gd lines 218–234 supplies the new left-side character controls. Existing full-size source preview is anchored to the real lobby podium.'),
('Settings',R21/'R21-MENU-root.png',None,NATIVE/'smoke-settings-source.png','Actual R23 menu.gd and pause_menu_style0993.gd retain these 1.30 × 1.48 m settings cards/icons/colors. No delivered settings-only before capture was available. KNOWN DEFECT in this unchanged native capture: the category icons render black although the source PNG contains white glyphs and a dark visor. A dedicated full-RGBA shader has since been published but is not rendered in this capture; native confirmation is pending.'),
('Gameplay HUD',HIST/'CYBER-BIRD-R6-Evidence/18C-HUD-HIRES.png',OLD/'smoke.png',NATIVE/'smoke-hud-source.png','Reference screenshot is historical; current reference_hud_090.gd supplies exact positions/colors and removes the old stamina percentage. New gameplay HUD has 0.29 m stacked meters, runner, build grid and compass; no menu card or menu buttons. The settings icon shader change also affects the runner icon and has not been captured yet.')]
parts=['<!doctype html><meta charset="utf-8"><title>ASHFALL source UI comparison</title><style>body{background:#181b20;color:#e3e8eb;font:16px system-ui;margin:32px}section{margin:40px 0}.pair{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}figure{margin:0}img{width:100%;height:auto;border:1px solid #515960}small{display:block;overflow-wrap:anywhere;color:#b7c0c5}p{max-width:1300px;line-height:1.5}.pending{border:1px dashed #89939a;padding:24px}h2{margin-bottom:12px}@media(max-width:1100px){.pair{grid-template-columns:1fr}}</style><h1>Actual source references and unchanged native Unity captures</h1><p>Source references, delivered Unity 193930 before revision, and coordinator native captures from floor-20261104-210248 are embedded without pixel edits. Historical source images are identified below; current Opus R23 source code governs the revision. These are desktop render comparisons, not headset acceptance. The later full-RGBA icon shader fix has only been CPU/API checked and is not shown in these native captures. Click an image to inspect its original pixels.</p>']
for name,reference,before,after,note in rows:
    parts.append('<section><h2>'+html.escape(name)+'</h2><p>'+html.escape(note)+'</p><div class="pair"><figure><h3>Source reference</h3>'+picture(reference)+'</figure><figure><h3>Delivered Unity 193930 before revision</h3>'+(picture(before) if before else '<div class="pending">No matching before screenshot available.</div>')+'</figure><figure><h3>Native Unity 210248 after layout revision</h3>'+picture(after)+'</figure></div></section>')
OUT.write_text(''.join(parts),encoding='utf-8');print(OUT)
