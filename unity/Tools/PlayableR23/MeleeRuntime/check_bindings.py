from pathlib import Path
import re,sys
R=Path(__file__).resolve().parents[4];H=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';G=R/'unity/Assets/Ashfall/Integration/Playable/Gallery';source=Path(sys.argv[1])if len(sys.argv)>1 else H/'R23HandsWeapons.cs';hands=source.read_text();runtime=(H/'R24MeleeRuntime.cs').read_text();physical=(H/'R24KnifePhysics.cs').read_text();root=(R/'unity/Assets/Ashfall/Integration/Playable/R23PlayableLoop.cs').read_text();rig=(R/'unity/Assets/Ashfall/Integration/Playable/R23VRRig.cs').read_text();wheel=(R/'unity/Assets/Ashfall/Integration/Playable/Input/R23WeaponWheelState.cs').read_text()
def entries(field):
 match=re.search(r'\b'+field+r'=\{(.*?)\};',hands,re.S);assert match,field
 values=[];depth=0;start=0
 for i,c in enumerate(match[1]):
  if c in '([{':depth+=1
  elif c in ')]}':depth-=1
  elif c==','and depth==0:values.append(match[1][start:i].strip());start=i+1
 values.append(match[1][start:].strip());return values
ids=entries('weaponIds');count=len(ids);assert len(set(ids))==count and count>=21
for field in ['magazineParts','boltParts','magazineHomes','modelScales','sourceMuzzles','feedbackStates']:
 actual=len(entries(field));assert actual==count,(source.name,field,actual,'profiles',count)
for field in ['offeredMagazines','heldMagazines','feedbackLenses','feedbackRearSights','feedbackFrontSights']:
 actual=int(re.search(r'\b'+field+r'=new Transform\[(\d+)\]',hands)[1]);assert actual==count,(field,actual,count)
assert '"katana"'in hands and 'if(IsMelee)return TickKnife(dt)'in hands
assert 'Actor.Point(local)'in runtime and 'if(travel>1)'in runtime and 'new SwordContacts(lease,64)'in runtime
assert 'Physics.ComputePenetration'in physical and 'R24KnifeMath.HullPoints'in physical and 'candidate.gameObject.layer==22'in physical
assert 'MeleeWorldTargetId'in root and 'TryReadDestructible'in root and 'TryReadPanel'in root and 'MeleeWorldInputAllowed'in root
assert 'ReadItemMovementMultiplier'in rig and 'ReadItemMovementMultiplier'in root
assert 'HideHeldPresentation'in(H/'R23HandsWeapons.Knife.cs').read_text()and'Math.Min(before.Health,amount),0'in(H/'R23RoundState.Knife.cs').read_text()
assert 'FIELD KNIFE'in wheel and 'profile!="knife"'in wheel
assert 'SampleKnife(seconds'in(G/'R23LobbyWeaponGallery.cs').read_text()and'new R24MeleeRuntime("lobby-live097/"'in(G/'R23KnifeGallery.cs').read_text()
for name in ['R23HandsWeapons.Knife.cs','R23RoundState.Knife.cs','R24KnifeRuntime.cs','R24KnifePhysics.cs','R24MeleeRuntime.cs','R24MeleeProfiles.cs']:
 assert re.search(r'^guid: [a-f0-9]{32}$',(H/(name+'.meta')).read_text(),re.M)
print('PASS actual',count,'profiles / all11 parallel table dimensions / native melee dispatch / Root Health/map/build/movement/gallery bindings; no engine')
