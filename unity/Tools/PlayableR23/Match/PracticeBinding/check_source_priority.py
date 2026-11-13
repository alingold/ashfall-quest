from pathlib import Path
import hashlib,json
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001')
manifest=(FROZEN/'SOURCE-MANIFEST.json').read_bytes()
assert hashlib.sha256(manifest).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={row['path']:row['sha256'] for row in json.loads(manifest)['files']}
names=['main','weapon_inventory','weapon_rack','weapon','auto_shotgun','catalog_firearm','compact_smg','top_feed_pdw','precision_rifle','revolver','dt11','belt_lmg','explosive_weapon','dual_pistols','player']
scripts={}
for name in names:
 path='scripts/'+name+'.gd';raw=(FROZEN/'source'/path).read_bytes();assert hashlib.sha256(raw).hexdigest()==pins[path],path;scripts[name]=raw.decode('utf-8-sig')
assert 'weapon_pickup_props = weapon_inventory' in scripts['main']
assert 'reload_claims_hand(hand):return false' in scripts['weapon_inventory']
policy=scripts['weapon'].split('func reload_claims_hand(',1)[1].split('\nfunc ',1)[0]
for exact in ['hand!=left_hand','not _input_allowed()','_support_hand_climbing()','is_hand_input_ready(hand)','is_hand_grip_reserved(hand)','hand.get_float("grip")>.2 and reload_grip_in_progress()','for zone in get_reload_zones()','_in_reload_reach(point,zone.position,zone.radius)']:
 assert exact in policy,exact
assert 'if reload_state not in ["needs_mag","held_mag","needs_rack"]:return []' in scripts['auto_shotgun']
assert '"radius":.115' in scripts['auto_shotgun'] and '>=.085:rack_bolt()' in scripts['auto_shotgun']
assert 'get_reload_zones()->Array:return []' in scripts['dual_pistols']
production=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
hands=(production/'R23HandsWeapons.cs').read_text()
assert '!ReloadClaimsHand(hand)' in hands.split('bool PickupHandAvailable(',1)[1].split('\n',1)[0]
assert 'if(!PickupHandAvailable(hand))return false;' in hands
assert 'WorldPickupHandEligible(int hand){return Ready&&Health>0&&PickupHandAvailable(hand);}' in hands
print('15 frozen R6 source hashes + exact source reload-priority/provider eligibility bindings PASS; no native execution.')
