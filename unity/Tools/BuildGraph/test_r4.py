"""Production API behavior regressions, all scratch files inside owned staging."""
import copy
import json
from pathlib import Path
import sys
import unittest
import build_graph as bg
from ports import Assembly, Graph
from boundaries import path_errors

ROOT = bg.STAGE / sys.argv.pop() if len(sys.argv) == 2 else None
if ROOT is None or ROOT.exists() or ROOT.parent != bg.STAGE:
    raise SystemExit('fresh direct owned child required')
ROOT.mkdir(parents=True)


class Regression(unittest.TestCase):
    def setUp(self):
        self.root = ROOT / self._testMethodName
        self.root.mkdir()

    def put(self, name, text):
        p = self.root / name
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text)
        return p

    def define(self, directory, name, guid, **options):
        p = directory + '/' + name + '.asmdef'
        self.put(p, json.dumps(dict(name=name, **options)))
        self.put(p + '.meta', 'guid: ' + guid)

    def source(self, path):
        self.put(path, 'namespace Demo { public class ' + Path(path).stem + ' {} }')

    def test_casefold_and_path_escape_no_writes(self):
        for names in [('Ashfall.Foo', 'ashfall.foo'), ('Foo', '../escape')]:
            graph = Graph([Assembly(n, 'unity/Assets/' + n, 'neutral', sources=[n + '.cs'], guid=bg.digest(n.encode())[:32]) for n in names])
            out = self.root / 'absent'
            with self.assertRaises(ValueError):
                bg.stage(graph, out)
            self.assertFalse(out.exists())
        self.assertTrue(path_errors(['A/Foo.cs', 'a/Bar.cs']))
        self.assertTrue(path_errors(['A/Foo.cs', 'A/foo.cs']))

    def test_existing_collision_no_overwrite(self):
        a = Assembly('A', 'unity/Assets/A', 'neutral', guid='a' * 32)
        out = self.root / 'out'
        sentinel = out / 'projects/A.csproj'
        sentinel.parent.mkdir(parents=True)
        sentinel.write_bytes(b'user-owned')
        before = {p.relative_to(out).as_posix(): p.read_bytes() for p in out.rglob('*') if p.is_file()}
        with self.assertRaises(ValueError):
            bg.stage(Graph([a]), out)
        self.assertEqual(before, {p.relative_to(out).as_posix(): p.read_bytes() for p in out.rglob('*') if p.is_file()})

    def test_forward_guid_parent_asmref_and_nested_override(self):
        base = 'unity/Assets/Ashfall/Modules/Demo'
        self.define(base + '/ZTarget', 'Custom', 'a' * 32, noEngineReferences=True, includePlatforms=['WindowsStandalone64'])
        self.put(base + '/AForward/Use.asmref', json.dumps({'reference': 'GUID:' + 'a' * 32}))
        self.source(base + '/AForward/Child/One.cs')
        self.source(base + '/ZTarget/Deep/Two.cs')
        self.define(base + '/AForward/Child/Nested', 'Nested', 'b' * 32, references=['GUID:' + 'a' * 32], noEngineReferences=True, includePlatforms=['WindowsStandalone64'])
        self.source(base + '/AForward/Child/Nested/Three.cs')
        g, guids = bg.generate(self.root)
        self.assertEqual([], bg.validate(g, guids))
        nodes = {a.name: a for a in g.assemblies}
        self.assertEqual(2, len(nodes['Custom'].sources))
        self.assertEqual(1, len(nodes['Nested'].sources))
        self.assertEqual(['Custom'], nodes['Nested'].references)
        self.assertEqual('a' * 32, nodes['Custom'].guid)
        out = self.root / 'out'
        bg.stage(g, out)
        self.assertFalse(out.exists())
        self.assertEqual(['WindowsStandalone64'], nodes['Custom'].definition['includePlatforms'])

    def test_name_asmref_and_parent_asmdef(self):
        base = 'unity/Assets/Ashfall'
        self.define(base, 'Parent', 'a' * 32, noEngineReferences=True)
        self.source(base + '/Modules/Demo/Sub/Main.cs')
        self.put(base + '/Other/Ref.asmref', '{"reference":"Parent"}')
        self.source(base + '/Other/Child/Main.cs')
        g, guids = bg.generate(self.root)
        self.assertEqual([], bg.validate(g, guids))
        self.assertEqual(1, len(g.assemblies))
        self.assertEqual(2, len(g.assemblies[0].sources))

    def test_dangling_duplicate_boundary_and_invalid_guid(self):
        base = 'unity/Assets/Ashfall/Modules/Demo'
        self.put(base + '/Ref.asmref', '{"reference":"GUID:bad"}')
        self.source(base + '/Main.cs')
        g, guids = bg.generate(self.root)
        self.assertTrue(any('dangling-asmref' in e for e in bg.validate(g, guids)))
        self.assertFalse(any(a.sources for a in g.assemblies))
        self.define(base, 'Custom', 'a' * 32)
        self.assertTrue(any('duplicate-boundary' in e for e in bg.validate_asmdefs(self.root)))

    def test_duplicate_names_guids_cycles_editor_ownership(self):
        self.define('A', 'A', 'a' * 32, references=['GUID:' + 'b' * 32])
        self.define('B', 'B', 'b' * 32, references=['A'], includePlatforms=['Editor'])
        self.source('A/Editor/Main.cs')
        errors = bg.validate_asmdefs(self.root)
        for prefix in ('cycle:', 'editor-leak:', 'mixed-editor-player:'):
            self.assertTrue(any(e.startswith(prefix) for e in errors), errors)
        self.define('C', 'A', 'a' * 32)
        errors = bg.validate_asmdefs(self.root)
        self.assertTrue(any('duplicate-assembly' in e for e in errors))
        self.assertTrue(any('duplicate-guid' in e for e in errors))

    def test_platform_mismatch(self):
        self.define('A', 'A', 'a' * 32, references=['B'])
        self.define('B', 'B', 'b' * 32, includePlatforms=['WindowsStandalone64'])
        self.assertIn('platform-reference-mismatch:A:B', bg.validate_asmdefs(self.root))

    def test_plan_checksums_rollback_and_additions(self):
        self.source('unity/Assets/Ashfall/Core/Main.cs')
        graph, guids = bg.generate(self.root)
        graph.inputs = [{'path': 'unity/Assets/Ashfall/Core/Main.cs', 'sha256': bg.digest((self.root / 'unity/Assets/Ashfall/Core/Main.cs').read_bytes())}]
        out = self.root / 'out'
        bg.stage(graph, out)
        plan = bg.proposal_plan(graph, self.root, out)
        self.assertEqual([], bg.validate_plan(plan, self.root, out))
        payload = out / plan['operations'][0]['payload']
        original = payload.read_bytes()
        payload.write_bytes(b'tampered')
        self.assertTrue(any('payload-drift' in e for e in bg.validate_plan(plan, self.root, out)))
        payload.write_bytes(original)
        changed = copy.deepcopy(plan)
        changed['operations'][0]['rollback']['sha256'] = 'bad'
        self.assertTrue(any('rollback-mismatch' in e for e in bg.validate_plan(changed, self.root, out)))
        self.source('unity/Assets/Ashfall/Core/Added.cs')
        self.assertTrue(any('precondition-added' in e for e in bg.validate_plan(plan, self.root, out)))
        self.put('unity/Assets/Ashfall/Core/Main.cs', 'changed')
        self.assertTrue(any('precondition-drift' in e for e in bg.validate_plan(plan, self.root, out)))


unittest.main(verbosity=2)
