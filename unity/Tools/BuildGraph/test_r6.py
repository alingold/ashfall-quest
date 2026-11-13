"""R6 production graph/plan regressions; no alternate validator implementation."""
import copy
import json
from pathlib import Path
import sys
import unittest
import build_graph as bg
from boundaries import path_errors
from ports import Assembly, Graph

ROOT = bg.STAGE / sys.argv.pop()
if ROOT.exists() or ROOT.parent != bg.STAGE:
    raise SystemExit('fresh direct child required')
ROOT.mkdir(parents=True)


class R6(unittest.TestCase):
    def setUp(self):
        self.root = ROOT / self._testMethodName
        self.root.mkdir()

    def put(self, name, data):
        p = self.root / name
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(data, encoding='utf-8')

    def test_existing_identity_requires_boundary(self):
        directory = 'unity/Assets/Ashfall/Elsewhere'
        self.put(directory + '/Demo.asmdef', json.dumps({'name': 'Ashfall.Modules.Demo', 'noEngineReferences': True}))
        self.put(directory + '/Demo.asmdef.meta', 'guid: ' + 'a' * 32)
        self.put('unity/Assets/Ashfall/Modules/Demo/Main.cs', 'public class Main {}')
        g, ids = bg.generate(self.root)
        self.assertTrue(any('inferred-assembly-identity-collision:' in e for e in bg.validate(g, ids)))
        self.assertEqual([], g.assemblies[0].sources)
        with self.assertRaises(ValueError):
            bg.stage(g, self.root / 'output')
        self.assertFalse((self.root / 'output').exists())
        self.put('unity/Assets/Ashfall/Modules/Demo/Link.asmref', '{"reference":"Ashfall.Modules.Demo"}')
        g, ids = bg.generate(self.root)
        self.assertEqual([], bg.validate(g, ids))
        self.assertEqual(['unity/Assets/Ashfall/Modules/Demo/Main.cs'], g.assemblies[0].sources)

    def test_windows_aliases(self):
        for p in ('A/./B.cs', 'A/../B.cs', 'A/B.cs.', 'A/B.cs ', 'A/CON.cs',
                  'A/NUL', 'A/COM1.txt', 'A/B.cs:stream', 'A\\B.cs', 'A//B.cs',
                  'A/B?.cs', '/A/B.cs', 'A/CONTRA~1.cs'):
            self.assertTrue(path_errors([p]), p)
        self.assertEqual([], path_errors(['A/Console.cs', 'A/B.cs']))

    def test_stage_unowned_scope_before_write(self):
        for directory in ('game', 'unity/Assets/Other', 'unity/ProjectSettings'):
            a = Assembly('A', directory, 'neutral', guid='a'*32)
            out = self.root / 'output'
            with self.assertRaises(ValueError):
                bg.stage(Graph([a]), out)
            self.assertFalse(out.exists())

    def test_reference_free_intrinsic_constraints(self):
        for definition, prefix in (({'includePlatforms':['Editor'], 'excludePlatforms':['Android']}, 'invalid-platform-constraints:'),
                ({'includePlatforms':'Editor'}, 'invalid-platform-field:'),
                ({'includePlatforms':['FutureOS']}, 'unsupported-platform-constraints:'),
                ({'excludePlatforms':['Android', 'Android']}, 'unsupported-platform-constraints:')):
            a = Assembly('A', 'unity/Assets/A', 'neutral', guid='a'*32, definition=definition)
            self.assertTrue(any(e.startswith(prefix) for e in bg.validate(Graph([a]))))
            self.put('A/A.asmdef', json.dumps(dict(name='A', **definition)))
            self.put('A/A.asmdef.meta', 'guid: ' + 'a'*32)
            self.assertTrue(any(e.startswith(prefix) for e in bg.validate_asmdefs(self.root)))

    def plan(self):
        self.put('unity/Assets/Ashfall/Core/C.cs', 'namespace Ashfall.Core { public class C {} }')
        g, _ = bg.generate(self.root)
        g.inputs = [{'path':'unity/Assets/Ashfall/Core/C.cs', 'sha256':bg.digest((self.root/'unity/Assets/Ashfall/Core/C.cs').read_bytes())}]
        out = self.root / 'out'
        bg.stage(g, out)
        plan = bg.proposal_plan(g, self.root, out)
        self.assertEqual([], bg.validate_plan(plan, self.root, out))
        return plan, out

    def test_target_scope_payload_and_rollback(self):
        plan, out = self.plan()
        for target in ('game/unowned.asmdef', 'unity/Assets/Other/A.asmdef',
                       'unity/ProjectSettings/A.asmdef', 'unity/Assets/Ashfall/Core/Other.asmdef'):
            changed = copy.deepcopy(plan)
            changed['operations'][0]['target'] = target
            self.assertTrue(bg.validate_plan(changed, self.root, out), target)
        for field, value in (('scope','game'), ('schema','BuildGraph/staged-plan/2'), ('stack',{})):
            changed = copy.deepcopy(plan); changed[field] = value
            self.assertTrue(bg.validate_plan(changed, self.root, out))
        changed = copy.deepcopy(plan)
        op = changed['operations'][0]
        payload = out / op['payload']
        payload.write_bytes(b'changed')
        op['sha256'] = bg.digest(payload.read_bytes())
        op['rollback']['sha256'] = op['sha256']
        self.assertTrue(any('target-payload-binding:' in e for e in bg.validate_plan(changed, self.root, out)))

    def test_all_rejections_leave_inputs_unchanged(self):
        plan, out = self.plan()
        before = {p:p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        changed = copy.deepcopy(plan)
        changed['operations'][0]['payload'] = '../escape'
        self.assertTrue(bg.validate_plan(changed, self.root, out))
        changed = copy.deepcopy(plan)
        changed['operations'][0]['rollback'] = {'operation':'delete'}
        self.assertTrue(bg.validate_plan(changed, self.root, out))
        self.assertEqual(before, {p:p.read_bytes() for p in self.root.rglob('*') if p.is_file()})

    def test_missing_precondition_and_malformed_fail_closed(self):
        plan, out = self.plan()
        self.assertTrue(bg.validate_plan({}, self.root, out))
        malformed = copy.deepcopy(plan)
        malformed['preconditions'][0]['path'] = 42
        self.assertTrue(bg.validate_plan(malformed, self.root, out))
        changed = copy.deepcopy(plan)
        changed['preconditions'] = [{'path':'game/C.cs','sha256':'A'*64}]
        self.assertTrue(bg.validate_plan(changed, self.root, out))
        (self.root/'unity/Assets/Ashfall/Core/C.cs').write_bytes(b'changed')
        self.assertTrue(any('precondition-drift:' in e for e in bg.validate_plan(plan, self.root, out)))


unittest.main(verbosity=2)
