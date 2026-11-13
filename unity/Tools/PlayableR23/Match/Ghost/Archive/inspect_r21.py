from inspect_source import Resource,S
import json,hashlib
rows=json.loads((S/'assets/data/hatch_backing_r21.json').read_text())['maps']
for slug,row in list(rows.items())[:1]:
 p=S/row['path'].replace('res://','');raw=p.read_bytes();assert hashlib.sha256(raw).hexdigest()==row['sha256'];r=Resource(raw)
 print('root props',r.resources[-1]['props'].keys())
 for k,v in r.resources[-1]['props'].items():
  if isinstance(v,dict):print(k,list(v))
 plans=r.resources[-1]['props'].get('metadata/plans');print('plans',plans and list(plans));print('attach',str(plans.get('attachments'))[:500]if plans else'none')
