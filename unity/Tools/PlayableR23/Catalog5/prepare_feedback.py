from pathlib import Path
import json,hashlib,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
def flatten(rows):return [x for r in rows for x in r]
def literal(x):return format(x,'.10g')+('f'if '.'in format(x,'.10g')or'e'in format(x,'.10g')else'f')
a=json.loads((SRC/'assets/data/recoil_endpoint_experiment_071.json').read_text());b=json.loads((SRC/'assets/data/recoil_observed_067.json').read_text())
values={'RfbOne':flatten(a['profiles']['rfb']['one']['samples_pitch_yaw_deg']),'Tec9One':flatten(b['profiles']['tec9']['one']['samples']),'Tec9Two':flatten(b['profiles']['tec9']['two']['samples'])}
code='// Exact original R23 source trace data; source uncertainty retained in Tools/Catalog5/source-port.json.\nnamespace Ashfall.Integration.Playable.HandsWeapons\n{\n public sealed partial class SourceCatalog5Feedback\n {\n'
for name,data in values.items():code+='  static readonly float[] '+name+'={'+','.join(literal(x)for x in data)+'};\n'
code+=' }\n}\n';(OUT/'SourceCatalog5FeedbackData.cs').write_text(code)
for p in OUT.glob('SourceCatalog5*.cs'):
 meta=Path(str(p)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-catalog5:'+p.name).hex+'\n')
print('Prepared exact source RFB/TEC9 trace arrays:',{k:len(v)for k,v in values.items()})
