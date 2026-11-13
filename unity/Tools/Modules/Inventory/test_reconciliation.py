import hashlib
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import inventory as tool
from reconciliation import Snapshot, SourceDrift, verify_receipt
from reconciliation_ports import SourcePort

class MemorySources(SourcePort):
    def __init__(self, files):
        self.files = dict(files)
        self.reads = 0
        self.on_read = lambda port, path: None
    def members(self): return list(reversed(list(self.files)))
    def read_bytes(self, path):
        self.reads += 1
        self.on_read(self, path)
        return self.files[path]

class ReconciliationTests(unittest.TestCase):
    def test_edit_between_hash_and_parse_cannot_mix(self):
        p = MemorySources({'a':b'func first():\n return 1\n','b':b'func second():\n return 2\n'})
        def edit(port, path):
            if port.reads == 2: port.files['a'] = b'func first():\n return 99\n'
        p.on_read = edit
        with self.assertRaises(SourceDrift): Snapshot.capture(p)
    def test_edit_after_capture_rejects_and_original_bytes_remain(self):
        p = MemorySources({'a':b'func first():\n return 1\n'})
        s = Snapshot.capture(p)
        p.files['a'] = b'func first():\n return 2\n'
        self.assertEqual(tool.symbols(s.text('a'))[0]['body_sha256'],tool.symbols('func first():\n return 1\n')[0]['body_sha256'])
        self.assertEqual(s.digest('a'),hashlib.sha256(s.files['a']).hexdigest())
        with self.assertRaises(SourceDrift): s.validate()
        with self.assertRaises(TypeError): s.files['a'] = b'bad'
    def test_added_removed_and_renamed_scripts_reject(self):
        for change in ['add','remove','rename']:
            with self.subTest(change=change):
                p=MemorySources({'a':b'a','b':b'b'}); s=Snapshot.capture(p)
                if change=='add':p.files['c']=b'c'
                elif change=='remove':del p.files['a']
                else:p.files['c']=p.files.pop('a')
                with self.assertRaises(SourceDrift):s.validate()
    def test_same_size_edit_and_order_repeatability(self):
        p=MemorySources({'a':b'abc','b':b'def'})
        a=Snapshot.capture(p); b=Snapshot.capture(p)
        self.assertEqual(dict(a.files),dict(b.files))
        p.files['a']=b'xyz'
        with self.assertRaises(SourceDrift):a.validate()
    def test_real_generator_abort_never_publishes_pending_outputs(self):
        # Actual generator operates only on injected owned/in-memory source port.
        # Failure occurs after generate has buffered outputs, before publication.
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            dest=Path(d)/'sentinel.json';dest.write_bytes(b'incumbent')
            port=MemorySources({'fixture':b'original'})
            def generate():
                tool.write(dest,{'candidate':True})
                port.files['fixture']=b'changed'
            with patch.object(tool,'FileSources',return_value=port),patch.object(tool,'generate',side_effect=generate):
                with self.assertRaises(SourceDrift):tool.run()
            self.assertEqual(dest.read_bytes(),b'incumbent')
            self.assertIsNone(tool._SNAPSHOT)
            self.assertIsNone(tool._PENDING)
    def test_receipt_rejects_partial_publication_and_byte_mutation(self):
        with tempfile.TemporaryDirectory(dir=tool.OUT) as d:
            p=Path(d)/'a.json';p.write_bytes(b'ab')
            row={'path':str(p),'bytes':2,'sha256':hashlib.sha256(b'ab').hexdigest()}
            receipt={'outputs':[row]}
            self.assertTrue(verify_receipt(receipt))
            p.write_bytes(b'ac')
            with self.assertRaises(SourceDrift):verify_receipt(receipt)
            p.unlink()
            with self.assertRaises(SourceDrift):verify_receipt(receipt)
    def test_actual_capture_receipt_and_repeat_identity(self):
        before=tool.read(tool.EVIDENCE/'capture-receipt.json')
        self.assertTrue(verify_receipt(before))
        tool.run()
        after=tool.read(tool.EVIDENCE/'capture-receipt.json')
        self.assertEqual(before,after)
        self.assertTrue(verify_receipt(after))

if __name__=='__main__': unittest.main(verbosity=2)
