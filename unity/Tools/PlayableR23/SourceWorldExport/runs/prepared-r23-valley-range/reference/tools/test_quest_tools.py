"""Offline tests of the merged build contracts using synthetic APK/zip fixtures."""
import importlib.util, struct, tempfile, unittest, zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
def module(name):
    spec=importlib.util.spec_from_file_location(name,HERE/(name+'.py'))
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
build=module('build_quest');setup=module('setup_quest')

class QuestContracts(unittest.TestCase):
    def test_archive_paths(self):
        for value in ['../escape','x/../../escape','/absolute','C:/drive','x\\..\\escape','x\0bad']:
            with self.subTest(value=value),self.assertRaises(SystemExit):setup.safe_parts(value)
        self.assertEqual(setup.safe_parts('asset/addons/plugin/file'),['asset','addons','plugin','file'])
    def test_command_line_parser(self):
        self.assertIsNone(build.decode_cl(b''));self.assertIsNone(build.decode_cl(struct.pack('<I',99)))
        raw=b'--';self.assertEqual(build.decode_cl(struct.pack('<II',1,len(raw))+raw),['--'])
        self.assertIsNone(build.decode_cl(struct.pack('<II',1,300)+raw))
    def fixture(self,root,changes=None,remove=()):
        script=root/'scripts/test.gd';script.parent.mkdir(exist_ok=True);script.write_text('extends Node\n')
        rows={'lib/arm64-v8a/libopenxr_loader.so':b'fixture-only', 'lib/arm64-v8a/libgodot_android.so':b'fixture-only','assets/scripts/test.gd.remap':b'[remap]\npath="res://scripts/test.gdc"\n','assets/scripts/test.gdc':b'fixture-only'}
        args=['--xr_mode_openxr','--',*build.USER_FLAGS]
        rows['assets/_cl_']=struct.pack('<I',len(args))+b''.join(struct.pack('<I',len(v.encode()))+v.encode() for v in args)
        for name in build.REQUIRED_DATA:
            file=root/name;file.parent.mkdir(parents=True,exist_ok=True);file.write_text('{"fixture":true}\n');rows['assets/'+name]=file.read_bytes()
        rows.update(changes or {})
        for name in remove:rows.pop(name,None)
        path=root/'test.apk'
        with zipfile.ZipFile(path,'w') as archive:
            for key,value in rows.items():archive.writestr(key,value)
        return path
    def test_apk_positive(self):
        with tempfile.TemporaryDirectory() as temp:
            old=build.ROOT;build.ROOT=Path(temp)
            try:
                report=build.audit_apk(self.fixture(build.ROOT));self.assertEqual(report['abi'],'arm64-v8a');self.assertEqual(report['script_count'],1)
            finally:build.ROOT=old
    def test_apk_rejections(self):
        cases=[({},['lib/arm64-v8a/libopenxr_loader.so']),({'lib/arm64-v8a/libopenxr_loader.so':b''},[]),({'lib/x86/libwrong.so':b'x'},[]),({'assets/scripts/test.gd.remap':b'path="res://wrong.gdc"\n'},[]),({},['assets/scripts/test.gdc']),({'assets/_cl_':b'bad'},[]),({},['assets/'+build.REQUIRED_DATA[0]]),({'assets/'+build.REQUIRED_DATA[0]:b'{"changed":true}'},[])]
        for changes,remove in cases:
            with self.subTest(changes=list(changes),remove=remove),tempfile.TemporaryDirectory() as temp:
                old=build.ROOT;build.ROOT=Path(temp)
                try:
                    with self.assertRaises(RuntimeError):build.audit_apk(self.fixture(build.ROOT,changes,remove))
                finally:build.ROOT=old

if __name__=='__main__':unittest.main(verbosity=2)
