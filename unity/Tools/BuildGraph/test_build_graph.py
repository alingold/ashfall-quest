"""Deterministic filesystem/graph/real compiler tests; no Unity stubs."""
import json
from pathlib import Path
import shutil
import sys
import unittest
import build_graph as bg
from ports import Assembly, Graph

TEST_ROOT = bg.STAGE / 'tests01'


def assembly(name, refs=(), kind='neutral', guid=None):
    return Assembly(name, name, kind, sources=[name + '.cs'], references=list(refs),
                    guid=guid or bg.digest(name.encode())[:32])


class GraphTests(unittest.TestCase):
    def test_dag_and_order_independent(self):
        nodes = [assembly('Core'), assembly('Player', ['Core']), assembly('View', ['Player'], 'engine')]
        self.assertEqual([], bg.validate(Graph(nodes)))
        self.assertEqual([], bg.validate(Graph(list(reversed(nodes)))))

    def test_missing_reference(self):
        self.assertIn('missing-reference:A:Absent', bg.validate(Graph([assembly('A', ['Absent'])])))

    def test_duplicate_guid(self):
        a, b = assembly('A'), assembly('B')
        b.guid = a.guid.upper()
        self.assertTrue(any(x.startswith('duplicate-guid:') for x in bg.validate(Graph([a, b]))))

    def test_allocated_guid_collision(self):
        a = assembly('A')
        self.assertEqual(['duplicate-guid:A:existing'], bg.validate(Graph([a]), {a.guid: 'existing'}))

    def test_cycle(self):
        self.assertTrue(any(x.startswith('cycle:') for x in bg.validate(Graph([assembly('A', ['B']), assembly('B', ['C']), assembly('C', ['A'])]))))

    def test_editor_leak_transitive(self):
        errors = bg.validate(Graph([assembly('Player', ['Middle']), assembly('Middle', ['Tools']), assembly('Tools', kind='editor')]))
        self.assertIn('editor-leak:Middle:Tools', errors)

    def test_editor_api_leak(self):
        a = assembly('A')
        a.external = ['UnityEditor.AssetDatabase']
        self.assertIn('editor-api-leak:A', bg.validate(Graph([a])))

    def test_engine_api_leak(self):
        a = assembly('A')
        a.external = ['UnityEngine']
        self.assertIn('engine-api-leak:A', bg.validate(Graph([a])))

    def test_membership_duplicate(self):
        a, b = assembly('A'), assembly('B')
        b.sources = a.sources[:]
        self.assertIn('duplicate-membership:A.cs', bg.validate(Graph([a, b])))

    def test_comments_strings(self):
        text = '// using UnityEditor;\n/* UnityEngine */\nvar x = @"UnityEditor.Foo"; var y="using Ashfall.Bad;";'
        self.assertNotIn('UnityEditor', bg.code(text))
        self.assertNotIn('Ashfall.Bad', bg.code(text))

    def test_package_names(self):
        a = assembly('A', kind='engine')
        a.external = ['UnityEngine.InputSystem', 'Unity.Networking.Transport.Utilities', 'Unity.Collections']
        self.assertEqual(['Unity.Collections', 'Unity.InputSystem', 'Unity.Networking.Transport'], bg.package_refs(a))

    def test_real_source_factory_and_concrete_asmdef_validation(self):
        root = TEST_ROOT / 'fixture'
        files = {'Core/Contracts.cs': 'namespace Ashfall.Core { public class Data {} }',
                 'Modules/Demo/Main.cs': 'using Ashfall.Core; namespace Ashfall.Modules.Demo { public class Main { Data x; } }',
                 'Modules/Demo/Engine/View.cs': '#if UNITY_6000_0_OR_NEWER\nusing UnityEditor; namespace Ashfall.Modules.Demo { public class View {} }\n#endif',
                 'Modules/Toolchain/Probe.cs': 'namespace Ashfall.Modules.Toolchain { public class Probe {} }'}
        for name, text in files.items():
            path = root / 'unity/Assets/Ashfall' / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text)
        graph, guids = bg.generate(root)
        self.assertIn('editor-api-leak:Ashfall.Modules.Demo.Engine', bg.validate(graph, guids))
        by_name = {a.name: a for a in graph.assemblies}
        self.assertEqual(['Ashfall.Core'], by_name['Ashfall.Modules.Demo'].references)
        self.assertEqual('editor', by_name['Ashfall.Modules.Toolchain'].kind)
        out = TEST_ROOT / 'staged'
        out.mkdir()
        with self.assertRaises(ValueError):
            bg.stage(graph, out)
        self.assertEqual([], list(out.iterdir()))
        by_name['Ashfall.Modules.Demo.Engine'].external = []
        bg.stage(graph, out)
        # Materialize the proposed metadata only in this fixture for validator tests.
        proposed = out / 'proposed'
        for path in proposed.rglob('*.meta.proposed'):
            shutil.copyfile(path, path.with_suffix(''))
        self.assertEqual([], bg.validate_asmdefs(proposed))
        core = next(proposed.rglob('Ashfall.Core.asmdef'))
        demo = next(proposed.rglob('Ashfall.Modules.Demo.asmdef'))
        core_guid = by_name['Ashfall.Core'].guid
        data = json.loads(demo.read_text())
        data['references'] = ['GUID:' + core_guid]
        bg.dump(demo, data)
        self.assertEqual([], bg.validate_asmdefs(proposed))
        data['references'] = ['GUID:' + '0' * 32]
        bg.dump(demo, data)
        self.assertTrue(any('missing-reference' in x for x in bg.validate_asmdefs(proposed)))
        shutil.copyfile(Path(str(core) + '.meta'), Path(str(demo) + '.meta'))
        self.assertTrue(any('duplicate-guid' in x for x in bg.validate_asmdefs(proposed)))

    def test_actual_compiler_missing_reference_and_warning(self):
        run = TEST_ROOT / 'compiler'
        run.mkdir()
        compiler = bg.ToolchainSnapshotCompiler(run)
        dep = run / 'Dep.cs'
        use = run / 'Use.cs'
        dep.write_text('namespace Dep { public class Value {} }')
        use.write_text('public class Use { public Dep.Value Read() { return new Dep.Value(); } }')
        self.assertEqual(0, compiler.compile([dep], run / 'separate-Dep', []))
        self.assertNotEqual(0, compiler.compile([use], run / 'separate-Missing', []))
        self.assertEqual(0, compiler.compile([use], run / 'separate-Use', [run / 'separate-Dep/Dep.dll']))
        warning = run / 'Warning.cs'
        warning.write_text('public class Warning { public void Run() { int unused = 7; } }')
        self.assertNotEqual(0, compiler.compile([warning], run / 'warning'))
        self.assertIn('CS0219', (run / 'warning.log').read_text())
        self.assertIn('CS0246', (run / 'separate-Missing.log').read_text())

    def test_outside_output_refused_before_write(self):
        with self.assertRaises(ValueError):
            bg.factory(bg.ROOT / 'never-write-buildgraph')

    def test_traversal_output_refused(self):
        with self.assertRaises(ValueError):
            bg.factory(bg.STAGE / '../../../../../never-write-buildgraph')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        raise SystemExit('test_build_graph.py <fresh-owned-test-name>')
    TEST_ROOT = bg.STAGE / sys.argv.pop()
    if TEST_ROOT.exists() or TEST_ROOT.parent != bg.STAGE:
        raise SystemExit('Fresh direct child required')
    TEST_ROOT.mkdir(parents=True)
    unittest.main(verbosity=2)
