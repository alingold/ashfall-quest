"""Convert only new outfits from the authoritative frozen R6 source bundle."""
from pathlib import Path
import hashlib,importlib.util,json,re
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
SOURCE=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');MANIFEST=SOURCE.parent/'SOURCE-MANIFEST.json'
assert hashlib.sha256(MANIFEST.read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
bundle=json.loads(MANIFEST.read_text());assert bundle['contentId']=='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520'
spec=importlib.util.spec_from_file_location('r5_converter',HERE.parent/'R5/prepare.py');core=importlib.util.module_from_spec(spec);spec.loader.exec_module(core)
r5_keys=core.keys[:];r5_labels=core.labels[:];r5_extra=core.extra.copy()
core.SRC=SOURCE;core.PREIMAGE=SOURCE;core.CONTENT=MANIFEST;core.content=dict(bundle,content_id=bundle['contentId']);core.known={r['path']:r['sha256']for r in bundle['files']};core.records={};core.locations={};core.HERE=HERE
core.OUT=Path('D:/ashfall-unity-appearance-staging/r6-v001');core.OUT.mkdir(parents=True,exist_ok=True);core.created=set();core.tex_rows={};core.payload_bytes=sum(p.stat().st_size for p in core.OUT.iterdir()if p.is_file()and not p.name.endswith('.meta'))
core.registry=core.read('scripts/gemini_player_models_099.gd').decode('utf8');core.drafts=json.loads(core.read('assets/data/draft_styles_r24.json'))
core.keys=json.loads(re.search(r'const STYLE_KEYS := (\[.*?\])',core.registry).group(1));core.labels=json.loads(re.search(r'const STYLES := (\[.*?\])',core.registry).group(1));core.extra=json.loads(re.search(r'const EXTRA_KITS := (\{.*\})',core.registry).group(1))
assert len(core.keys)==len(core.labels)==115 and core.keys[:85]==r5_keys and core.labels[:85]==r5_labels
assert all(core.extra[k]==v for k,v in r5_extra.items())
def __getattr__(name):return getattr(core,name)
if __name__=='__main__':
 core.main(core.keys[85:],'receipt.json')
 p=HERE/'receipt.json';receipt=json.loads(p.read_text());receipt['priorVerifiedIndices']=list(range(85));receipt['remainingIndices']=[];receipt['fullRegistryStyles']=115;p.write_text(json.dumps(receipt,indent=2)+'\n')
