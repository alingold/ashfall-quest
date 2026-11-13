"""Publish checked actual outfits only after root releases its Assets freeze."""
from pathlib import Path
import argparse,hashlib,json,re,shutil
import prepare as p
HERE=Path(__file__).resolve().parent;LIVE=p.ROOT/'unity/Assets/StreamingAssets/PlayableR23/Appearance'
RUNTIME=p.ROOT/'unity/Assets/Ashfall/Integration/Playable/Appearance/R23PlayerAppearance.cs'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main(include_r6):
 receipts=[(p.OUT,json.loads((HERE/'full-r5-receipt.json').read_text())),(p.OUT,json.loads((HERE/'receipt.json').read_text()))]
 if include_r6:
  r6=HERE.parent/'R6';receipts.append((Path('D:/ashfall-unity-appearance-staging/r6-v001'),json.loads((r6/'receipt.json').read_text())))
 styles={row['sourceIndex']:row for _,receipt in receipts for row in receipt['styles']};assert set(styles)==set(range(8,115 if include_r6 else 85))
 original={path.name:sha(path)for path in LIVE.iterdir()if path.is_file()};copied=0;total=0
 for staged,_ in receipts:
  for source in staged.iterdir():
   if not source.is_file():continue
   destination=LIVE/source.name
   if destination.exists():
    if source.name.endswith('.meta'):
     assert (staged/source.name[:-5]).read_bytes()==(LIVE/source.name[:-5]).read_bytes(),'Meta collision without identical shared content';continue
    assert destination.read_bytes()==source.read_bytes(),'Refuse changing existing outfit file '+source.name
   else:shutil.copyfile(source,destination);copied+=1;total+=source.stat().st_size
 for name,digest in original.items():assert sha(LIVE/name)==digest
 # Stable source indexes and original eight seed declarations stay unchanged.
 lines=['        static readonly R23AppearanceStyle[] r5Styles={']
 for index,row in sorted(styles.items()):
  assert (LIVE/(row['id']+'.json')).is_file()and(LIVE/(row['id']+'.meshbin')).is_file()
  lines.append('            new R23AppearanceStyle('+json.dumps(row['id'])+','+json.dumps(row['label'],ensure_ascii=False)+','+str(index)+'),')
 lines.append('        };');replacement='\n'.join(lines)
 old=RUNTIME.read_bytes();text=old.decode('utf8');pattern=r'        static readonly R23AppearanceStyle\[\] r5Styles=\{.*?\};'
 text,count=re.subn(pattern,lambda match:replacement,text,count=1,flags=re.S);assert count==1
 RUNTIME.write_bytes(text.encode('utf8'))
 report=dict(styles=8+len(styles),newCopiedFiles=copied,newCopiedBytes=total,existingFilesUnchanged=len(original),sourceIndices=sorted(styles),liveFolder=str(LIVE),engineRun=False)
 (HERE/'publication.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--include-r6',action='store_true');args=parser.parse_args();main(args.include_r6)
