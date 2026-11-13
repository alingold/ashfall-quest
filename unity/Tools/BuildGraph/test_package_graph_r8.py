import copy
import json
import sys
import unittest
import tempfile
from pathlib import Path
import package_graph_r8 as r
from verify_r8 import preflight


class PackageGraphTests(unittest.TestCase):
    def test_guarded_input_openxr_management(self):
        source = '#if UNITY_6000_0_OR_NEWER\nusing UnityEngine.InputSystem;\nusing UnityEngine.XR.OpenXR.Features;\n#if UNITY_EDITOR\nusing UnityEngine.XR.Management;\n#endif\n#endif'
        self.assertEqual([], r.consumed(source, set()))
        self.assertEqual(['Unity.InputSystem','Unity.XR.OpenXR'], r.consumed(source, r.PROFILES['windows']))
        self.assertEqual(['Unity.InputSystem','Unity.XR.Management','Unity.XR.OpenXR'], r.consumed(source, r.PROFILES['editor']))

    def test_comments_strings_and_namespace_boundaries(self):
        self.assertEqual([], r.consumed('// Unity.Collections\nstring x="UnityEngine.InputSystem"; using Unity.CollectionsFake;', set()))

    def test_boolean_nested_else_elif(self):
        s = '#if (A && !B) || C\nusing Unity.Collections;\n#elif D\nusing Unity.Networking.Transport;\n#else\nusing Unity.Burst;\n#endif'
        self.assertEqual(['Unity.Collections'], r.consumed(s, {'A'}))
        self.assertEqual(['Unity.Networking.Transport'], r.consumed(s, {'A','B','D'}))
        self.assertEqual(['Unity.Burst'], r.consumed(s, set()))

    def test_unknown_expression_unbalanced_fail_closed(self):
        for text in ('#if A == B\n#endif', '#else', '#if A'):
            with self.assertRaises(ValueError): r.active_code(text, set())

    def test_platform_define_constraints(self):
        d = {'includePlatforms':['Editor'], 'defineConstraints':['UNITY_INCLUDE_TESTS','!BROKEN || FIXED']}
        self.assertFalse(r.eligible(d, 'Android', r.PROFILES['tests']))
        self.assertFalse(r.eligible(d, 'Editor', r.PROFILES['editor']))
        self.assertTrue(r.eligible(d, 'Editor', r.PROFILES['tests']))
        self.assertFalse(r.eligible(d, 'Editor', r.PROFILES['tests']|{'BROKEN'}))

    def test_declared_references_only_mutant(self):
        packages = {'Unity.InputSystem':Path('Input.dll'), 'Unity.XR.OpenXR':Path('OpenXR.dll')}
        d = {'references':['Core','Unity.InputSystem']}
        self.assertEqual([Path('Core.dll'),Path('Input.dll')], r.accessible(d, {'Core':Path('Core.dll')}, packages))
        mutant = copy.deepcopy(d); mutant['references'].remove('Unity.InputSystem')
        self.assertNotIn(Path('Input.dll'), r.accessible(mutant, {'Core':Path('Core.dll')}, packages))
        with self.assertRaises(ValueError): r.accessible({'references':['Unknown']}, {}, packages)

    def test_exact_proposal_preflight_mutations(self):
        with tempfile.TemporaryDirectory(dir=r.BASE) as folder:
            root = Path(folder); staged=root/'staged'; live=root/'live'; staged.mkdir()
            source=live/'unity/Assets/Ashfall/A.cs'; source.parent.mkdir(parents=True); source.write_text('original')
            target='unity/Assets/Ashfall/A.asmdef'
            payload=staged/'payload.json'; payload.write_text('{"name":"A"}')
            inventory=[{'path':'unity/Assets/Ashfall/A.cs','source':str(source),'sha256':r.b.digest(source.read_bytes())}]
            r.b.dump(staged/'input-inventory.json',inventory)
            plan={'preconditions':inventory,'operations':[{'target':target,'expectedBefore':None,'payload':'payload.json','sha256':r.b.digest(payload.read_bytes()),'rollback':{'operation':'remove-if-hash','sha256':r.b.digest(payload.read_bytes()),'beforePayload':None}}]}
            r.b.dump(staged/'proposal.json',plan)
            ps=r.b.digest((staged/'proposal.json').read_bytes()); ins=r.b.digest((staged/'input-inventory.json').read_bytes())
            self.assertEqual([],preflight(staged,live,ps,ins))
            source.write_text('drift'); self.assertTrue(preflight(staged,live,ps,ins));source.write_text('original')
            payload.write_text('tampered');self.assertTrue(preflight(staged,live,ps,ins));payload.write_text('{"name":"A"}')
            (live/target).write_text('occupied');self.assertTrue(preflight(staged,live,ps,ins));(live/target).unlink()
            plan['operations'][0]['target']='../escape';r.b.dump(staged/'proposal.json',plan)
            self.assertEqual(['trusted-plan-mismatch'],preflight(staged,live,ps,ins))


if __name__ == '__main__': unittest.main(verbosity=2)
