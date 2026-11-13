"""Source-bound UI comparison plus real installed-API compile/pure data-input cases."""
from pathlib import Path
import ast,hashlib,json,re,subprocess
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];UI=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI'
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(name,files,refs=(),library=False):
    output=HERE/(name+('.dll'if library else'.exe'));rsp=HERE/(name+'.rsp')
    if not library:refs=list(refs)+[CSC.parent/'System.dll',CSC.parent/'System.Core.dll']
    args=['/nologo','/warn:4','/warnaserror+','/langversion:5','/target:'+('library'if library else'exe'),'/out:'+str(output)]+['/reference:'+str(r)for r in refs]+[str(f)for f in files]
    if library:args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
    rsp.write_text('\n'.join('"'+s+'"'for s in args),encoding='utf-8');result=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True)
    print(name,result.returncode,result.stdout+result.stderr);assert result.returncode==0
    if not library:
        result=subprocess.run([str(output)],capture_output=True,text=True);print(result.stdout+result.stderr);assert result.returncode==0
def main():
    manifest=FROZEN.parent/'SOURCE-MANIFEST.json';assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
    table={r['path']:r for r in json.loads(manifest.read_text())['files']};bound={}
    for name in ['menu','front_menu1000','front_menu_091','item_catalog_ui_088','item_catalog_service_088','weapon_rack','weapon_rarity_r6','supply_catalog_088']:
        rel='scripts/'+name+'.gd';bound[rel]=sha(FROZEN/rel);assert bound[rel]==table[rel]['sha256']
    current=(UI/'R23VrUI.cs').read_text(encoding='utf-8');source=(FROZEN/'scripts/front_menu1000.gd').read_text(encoding='utf-8')
    assert 'buttons[3].size.x=180'in source and 'Vector2(650,712),Vector2(180,64)'in source
    assert 'FrontButton("LOADOUT",450,712,180,64,OpenWeapons'in current
    assert 'FrontButton("OUTFITS",650,712,180,64,OpenAppearance'in current
    assert 'public void OpenWeapons() { weaponChoice=0;OpenSubpage(R23UiPage.Weapons); }'in current
    assert 'MenuCard("WEAPONS",-.09f,"settings_2",OpenWeaponSettings)'in current
    backpack=current.split('void BuildLoadout()',1)[1].split('void BuildAppearance()',1)[0]
    assert 'OpenWeapons'not in backpack and 'WeaponSelected'not in backpack
    assert 'inventoryCells={0,1,4,5,8,9}'in backpack and 'resourceCells={2,3,6,7,10,11}'in backpack
    assert 'Page==R23UiPage.Loadout?.0288f:.042f'in current and abs(32*1.2/960*.72-.0288)<1e-12
    assert 'count.anchor=TextAnchor.UpperLeft' in backpack and 'caption.anchor=TextAnchor.UpperCenter' in backpack
    assert '-.43875f+cell%4*.2925f' in backpack and '.2825f,.145f' in backpack
    assert 'family==null?.07875f:-.13f' in backpack and 'family==null?.015f:-.02125f' in backpack
    assert 'new Vector3(0,-.0325f,-.007f)' in backpack and '.0144f,.2675f' in backpack
    assert 'selected=selected&&button.Enabled'in current and 'button.Edge.gameObject.SetActive(selected)'in current
    assert 'panel.position+=at-lastLoadoutAnchor'in current and 'FollowLoadoutAnchor();'in current
    assert 'PaddlesRequested' in current and 'REVIVE PADDLES'in backpack and 'paddlesStatusText.text=paddlesStatus'in current
    assert 'Settings.MotionDamping;},0,2,.01f'in current and 'Settings.OffhandInfluence;},0,1,.01f'in current
    wheel=ROOT/'unity/Assets/Ashfall/Integration/Playable/Input/R23WeaponWheelState.cs'
    run('ApiCases',[UI/'R23UiState.cs',UI/'R23LoadoutGesture.cs',wheel,HERE/'ApiCases.cs'])
    refs=[p for p in(E/'Managed/UnityEngine').glob('*.dll')if not p.name.startswith('UnityEditor')]+list((E/'NetStandard/ref/2.1.0').glob('*.dll'))
    extra=[UI.parent/'Appearance/R23PlayerAppearance.cs',UI.parent/'HandsWeapons/R23Visuals.cs',wheel,UI.parent/'Input/R23WeaponWheelIcons.cs']
    run('R6UI',list(UI.glob('*.cs'))+extra,refs,True)
    icon=UI/'Resources/R23SourceUI/backpack_r23_revive.png';proof=json.loads((HERE/'icon-provenance.json').read_text());assert sha(icon)==proof['outputSha256']
    meta=Path(str(icon)+'.meta').read_text(encoding='utf-8');assert '  nPOTScale: 0' in meta,'Unity must preserve the genuine 256x171 raster rather than power-of-two resampling'
    image=Image.open(icon).convert('RGBA');assert image.size==(256,171);pixels=list(image.getdata());assert any(r>245 and g>245 and b>245 and a>245 for r,g,b,a in pixels)and any(g>200 and b>200 and r<100 and a>245 for r,g,b,a in pixels)
    (HERE/'source-bindings.json').write_text(json.dumps(bound,indent=2)+'\n',encoding='utf-8')
    print('Frozen R6 lobby routes/layout, true pinned backpack, four live setting controls, source RGBA utility art and installed API PASS. Native/render/device unrun.')
if __name__=='__main__':main()
