"""Focused human UI fix payload/API checks. No Unity/Godot/native rendering."""
from pathlib import Path
import hashlib,json,re,subprocess
from PIL import Image

ROOT=Path(__file__).resolve().parents[5];HERE=Path(__file__).resolve().parent
BASE=ROOT/'unity/Assets/Ashfall/Integration/Playable';UI=BASE/'UI'
ui=(UI/'R23VrUI.cs').read_text(encoding='utf-8')
appearance=(BASE/'Appearance/R23PlayerAppearance.cs').read_text(encoding='utf-8')
preview=(UI/'R23AppearancePreview.cs').read_text(encoding='utf-8')
source=(ROOT/'quest-r24/source/scripts/front_menu1000.gd').read_text(encoding='utf-8')
checks=[]
assert 'CurrentViewedAppearanceId' in ui and 'AppearanceViewed' in ui
assert 'delegate{CycleAppearance(-1);}' in ui and 'delegate{CycleAppearance(1);}' in ui
assert 'PublishAppearanceViewed(appearanceOptions.Count==0?null:appearanceOptions[appearancePage].Id)' in ui
assert 'if(Page!=R23UiPage.Appearance)PublishAppearanceViewed(null)' in ui
checks.append('Both real arrows use CycleAppearance and publish currently browsed ID separately from worn selection')
assert 'SELECT YOUR APPEARANCE' not in ui and 'FrontText(appearanceStatus' not in ui
assert 'HOLD STILL TO NORMALIZE HEIGHT' in ui and 'STAND UPRIGHT TO CALIBRATE' not in ui
assert 'Resources.Load<Font>("R23GodotUI")' in ui
font=UI/'Resources/R23GodotUI.ttf';assert font.is_file() and font.stat().st_size>10000
checks.append('Bottom prompt removed; posture-neutral calibration text; actual R23GodotUI font retained')
position=re.search(r'if page=="maps":buttons\[10\].position=Vector2\(([^)]+)\);buttons\[10\].size=Vector2\(([^)]+)\)',source)
assert position and position.groups()==('40,720','170,58')
maps=ui.split('void BuildMaps()',1)[1].split('void BuildSpawnChoice()',1)[0]
assert 'FrontButton("BACK",40,720,170,58,' in maps
checks.append('Map BACK matches actual source final map layout override at (40,720), size170x58')
textures=[]
for key in ('valley','range','cyber_bird'):
    original=ROOT/f'quest-r24/source/assets/maps1000/{key}.png'
    published=UI/f'Resources/R23MapThumbnails/{key}.png'
    assert original.read_bytes()==published.read_bytes()
    with Image.open(published) as image:
        assert image.mode in ('RGB','RGBA') and image.width>100 and image.height>100
        assert len(image.getcolors(image.width*image.height))>100
        dimensions=image.size
    textures.append(dict(id=key,resource='R23MapThumbnails/'+key,dimensions=dimensions,sha256=hashlib.sha256(published.read_bytes()).hexdigest()))
assert 'SourceMapThumbnailResource(option)' in ui and '"R23MapThumbnails/"+option.Id' in ui
checks.append('Three canonical map resources bind real unchanged RGB source PNG payloads, including Valley/Range')
styles=re.findall(r'new R23AppearanceStyle\("([^"]+)","[^"]+",(\d+)\)',appearance)
assert len(styles)==8
data=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Appearance'
for key,index in styles:
    profile=json.loads((data/(key+'.json')).read_text(encoding='utf-8'))
    assert profile['id']==key and profile['sourceIndex']==int(index)
    assert (data/(key+'.meshbin')).is_file()
    for material in profile['materials']:
        for field in ('baseColor','normal','metallicMap','occlusion','emission'):
            if material.get(field):assert (data/material[field]).is_file()
assert hashlib.sha256((data/'camo.meshbin').read_bytes()).digest()!=hashlib.sha256((data/'witch_doctor.meshbin').read_bytes()).digest()
checks.append('All8 available styles have matching real manifests/meshes/textures; first arrow targets genuinely different source mesh bytes')
assert 'if(serial!=loadSerial){pendingAsset.Dispose();pendingAsset=null;yield break;}' in appearance
assert 'current.Profile.id' in appearance and 'renderer.gameObject.activeInHierarchy' in appearance
assert 'appearance.InspectPresentation()' in preview
native=ui.split('public IEnumerator CheckAppearancePreviewChange(',1)[1].split('public void SetAppearanceStatus',1)[0]
assert 'CycleAppearance(step==0?1:-1)' in native and 'preview.Show(' not in native
assert 'receipt.ProfileId!=expected' in native and 'receipt.VisibleRenderers<1' in native and '!receipt.HeadVisible' in native
checks.append('Callable check uses real arrow/host frame path and actual committed profile/renderers/head, not caption alone')
# The root/editor owner supplied these two handoffs; inspect current live bindings only.
root=(BASE/'R23PlayableLoop.cs').read_text(encoding='utf-8')
editor=(ROOT/'unity/Assets/Ashfall/Editor/Playable/R23PlayableBuild.cs').read_text(encoding='utf-8')
assert 'Preview.Show(UI.CurrentViewedAppearanceId??Appearance.RequestedStyleId??"camo")' in root
assert 'Resources/R23MapThumbnails' in editor and 'TextureImporterFormat.RGBA32' in editor
checks.append('Root viewed-style frame binding and thumbnail color importer scope are present (edited by root owner)')
editor_home=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
refs=list((editor_home/'Managed/UnityEngine').glob('*.dll'))+list((editor_home/'NetStandard/ref/2.1.0').glob('*.dll'))
sources=list(UI.glob('*.cs'))+[BASE/'Appearance/R23PlayerAppearance.cs',BASE/'HandsWeapons/R23Visuals.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/nostdlib+','/define:UNITY_6000_0_OR_NEWER','/out:'+str(HERE/'HumanFix.Native.dll')]+['/reference:'+str(path) for path in refs]+[str(path) for path in sources]
rsp=HERE/'compile.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args),encoding='utf-8')
run=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True)
(HERE/'compile.log').write_text(run.stdout+run.stderr,encoding='utf-8');assert run.returncode==0,run.stdout+run.stderr
checks.append('Full current UI/preview/appearance and native callable check compile against installed Unity6000.6.8f1 APIs, C#5 warnings-as-errors')
(HERE/'checks.json').write_text(json.dumps(dict(checks=checks,sourceFontSha256=hashlib.sha256(font.read_bytes()).hexdigest(),mapTextures=textures,
    nativePreviewChange='NOT RUN: call UI.CheckAppearancePreviewChange in reserved coordinator lane',
    limits='CPU assets/source binding checks and installed API compilation only. No engine, GPU, native preview change, texture import or headset execution.'),indent=2)+'\n',encoding='utf-8')
print('\n'.join('PASS '+check for check in checks))
