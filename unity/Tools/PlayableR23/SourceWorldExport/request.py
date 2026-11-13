"""Create concrete bounded WORLD commands. Never launches Godot or writes source."""
from pathlib import Path
import argparse,datetime,hashlib,json
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
BASELINE=ROOT/'docs/agent-runs/20261104-three-chats/world/r24-integration/R23-BASELINE.json'
CRITICAL=['project.godot','scripts/world.gd','scripts/sandbox_map_091.gd','scripts/field_item_models.gd','scripts/light_fixture.gd','assets/materials/alpine_terrain.gdshader']
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def inside(path,root):
    try:Path(path).resolve().relative_to(Path(root).resolve());return True
    except ValueError:return False
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--name',default='request-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));args=parser.parse_args()
    if not args.name or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_' for c in args.name):raise ValueError('Run name must be a simple unique leaf name')
    baseline=json.loads(BASELINE.read_text());source=Path(baseline['base']).resolve();pinned={x['path']:x for x in baseline['actualFiles']}
    critical={name:pinned[name]['sha256'] for name in CRITICAL}
    for name,digest in critical.items():
        if sha(source/name)!=digest:raise ValueError('Original baseline mismatch '+name)
        if name!='project.godot' and sha(ROOT/'quest-r24/source'/name)!=digest:raise ValueError('R24 builder/resource snapshot mismatch '+name)
    run=(HERE/'runs'/args.name).resolve()
    if not inside(run,HERE/'runs') or run.exists():raise ValueError('Choose a fresh owned run directory')
    run.mkdir(parents=True)
    digest=hashlib.sha256('\n'.join(x['path']+'\0'+x['sha256'] for x in baseline['actualFiles']).encode()).hexdigest()
    common={'version':1,'referenceRoot':str(run/'reference'),'originalRoot':str(source),'criticalSourceHashes':critical,'baselineSha256':sha(BASELINE),'sourceTreeDigest':digest,'timeoutSeconds':180,'maxNodes':200000,'maxPayloadBytes':2147483648,'maxTextureBytes':2147483648}
    for map_id in ['valley','range']:(run/(map_id+'-request.json')).write_text(json.dumps(dict(common,map=map_id,output=str(run/map_id)),indent=2))
    plan={'version':1,'run':str(run),'reference':str(run/'reference'),'original':str(source),'baseline':str(BASELINE),'baselineSha256':sha(BASELINE),'sourceTreeDigest':digest,'sourceFiles':len(pinned),'sourceBytes':sum(x['bytes'] for x in pinned.values()),'exporter':str(HERE/'export_source_world.gd'),'exporterSha256':sha(HERE/'export_source_world.gd'),'maxCopySeconds':900,'importTimeoutSeconds':900,'exportTimeoutSeconds':210,'criticalSourceHashes':critical}
    plan_path=run/'plan.json';plan_path.write_text(json.dumps(plan,indent=2))
    # Each engine phase is an explicit future WORLD command; this tool executes none.
    commands=["# WORLD: reserve the single engine slot before the engine phases.","$sourceExportGodot = 'C:/path/to/actual-Godot-4.7-console.exe'",f"python '{HERE/'stage_reference.py'}' --plan '{plan_path}'"]
    for phase in ['import','valley','range']:commands.append(f"python '{HERE/'run_phase.py'}' --plan '{plan_path}' --godot $sourceExportGodot --phase {phase}")
    (run/'commands.ps1').write_text('\n'.join(commands)+'\n')
    print(json.dumps({'plan':str(plan_path),'commands':str(run/'commands.ps1'),'sourceFiles':len(pinned),'sourceBytes':plan['sourceBytes'],'engineStarted':False,'referenceCopied':False},indent=2))
if __name__=='__main__':main()
