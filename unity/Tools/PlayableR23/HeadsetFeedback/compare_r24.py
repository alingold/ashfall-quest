"""Verify the published R24 R5 inputs, then identify actual deltas from original R23."""
import argparse,hashlib,json,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4];SRC=ROOT/'quest-r24/source'
OLD=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
MANIFEST=ROOT/'campaign-staging/20261104-three-chats/world/morning-r5/r5/delivery/SOURCE-CONTENT.json'
EXPECTED='d4fb1053acfbaa7f3eb285879472bfdd136e09bf2f7e1f2dc6478f23d8f12bc7'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def frozen():
    assert sha(MANIFEST)==EXPECTED,'Published R5 manifest changed'
    m=json.loads(MANIFEST.read_text());assert m['content_id']=='da1db74984ea1b82a8b60b7cb8baae9d31dc5483f387fdcf105f3dd0a308895c'
    return {r['path']:r['sha256']for r in m['files']}
def read(relative,index=None):
    index=frozen()if index is None else index;p=SRC/relative
    assert relative in index,'Input not in published R5: '+relative
    if p.is_file()and sha(p)==index[relative]:return p
    # WORLD published these exact R5 preimages before its separate R6 map delta.
    prior=Path('D:/ashfall-r6-staging/world/classic-family-v001/r5-preimages')/relative
    assert prior.is_file()and sha(prior)==index[relative],'No exact published R5 bytes available: '+relative
    return prior
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path);args=parser.parse_args()
    index=frozen();rows=[]
    for relative in ['scripts/front_menu1000.gd','scripts/outfit_browser_r24.gd','scripts/menu.gd','scripts/main.gd','scripts/weapon.gd','scripts/weapon_rack.gd','scripts/player.gd','scripts/explosive_weapon.gd','scripts/field_builder.gd','scripts/gemini_player_models_099.gd','assets/data/draft_styles_r24.json','assets/data/architecture_r24.json']:
        if relative not in index:rows.append(dict(path=relative,status='not in R5 manifest'));continue
        p=read(relative,index);old=OLD/relative
        rows.append(dict(path=relative,resolvedSource=str(p),sha256=index[relative],matchesOriginalR23=old.is_file()and sha(old)==index[relative]))
    # The new mechanics repairs were traced against original R23. Explicitly
    # qualify those recorded inputs against R5 rather than assuming equivalence.
    existing=json.loads((ROOT/'unity/Tools/PlayableR23/HeadsetRecoil/source-inputs.json').read_text())['sha256']
    grenades=json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23/Explosives/provenance.json').read_text())
    existing.update(grenades['inputs'])
    generated=[]
    for row in grenades['geometry']+grenades['audio']:
        if 'source'not in row or 'sha256'not in row:continue
        relative=row['source'].removeprefix('res://').replace('\\','/')
        if '::'in relative:
            recipe=relative.split('::',1)[0];assert recipe in grenades['inputs'] and grenades['inputs'][recipe]==index[recipe]
            generated.append(dict(recipe=relative,recipeSha256=index[recipe],generatedSha256=row['sha256']));continue
        existing[relative]=row['sha256']
    compatibility=[]
    for relative,digest in sorted(existing.items()):
        assert relative in index and digest==index[relative],'Original repair source differs from R5: '+relative
        p=read(relative,index);compatibility.append(dict(path=relative,sha256=digest,resolvedSource=str(p)))
    result=dict(target='R24 R5',manifest=str(MANIFEST.relative_to(ROOT)),manifestSha256=EXPECTED,sourceContentId='da1db74984ea1b82a8b60b7cb8baae9d31dc5483f387fdcf105f3dd0a308895c',inputs=rows,qualifiedRepairInputs=compatibility,generatedAudioRecipes=generated)
    if args.output:
        temp=args.output.with_suffix('.tmp');temp.write_text(json.dumps(result,indent=2)+'\n');os.replace(temp,args.output)
        print('R24 R5 bindings verified:',len(rows),'target inputs,',len(compatibility),'repair inputs,',len(generated),'source warning recipes')
    else:print(json.dumps(result,indent=2))
if __name__=='__main__':main()
