"""Add exact original archive environment/template descriptors; no geometry changes."""
import prepare as p
def main():
    for f in sorted((p.HERE/'results').glob('*.json')):
        row=p.load(f)
        if row.get('refused'):continue
        slug=row['slug'];assert slug not in p.EXCLUDED
        original=p.LIB/'maps'/slug/'converted/manifest.json';m=p.load(original)
        for folder in [p.STAGE/slug]+([p.LIVE/slug]if row.get('published')else[]):
            path=folder/'source-semantics.json';value=p.load(path)
            if 'metadata'in value:continue
            receipt=p.load(folder/'provenance.json');assert p.sha(original)==receipt['archiveInputs'][str(original)]
            value['metadata']={k:m.get(k)for k in ['map_id','title','creator','template','center','recorded_spawns','bounds_min','bounds_max','solid_min','solid_max','source','license']}
            moon='Moon'in m.get('template','')
            value['sourceEnvironment']=dict(background='101a2b'if moon else'9db6c6',ambientColor='c5d5e2',ambientEnergy=.6 if moon else .8,sunEulerGodot=[-53,-28,0],sunEnergy=1,shadows=False,toneMapper='Filmic',source='scripts/archive_map1003.gd:70-77')
            p.write(path,value)
    print('Exact original template/metadata/environment descriptors complete; geometry unchanged.')
if __name__=='__main__':main()
