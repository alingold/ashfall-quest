"""Read-only Godot source/PCK4 inventory -> versioned neutral JSON. No engine/cache imports.
Only writes beneath the four Inventory-owned roots. Python standard library only.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from reconciliation import FileSources, Snapshot, SourceDrift

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'campaign-staging/20261104-three-chats/unity/Inventory/r3'
EVIDENCE = ROOT / 'docs/agent-runs/20261104-three-chats/unity/Inventory/r3'
BASELINE = 'bdba6e30012e1733efd5021fd8ded593f82f26e6'

_SNAPSHOT = None
_PENDING = None
_EXTERNAL_HASHES = None
_OBSERVATIONS = None

def text(path):
    return _SNAPSHOT.text(path) if _SNAPSHOT is not None else path.read_text(encoding='utf-8-sig')

def observe(path, operation):
    value = operation(path)
    if _OBSERVATIONS is not None:
        key = (path, operation)
        if key in _OBSERVATIONS and _OBSERVATIONS[key] != value:
            raise SourceDrift('Resource presence changed during generation: '+str(path))
        _OBSERVATIONS[key] = value
    return value

def exists(path): return observe(path, Path.exists)
def is_file(path): return observe(path, Path.is_file)

def sha(path):
    if _SNAPSHOT is not None and path in _SNAPSHOT.files:
        return _SNAPSHOT.digest(path)
    digest = raw_sha(path)
    if _EXTERNAL_HASHES is not None:
        prior = _EXTERNAL_HASHES.setdefault(path, digest)
        if prior != digest: raise SourceDrift('External hash changed: '+str(path))
    return digest

def raw_sha(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(1024*1024), b''): h.update(block)
    return h.hexdigest()

def encode(data):
    return (json.dumps(data, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False)+'\n').encode('utf-8')

def write(path, data):
    allowed = [OUT, EVIDENCE, ROOT/'unity/Tools/Modules/Inventory', ROOT/'unity/Assets/Ashfall/Modules/Inventory']
    dest = path.resolve()
    if not any(dest.is_relative_to(p.resolve()) for p in allowed): raise ValueError('Output outside ownership')
    if _PENDING is not None:
        _PENDING[path] = encode(data)
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encode(data))

def read(path): return json.loads(text(path))

class Pck4:
    """Seek-only unencrypted PCK v4 index reader. Never extracts scenes/textures.
    Bounds, duplicate entries, removals and per-file encryption are fail-closed.
    Each selected JSON payload is verified against its directory MD5.
    """
    def __init__(self, path):
        self.path = Path(path)
        self.entries = {}
        self.size = self.path.stat().st_size
        with self.path.open('rb') as f:
            def unpack(fmt):
                n = struct.calcsize(fmt); b = f.read(n)
                if len(b) != n: raise ValueError('Truncated PCK')
                return struct.unpack(fmt, b)
            magic, version, major, minor, patch, flags, base, directory = unpack('<4sIIIIIQQ')
            if magic != b'GDPC' or version != 4 or flags & ~2: raise ValueError('Unsupported PCK version/flags')
            if directory < 128 or directory >= self.size: raise ValueError('Invalid PCK directory')
            if self.size-directory > 64*1024*1024: raise ValueError('PCK directory exceeds bounded reader limit')
            f.seek(directory)
            count, = unpack('<I')
            if count > 1000000: raise ValueError('Excessive PCK entries')
            for _ in range(count):
                length, = unpack('<I')
                if length == 0 or length > 65536: raise ValueError('Invalid path size')
                raw = f.read(length)
                if len(raw) != length: raise ValueError('Truncated path')
                name = raw.rstrip(b'\0').decode('utf-8')
                name = name.removeprefix('res://')
                if any(c in name for c in '\\:\0') or any(ord(c) < 32 or ord(c) == 127 for c in name) or any(not part or set(part) == {'.'} for part in name.split('/')): raise ValueError('Noncanonical resource path')
                offset, size, digest, entry_flags = unpack('<QQ16sI')
                offset += base if flags & 2 else 0
                if entry_flags or offset < 128 or offset+size > directory: raise ValueError('Unsupported or invalid entry')
                if name in self.entries: raise ValueError('Duplicate PCK identity')
                self.entries[name] = (offset, size, digest)
        self.directory = directory
        with self.path.open('rb') as f:
            f.seek(directory)
            self.directory_sha256=hashlib.sha256(f.read()).hexdigest()
    def payload(self, name, limit=16*1024*1024):
        offset, size, digest = self.entries[name]
        if size > limit: raise ValueError('Payload exceeds bounded export limit')
        with self.path.open('rb') as f:
            f.seek(offset); data = f.read(size)
        if len(data) != size or hashlib.md5(data).digest() != digest: raise ValueError('PCK payload digest mismatch')
        return data
    def json(self, name): return json.loads(self.payload(name).decode('utf-8-sig'))

# Each family has observable acceptance; exhaustive symbols also retain unclassified work.
FAMILIES = [
 ('Flight', r'flight|launch|capsule', 'Launch from oriented pad repeatedly with cooldown and host authority; glide/dive/land without retriggers; prohibit indoor capsules.'),
 ('Networking', r'net_|remote_|voice', 'Join compatible host before spawn; reject wrong schema/content and replayed intents; reconnect/late join restores authoritative state.'),
 ('Persistence', r'save|campaign_progress', 'Migrate isolated legacy settings, preserve unknown keys; reject corrupt/future data and recover backup without overwriting Godot originals.'),
 ('Avatar', r'player_body|gemini_player|glove|skin|ragdoll|suit|roster', 'Resolve each stable style key, assemble head/13 body pieces/both hands; inspect holdout poses and PCVR/light/remote paths.'),
 ('Modes', r'tactical|ping|score|zone_|net_rules', 'Exercise revive/respawn, team/FFA scores, round end and ping team visibility/cooldown; verify tactical world bounds.'),
 ('World', r'map|archive|world|lobby|cyber|foliage|spawn', 'Load every installed stable map ID; validate mode/team floor/headroom, indoor no-capsule policy and all oriented pad routes/fixture ray ownership.'),
 ('Construction', r'build|construction', 'Place with support/range/collision checks; reject blocked/duplicate requests, charge once and replicate remove/damage.'),
 ('HealthStates', r'revive|rescue|health', 'Damage to downed/dead; valid team revive completes once; release, distance, death and competing requests cancel safely.'),
 ('WeaponHandling', r'reload|scope|recoil|grip|break_action', 'Physical magazine/chamber cycle including empty/cancel; two-hand release and scope at both eyes with tracking loss.'),
 ('Combat', r'weapon|gun|rifle|pistol|shotgun|sword|knife|damage|grenade|ballistic|reticle|sight|m60|dt11|spr|matador', 'Equip each active ID and fire full/empty; authoritative range/LOS/team/headshot/shield and melee hit deduplication.'),
 ('Pickups', r'item|consum|supply|ammo|pickup|banana', 'Compete for one visible pickup; blocked wall rejects; consume/stack/drop at capacity and replicate one accepted mutation.'),
 ('Presentation', r'menu|hud|ui_|calibrat', 'Navigate focus/scroll/back/resume, loading/error/disabled states, apply and reopen persisted settings; inspect headset readability.'),
 ('Audio', r'audio|sound|music|ambience', 'Listen to sustained and start/stop flight, gun and remote events; no duplicate voices, loop seam or clipping.'),
 ('Climbing', r'climb|mantle|hand_wall', 'Anchor either tracked hand, release/fling/mantle against valid collision; reject occlusion and tracking loss.'),
 ('Player', r'player|movement', 'Desktop/XR locomotion, stamina, grounded jump and turn; root collider retains authority while tracked children move.'),
]

def family(path):
    if path.name == 'dual_pistols.gd': return 'WeaponHandling', 'PairedUzi flick reload/tracking suspension, occupied-hand ammo and rearm; conserve each hand ammunition and preserve source exceptions with InventoryItems.'
    for row in read(Path(__file__).with_name('assignments-r2.json')):
        if row['path'] == 'scripts/'+path.name: return row['owner'], row['acceptance']
    for owner, pattern, scenario in FAMILIES:
        if re.search(pattern, path.name): return owner, scenario
    return 'unassigned', 'Trace caller and establish observable behavior before implementation; uncovered source is an open parity task.'

def symbols(text):
    # Indentation scopes distinguish class members from local named/anonymous lambdas.
    # This inventories source sections, not a GDScript AST or inherited/dynamic methods.
    lines = text.splitlines()
    starts = []
    scopes = []
    quote = None
    code_lines = []
    for i, line in enumerate(lines):
        code = ''
        j = 0
        while j < len(line):
            if quote:
                if line.startswith(quote, j):
                    j += len(quote); quote = None
                elif line[j] == chr(92): j += 2
                else: j += 1
            elif line[j] == '#': break
            elif line[j] in (chr(34), chr(39)):
                q = line[j] * 3 if line.startswith(line[j] * 3, j) else line[j]
                quote = q; j += len(q); code += ' '
            else:
                code += line[j]; j += 1
        if quote and len(quote) == 1: quote = None
        code_lines.append(code)
        if not code.strip(): continue
        indent = len(code.expandtabs(4)) - len(code.lstrip().expandtabs(4))
        while scopes and indent <= scopes[-1][0]: scopes.pop()
        cls = re.match(r'^\s*class\s+(\w+)\s*(?:extends\b[^:]*)?:', code)
        method = re.match(r'^\s*(?:static\s+)?func\s+(\w+)\s*\(', code)
        if cls and not any(k == 'func' for _, k, _ in scopes):
            scopes.append((indent, 'class', cls[1]))
        elif method:
            if not any(k == 'func' for _, k, _ in scopes):
                name = '.'.join([n for _, k, n in scopes if k == 'class'] + [method[1]])
                starts.append((i, name, indent))
            scopes.append((indent, 'func', method[1]))
    result = []
    for n, (start, name, indent) in enumerate(starts):
        end = starts[n+1][0] if n+1 < len(starts) else len(lines)
        if indent:
            for j in range(start+1, end):
                code = code_lines[j]
                if code.strip() and len(code.expandtabs(4))-len(code.lstrip().expandtabs(4)) < indent:
                    end = j; break
        result.append({'symbol':name,'line':start+1,'end_line':end,'declaration':lines[start], 'body_sha256':hashlib.sha256(('\n'.join(lines[start:end])+'\n').encode()).hexdigest()})
    return result

def literal_array(text, name):
    match = re.search(r'\b'+re.escape(name)+r'\s*(?::[^=\n]+)?\s*:?=\s*(\[[^\n]*?\])', text)
    if not match: raise ValueError('Missing source literal '+name)
    return json.loads(match[1])

def behavior_rows(symbol_rows):
    # Cross-file slices recover the old inventory intent but bind to fresh exact symbols.
    specs = [
        ('ai-navigation','Combat',r'enemy.gd$|guard_routes.gd$',r'.*'),
        ('explosive-zone-shield','Combat',r'explosive_projectile|directional_shield',r'.*'),
        ('weapon-specific-reload','WeaponHandling',r'dual_pistols|closure_flick|revolver|belt_lmg|compact_smg|top_feed_pdw|arsenal_data',r'.*'),
        ('station-recharge','HealthStates',r'healing_stations_r18',r'.*'),
        ('training-orchestration','Presentation',r'training.gd$|campaign_extension|main.gd$',r'.*'),
        ('catalog','World',r'map_catalog|archive_catalog|community_catalog',r'ids|catalog|rows|installed|verify|playable'),
        ('map-spawn-fixtures','World',r'archive_map|map_semantics|archive_semantics|team_launch',r'.*'),
        ('movement','Player',r'player.gd$',r'apply_locomotion_motion|_physics_process|.*stamina.*|.*ground.*'),
        ('climb-mantle','Climbing',r'player.gd$|release_mantle|hand_wall',r'.*climb.*|.*grab.*|.*mantle.*'),
        ('glide-dive-launch','Flight',r'player.gd$|team_launch|net_session',r'.*glide.*|.*launch.*|.*pod.*|.*flight.*'),
        ('inventory-equip','InventoryItems',r'arsenal_data|weapon_inventory|weapon_rack',r'.*'),
        ('reload-twohand-scope','WeaponHandling',r'weapon.gd$|reload_visuals|objective_grip|awp_scope|spr_scope|break_action',r'.*reload.*|.*mag.*|.*bolt.*|.*chamber.*|.*support.*|.*scope.*|.*grip.*|.*step.*'),
        ('damage-melee','Combat',r'main.gd$|ballistic|net_session|sword|blade_sweep',r'.*damage.*|.*hit.*|.*fire.*|.*attack.*|.*sweep.*'),
        ('consumables-pickups','Pickups',r'field_consumables|pickup_focus|item_catalog_service|supply_catalog',r'.*'),
        ('health-revive','HealthStates',r'net_session|main.gd$|revive|rescue',r'.*reviv.*|.*heal.*|.*down.*|.*dead.*|.*damage.*'),
        ('construction','Construction',r'field_builder|building_input|construction_support',r'.*'),
        ('transport-authority','Networking',r'net_session|net_bridge|net_avatar',r'.*'),
        ('sessions','Sessions',r'net_session|front_menu',r'.*spawn.*|.*join.*|.*connect.*|.*lobby.*|.*host.*|.*leave.*|.*countdown.*'),
        ('menus-hud-settings','Presentation',r'menu|hud|item_catalog_ui',r'.*'),
        ('save-migration','Persistence',r'save_store|campaign_progress|menu.gd$',r'.*save.*|.*load.*|.*setting.*|.*config.*|.*apply.*'),
        ('audio-routing','Audio',r'audio_bank|music|ambience|voice_r4',r'.*'),
        ('modes-rounds','Modes',r'net_rules|net_session|score',r'.*score.*|.*round.*|.*match.*|.*preset.*|.*validate.*|.*zone.*'),
        ('pings-tactical','Modes',r'ping|tactical_map',r'.*'),
        ('avatar-assembly','Avatar',r'gemini_player|player_body|net_avatar',r'.*'),
    ]
    scenarios={owner:scenario for owner,_,scenario in FAMILIES}
    scenarios['InventoryItems']='Equip/drop every active catalog index; preserve inactive slots 4/22, stable string keys and ammo ownership; enforce capacity and initial loadout from source.'
    scenarios['Sessions']='Compatibility before spawn; join/respawn/team/reconnect and authority epoch transitions; indoor spawn headroom and safe ground.'
    scenarios_by_key = {
        'ai-navigation': 'enemy.gd detection/FOV/attack/state/death; guard_routes.gd Traversal.direction time budget, blocked timeout and waypoint completion. AI/navigation owner unresolved; Combat provisional with HealthStates damage and Construction obstruction ports.',
        'explosive-zone-shield': 'explosive_projectile.gd zone tick boundaries/shrinking radius and one-shot detonation; directional_shield.gd finite front-face rectangular bullet intersection, exclude area/melee/environment damage.',
        'weapon-specific-reload': 'dual_pistols.gd class-qualified flick reload/tracking/occupied-hand ammo; closure_flick, revolver, belt_lmg, compact_smg, top_feed_pdw per-weapon phase/cancel/rearm/ammo acceptance with InventoryItems arsenal_data profile exceptions.',
        'station-recharge': 'healing_stations_r18.gd clear semantic contact, shared 15-second station recharge, health/shield restoration and host-owned multiplayer timers/feedback; Quest-only presence remains explicit.',
        'training-orchestration': 'training.gd progress observation/hints/reset, main.gd and campaign_extension.gd lifecycle/mission transitions; Presentation with Modes/Persistence/World split requires coordinator decision.'
    }
    return [{'id':'feature:'+key,'owner':owner,'acceptance':scenarios_by_key.get(key, scenarios[owner]), 'sources':[s['id'] for s in symbol_rows if re.search(files,s['source']['path']) and re.fullmatch(names,s['symbol'])], 'status':'unchecked','runtime_evidence':None} for key,owner,files,names in specs]

def generate():
    trees = {'quest':ROOT/'quest-r22/source', 'game':ROOT/'game'}
    file_rows = {}; symbol_rows = []; features = []; identities = {}; references = []
    for tree, root in trees.items():
        files = {}
        for path in sorted((root/'scripts').rglob('*.gd')):
            rel = path.relative_to(root).as_posix(); digest = sha(path)
            files[rel] = digest; text_value = text(path)
            owner, scenario = family(path)
            source = {'tree':tree,'path':rel,'absolute_path':str(path),'sha256':digest}
            ids = []
            for symbol in symbols(text_value):
                sid = tree+':'+rel+':'+symbol['symbol']; ids.append(sid)
                symbol_rows.append(dict(symbol, id=sid, source=source))
            features.append({'id':'source:'+rel,'tree':tree,'suggested_owner':owner,'owner_assignment':'inferred; coordinator confirms', 'source':source,'symbols':ids,'acceptance':scenario,'status':'unchecked','gates':dict.fromkeys(['Unity','desktop','XR','Quest','WAN','visual','listening'],'unknown')})
            for i,line in enumerate(text_value.splitlines(),1):
                for resource in re.findall(r'[\"\x27]((?:res|user)://[^\"\x27\n]+)', line):
                    references.append(dict(source=source,line=i,resource=resource,exists=exists(root/resource[6:]) if resource.startswith('res://') else None))
        file_rows[tree] = files
        style = text(root/'scripts/gemini_player_models_099.gd')
        keys, labels = literal_array(style,'STYLE_KEYS'), literal_array(style,'STYLES')
        if len(keys)!=len(labels) or len(set(keys))!=len(keys): raise ValueError('Invalid style IDs')
        rack = text(root/'scripts/weapon_rack.gd')
        profiles = read(root/'assets/data/one_star_050.json')['profiles']
        identities[tree] = {'styles':[{'key':k,'index':i,'label':labels[i]} for i,k in enumerate(keys)], 'active_weapon_indices':literal_array(rack,'ACTIVE_WEAPON_INDICES'), 'weapon_labels':literal_array(rack,'weapon_names'), 'weapon_profiles':profiles, 'consumables':literal_array(text(root/'scripts/field_consumables.gd'),'KINDS'), 'source_files':{p:files[p] for p in ['scripts/gemini_player_models_099.gd','scripts/weapon_rack.gd','scripts/field_consumables.gd']}, 'profile_sha256':sha(root/'assets/data/one_star_050.json')}
    reconciliation = [{'path':p,'sha256':{t:file_rows[t].get(p) for t in trees},'relationship':'identical' if file_rows['game'].get(p)==file_rows['quest'].get(p) else 'different' if p in file_rows['game'] and p in file_rows['quest'] else 'tree-only','resolution':'preserve; per-feature central decision required'} for p in sorted(set(file_rows['game'])|set(file_rows['quest']))]
    symbol_by_tree={t:{s['source']['path']+':'+s['symbol']:s for s in symbol_rows if s['source']['tree']==t} for t in trees}
    symbol_diffs=[]
    for key in sorted(set(symbol_by_tree['quest'])|set(symbol_by_tree['game'])):
        a=symbol_by_tree['quest'].get(key); b=symbol_by_tree['game'].get(key)
        if a is None or b is None or a['body_sha256']!=b['body_sha256']:
            symbol_diffs.append({'symbol_key':key,'quest':a,'game':b,'resolution':'preserve both; coordinator chooses behavior'})
    pack = Pck4(ROOT/'builds/ASHFALL-Local-Maps.pck')
    adapters = Pck4(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck')
    catalog = pack.json('local_archive/catalog.json')
    maps = []
    for row in catalog['maps']:
        slug = row['slug']; scene = 'local_archive/maps/'+slug+'/converted/map.scn'
        manifest_path = 'local_archive/maps/'+slug+'/converted/manifest.json'
        manifest = pack.json(manifest_path) if manifest_path in pack.entries else None
        adapter='local_archive_r10/'+slug+'.res'
        maps.append({'id':row['id'],'catalog_row':row,'scene_indexed':scene in pack.entries,'adapter_indexed':adapter in adapters.entries,'manifest':manifest,'approval':'unreviewed reference only','runtime_verified':False})
    if len({m['id'] for m in maps})!=len(maps): raise ValueError('Duplicate map IDs')
    registry = read(ROOT/'skins_pipeline/registry.json')
    registry_checks = []
    registry_id_checks=[]
    for entry in registry['entries']:
        for t,identity in identities.items():
            matches=[s for s in identity['styles'] if s['key']==entry['key']]
            registry_id_checks.append({'tree':t,'key':entry['key'],'registry_index':entry['id'],'source_index':matches[0]['index'] if matches else None,'matches':len(matches)==1 and matches[0]['index']==entry['id']})
        for tier, asset in sorted(entry.get('files',{}).items()):
            path = ROOT/'game'/asset['path']
            actual = sha(path) if is_file(path) else None
            registry_checks.append({'key':entry['key'],'index':entry['id'],'tier':tier,'path':str(path),'resolved_path':str(path.resolve()),'expected_sha256':asset['sha256'],'actual_sha256':actual,'matches':actual==asset['sha256'].lower(),'approval':'historical registry status is not campaign approval'})
    historical = ROOT/'docs/agent-runs/20261104-0020/lead-u/inventory/installed-map-catalog.json'
    old_ids = [x['id'] for x in read(historical)['entries']]
    installed = ['valley','range','cyber_bird']
    community = {t:all(is_file(r/'local_community/converted'/n) for n in ['manifest.json','map.scn']) for t,r in trees.items()}
    # Community may be pack-mounted elsewhere; never infer installation from old receipt.
    observed_ids = installed+[m['id'] for m in maps]
    asset_acceptance=[{'id':'map:'+m['id'],'source_id':m['id'],'kind':'map','status':'unchecked','acceptance':'Load with manifest fingerprint and scene/adapter integrity; test all mode/team spawns, pads and intended fixtures, textures/collision and host/client routes.','source_container':str(pack.path),'source_pointer':'local_archive/catalog.json#/maps/'+str(i)} for i,m in enumerate(maps)]
    for map_id in installed:
        asset_acceptance.append({'id':'map:'+map_id,'source_id':map_id,'kind':'procedural map','status':'unchecked','acceptance':'Load procedural map and test safe mode/team spawn, every pad/interactable, collision/materials and host/client agreement.','source':'scripts/map_catalog1000.gd:ids'})
    asset_acceptance.append({'id':'lobby-scene','source_id':None,'kind':'separate lobby; not a map catalog ID','status':'unchecked','acceptance':'Join/reconnect and team placement; menus, avatars, pickups and safe spawn; preserve separate lobby routing.','source':'scripts/lobby_map_091.gd'})
    for t,identity in identities.items():
        for item in identity['consumables']:
            asset_acceptance.append({'id':t+':consumable:'+item,'source_id':item,'kind':'consumable','status':'unchecked','acceptance':'Pickup, stack to capacity, use intended gesture/duration, apply source health/shield amount once and replicate; reject blocked/unauthorized use.','source':str(trees[t]/'scripts/field_consumables.gd')})
        for style in identity['styles']:
            asset_acceptance.append({'id':t+':style:'+style['key'],'source_id':style['key'],'index':style['index'],'kind':'style','status':'unchecked','acceptance':'Three independent critique/fix/recheck rounds; head/13 pieces/both gloves, holdout poses, light/PCVR, local/remote and XR inspection.','source':str(trees[t]/'scripts/gemini_player_models_099.gd')})
        for index in identity['active_weapon_indices']:
            asset_acceptance.append({'id':t+':weapon-index:'+str(index),'source_id':str(index),'label':identity['weapon_labels'][index],'kind':'weapon catalog index','status':'unchecked','acceptance':'Equip/fire/empty, physical reload stage order/cancel, drop/pickup, ownership/ammo and authoritative damage for this exact catalog entry.','source':str(trees[t]/'scripts/weapon_rack.gd')})
    data = {'schema':1,'baseline_commit_reference':BASELINE,'baseline_note':'Quest commit reference supplied; current source hashes authoritative, no git mutation', 'identities':identities,'maps':maps,'community_filesystem_presence':community,'historical_catalog':{'path':str(historical),'sha256':sha(historical),'only_historical':sorted(set(old_ids)-set(observed_ids)),'only_current':sorted(set(observed_ids)-set(old_ids))},'pack':{'path':str(pack.path),'bytes':pack.size,'entry_count':len(pack.entries),'catalog_payload_sha256':hashlib.sha256(pack.payload('local_archive/catalog.json')).hexdigest(),'verification':'selected JSON MD5 checked; full container/scene integrity not rehashed','approval':'unreviewed'},'registry':{'sha256':sha(ROOT/'skins_pipeline/registry.json'),'checks':registry_checks}}
    write(OUT/'identity-export-v1.json',data)
    write(EVIDENCE/'pack-index-evidence.json',[{'path':str(p.path),'resolved_path':str(p.path.resolve()),'bytes':p.size,'directory_offset':p.directory,'directory_sha256':p.directory_sha256,'entries':len(p.entries),'scope':'index bytes only, not full container SHA256; selected JSON payloads MD5 verified'} for p in [pack,adapters]])
    write(EVIDENCE/'registry-id-reconciliation.json',registry_id_checks)
    write(EVIDENCE/'source-symbols.json',symbol_rows)
    write(EVIDENCE/'behavior-features.json',behavior_rows(symbol_rows))
    write(EVIDENCE/'asset-acceptance.json',asset_acceptance)
    write(EVIDENCE/'symbol-reconciliation.json',symbol_diffs)
    assignments = {r['path']:r for r in read(Path(__file__).with_name('assignments-r2.json'))}
    proposals = []
    for feature in features:
        assignment = assignments.get(feature['source']['path'])
        if assignment:
            feature['owner_assignment'] = assignment['status']
            feature['partners'] = assignment['partners']
            feature['ambiguity'] = assignment['ambiguity']
            proposals.append(dict(assignment, source=feature['source'], symbols=feature['symbols'], tree=feature['tree']))
    write(EVIDENCE/'assignment-proposals.json', proposals)
    write(EVIDENCE/'feature-acceptance.json',features)
    write(EVIDENCE/'tree-reconciliation.json',reconciliation)
    write(EVIDENCE/'resource-references.json',references)
    summary = {'symbols':len(symbol_rows),'feature_source_rows':len(features),'map_archive_rows':len(maps),'styles':{t:len(x['styles']) for t,x in identities.items()},'changed_scripts':sum(r['relationship']=='different' for r in reconciliation),'registry_assets':len(registry_checks),'registry_mismatches':sum(not x['matches'] for x in registry_checks),'unassigned_features':sum(x['suggested_owner']=='unassigned' for x in features),'proposed_source_assignments':len(proposals),'ambiguous_source_assignments':sum(bool(x['ambiguity']) for x in proposals),'status':'candidate; independent review and all runtime gates open'}
    write(EVIDENCE/'summary.json',summary)
    return summary

def run():
    global _SNAPSHOT, _PENDING, _EXTERNAL_HASHES, _OBSERVATIONS
    if _SNAPSHOT is not None: raise RuntimeError('Reentrant generation')
    port = FileSources(ROOT, Path(__file__).with_name('assignments-r2.json'))
    snapshot = Snapshot.capture(port)
    packs = [ROOT/'builds'/name for name in ['ASHFALL-Local-Maps.pck', 'ASHFALL-Map-Adapters-R10.pck']]
    # Large containers are never copied/rehashed here. Stat + directory/payload integrity
    # is bounded evidence only, not a full-container immutable freeze.
    pack_stats = {p:(p.stat().st_size, p.stat().st_mtime_ns, p.stat().st_ctime_ns, p.stat().st_ino) for p in packs}
    _SNAPSHOT, _PENDING, _EXTERNAL_HASHES, _OBSERVATIONS = snapshot, {}, {}, {}
    try:
        summary = generate()
        pending = dict(_PENDING)
        snapshot.validate()
        for p, digest in _EXTERNAL_HASHES.items():
            if raw_sha(p) != digest: raise SourceDrift('External asset changed: '+str(p))
        for (p, op), expected in _OBSERVATIONS.items():
            if op(p) != expected: raise SourceDrift('Resource presence changed: '+str(p))
        for p, stat in pack_stats.items():
            if (p.stat().st_size, p.stat().st_mtime_ns, p.stat().st_ctime_ns, p.stat().st_ino) != stat: raise SourceDrift('Pack metadata changed: '+str(p))
        snapshot.validate() # catch edits during external-asset verification too
        inputs = [{'path':str(p),'sha256':snapshot.digest(p),'bytes':len(b)} for p,b in sorted(snapshot.files.items())]
        # Persist exact small consumed bytes, never large assets/packs. All symbols
        # and hashes came from this same immutable in-memory capture.
        for p,b in snapshot.files.items():
            pending[OUT/'inputs'/p.relative_to(ROOT)] = b
        receipt = {'schema':1,'status':'captured source candidate; not atomic global filesystem freeze',
                   'inputs':inputs,'external_hashes':{str(p):h for p,h in sorted(_EXTERNAL_HASHES.items())},
                   'pack_scope':'stat recheck plus existing directory and selected payload digests; full containers unverified',
                   'outputs':[{'path':str(p),'sha256':hashlib.sha256(b).hexdigest(),'bytes':len(b)} for p,b in sorted(pending.items())]}
        _PENDING = None
        for p,b in pending.items():
            p.parent.mkdir(parents=True,exist_ok=True)
            temp=p.with_name(p.name+'.pending')
            temp.write_bytes(b); temp.replace(p)
        write(EVIDENCE/'capture-receipt.json', receipt) # last: consumers must verify every output
        print(json.dumps(summary,sort_keys=True))
    finally:
        _SNAPSHOT = _PENDING = _EXTERNAL_HASHES = _OBSERVATIONS = None

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__); parser.parse_args(); run()
