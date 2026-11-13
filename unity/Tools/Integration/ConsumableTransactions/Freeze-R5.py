from pathlib import Path
import json, hashlib
R=Path.cwd(); E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCustodyRecoveryR5'; O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCustodyRecoveryR5'
def freeze(paths,folder):
 rows=[]
 for p in sorted(set(paths)):
  b=p.read_bytes(); q=O/folder/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
  rows.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper()))
 (E/(folder+'.json')).write_text(json.dumps(rows,indent=2))
if __name__=='__main__':
 freeze(list((R/'unity/Assets/Ashfall').rglob('*.cs'))+list((R/'unity/Tools/Integration/ConsumableTransactions').glob('*.*'))+list((R/'unity/Tools/Modules/Pickups').rglob('*.*')),'before')
 reads=list((R/'unity/Documentation').glob('*.md'))+list((E.parent).glob('accepted-neutral-v*.json'))
 for d in ['phase16','phase18','ConsumableLiveBindingsR4','InventoryCheckpointR2','reviews/ConsumableLiveReviewR4']:
  reads+=list((E.parent/d).glob('*.json'))+list((E.parent/d).glob('*.md'))
 freeze(reads,'context')
 print('Frozen before and context bytes')
