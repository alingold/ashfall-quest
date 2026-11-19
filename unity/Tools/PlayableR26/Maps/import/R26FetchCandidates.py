"""Fetch only candidates first listed after the 2026-11-05 Unity import.

Cache every response. Keep one HTTP transfer active and leave a gap between
requests. Fetch the larger GLB only after known texture dependencies pass.
"""
from pathlib import Path
import json
import subprocess
import time

import R26ArchiveCandidate as c

TMP=c.TMP

def fetch(slug, artifact):
    saved=TMP/f'{slug}-{artifact}'
    if saved.is_file() and saved.stat().st_size:
        return saved
    temporary=saved.with_suffix(saved.suffix+'.part')
    cmd=['curl','-sSL','--fail','--max-time','120',
         '-A','ASHFALL map archive compatibility review (single requests)',
         '-D',str(saved)+'.headers','-w','%{http_code} %{size_download}',
         c.ARCHIVE+f'maps/{slug}/{artifact}','-o',str(temporary)]
    r=subprocess.run(cmd,text=True,capture_output=True)
    time.sleep(2)
    http=int(r.stdout.split()[0]) if r.stdout else 0
    if http==429 or http>=500:
        raise SystemExit(f'Archive asked to slow down ({http}); stop and inspect before resuming')
    if r.returncode:
        raise ValueError(f'{artifact}: HTTP {http}, {r.stderr.strip()}')
    temporary.rename(saved)
    return saved

survey=c.read(TMP/'R26MapsiteSurvey.json')
rows=[r for r in survey['new'] if r['first']>survey['originalUnityPayloadImport']]
assert len(rows)==57
report=TMP/'R26ArchiveBatchResults.jsonl'
previous={json.loads(line)['slug'] for line in report.read_text().splitlines()} if report.is_file() else set()
table=c.get_texture_table()
with report.open('a') as log:
    for row in rows:
        slug=row['id']
        if slug in previous:
            continue
        try:
            meta=c.read(fetch(slug,'map.json'))
            assert meta['creator']==row['creator']
            missing=sorted({uri for mat in meta['materials'].values()
                            for kind,uri in mat.items() if kind in ('c','n')
                            and uri.startswith('tex/') and uri[4:] not in table})
            if missing:
                raise ValueError('Missing ASHFALL replacement texture slots: '+', '.join(missing))
            fetch(slug,'map.glb')
            artifact=TMP/'stage'/slug/'geometry.bytes'
            trial=TMP/'trials'/slug/'converted-result.json'
            if not trial.exists() or not artifact.exists():
                run=subprocess.run(['python',str(TMP/'R26ArchiveCandidate.py'),slug],text=True,capture_output=True)
                (TMP/f'{slug}.batch.log').write_text(run.stdout+run.stderr)
                if run.returncode:
                    raise ValueError(run.stderr.splitlines()[0] if run.stderr else 'converter exited '+str(run.returncode))
            result=c.read(trial)
            record=dict(slug=slug,result='converted',chunks=result['chunks'],bytes=result['payloadBytes'])
        except (ValueError,AssertionError,KeyError) as error:
            record=dict(slug=slug,result='skipped',reason=str(error))
        print(json.dumps(record,ensure_ascii=False),flush=True)
        log.write(json.dumps(record,ensure_ascii=False)+'\n')
        log.flush()
