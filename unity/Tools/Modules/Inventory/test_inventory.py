import hashlib
import json
import struct
import tempfile
import unittest
from pathlib import Path
import inventory as tool

class PackTests(unittest.TestCase):
    def pack(self, path, content=b'{"maps":[{"id":"archive:Case-sensitive"}]}', digest=None, name=b'local_archive/catalog.json'):
        # Independent small fixture laid out as documented GDPC v4 header/data/directory.
        name += b'\0' * ((4-len(name)%4)%4)
        directory=128+len(content)
        header=struct.pack('<4sIIIIIQQ',b'GDPC',4,4,7,2,2,128,directory)
        entry=struct.pack('<I',len(name))+name+struct.pack('<QQ16sI',0,len(content),digest or hashlib.md5(content).digest(),0)
        path.write_bytes(header.ljust(128,b'\0')+content+struct.pack('<I',1)+entry)
        return path
    def test_seek_export_preserves_ids(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            pack=tool.Pck4(self.pack(Path(d)/'test.pck'))
            self.assertEqual(pack.json('local_archive/catalog.json')['maps'][0]['id'],'archive:Case-sensitive')
            self.assertEqual(len(pack.entries),1)
    def test_tampered_payload_rejected(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            pack=tool.Pck4(self.pack(Path(d)/'test.pck',digest=b'0'*16))
            with self.assertRaises(ValueError): pack.payload('local_archive/catalog.json')
    def test_oversized_export_rejected(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            pack=tool.Pck4(self.pack(Path(d)/'test.pck'))
            with self.assertRaises(ValueError): pack.payload('local_archive/catalog.json',limit=2)
    def test_path_traversal_rejected(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            with self.assertRaises(ValueError): tool.Pck4(self.pack(Path(d)/'test.pck',name=b'../catalog.json'))
    def test_truncated_directory_rejected(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            p=self.pack(Path(d)/'test.pck'); p.write_bytes(p.read_bytes()[:-10])
            with self.assertRaises(ValueError): tool.Pck4(p)
    def test_symbol_sections_ignore_inner_lambdas(self):
        rows=tool.symbols('extends Node\nfunc first():\n\tvar f = func(): return 1\n\treturn f.call()\nstatic func second():\n\treturn 2\n')
        self.assertEqual([r['symbol'] for r in rows],['first','second'])
        self.assertEqual(rows[0]['end_line'],4)
        self.assertNotEqual(rows[0]['body_sha256'],rows[1]['body_sha256'])
    def test_content_change_changes_symbol_identity(self):
        a=tool.symbols('func fire():\n\treturn 1\n')[0]
        b=tool.symbols('func fire():\n\treturn 2\n')[0]
        self.assertEqual(a['symbol'],b['symbol'])
        self.assertNotEqual(a['body_sha256'],b['body_sha256'])
    def test_noncanonical_resource_names(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            for name in [b'',b'\0'*4,b'.',b'..',b'...',b'./x',b'x/./y',b'x//y',b'x/',b'/x',b'res://',b'res://.',b'res://x//y',b'C:/x',b'x\\y',b'x\0y',b'x\ny']:
                with self.subTest(name=name), self.assertRaises(ValueError):
                    tool.Pck4(self.pack(Path(d)/'test.pck',name=name))
            for name in [b'x',b'res://folder/File.tscn',b'.hidden',b'folder/a..b']:
                pack=tool.Pck4(self.pack(Path(d)/'test.pck',name=name))
                self.assertIn(name.decode().removeprefix('res://'),pack.entries)
    def test_reviewer_header_entry_and_bounds_adversaries(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            p=Path(d)/'test.pck'
            original=self.pack(p).read_bytes()
            directory=struct.unpack_from('<Q',original,32)[0]
            length=struct.unpack_from('<I',original,directory+4)[0]
            for offset,value,fmt in [(20,4,'<I'),(directory+8+length+32,1,'<I'),(directory+8+length+32,2,'<I'),(directory+8+length,999999,'<Q')]:
                data=bytearray(original);struct.pack_into(fmt,data,offset,value);p.write_bytes(data)
                with self.subTest(offset=offset,value=value), self.assertRaises(ValueError): tool.Pck4(p)
            data=bytearray(original);struct.pack_into('<I',data,directory,2);data.extend(original[directory+4:]);p.write_bytes(data)
            with self.assertRaises(ValueError):tool.Pck4(p)
    def test_actual_nested_gameplay_changes(self):
        for tree in ['quest-r22/source','game']:
            p=tool.ROOT/tree/'scripts/dual_pistols.gd'
            text=p.read_text(encoding='utf-8-sig')
            changed=text.replace('_uzi_baseline_pitch=0.0;_uzi_prefix_time=0.0','_uzi_baseline_pitch=0.5;_uzi_prefix_time=0.0',1)
            self.assertNotEqual(text,changed)
            before={s['symbol']:s for s in tool.symbols(text)}
            after={s['symbol']:s for s in tool.symbols(changed)}
            for method in ['reset_weapon','fire','reload_flick','flick_sample']:
                self.assertIn('PairedUzi.'+method,before)
            self.assertNotEqual(before['PairedUzi.reset_weapon']['body_sha256'],after['PairedUzi.reset_weapon']['body_sha256'])
            self.assertEqual([k for k in before if before[k]!=after[k]],['PairedUzi.reset_weapon'])
            routes=tool.symbols((tool.ROOT/tree/'scripts/guard_routes.gd').read_text(encoding='utf-8-sig'))
            self.assertIn('Traversal.direction',[s['symbol'] for s in routes])
    def test_nested_scopes_and_string_lambda_exclusion(self):
        text='class A:\n    class B:\n        func fire():\n            var cb = func named(): return 1\n            return cb.call()\n    func fire():\n        pass\nfunc root():\n    var s = """\nfunc fake():\n"""\n    pass\n'
        rows=tool.symbols(text)
        self.assertEqual([s['symbol'] for s in rows],['A.B.fire','A.fire','root'])
        self.assertEqual(rows[0]['end_line'],5)
    def test_assignment_sources_and_ambiguity(self):
        proposals=tool.read(tool.EVIDENCE/'assignment-proposals.json')
        self.assertEqual(len(proposals),142)
        self.assertEqual(len({r['path'] for r in proposals}),76)
        ids={r['id'] for r in tool.read(tool.EVIDENCE/'source-symbols.json')}
        for row in proposals:
            self.assertTrue(Path(row['source']['absolute_path']).is_file())
            self.assertEqual(tool.sha(Path(row['source']['absolute_path'])),row['source']['sha256'])
            self.assertTrue(all(s in ids for s in row['symbols']))
            self.assertTrue(row['acceptance'])
            if Path(row['path']).stem in ['enemy','guard_routes','main','campaign_extension']:
                self.assertTrue(row['ambiguity'])
    def test_emitted_section_hashes_and_slice_references(self):
        rows=tool.read(tool.EVIDENCE/'source-symbols.json')
        ids={s['id'] for s in rows}
        self.assertEqual(len(ids),len(rows))
        for row in rows:
            lines=Path(row['source']['absolute_path']).read_text(encoding='utf-8-sig').splitlines()
            self.assertEqual(lines[row['line']-1],row['declaration'])
            self.assertEqual(hashlib.sha256(('\n'.join(lines[row['line']-1:row['end_line']])+'\n').encode()).hexdigest(),row['body_sha256'])
        slices=tool.read(tool.EVIDENCE/'behavior-features.json')
        for feature in slices:
            self.assertTrue(feature['sources'],feature['id'])
            self.assertTrue(all(i in ids for i in feature['sources']))
    def test_frozen_reviewer_pck_fixtures_read_only(self):
        root=tool.ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Inventory-r1'
        for name in ['valid-relative','valid-absolute']:
            self.assertEqual(list(tool.Pck4(root/(name+'.pck')).entries),['x'])
        for name in ['bad-header-flags','encrypted-entry','removed-entry','traversal','embedded-null','duplicate','past-data','empty-path','dot-path']:
            with self.subTest(name=name), self.assertRaises(ValueError): tool.Pck4(root/(name+'.pck'))
    def test_output_guard(self):
        with self.assertRaises(ValueError): tool.write(tool.ROOT/'forbidden.json',{})
    def test_actual_pack_against_historical_catalog(self):
        pack=tool.Pck4(tool.ROOT/'builds/ASHFALL-Local-Maps.pck')
        actual=pack.json('local_archive/catalog.json')['maps']
        historical=tool.read(tool.ROOT/'docs/agent-runs/20261104-0020/lead-u/inventory/installed-map-catalog.json')['entries']
        self.assertEqual([r['id'] for r in actual],[r['id'] for r in historical if r['kind']=='archive'])
    def test_export_determinism(self):
        paths=[tool.OUT/'identity-export-v1.json']+[tool.EVIDENCE/n for n in ['source-symbols.json','feature-acceptance.json','tree-reconciliation.json','resource-references.json','summary.json','behavior-features.json','asset-acceptance.json','symbol-reconciliation.json','registry-id-reconciliation.json','pack-index-evidence.json','assignment-proposals.json']]
        before={str(p):tool.sha(p) for p in paths}
        tool.run()
        self.assertEqual(before,{str(p):tool.sha(p) for p in paths})

if __name__=='__main__': unittest.main(verbosity=2)
