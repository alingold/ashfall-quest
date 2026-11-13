"""ASHFALL / Signal Through Snow. Original deterministic score, no samples.
Run with Python 3 + numpy. Produces synchronized 40-second stereo PCM16 stems.
"""
from pathlib import Path
import json, math, wave, hashlib
import numpy as np

ROOT = Path(__file__).resolve().parent
SR, BPM, BARS = 48000, 96, 16
BEAT = 60 / BPM
N = int(SR * BEAT * BARS * 4)
rng = np.random.default_rng(714203)
stems = {k: np.zeros((N, 2), dtype=np.float64) for k in ('exploration','combat','extraction')}

def freq(midi): return 440 * 2 ** ((midi-69)/12)

def add(stem, start, samples, pan=0, gain=1):
    # Wrapped releases/reverb carry across the loop boundary instead of cutting.
    p = np.array([math.cos((pan+1)*math.pi/4), math.sin((pan+1)*math.pi/4)])
    v = samples[:,None] * p * gain
    offset = int(round(start * SR)) % N
    first = min(len(v), N-offset)
    stems[stem][offset:offset+first] += v[:first]
    if first < len(v): stems[stem][:len(v)-first] += v[first:]

def note(kind, midi, length, velocity=1):
    t = np.arange(int(length*SR))/SR
    f = freq(midi)
    if kind == 'pad':
        y = sum(np.sin(2*np.pi*f*ratio*t+phase)*amp for ratio,phase,amp in
                [(0.998,0.3,.42),(1.002,1.2,.42),(2,0.5,.13),(3,0.8,.04)])
        env = np.minimum(t/0.8,1)*np.minimum((length-t)/1.3,1)
        y *= env * (.92+.08*np.sin(2*np.pi*.17*t))
    elif kind == 'piano':
        y = sum(np.sin(2*np.pi*f*h*(1+.00012*h*h)*t)*a*np.exp(-t*(1.1+h*.32))
                for h,a in [(1,.65),(2,.24),(3,.11),(4,.05),(6,.02)])
        y *= np.minimum(t/.008,1)*np.minimum((length-t)/.15,1)
    elif kind == 'string':
        y = sum(np.sin(2*np.pi*f*h*t+.003*np.sin(2*np.pi*5.2*t))*1/(h*h) for h in range(1,7))
        y *= np.minimum(t/.045,1)*np.exp(-t*5)*np.minimum((length-t)/.12,1)
    elif kind == 'bass':
        y = (np.sin(2*np.pi*f*t)+.16*np.sin(4*np.pi*f*t))*.7
        y *= np.minimum(t/.018,1)*np.minimum((length-t)/.1,1)*np.exp(-t*.8)
    else: # Breathy upper string-like lead, slower than a pulse oscillator.
        y = sum(np.sin(2*np.pi*f*h*t+.025*np.sin(2*np.pi*4.7*t))*a for h,a in [(1,.65),(2,.13),(3,.04)])
        y *= np.minimum(t/.18,1)*np.minimum((length-t)/.45,1)*np.exp(-t*.2)
    return y*velocity

def percussion(kind):
    length={'kick':.5,'snare':.32,'hat':.12,'tom':.7}[kind]
    t=np.arange(int(length*SR))/SR
    noise=rng.normal(0,1,len(t))
    if kind=='kick':
        phase=2*np.pi*(44*t+45*.028*(1-np.exp(-t/.028)))
        y=np.sin(phase)*np.exp(-t*10)+noise*.04*np.exp(-t*150)
    elif kind=='snare':
        bright=noise-np.convolve(noise,np.ones(17)/17,mode='same')
        y=bright*.23*np.exp(-t*20)+np.sin(2*np.pi*178*t)*.16*np.exp(-t*24)
    elif kind=='hat':
        bright=noise-np.convolve(noise,np.ones(7)/7,mode='same')
        y=bright*.08*np.exp(-t*55)
    else:
        y=np.sin(2*np.pi*(72*t+18*.07*(1-np.exp(-t/.07))))*np.exp(-t*7)
    return y*np.minimum(t/.002,1)*np.minimum((length-t)/.015,1)

# Dm(add9), Bbmaj7, F(add9), C(add9), Gm9, Bbmaj7, C(sus2), Am7.
# Each chord spans two bars; voices stay close for connected movement.
chords=[[50,57,60,64,69],[46,53,57,62,65],[48,53,57,60,67],[48,55,60,62,67],
        [43,50,57,58,62],[46,53,57,62,65],[48,55,60,62,67],[45,52,55,60,64]]
