"""Publish only this worker's new Lobby assets after root releases its snapshot."""
from pathlib import Path
import shutil,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
def meta(path):
    target=path.with_name(path.name+'.meta')
    if target.exists():return
    if path.is_dir():target.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    else:target.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
for stage,destination in [(HERE/'StagedCode',ROOT/'unity/Assets/Ashfall/Integration/Playable/Lobby'),(HERE/'StagedPayload',ROOT/'unity/Assets/StreamingAssets/PlayableR23/Lobby')]:
    destination.mkdir(parents=True,exist_ok=True);meta(destination)
    for source in stage.rglob('*'):
        target=destination/source.relative_to(stage)
        if source.is_dir():target.mkdir(parents=True,exist_ok=True)
        else:shutil.copyfile(source,target)
        meta(target)
    print('Published',destination)
