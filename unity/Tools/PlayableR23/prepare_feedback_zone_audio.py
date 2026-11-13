"""User-requested hum-only zone and original pad cue; no engine/device needed."""
from pathlib import Path
import hashlib,json,wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/StreamingAssets/PlayableR23'
SOURCE=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source/assets/cyber_bird/zone_r5/danger_zone_loop.wav')
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()=='3ad93fd0711159cebd2aa5b454acd7901450852be04e4c48402312efdf7fa4d1'
def write(path,rate,x):
    path.parent.mkdir(parents=True,exist_ok=True)
    pcm=np.rint(np.clip(x,-1,1)*32767).astype('<i2').tobytes()
    with wave.open(str(path),'wb') as w:
        w.setparams((1,2,rate,len(x),'NONE','not compressed'));w.writeframes(pcm)
    return dict(file=path.name,rate=rate,samples=len(x),pcmSha256=hashlib.sha256(pcm).hexdigest(),peak=float(np.max(abs(x))),rms=float(np.sqrt(np.mean(x*x))))
with wave.open(str(SOURCE),'rb') as w:
    assert w.getnchannels()==1 and w.getsampwidth()==2
    rate=w.getframerate();x=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float)/32768
f=np.fft.rfftfreq(len(x),1/rate)
# Periodic FFT preserves the original loop, its low hum and gain. Smooth
# rejection of broadband hiss above the hum; no realtime filter or gain boost.
weight=np.clip((f-25)/10,0,1)*(.5+.5*np.cos(np.pi*np.clip((f-165)/35,0,1)))
y=np.fft.irfft(np.fft.rfft(x)*weight,n=len(x))
hum=write(OUT/'MatchZone/danger_zone_hum.wav',rate,y)
PAD_SOURCE=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source/assets/audio/sfx/jump_pad_r8.wav')
assert hashlib.sha256(PAD_SOURCE.read_bytes()).hexdigest()=='01c0e17a606fc7ea7e02e989f05faa6d76f57d6f70e8677140d5c070cccc0f58'
(OUT/'JumpPads').mkdir(parents=True,exist_ok=True)
(OUT/'JumpPads/pad_launch.wav').write_bytes(PAD_SOURCE.read_bytes())
with wave.open(str(PAD_SOURCE),'rb') as w:
    assert w.getnchannels()==1 and w.getsampwidth()==2
    pad_rate=w.getframerate();pad_pcm=w.readframes(w.getnframes())
pad_samples=np.frombuffer(pad_pcm,dtype='<i2').astype(float)/32768
pad=dict(file='pad_launch.wav',rate=pad_rate,samples=len(pad_samples),pcmSha256=hashlib.sha256(pad_pcm).hexdigest(),peak=float(np.max(abs(pad_samples))),rms=float(np.sqrt(np.mean(pad_samples*pad_samples))))
report=dict(source=str(SOURCE),sourceSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),license='CC0-1.0',zone='Derivative: retain original low hum; reject hiss above200Hz, no normalization/gain boost.',hum=hum,pad=pad,padOrigin='Exact frozen R6 assets/audio/sfx/jump_pad_r8.wav; byte-for-byte copy, no synthesis/resampling.',padSource=str(PAD_SOURCE),padSourceSha256=hashlib.sha256(PAD_SOURCE.read_bytes()).hexdigest(),padSourceContentId='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520')
(OUT/'MatchZone/feedback-audio-provenance.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
