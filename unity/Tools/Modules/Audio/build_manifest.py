"""Convert centrally accepted inventory rows to the runtime TSV. Never grants approval.

Receipt schema: {inventory_sha256, approval: 'accepted:<review-id>', version,
license, rows: [{tree: 0, route: 'generic/glide', variant: 0}, ...]}.
Coordinator must supply a reviewed immutable receipt; test receipts are not asset acceptance.
"""
import argparse
import hashlib
import json
import pathlib
import wave

def build(inventory, receipt):
    raw=inventory.read_bytes()
    approval=json.loads(receipt.read_text(encoding='utf-8-sig'))
    if hashlib.sha256(raw).hexdigest().upper()!=approval['inventory_sha256'].upper():
        raise ValueError('Receipt inventory hash mismatch')
    if not approval['approval'].startswith('accepted:') or len(approval['approval'])<=9:
        raise ValueError('Acceptance receipt required')
    data=json.loads(raw)
    selected=[]
    for key in approval['rows']:
        candidates=[r for r in data['trees'][key['tree']]['assets'] if r['route']==key['route'] and r['variant']==key['variant']]
        if len(candidates)!=1:
            raise ValueError('Ambiguous or absent inventory row')
        row=candidates[0]
        path=pathlib.Path(row['source'])
        if not path.is_absolute():
            raise ValueError('Fully qualified asset path required')
        path=path.resolve(strict=True)
        if 'error' in row or hashlib.sha256(path.read_bytes()).hexdigest().upper()!=row['sha256']:
            raise ValueError('Missing or changed source audio')
        with wave.open(str(path),'rb') as w:
            if w.getsampwidth()!=2 or w.getnchannels() not in (1,2) or not 8000<=w.getframerate()<=192000 or not w.getnframes():
                raise ValueError('PCM16 mono/stereo required')
        selected.append(dict(row,source=str(path)))
    selected.sort(key=lambda r:(r['route'],r['variant']))
    lines=['ASHFALL-AUDIO/1']
    counts={}
    for row in selected:
        n=counts.get(row['route'],0)
        if row['variant']!=n:
            raise ValueError('Duplicate/gapped variants')
        counts[row['route']]=n+1
        fields=[row['route'],str(n),row['source'],row['sha256'],approval['version'],approval['approval'],approval['license'],row['bus'],'1' if row['loop'] else '0']
        if any(not f or any(c in f for c in '\t\r\n') for f in fields):
            raise ValueError('Empty or multiline manifest field')
        lines.append('\t'.join(fields))
    if len(lines)==1:
        raise ValueError('Empty selection')
    return '\n'.join(lines)+'\n'

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('inventory',type=pathlib.Path);p.add_argument('receipt',type=pathlib.Path);p.add_argument('output',type=pathlib.Path)
    a=p.parse_args();a.output.write_text(build(a.inventory,a.receipt),encoding='utf-8')
