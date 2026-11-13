"""Verify a newly built Unity APK without installing or launching it."""
import argparse,hashlib,json,os,pathlib,re,subprocess,zipfile
p=argparse.ArgumentParser();p.add_argument('apk',type=pathlib.Path);p.add_argument('--output',required=True,type=pathlib.Path);p.add_argument('--source-freeze',type=pathlib.Path);p.add_argument('--require-compressed-geometry',action='store_true');a=p.parse_args()
apk=a.apk.resolve();assert apk.is_file() and apk.suffix=='.apk'
sdk=pathlib.Path.home()/'.cache/ashfall-unity-sdk';tools=sdk/'build-tools/36.0.0'
env=dict(os.environ);env['JAVA_HOME']='C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK';env['PATH']=env['JAVA_HOME']+'/bin;'+env['PATH']
def run(args):
    x=subprocess.run(args,capture_output=True,text=True,errors='replace',env=env,timeout=90)
    assert x.returncode==0,(args,x.stdout,x.stderr)
    return x.stdout+x.stderr
signature=run([str(tools/'apksigner.bat'),'verify','--verbose','--print-certs',str(apk)])
badging=run([str(tools/'aapt.exe'),'dump','badging',str(apk)])
package=re.search(r"package: name='([^']+)'",badging).group(1)
label=re.search(r"application-label:'([^']+)'",badging).group(1)
minimum=int(re.search(r"sdkVersion:'(\d+)'",badging).group(1))
target=int(re.search(r"targetSdkVersion:'(\d+)'",badging).group(1))
buildReportPath=apk.with_suffix('.build-report.json');buildReport=json.loads(buildReportPath.read_text())
assert buildReport['result']=='Succeeded' and buildReport['errors']==0 and pathlib.Path(buildReport['outputPath']).resolve()==apk,'Actual clean BuildReport required'
manifest=run([str(tools/'aapt.exe'),'dump','xmltree',str(apk),'AndroidManifest.xml'])
assert 'com.oculus.intent.category.VR' in manifest,'Quest VR launch category missing'
payloadQualification={}
with zipfile.ZipFile(apk) as z:
    names=z.namelist();abis=sorted({n.split('/')[1] for n in names if n.startswith('lib/') and n.endswith('.so')})
    assert 'lib/arm64-v8a/libUnityOpenXR.so' in names,'OpenXR ARM64 library missing'
    assert any(n.endswith('/libil2cpp.so') for n in names),'IL2CPP runtime missing'
    assert any(n.endswith('PlayableR23/World/map.json') or 'PlayableR23/World/' in n for n in names),'Playable source map missing'
    geometry=[z.getinfo(n) for n in names if n.startswith('assets/PlayableR23/') and n.endswith(('.bytes','.meshbin'))]
    if a.require_compressed_geometry:
        assert geometry and all(r.compress_type==zipfile.ZIP_DEFLATED for r in geometry),'Raw geometry must be compressed in the actual APK'
        payloadQualification.update(compressedGeometryFiles=len(geometry),geometryRawBytes=sum(r.file_size for r in geometry),geometryPackedBytes=sum(r.compress_size for r in geometry))
    if a.source_freeze:
        frozenPath=a.source_freeze.resolve()/'inputs.json';frozen=json.loads(frozenPath.read_text());payload=[r for r in frozen['content'] if r['path'].startswith('Assets/StreamingAssets/PlayableR23/')]
        assert payload,'Frozen source payload required'
        for row in payload:
            name='assets/'+row['path'].removeprefix('Assets/StreamingAssets/')
            assert name in names,'Packaged source asset missing: '+name
            assert z.getinfo(name).file_size==row['bytes'] and hashlib.sha256(z.read(name)).hexdigest()==row['sha256'],'Packaged source asset mismatch: '+name
        payloadQualification.update(sourceFreeze=str(frozenPath),sourceFreezeSha256=hashlib.sha256(frozenPath.read_bytes()).hexdigest(),packagedSourceFilesVerified=len(payload),packagedSourceBytesVerified=sum(r['bytes'] for r in payload))
assert package=='org.alingold.ashfallunityquest' and label=='ASHFALL Unity Quest'
assert minimum==32 and target==36 and abis==['arm64-v8a']
receipt=dict(apk=str(apk),sha256=hashlib.file_digest(apk.open('rb'),'sha256').hexdigest(),bytes=apk.stat().st_size,package=package,label=label,minSdk=minimum,targetSdk=target,abis=abis,signatureVerified=True,signature=signature,badging=badging,installed=False,launched=False,godotPackageUntouched=True,headsetVerified=False)
receipt.update(buildReport=str(buildReportPath),buildErrors=buildReport['errors'],buildWarnings=buildReport['warnings'],permissions=re.findall(r"uses-permission: name='([^']+)'",badging),questVrCategory=True)
receipt.update(payloadQualification)
apk.with_suffix('.android-manifest.txt').write_text(manifest,encoding='utf-8')
assert not a.output.exists(),'Preserve prior APK receipt'
a.output.write_text(json.dumps(receipt,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in receipt.items() if k not in ('signature','badging')},indent=2))
