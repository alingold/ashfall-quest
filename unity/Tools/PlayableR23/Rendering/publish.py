"""Publish only the new owned Rendering subtree after bounded compile and root release."""
from pathlib import Path
import json,shutil,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
DEST=ROOT/'unity/Assets/Ashfall/Integration/Playable/Rendering'
def metadata(path):
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-source-filmic/'+path.relative_to(DEST).as_posix()).hex
    header='fileFormatVersion: 2\nguid: '+guid+'\n'
    if path.is_dir():text=header+'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif path.suffix=='.cs':text=header+'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
    elif path.suffix=='.shader':text=header+'ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  preprocessorOverride: 0\n'
    else:text=header+'TextScriptImporter:\n  externalObjects: {}\n'
    Path(str(path)+'.meta').write_text(text+'  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
def main():
    checks=json.loads((HERE/'checks.json').read_text());assert checks['installedApiCompile']and checks['pureMathExit']==0
    assert not DEST.exists(),'Do not overwrite a previously handed-off source revision'
    DEST.mkdir();metadata(DEST)
    for source in (HERE/'Staged').rglob('*'):
        target=DEST/source.relative_to(HERE/'Staged')
        if source.is_dir():target.mkdir();metadata(target)
        else:shutil.copyfile(source,target);metadata(target)
    print('Published new source FILMIC feature/shader/license only; no activation or pipeline setting mutation.')
if __name__=='__main__':main()
