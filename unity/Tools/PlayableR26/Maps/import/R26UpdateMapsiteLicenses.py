from pathlib import Path
import json
import os
import re
import uuid

repo = Path.cwd()
root = Path('Assets/StreamingAssets/PlayableR23')
campaign = repo.parent / 'campaign'
claims = campaign / 'claims.txt'
changes = {}
community = 'Community map from the public Population: ONE archive. The creator and other rights holders retain their rights. The ASHFALL MIT license and CC0 1.0 dedication do not cover this map.'
official = 'Official Population: ONE map from the public archive. BigBox VR and other rights holders retain their rights. The ASHFALL MIT license and CC0 1.0 dedication do not cover this map.'
old = 'UNKNOWN REUSE LICENSE — LOCAL ONLY. Excluded from ASHFALL Apache-2.0 and redistribution.'
escaped = old.replace('—', r'\u2014')

def update(path, transform):
    original = path.read_bytes()
    new = transform(original.decode('utf-8')).encode('utf-8')
    if original != new:
        changes[path] = new

def asset(path, creator, slug):
    assert not path.exists()
    changes[path] = (community + '\nSource: https://maps.deliciouswines.org/maps/' + slug + '/\nCreator: ' + creator + '\n').encode()
    meta = Path(str(path) + '.meta')
    assert not meta.exists()
    changes[meta] = ('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n').encode()

for area in ('Maps', 'Legacy/Maps'):
    for path in sorted((root/area).glob('*/SOURCE-LICENSE.txt')):
        notice = official if re.search(r'^Creator: (?:bigbox(?: vr)?)\s*$', path.read_text(), re.I | re.M) else community
        update(path, lambda src: src.replace(old, notice))
        for name in ('source-semantics.json', 'provenance.json'):
            sidecar = path.with_name(name)
            if sidecar.is_file():
                update(sidecar, lambda src: src.replace(old, notice).replace(escaped, notice))

asset(root/'World/SOURCE-LICENSE.txt', 'GODS_PITDEVIL', 'battlefield-paintball-gods-pitdevil')
asset(root/'Legacy/Maps/alfa-aglae-souza/SOURCE-LICENSE.txt', 'aglae_souza', 'alfa-aglae-souza')

credit = Path('CREDITS-ASSETS.md')
def update_credits(src):
    begin, end = src.index('The 61 active community maps'), src.index('## Player skins')
    return src[:begin] + '''Community maps retain their creator credit and archive source URL in each map's
`SOURCE-LICENSE.txt`. This includes optional Legacy community maps and Battlefield
Paintball, the default map. The map layouts and archive-derived material are outside
the ASHFALL code license and original-asset dedication. See
[Third-party notices](THIRD-PARTY-NOTICES.md).

''' + src[end:]
update(credit, update_credits)
update(Path('Documentation/PLAYTEST-2026-11-18.md'), lambda src: src.replace('Replace old "ASHFALL Apache-2.0" mentions.', 'Replace obsolete references to the previous project license.'))
update(root/'SourceArt/art_r19-ATTRIBUTION.json', lambda src: src.replace('Unmodified archive models and original textures retain all prior credits and UNKNOWN REUSE LICENSE / LOCAL ONLY restrictions. Selectors and baked triangle/UV derivatives remain subject to the same local-only restrictions.', 'Archive models, their original textures and baked triangle/UV derivatives retain their source credits and terms. They are outside the ASHFALL code license and original-asset dedication.'))
update(root/'SourceArt/art_r21-ATTRIBUTION.json', lambda src: src.replace('UNKNOWN LOCAL-ONLY restrictions retained; archive originals unchanged', 'Original archive material retains its source credits and terms and is outside the ASHFALL code license and original-asset dedication'))

claims_by_path = dict(line.rsplit(' ',1) for line in claims.read_text().splitlines() if line)
new_files = [Path('LICENSE'), Path('LICENSE-ASSETS.md'), Path('THIRD-PARTY-NOTICES.md')]
files = sorted(set(changes) | set(new_files))
for path in files:
    assert claims_by_path.get(str(path),'MAPSITE') == 'MAPSITE', (path, claims_by_path.get(str(path)))
with claims.open('a') as f:
    for path in files:
        if str(path) not in claims_by_path:
            f.write(f'{path} MAPSITE\n')
for path, raw in changes.items():
    path.write_bytes(raw)
(campaign/'tmp/MAPSITE/license-file-list.txt').write_text(''.join(f'{path}\n' for path in files))
print(f'{len(changes)} attribution, metadata and documentation files updated. {len(new_files)} root documents claimed.')
