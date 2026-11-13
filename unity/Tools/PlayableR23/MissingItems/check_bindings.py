from pathlib import Path
import json,uuid,hashlib,re
R=Path.cwd();T=R/'unity/Tools/PlayableR23/MissingItems';H=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';G=R/'unity/Assets/Ashfall/Integration/Playable/Gallery'
for p in [H/'R23RoundState.Smaw.cs',H/'R23RoundState.Paddles.cs',H/'R23HandsWeapons.Smaw.cs',G/'R23SmawGallery.cs']:
 m=p.with_name(p.name+'.meta')
 if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
hands=(H/'R23HandsWeapons.cs').read_text();root=(R/'unity/Assets/Ashfall/Integration/Playable/R23PlayableLoop.cs').read_text();smaw=(H/'R23HandsWeapons.Smaw.cs').read_text();view=(H/'R24VRSmaw.cs').read_text();gallery=(G/'R23SmawGallery.cs').read_text();ui=(R/'unity/Assets/Ashfall/Integration/Playable/UI/R23VrUI.cs').read_text()
assert '"s85","smaw"'in hands and 'IsSmaw)return TickSmaw' in hands
assert 'smawView.BindOwner(round.Smaw)'in smaw and 'round.Rifle'not in smaw
assert 'ownedWeapons.Remove(firedAction);ownedWeaponOrder.Remove(firedAction)'in smaw
assert 'Gameplay.ConfigureSmawEffects(this)'in root and 'Explosives.PlaySourceVisual("smaw"'in root
assert 'Map.BindViewCamera(Rig.Head)'in root
assert 'UI.PaddlesRequested+=TogglePaddles' in root and 'StepPaddles(Mathf.Clamp' in root
assert 'PaddleHands||Gameplay.RightHandOccupied' in root and 'PaddleHands||leftUi||Gameplay.LeftHandOccupied'in root
assert 'int[] inventoryCells={0,1,4,5,8,9}'in ui and 'resourceCells={2,3,6,7,10,11}'in ui
assert 'utility/revive'in ui and 'paddles'not in hands.split('StarterWeaponIds=')[1].split(';',1)[0]
assert 'HarmlessPreview=true' in gallery and 'if(!HarmlessPreview)Spawn(descriptor)'in view
assert 'R23GrenadeBlast(r.Launch.InstanceId' in view and 'r.Damage,true' in view
assert 'target.transform.position' in (H/'R23HandsWeapons.DisplayedPose.cs').read_text()
icons=(R/'unity/Assets/Ashfall/Integration/Playable/Input/R23WeaponWheelIcons.cs').read_text();encoded=re.search(r'case "smaw":return System.Convert.FromBase64String\("([^"]+)"\)',icons).group(1)
import base64
assert base64.b64decode(encoded)==(R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons/smaw-icon.png').read_bytes()
print('PASS live17 actual owner/Root/paddles/12tiles/source27icon/harmlessgallery bindings')
