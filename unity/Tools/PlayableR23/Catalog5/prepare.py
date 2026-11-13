from pathlib import Path
import json,struct,hashlib,shutil,uuid,wave
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';IDS=['1911','tec9','rfb','fal','s85']
FOLDERS={'1911':'tactical_1911','tec9':'download_tec9','rfb':'download_rfb','fal':'download_fal','s85':'download_s85_m1_099'}
SCALES={'1911':.55,'tec9':.72,'rfb':.8,'fal':1.037875,'s85':1.12}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    OUT.mkdir(exist_ok=True,parents=True)
    # Read existing complete glTF CPU converter without its shared main/writes. Adapt ONLY the
    # source node names and S85's explicit ZERO charging rest; original source remains readonly.
    path=ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py';prefix=path.read_text().split('OUT.mkdir(parents=True')[0]
    prefix=prefix.replace("if n.get('name')=='Magazine':","if n.get('name') in ['Magazine','Tactical1911Magazine']:").replace("elif name!='spr' and n.get('name') in ['Slide','Bolt']:","elif name not in ['spr','s85'] and n.get('name') in ['Slide','Bolt','Tactical1911Slide']:").replace("n.get('name')=='Slide'","n.get('name') in ['Slide','Tactical1911Slide']")
    ns={'__file__':str(path)};exec(compile(prefix,'existing_gltf_catalog5','exec'),ns);ns['SRC']=SRC;ns['OUT']=OUT
    sourceProfiles=json.loads((SRC/'assets/data/one_star_050.json').read_text())['profiles'];damage=json.loads((SRC/'assets/data/one_star0991.json').read_text())['weapons'];records=[]
    pinned={p['path']:p['sha256']for p in json.loads((ROOT/'docs/agent-runs/20261104-three-chats/world/r24-integration/R23-BASELINE.json').read_text())['actualFiles']}
    for name in IDS:
        folder=SRC/'assets/models'/FOLDERS[name];source=folder/('tactical_1911.glb'if name=='1911'else'model.glb');data=json.loads((folder/'asset-provenance.json').read_text());assert sha(source)==pinned[str(source.relative_to(SRC)).replace('\\','/')]
        result=ns['convert'](source,name);manifest=json.loads((OUT/(name+'.json')).read_text())
        manifest['parts']=[{'Tactical1911Body':'Body','Tactical1911Magazine':'Magazine','Tactical1911Slide':'Slide'}.get(x,x)for x in manifest['parts']]
        (OUT/(name+'.json')).write_text(json.dumps(manifest)+'\n')
        raw=source.read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);blob=raw[28+n:]
        movingName='Tactical1911Slide'if name=='1911'else'Bolt';node=next(n for n in g['nodes']if n.get('name')==movingName)
        points=[]
        for prim in g['meshes'][node['mesh']]['primitives']:
            a=g['accessors'][prim['attributes']['POSITION']];b=g['bufferViews'][a['bufferView']];points.append(np.ndarray((a['count'],3),'<f4',blob,offset=b.get('byteOffset',0)+a.get('byteOffset',0),strides=(b.get('byteStride',12),4)))
        points=np.concatenate(points);meshCenter=(points.min(0)+points.max(0))*.5;rest=np.array([0,.04,-.10]if name=='1911'else[0,0,0]if name=='s85'else[.065 if name=='rfb'else-.045,.064,-.10]);charging=(rest+meshCenter)*[1,1,-1]
        if name=='rfb':charging=np.array([.065,.055,.10])
        home=np.array(data['magazine_center'])*[1,1,-1];offerY=-.28 if name=='rfb'else-.278+(.1524/SCALES[name]if name=='s85'else 0);insertY=-.10 if name=='rfb'else-.18
        p=next(p for p in sourceProfiles if p['id']==name);d=damage[name];family='sniper'if name=='s85'else'pistol'if name in ['1911','tec9']else'rifle'
        rear=.135-.01/1.12 if name=='s85'else.11;frontZ=.605 if name=='s85'else.265 if name in ['1911','tec9']else.465 if name=='rfb'else.605;muzzle=np.array(data['muzzle'])*[1,1,-1];frontY=rear+(.045-rear)*(frontZ-.015)/(80+muzzle[2]-.015)
        fit=dict(id=name,modelScale=SCALES[name],reloadScale=SCALES[name],weaponGripOrigin=[0,0,0],muzzle=muzzle.tolist(),magazineHome=home.tolist(),chargingRest=(rest*[1,1,-1]).tolist(),chargingMeshCenter=(meshCenter*[1,1,-1]).tolist(),chargingGrip=charging.tolist(),offer=[0,offerY,home[2]],insert=[0,insertY,home[2]],chargingPull=.08 if name=='rfb'else.075,chargingMaximum=.09,offerRadius=.14 if name=='rfb'else.10,chargeRadius=.13 if name=='rfb'else.18,insertionTravel=.06,insertionAxis='Y',supportPoint=[0,-.025,.36],ammoFamily=family,reserve=1200 if family=='pistol'else 2400,capacity=p['magazine_per_hand'],mode=p['fire_mode'],interval=1/p['rounds_per_second'],bodyDamage=d['body'],headDamage=d['head'],maximumRange=180,sharedMagazineControls=name!='rfb',hideSeatedMagazine=name=='s85',boltAfterEveryShot=name=='s85',sourceGLBTriangles=result['triangles'],legacyProvenanceTriangles=data['source_triangles'],sourceVisibleTriangles=result['triangles'],rearSight=[0,rear,.015],frontSight=[0,frontY,frontZ],zeroDistance=80,optic='source-floating-rings'if name=='s85'else'source-common-reflex'if name=='rfb'else'actual-model-irons',opticScale=1,opticMountHeight=.165-.006/.8 if name=='rfb'else 0,slideCycleDistance=.035 if name=='1911'else 0,slideCycleSeconds=.095 if name=='1911'else 0,renderedHandOffset=[0,-.0254,0]if name=='1911'else[0,.0762,-.0508]if name=='s85'else[0,0,0],renderedHandPitchDegrees=-35 if name=='s85'else 0,notes=['Coordinates already Unity reflected once; model display scale is not baked.','All5 inherit catalog_firearm/base foregrip;1911 is not PX4 pistol.gd.','S85 uses actual original download_s85_m1_099 recipe, retains source omitted sling/debris exclusions and hides seated clip.','Rate confidence follows original one_star_050, not a new reference measurement.'])
        fit.update(primaryGrip=([0,-.057,.025]if name=='tec9'else(np.array(data.get('primary_grip',[0,-.057,.025]))*[1,1,-1]).tolist()),renderedHandOffsetPhysical=fit['renderedHandOffset'])
        if name=='s85':fit.update(heldMagazineGunBasis=True,insertionFromOfferMinY=.045,insertionFromOfferMaxY=.40,insertionFromOfferRadius=.16,heldGuide=[0,offerY+.20/SCALES[name],home[2]],sightAssemblyOffset=[0,-.0381/SCALES[name],0])
        (OUT/(name+'-fit.json')).write_text(json.dumps(fit,indent=2)+'\n')
        for original,suffix in [('asset-provenance.json','provenance.json'),('ATTRIBUTION.txt','ATTRIBUTION.txt')]:shutil.copyfile(folder/original,OUT/(name+'-'+suffix))
        records.append(dict(id=name,source=str(source.relative_to(SRC)),sha256=sha(source),legacyProvenanceOutputSha=data['output_sha256'],legacyProvenanceOutputMatches=sha(source)==data['output_sha256'],vertices=result['vertices'],triangles=result['triangles'],sourceTriangles=data['source_triangles'],profile=p,damage=d))
    routing=json.loads((SRC/'assets/audio/reference0992/manifest.json').read_text());cues=[];audio=[]
    for name in IDS:
        for action,paths in routing['weapons'][name].items():
            if action not in ['shot','mag_eject','pouch','mag_insert','bolt','dry']:continue
            files=[]
            for i,original in enumerate(paths):
                src=SRC/original.removeprefix('res://');file=f'{name}-audio-{action}-{i}.wav';shutil.copyfile(src,OUT/file);files.append(file)
                with wave.open(str(src),'rb')as w:pcm=w.readframes(w.getnframes());channels=w.getnchannels();rate=w.getframerate();frames=w.getnframes();assert w.getsampwidth()==2
                audio.append(dict(source=original,file=file,sha256=sha(src),pcmSha256=hashlib.sha256(pcm).hexdigest(),channels=channels,sampleRate=rate,frames=frames))
            cues.append(dict(weapon=name,action=action,files=files))
        for ref in ['reference097','reference099']:shutil.copyfile(SRC/f'assets/audio/{ref}/ATTRIBUTION.txt',OUT/f'{name}-audio-{ref}-ATTRIBUTION.txt')
    # Shared audio.json remains Hands-owned. Only this additive fragment is emitted.
    (HERE/'catalog5-audio-cues.json').write_text(json.dumps(dict(cues=cues),indent=2)+'\n')
    sourceFiles=['scripts/catalog_firearm.gd','scripts/tactical_1911_model.gd','scripts/downloaded_firearm_model.gd','scripts/weapon.gd','scripts/weapon_rack.gd','scripts/model_variants.gd','scripts/model_scale_084.gd','scripts/playtest_fit_088.gd','scripts/authored_ring_dot_sight.gd','scripts/common_reflex_090.gd','scripts/player.gd','scripts/grip_hand_086.gd','scripts/reticle_bounce_071.gd','assets/data/recoil_observed_067.json','assets/data/recoil_endpoint_experiment_071.json','assets/data/reference_062.json','assets/data/one_star_050.json','assets/data/one_star0991.json','assets/audio/reference0992/manifest.json']
    record=dict(originalSource=str(SRC),geometry=records,audio=audio,sourceInputs={p:sha(SRC/p)for p in sourceFiles},conversion='Original current GLBs without decimation, source node homes once, inverse-transpose normals, Z+index winding+UV reflection once; immutable source and shared runtime untouched.')
    (HERE/'source-port.json').write_text(json.dumps(record,indent=2)+'\n')
    for p in OUT.iterdir():
        if p.name.startswith(tuple(IDS))and p.is_file()and p.suffix!='.meta':
            meta=Path(str(p)+'.meta')
            if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-catalog5:'+p.name).hex+'\n')
    total=sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()and p.name.startswith(tuple(IDS)))
    assert total<100000000,total
    print(records);print('Catalog5 payload bytes',total,'audio clips',len(audio),'shared audio untouched')
if __name__=='__main__':main()



