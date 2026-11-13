"""Verify the Windows sideloader's no-device modes and corrupt-payload refusals."""
import hashlib,json,os,shutil,subprocess,tempfile,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parent
POWERSHELL=Path(os.environ.get('SystemRoot','C:/Windows'))/'System32/WindowsPowerShell/v1.0/powershell.exe'
class Installer(unittest.TestCase):
    def bundle(self,root):
        shutil.copyfile(ROOT/'Install-Quest.ps1',root/'Install-Quest.ps1')
        files=[]
        for name,kind in [('ASHFALL-R15-Quest.apk','apk'),('ASHFALL-Local-Maps.pck','map-pack'),('ASHFALL-Map-Adapters-R10.pck','map-pack')]:
            data=b'fixture only - no real APK or map';(root/name).write_bytes(data)
            files.append(dict(path=name,kind=kind,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
        manifest=dict(android_package='org.alingold.ashfallquest',apk='ASHFALL-R15-Quest.apk',files=files)
        (root/'PACKAGE.json').write_text(json.dumps(manifest));return manifest
    def invoke(self,root,mode):
        return subprocess.run([str(POWERSHELL),'-NoProfile','-ExecutionPolicy','Bypass','-File',str(root/'Install-Quest.ps1'),mode],capture_output=True,text=True,timeout=30)
    def test_verify_and_plan_do_not_require_adb(self):
        with tempfile.TemporaryDirectory() as value:
            root=Path(value);self.bundle(root)
            for mode in ['-VerifyOnly','-PlanOnly']:
                result=self.invoke(root,mode);self.assertEqual(result.returncode,0,result.stderr)
                self.assertFalse((root/'platform-tools/adb.exe').exists())
    def test_corrupt_payload_refused(self):
        with tempfile.TemporaryDirectory() as value:
            root=Path(value);self.bundle(root);(root/'ASHFALL-Local-Maps.pck').write_bytes(b'corrupt')
            self.assertNotEqual(self.invoke(root,'-VerifyOnly').returncode,0)
    def test_wrong_app_refused(self):
        with tempfile.TemporaryDirectory() as value:
            root=Path(value);manifest=self.bundle(root);manifest['android_package']='com.other.application';(root/'PACKAGE.json').write_text(json.dumps(manifest))
            self.assertNotEqual(self.invoke(root,'-PlanOnly').returncode,0)
    def test_missing_pack_refused(self):
        with tempfile.TemporaryDirectory() as value:
            root=Path(value);manifest=self.bundle(root);manifest['files'].pop();(root/'PACKAGE.json').write_text(json.dumps(manifest))
            self.assertNotEqual(self.invoke(root,'-VerifyOnly').returncode,0)
if __name__=='__main__':unittest.main(verbosity=2)
