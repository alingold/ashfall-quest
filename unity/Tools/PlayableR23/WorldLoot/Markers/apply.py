"""Apply/restore only the proven 35 chunk-role headers, never copy map bodies."""
from pathlib import Path
import argparse,hashlib,json,os,struct,datetime
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
p=argparse.ArgumentParser();p.add_argument('--restore',action='store_true');args=p.parse_args()
jobs=[]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
for stage in sorted((HERE/'StagedPayload').iterdir()):
    if not stage.is_dir():continue
    proof=json.loads((stage/'marker-removal.json').read_text())
    live=Path(proof['liveFolder'])/'geometry.bytes'
    if not live.is_absolute():live=ROOT/live
    assert live.is_relative_to(ROOT/'unity/Assets/StreamingAssets/PlayableR23')
    assert os.stat(live).st_nlink==1,'Refuse mutation of a hardlinked payload'
    before=proof['geometrySha256'] if args.restore else proof['originalGeometrySha256']
    after=proof['originalGeometrySha256'] if args.restore else proof['geometrySha256']
    current=sha(live)
    assert current in [before,after],f'Drifted working payload: {live}'
    assert sha(stage/'geometry.bytes')==proof['geometrySha256']
    assert sha(live.parent/'map.json')==proof['originalMapSha256']
    if current==after:continue
    wanted={r['chunk'] for r in proof['changes']};offsets=[]
    with live.open('rb') as f:
        magic,count=struct.unpack('<ii',f.read(8));assert magic==0x52323357
        for ci in range(count):
            at=f.tell();mid,co,vi,nv,ni=struct.unpack('<iiiii',f.read(20))
            if ci in wanted:
                assert (co,vi)==((0,0) if args.restore else (1,1));offsets.append(at+4)
            f.seek(nv*48+ni*4,1)
        assert f.tell()==live.stat().st_size and len(offsets)==len(wanted)
    jobs.append((live,offsets,after))
for live,offsets,after in jobs:
    with live.open('r+b') as f:
        for at in offsets:f.seek(at);f.write(struct.pack('<ii',1 if args.restore else 0,1 if args.restore else 0))
        f.flush();os.fsync(f.fileno())
    assert sha(live)==after,'Post-edit hash mismatch'
    print(('RESTORED' if args.restore else 'APPLIED'),len(offsets),'headers',live,after)
print('No map body, source baseline, build output, metadata or texture changed; native validation remains required.')
