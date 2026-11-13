"""Bounded sequential conversion + actual-game validation, resumable per map."""
from pathlib import Path
import json,subprocess,os,time,hashlib,shutil,ctypes,sys
from prepare1003 import prepare
R=Path(__file__).resolve().parents[3];P=R/'candidate';A=P/'local_archive';E=R/'evidence';M=E/'matrix';M.mkdir(exist_ok=True)
ENGINE=R.parent/'standalone088-playtest/engine/Godot_v4.7.2-stable_win64.exe'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def valid_cache(manifest):
 try:
  path=A/'maps'/manifest['slug']/'converted/map.scn'
  value=sha(path)+'\n'
  for row in manifest['dependencies']:
   relative=row['path'];assert relative.startswith('res://local_archive/') and '..' not in Path(relative).parts
   value+=relative+':'+sha(P/relative.removeprefix('res://'))+'\n'
  return hashlib.sha256(value.encode()).hexdigest()==manifest['local_cache_hash']
 except Exception:return False
def save(path,data):
 tmp=path.with_suffix('.tmp');tmp.write_text(json.dumps(data,indent=2)+'\n');os.replace(tmp,path)
downloads=json.loads((A/'downloads.json').read_text());rows=[{'id':r['map_id'],'slug':r['slug'],'title':r['title'],'creator':r['creator'],'official':r['official'],'status':'pending','bytes':r.get('bytes',0),'error':r.get('error','')} for r in downloads['rows']]
save(E/'MATRIX-PROCESS.json',{'pid':os.getpid(),'started':time.time(),'limit':sys.argv[1] if len(sys.argv)>1 else 'all','scope':'owned sequential XR-off archive conversion/validation'})
def catalog():
 fingerprint='\n'.join(r['id']+':'+str(r.get('cache_fingerprint','missing')) for r in rows if r['status']=='ready')
 save(A/'catalog.json',{'schema':1003,'maps':rows,'content_id':hashlib.sha256(fingerprint.encode()).hexdigest(),'local_only':True,'license':'UNKNOWN REUSE RIGHTS — NOT DISTRIBUTABLE'})
class PMC(ctypes.Structure):
 _fields_=[('cb',ctypes.c_ulong),('PageFaultCount',ctypes.c_ulong),('PeakWorkingSetSize',ctypes.c_size_t),('WorkingSetSize',ctypes.c_size_t),('QuotaPeakPagedPoolUsage',ctypes.c_size_t),('QuotaPagedPoolUsage',ctypes.c_size_t),('QuotaPeakNonPagedPoolUsage',ctypes.c_size_t),('QuotaNonPagedPoolUsage',ctypes.c_size_t),('PagefileUsage',ctypes.c_size_t),('PeakPagefileUsage',ctypes.c_size_t)]
def child(slug,stage,script,args,timeout):
 profile=E/'profiles'/slug;profile.mkdir(parents=True,exist_ok=True);env=os.environ.copy();env['APPDATA']=str(profile/'Roaming');env['LOCALAPPDATA']=str(profile/'Local');Path(env['APPDATA']).mkdir(exist_ok=True);Path(env['LOCALAPPDATA']).mkdir(exist_ok=True)
 log=M/f'{slug}-{stage}.txt';cmd=[str(ENGINE),'--headless','--path',str(P),'--xr-mode','off','--audio-driver','Dummy','--log-file',str(M/f'{slug}-{stage}.log'),'--script',script,'--',*args,'--desktop','--extended','--playtest-catalog','--archive-validation']
 started=time.time();peak=0;reason=''
 with log.open('w',encoding='utf-8') as f:
  proc=subprocess.Popen(cmd,cwd=P,env=env,stdout=f,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
  while proc.poll() is None:
   counters=PMC();counters.cb=ctypes.sizeof(counters)
   if ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.c_void_p(int(proc._handle)),ctypes.byref(counters),ctypes.sizeof(counters)):peak=max(peak,counters.PeakWorkingSetSize)
   if time.time()-started>timeout:reason='owned child exceeded '+str(timeout)+' seconds';proc.kill();break
   if peak>8*1024**3:reason='owned child exceeded 8 GiB working-set cap';proc.kill();break
   time.sleep(.1)
  proc.wait()
 text=log.read_text(errors='replace');ok=proc.returncode==0 and not reason and not any(s in text for s in ['SCRIPT ERROR:','\nERROR:','FAIL '])
 return {'passed':ok,'exit':proc.returncode,'reason':reason,'seconds':round(time.time()-started,3),'peakWorkingSet':peak,'args':cmd,'pid':proc.pid,'log':str(log),'tail':text[-3000:] if not ok else ''}
