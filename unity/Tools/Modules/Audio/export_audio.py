"""Read-only source reconciliation/export. Outputs references only; cannot grant approval."""
import argparse, hashlib, json, pathlib, re, wave

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def export(root):
    script = root / 'scripts/audio_bank.gd'
    text = script.read_text(encoding='utf-8-sig')
    counts = json.loads(re.search(r'const VARIANTS := (\{.*\})', text)[1])
    loops = json.loads(re.search(r'const LOOPS := (\[.*\])', text)[1])
    manifest_path = root / 'assets/audio/reference0992/manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    rows = []
    def add(route, paths, loop):
        kind = route.split('/')[-1]
        bus = 'Music' if kind == 'game_start_music' else ('Ambience' if kind in ('wind','turbine','creak') else 'SFX')
        for variant, path in enumerate(paths):
            p = root / path.removeprefix('res://')
            row = dict(route=route, variant=variant, source=str(p.resolve()), bus=bus, loop=loop,
                       approval='unreviewed-reference', version='source-0992', license='requires-central-provenance-review')
            if p.exists():
                row['sha256'] = digest(p)
                try:
                    with wave.open(str(p), 'rb') as w:
                        row.update(channels=w.getnchannels(), rate=w.getframerate(), frames=w.getnframes(), bits=w.getsampwidth()*8)
                except (wave.Error, EOFError) as e:
                    row['error'] = str(e)
            else:
                row['error'] = 'missing source WAV; no synthesis substituted'
            rows.append(row)
    for kind, paths in manifest['generic'].items():
        add('generic/'+kind, paths, kind in ('katana_shield','glide','build_lightning'))
    for family in ('weapons','actions'):
        for subject, cues in manifest[family].items():
            for kind, paths in cues.items():
                add(f'{family}/{subject}/{kind}', paths, family=='actions' and kind=='shield_sustain')
    # Authored WAVs retain source variant counts and loop flags; no fabricated waveforms.
    for kind, count in counts.items():
        paths = [f'res://assets/audio/sfx/{kind}_{i:02}.wav' for i in range(count)]
        add('generic/authored/'+kind, paths, kind in loops)
        if kind not in manifest['generic']:
            add('generic/'+kind, paths, kind in loops)
    # Explicit Quest R21 branch absent from game skin runtime.
    if 'revive_charge_full_r21' in (root/'scripts/reference_audio_099.gd').read_text():
        add('generic/revive_charge_full_r21',['res://assets/audio/reference_r21/revive_charge_full.wav'],False)
    return dict(root=str(root.resolve()), files={str(p.relative_to(root)):digest(p) for p in
        (script,root/'scripts/ambience.gd',root/'scripts/reference_audio_099.gd',manifest_path)},assets=rows)

if __name__ == '__main__':
    p=argparse.ArgumentParser();p.add_argument('--quest',type=pathlib.Path,required=True);p.add_argument('--game',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True)
    a=p.parse_args();data={'schema':1,'status':'unreviewed-reference-only','baseline':'bdba6e30012e1733efd5021fd8ded593f82f26e6','trees':[export(a.quest),export(a.game)]}
    a.output.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
    for tree in data['trees']:
        print(tree['root'],len(tree['assets']),'rows;',sum('error' in x for x in tree['assets']),'missing/invalid')
