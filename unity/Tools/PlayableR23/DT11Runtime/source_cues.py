"""Offline port of frozen shotgun.gd::_bake_cue; never starts an engine.

Godot-generated PCM equality remains unqualified. The current published blast
is independently checked for byte preservation before this helper is adopted.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import wave

MANIFEST_SHA = 'b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
RATE = 32000
KINDS = ('blast', 'open', 'insert', 'close')
MASK = (1 << 64) - 1


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


class PCG:
    def __init__(self, seed, sequence=1442695040888963407):
        self.state = 0
        self.inc = ((sequence << 1) | 1) & MASK
        self.rand()
        self.state = (self.state + seed) & MASK
        self.rand()

    def rand(self):
        old = self.state
        self.state = (old * 6364136223846793005 + self.inc) & MASK
        value = (((old >> 18) ^ old) >> 27) & 0xffffffff
        rotation = old >> 59
        return ((value >> rotation) | (value << ((-rotation) & 31))) & 0xffffffff

    def randf(self):
        exponent = self.rand()
        if not exponent:
            return 0.0
        significand = f32(self.rand() | 0x80000001)
        return f32(math.ldexp(significand, -32 - (32 - exponent.bit_length())))


def seed_for(kind):
    if kind not in KINDS:
        raise ValueError('Unknown source DT11 cue: ' + kind)
    value = 5381
    for character in 'Cinder2/' + kind:
        value = (value * 33 + ord(character)) & 0xffffffff
    return value


def bake_cue(kind):
    rng = PCG(seed_for(kind))
    samples = int((.65 if kind == 'blast' else .2) * RATE)
    pcm = bytearray(samples * 2)
    low = 0.0
    for i in range(samples):
        t = i / RATE
        noise = f32(f32(rng.randf() * 2) - f32(1))
        low += (noise - low) * .12
        if kind == 'blast':
            value = noise * math.exp(-t * 45) * .55 + low * math.exp(-t * 8) * .5 + math.sin(math.tau * 72 * t) * math.exp(-t * 18) * .25
        else:
            decay = 38.0 if kind == 'close' else 24.0
            frequency = 320 if kind == 'insert' else 170
            value = (noise * .5 + math.sin(math.tau * frequency * t) * .2) * math.exp(-t * decay)
        struct.pack_into('<h', pcm, i * 2, int(max(-.85, min(.85, value)) * 32767))
    return bytes(pcm)


def write_cue(path, kind):
    pcm = bake_cue(kind)
    with wave.open(str(path), 'wb') as output:
        output.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        output.writeframes(pcm)
    return {'file': Path(path).name, 'kind': kind, 'seed': seed_for(kind),
            'rate': RATE, 'channels': 1, 'sampleWidth': 2, 'samples': len(pcm) // 2,
            'pcmSha256': hashlib.sha256(pcm).hexdigest(),
            'sha256': hashlib.sha256(Path(path).read_bytes()).hexdigest()}


def verify_source(source):
    source = Path(source)
    manifest = source.parent / 'SOURCE-MANIFEST.json'
    if hashlib.sha256(manifest.read_bytes()).hexdigest() != MANIFEST_SHA:
        raise ValueError('Unexpected frozen source manifest')
    pins = {row['path']: row['sha256'] for row in json.loads(manifest.read_text())['files']}
    result = {}
    for relative in ('scripts/shotgun.gd', 'scripts/dt11.gd', 'scripts/weapon.gd'):
        actual = hashlib.sha256((source / relative).read_bytes()).hexdigest()
        if actual != pins[relative]:
            raise ValueError('Frozen source changed: ' + relative)
        result[relative] = actual
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-root', type=Path, required=True)
    parser.add_argument('--output-root', type=Path, required=True)
    args = parser.parse_args()
    pins = verify_source(args.source_root)
    if args.output_root.exists():
        raise SystemExit('Preserve previous output; a fresh directory is required')
    args.output_root.mkdir(parents=True)
    cues = [write_cue(args.output_root / ('dt11-authored-' + kind + '.wav'), kind) for kind in KINDS]
    receipt = {'sourcePins': pins, 'sourceManifestSha256': MANIFEST_SHA, 'cues': cues,
               'engine': False, 'device': False, 'nativeSourcePCMCompared': False}
    (args.output_root / 'SOURCE-CUES.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt, indent=2))