codehash=hashlib.sha256(''.join(sha(P/p) for p in ['tools/convert_archive1003.gd','scripts/archive_map1003.gd','tests/archive_validate1003.gd']).encode()).hexdigest()
limit=int(sys.argv[1]) if len(sys.argv)>1 and sys.argv[1].isdigit() else len(rows)
for i,row in enumerate(rows):
 if (R/'STOP-MATRIX').exists():print('Checkpoint stop requested; current-map receipts preserved',flush=True);break
 previous=M/(row['slug']+'.json')
 old=None
 if previous.exists():
  old=json.loads(previous.read_text())
  if old.get('passed') and old.get('codeHash')==codehash:
   row.update(status='ready',cache_fingerprint=old['manifest']['cache_fingerprint'],facts=old['validation']['facts']);continue
 catalog()
 if i>=limit:break
 slug=row['slug'];receipt={'slug':slug,'codeHash':codehash,'passed':False}
 try:
  if downloads['rows'][i]['status']!='downloaded':raise RuntimeError('download failed: '+downloads['rows'][i].get('error',''))
  if shutil.disk_usage(A).free<50*1024**3+int(row['bytes'])*6:raise RuntimeError('50 GiB disk reserve would be consumed')
  receipt['preparation']=prepare(slug)
  if '--reuse-conversion' in sys.argv and old and old.get('conversion',{}).get('passed') and valid_cache(old.get('manifest',{})):
   conversion={**old['conversion'],'reusedVerifiedCache':True};receipt['conversion']=conversion
  else:
   conversion=child(slug,'convert','res://tools/convert_archive1003.gd',[slug],150);receipt['conversion']=conversion
  if not conversion['passed']:raise RuntimeError('conversion: '+conversion['reason']+' '+conversion['tail'][-1200:])
  manifest=json.loads((A/'maps'/slug/'converted/manifest.json').read_text());receipt['manifest']=manifest;row.update(status='validating',cache_fingerprint=manifest['cache_fingerprint']);catalog()
  output=M/(slug+'-validation.json');validation=child(slug,'validate','res://tests/archive_validate1003.gd',[slug,str(output)],120);receipt['validationProcess']=validation
  if output.exists():receipt['validation']=json.loads(output.read_text())
  if not validation['passed'] or not receipt.get('validation',{}).get('passed'):raise RuntimeError('validation: '+validation['reason']+' '+validation['tail'][-1400:])
  receipt['passed']=True;row.update(status='ready',facts=receipt['validation']['facts'],error='')
 except Exception as exc:receipt['error']=repr(exc);row.update(status='blocked',error=str(exc))
 save(previous,receipt);catalog();print(f"{i+1}/{len(rows)} {slug}: {row['status']} {row.get('error','')[:200]}",flush=True)
 if not receipt['passed'] and 'Parse Error:' in row['error']:print('SYSTEMATIC_SCRIPT_ERROR_STOP',flush=True);break
catalog();save(E/'MATRIX-1003.json',{'completed':sum(r['status'] in ['ready','blocked'] for r in rows),'total':len(rows),'ready':sum(r['status']=='ready' for r in rows),'blocked':sum(r['status']=='blocked' for r in rows),'codeHash':codehash,'rows':rows});print('MATRIX_SUMMARY',sum(r['status']=='ready' for r in rows),'ready',sum(r['status']=='blocked' for r in rows),'blocked',flush=True)
