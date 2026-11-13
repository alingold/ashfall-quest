"""Preparation checks only: no source copy, engine process or generated terrain."""
from pathlib import Path
import copy,hashlib,json,os,struct,sys,tempfile
from request import HERE,ROOT,BASELINE,CRITICAL,sha
from validate_export import validate
checks=0
def check(ok,message):
    global checks
    assert ok,message
    checks+=1
def reject(call,message):
    try:call()
    except ValueError:check(True,message);return
    raise AssertionError(message)
for file in HERE.glob('*.py'):compile(file.read_text(),str(file),'exec');check(True,'Python syntax '+file.name)
parser_path=Path(tempfile.gettempdir())/'ashfall-sourceworld-gdparse'
if parser_path.exists():sys.path.insert(0,str(parser_path))
from gdtoolkit.parser import parser
script=(HERE/'export_source_world.gd').read_text();parser.parse(script);check(True,'GDScript4.5 static grammar parses; no Godot execution')
baseline=json.loads(BASELINE.read_text());source=Path(baseline['base']);rows={row['path']:row for row in baseline['actualFiles']}
check(len(rows)==5586 and sum(row['bytes'] for row in rows.values())==3199689639,'Pinned actual original5586 files and source byte count')
for relative in CRITICAL:check(sha(source/relative)==rows[relative]['sha256'],'Pinned original critical source '+relative)
for relative in CRITICAL:
    if relative!='project.godot':check(sha(ROOT/'quest-r24/source'/relative)==rows[relative]['sha256'],'Unchanged R24 source builder/resource '+relative)
for relative in ['scripts/world.gd','scripts/sandbox_map_091.gd']:
    parser.parse((source/relative).read_text());check(True,'Actual source builder grammar '+relative)
prepared=HERE/'runs/prepared-r23-valley-range';plan=json.loads((prepared/'plan.json').read_text())
check(plan['exporterSha256']==sha(HERE/'export_source_world.gd') and plan['baselineSha256']==sha(BASELINE),'Concrete prepared request matches final exporter/baseline bytes')
check(not Path(plan['reference']).exists() and not (prepared/'valley').exists() and not (prepared/'range').exists(),'Preparation never copied source or generated actual map output')
for map_id in ['valley','range']:
    request=json.loads((prepared/(map_id+'-request.json')).read_text());check(request['map']==map_id and request['maxTextureBytes']==2147483648 and request['timeoutSeconds']==180,'Prepared bounded actual map request '+map_id)
for required in ['world.build()','world.enter(context)','mesh.surface_get_arrays(index)','RenderingServer.mesh_get_surface','node.is_shape_owner_disabled(owner)','resource.get_faces()','texture.get_image()','image.get_data()','await _wait_for_generated_images()','_resource(node.get_active_material(surface))']:
    check(required in script,'Genuine source/capture API retained '+required)
check('FastNoiseLite.new' not in script and 'get_noise_2d' not in script,'No substitute terrain generator')
# Exercise the output reader with an explicitly synthetic byte-span fixture. This
# fixture is deleted after the check and is never presented as a real map export.
parent=HERE/'_checks';parent.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(prefix='validator-only-',dir=parent) as temporary:
    folder=Path(temporary).resolve();check(folder.is_relative_to(parent.resolve()),'Bounded owned temporary fixture path')
    values=struct.pack('<9f3i',0,0,0,1,0,0,0,0,1,0,1,2);(folder/'payload.bin').write_bytes(values)
    blob=lambda offset,count,components,variant:dict(blob='payload.bin',variantType=variant,count=count,components=components,scalarBytes=4,offset=offset,bytes=count*components*4)
    slots=[None]*13;slots[0]=blob(0,3,3,36);slots[12]=blob(36,3,1,30)
    transform=lambda at:[1,0,0,0,1,0,0,0,1]+list(at)
    centers=[(0,-.2,600),(0,8,562),(-45,4,600),(45,4,600)]
    resource=dict(id='shape',**{'class':'BoxShape3D','properties':{}})
    d=dict(version=1,complete=True,map='range',byteOrder='little',spawn=[0,.2,608],bytes=len(values),payloadSha256=sha(folder/'payload.bin'),meshes=[dict(id='fixture-only',surfaces=[dict(arrays=slots,primitive=3)])],meshInstances=[dict(mesh='fixture-only',globalTransform=transform(at)) for at in centers],multiMeshes=[],staticBodies=[dict(shapes=[dict(shape={'resourceRef':'shape'},globalTransform=transform(at),ownerTransform=transform((0,0,0)))]) for at in centers],resources=[resource],unsupported=[],fixtureOnly=True)
    def write(document):
        (folder/'world.json').write_text(json.dumps(document));(folder/'complete.json').write_text(json.dumps(dict(complete=True,worldSha256=sha(folder/'world.json'),payloadSha256=document['payloadSha256'])))
    write(d);result=validate(folder);check(result['uniqueMeshTriangles']==1 and result['staticBodies']==4,'Positive reader byte-span/source-placement fixture')
    bad=copy.deepcopy(d);bad['meshes'][0]['surfaces'][0]['arrays'][0]['bytes']+=4;write(bad);reject(lambda:validate(folder),'Reject inconsistent stride instead of accepting fabricated payload')
    bad=copy.deepcopy(d);bad['meshes'][0]['surfaces'][0]['arrays'][12]['offset']=len(values);write(bad);reject(lambda:validate(folder),'Reject index span outside real bytes')
    bad=copy.deepcopy(d);bad['spawn']=[0,.2,-608];write(bad);reject(lambda:validate(folder),'Reject reflected coordinates at source export stage')
    bad=copy.deepcopy(d);bad['staticBodies']=[];write(bad);reject(lambda:validate(folder),'Reject missing actual collision export')
    write(d);(folder/'payload.bin').write_bytes(values[:-1]+b'\xff');reject(lambda:validate(folder),'Reject payload integrity mismatch')
report=dict(passed=checks,scope='CPU preparation/static grammar and validator unit fixtures only',sourceCopied=False,engineStarted=False,terrainGenerated=False,originalSourceModified=False,assetsModified=False,baselineFiles=len(rows),baselineBytes=sum(row['bytes'] for row in rows.values()))
(HERE/'checks.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
