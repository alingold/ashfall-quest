import hashlib
import json
import pathlib
import sys
import tempfile
import unittest
import wave
from build_manifest import build

class ImportTests(unittest.TestCase):
    def test_accepted_identity_and_rejections(self):
        # Temporary output stays inside the explicitly owned staging directory.
        parent=pathlib.Path(__file__).resolve().parents[4]/'campaign-staging/20261104-three-chats/unity/Audio'
        with tempfile.TemporaryDirectory(dir=parent) as d:
            root=pathlib.Path(d);wav=root/'original.wav'
            with wave.open(str(wav),'wb') as w:
                w.setparams((2,2,32000,0,'NONE','not compressed'));w.writeframes(bytes(16))
            row=dict(route='generic/glide',variant=0,source=str(wav),sha256=hashlib.sha256(wav.read_bytes()).hexdigest().upper(),bus='SFX',loop=True)
            inventory=root/'inventory.json';inventory.write_text(json.dumps(dict(trees=[dict(assets=[row])])) )
            receipt=root/'receipt.json'
            data=dict(inventory_sha256=hashlib.sha256(inventory.read_bytes()).hexdigest(),approval='accepted:test-only',version='fixture1',license='test original',rows=[dict(tree=0,route='generic/glide',variant=0)])
            def save(): receipt.write_text(json.dumps(data))
            save();self.assertIn('\tSFX\t1\n',build(inventory,receipt))
            original_source=row['source']
            for bad in ('original.wav',original_source[2:],original_source[0]+':'+original_source[3:]):
                row['source']=bad
                inventory.write_text(json.dumps(dict(trees=[dict(assets=[row])])))
                data['inventory_sha256']=hashlib.sha256(inventory.read_bytes()).hexdigest();save()
                with self.assertRaises(ValueError):build(inventory,receipt)
            row['source']=original_source
            inventory.write_text(json.dumps(dict(trees=[dict(assets=[row])])))
            data['inventory_sha256']=hashlib.sha256(inventory.read_bytes()).hexdigest();save()
            data['approval']='unreviewed';save()
            with self.assertRaises(ValueError):build(inventory,receipt)
            data['approval']='accepted:test-only';data['inventory_sha256']='0'*64;save()
            with self.assertRaises(ValueError):build(inventory,receipt)
            data['inventory_sha256']=hashlib.sha256(inventory.read_bytes()).hexdigest();save();wav.write_bytes(b'changed')
            with self.assertRaises(ValueError):build(inventory,receipt)

if __name__=='__main__':unittest.main(testRunner=unittest.TextTestRunner(stream=sys.stdout,verbosity=2))
