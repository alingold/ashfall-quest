from pathlib import Path
import shutil,json
R=Path(__file__).resolve().parents[3];U=R/'unity';P=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
# One real runnable project, using the existing installed packages and one Library.
for folder in ['Packages','ProjectSettings']:
    (U/folder).mkdir(exist_ok=True)
    for p in (P/folder).iterdir():
        if p.is_file() and not (U/folder/p.name).exists():shutil.copy2(p,U/folder/p.name)
if not (U/'Assets/XR').exists():shutil.copytree(P/'Assets/XR',U/'Assets/XR')
manifest=U/'Packages/manifest.json';m=json.loads(manifest.read_text())
for name in ['com.unity.modules.unitywebrequest','com.unity.modules.imgui','com.unity.modules.xr','com.unity.modules.vr']:
    m['dependencies'][name]='1.0.0'
manifest.write_text(json.dumps(m,indent=2)+'\n',encoding='utf-8')
print('Prepared unity/Packages, ProjectSettings and existing XR settings. Library junction is root-owned; no engine launched.')
