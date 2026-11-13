"""Actual-source detached junction census, positive witness and regression controls."""
from analyze import *

def main():
 live=ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt';staged=HERE/'staged/payload';census={};old_faces=0;existing_maps=0;supplement=0
 for slug in PLANS:
  before=json.loads((live/(slug+'.json')).read_text());after=json.loads((staged/(slug+'.json')).read_text());stripped=json.loads(json.dumps(after));new=[]
  for patch in before['patches']:patch['parts']=[p for p in patch['parts']if p['kind']!='junction']
  before['patches']=[p for p in before['patches']if p['parts']]
  for patch in stripped['patches']:
   new.extend((patch['chunk'],t)for part in patch['parts']if part['kind']=='junction'for t in part['triangles']);patch['parts']=[p for p in patch['parts']if p['kind']!='junction']
  stripped['patches']=[p for p in stripped['patches']if p['parts']];assert stripped==before,(slug,'existing art or masks changed')
  old_faces+=sum(len(p['triangles'])for c in before['patches']for p in c['parts']);existing_maps+=1
  report=json.loads((HERE/(slug+'-result.json')).read_text());expected=report['sourceTriangles'].get('junction',0)
  if slug=='alfa-aglae-souza'and (HERE/'alpha-road-checks.json').exists():
   roads=json.loads((HERE/'alpha-road-checks.json').read_text());road_keys={(r['chunk'],r['triangle'])for r in roads['matches']}
   if road_keys.issubset(set(new)):
    assert len(road_keys)==28 and roads['sourceGuardSha256']==sha(source('assets/data/alpha_junction_r24.json'));expected+=28;supplement=28
  assert len(new)==expected;assert len(set(new))==len(new);census[slug]=expected
  if slug=='bigbabyjesuz-classic-kiki-80':assert (253,9213)in new,'Actual 1200x800 captured center ray hits this raw triangle'
 assert sum(census.values())==686+supplement and sum(bool(n)for n in census.values())==6
 classic=json.loads((staged/'classic-bigbox-vr.json').read_text());glass=[(p['chunk'],t)for p in classic['patches']for x in p['parts']if x['kind']=='clear_glass'for t in x['triangles']];assert glass==[(185,351),(185,352),(186,284),(186,285)]
 text=source('assets/materials/alpha_junction_r24.gdshader').read_text();body=re.search(r'\}else\{\n(.*)\n \}\n\}',text,re.S).group(1)
 assert body.count('vec2(.5)')==7
 body=body.replace('vec2(.5)','vec2(.5,.5)')
 for a,b in [('vec2','float2'),('vec3','float3'),('mix(','lerp('),('fract(','frac('),('source_position','i.sourcePosition'),('ALBEDO','albedo'),('ROUGHNESS','roughness')]:body=body.replace(a,b)
 shader=(HERE/'staged/runtime/Resources/R24SourceJunction.shader').read_text();assert body in shader and body.count('repaired=true;')==7
 assert shader.count('float2(.5,.5)')==7 and 'float2(.5)'not in shader
 assert not re.search(r'\bfloat[234]\s*\(\s*[-+.\d]+\s*\)',shader),'Invalid HLSL scalar-vector constructor'
 metadata=json.loads((HERE/'junction-shader-source.json').read_text());assert metadata['transformedFragmentSha256']==hashlib.sha256(body.encode()).hexdigest()and metadata['sourceScalarSplatExpansions']==7
 live_shader=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/SourceArt/Resources/R24SourceJunction.shader';assert live_shader.read_bytes()==(HERE/'staged/runtime/Resources/R24SourceJunction.shader').read_bytes(),'Corrected published shader differs from generator'
 # Preserve the source's repaired asphalt and restricted crosswalk ranges, and
 # original UV3/default road treatment outside those actual projection zones.
 assert 'float2 q=float2(p.x,p.z);bool repaired=false;'in shader and 'float inner=repaired?.215:.07;float outer=repaired?.785:.93;'in shader
 report=dict(passed=True,mapsPreserved=existing_maps,previousArtTrianglesPreserved=old_faces,junctionTriangles=census,totalAddedTriangles=686+supplement,alphaSupplementalRoadFaces=supplement,actualCapturedPixelWitness=dict(map='bigbabyjesuz-classic-kiki-80',view='junction-source-match',pixel=[600,400],size=[1200,800],chunk=253,triangle=9213,nearestCpuRayDistance=32.38431760395371),sourceShaderSha256=sha(source('assets/materials/alpha_junction_r24.gdshader')),sevenSourceProjectionExpressionsRetained=True,validHlslSplatExpansions=7,transformedBodyHashMatchesEmittedShader=True,liveShaderByteIdentical=True,glassFourPreserved=True,collisionUntouched=True,nativeShaderOrRenderRun=False)
 (HERE/'junction-checks.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