roots=[38,34,41,36,31,34,36,33]
for c,chord in enumerate(chords):
    start=c*8*BEAT
    for j,midi in enumerate(chord):
        add('exploration',start-.4,note('pad',midi,8*BEAT+1.5),pan=(j-2)*.32,gain=.08)
    # A sparse recurring four-note motif develops through harmony and register.
    motif=[chord[2]+12,chord[3]+12,chord[1]+12,chord[4]+12]
    for j,beat in enumerate([.5,2.5,4,6.5]):
        add('exploration',start+beat*BEAT,note('piano',motif[(j+c%2)%4],2.7),pan=(-.32 if j%2 else .32),gain=.115 if j!=2 else .15)
    for b in range(8):
        add('combat',start+b*BEAT,note('bass',roots[c],BEAT*.85),pan=0,gain=.18 if b%4==0 else .13)
        for sub in [0,.5]:
            midi=chord[(b+int(sub*2))%3]+12
            add('combat',start+(b+sub)*BEAT,note('string',midi,.42),pan=(-.5 if sub else .5),gain=.07 if sub else .10)
        if b%4 in [0,2] or (c%2 and b==7): add('combat',start+b*BEAT,percussion('kick'),gain=.31)
        if b%4 in [1,3]: add('combat',start+b*BEAT,percussion('snare'),pan=.1,gain=.32)
        for sub in [0,.5]: add('combat',start+(b+sub)*BEAT,percussion('hat'),pan=(-.4 if sub else .4),gain=.45 if sub else .65)
    if c%2==1:
        for b in [6.5,7,7.5]: add('combat',start+b*BEAT,percussion('tom'),pan=(b-7)*.9,gain=.12)
    # Broader, optimistic countermelody: long tones over the same harmony.
    lead=[chord[2]+12,chord[4]+12,chord[3]+12]
    for j,(beat,duration) in enumerate([(0,2.6),(3,2.1),(5.5,2.5)]):
        add('extraction',start+beat*BEAT,note('lead',lead[j],duration*BEAT+.4),pan=.12,gain=.12)
        add('extraction',start+beat*BEAT,note('piano',lead[j]-12,2.4),pan=-.3,gain=.09)
    add('extraction',start,percussion('tom'),pan=-.1,gain=.12)

# Circular stereo early reflections/tail: deterministic and seamless at wrap.
for name,data in stems.items():
    dry=data.copy()
    for delay,gain in [(0.173,.13),(.281,.10),(.419,.08),(.683,.055),(1.043,.035),(1.417,.02)]:
        data += np.roll(dry[:,::-1],int(delay*SR),axis=0)*gain
    data -= data.mean(axis=0)
    # Very low-frequency DC correction ensures exact endpoint continuity without
    # a hard silence gap; correction magnitude is reported below.
    seam=data[-1]-data[0]
    data -= np.linspace(-.5,.5,N)[:,None]*seam

combined=sum(stems.values())
scale=.78/max(float(np.abs(combined).max()),.01)
report={'sample_rate':SR,'bpm':BPM,'bars':BARS,'seconds':N/SR,'seed':714203,'shared_scale':scale,'stems':{}}
for name,data in stems.items():
    data*=scale
    pcm=np.round(np.clip(data,-1,1)*32767).astype('<i2')
    path=ROOT/(name+'.wav')
    with wave.open(str(path),'wb') as out:
        out.setnchannels(2);out.setsampwidth(2);out.setframerate(SR);out.writeframes(pcm.tobytes())
    peak=float(np.abs(data).max()); rms=float(np.sqrt(np.mean(data*data)))
    report['stems'][name]={'peak_dbfs':20*math.log10(max(peak,1e-12)),'rms_dbfs':20*math.log10(max(rms,1e-12)),
        'boundary_step_pcm':int(np.max(np.abs(pcm[0].astype(int)-pcm[-1].astype(int)))),
        'max_adjacent_step_pcm':int(np.max(np.abs(np.diff(pcm.astype(np.int32),axis=0)))),
        'frames':N,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
assert all(v['boundary_step_pcm']==0 for v in report['stems'].values())
assert float(np.abs(sum(stems.values())).max())<=.781
(ROOT/'analysis.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
