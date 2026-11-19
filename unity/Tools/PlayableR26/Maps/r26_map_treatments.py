#!/usr/bin/env python3 -I
"""MAPKIT (r19): detect and apply the per-map gameplay treatments to an imported map.

Every treatment that the first 105 catalog maps received (hexagon weapon caches, WEAPONS sign
racks, jump pads, health stations, authored warm-up / deathmatch markers, hidden authoring
markers) comes from recognizable baked geometry in the source map. The Godot-side recovery
(reference/tools/r14/semantic_catalog.cjs, recover_caches_r17.cjs, archive_semantics_r8.gd) is
ported here to run on the Unity payload (Maps/<slug>/geometry.bytes + map.json). The detectors
reproduce the stored semantics of the original maps exactly (jump, health, warm-up, deathmatch)
and the stored WEAPONS boards (see `validate`).

  python3 -I r26_map_treatments.py scan   [--ids slug,slug|--r18] [--out DIR]
  python3 -I r26_map_treatments.py validate                       # detectors vs stored data, old maps
  python3 -I r26_map_treatments.py apply  [--ids slug,slug|--r18] [--repo DIR]
  python3 -I r26_map_treatments.py pods   [--ids slug,slug|--r18]    # pod-policy rows for maps without one
  python3 -I r26_map_treatments.py styles [--ids slug,slug|--r18]    # Styles/styles.json rows for maps without one
  python3 -I r26_map_treatments.py audit  [--ids slug,slug|--r18] [--out DIR]

`apply` rewrites, for the selected maps only (other rows are preserved byte for byte in value):
  Maps/<slug>/source-semantics.json  semantics + caches (the same sidecar the importer wrote for old maps)
  Loot/caches.json                   cache sites = red hexagon rings (when a map has any)
  Loot/weapon-boards-r18.json        WEAPONS sign boards (rack rows)
  HealthStations/stations.json       health stations
  JumpPads/pads.json (+provenance)   jump pads, via r26_map_data.py
  TeamLaunch/ArchiveRecovery/maps.json  authored warm-up / deathmatch markers
  World/Resources/R26MapAuthoringMarkers.json  hidden marker triangles (`parts`, triangle ranges)
Pod policy, scoreboards and spawn audits have their own tools; see Documentation/ADDING-MAPS.md.
"""
import sys, os, json, struct, collections, hashlib, importlib.util
import numpy as np

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
RED, WHITE, DARK, GREEN, RIM = 'p2a61c272', 'p054b1b60', 'p52c49b5d', 'p6da8d940', 'pee49fa59'
BASES = ('p8ad59c41', 'pd959969c')
SIGN_LETTERS = [48, 44, 44, 104, 176, 36, 236]   # W E A P O N S triangle counts


def paths(repo):
    sa = os.path.join(repo, 'Assets/StreamingAssets/PlayableR23')
    res = os.path.join(repo, 'Assets/Ashfall/Integration/Playable/World/Resources')
    return sa, res


# ------------------------------------------------------------------ geometry
def load(folder):
    m = json.load(open(os.path.join(folder, 'map.json')))
    stride = 72 if m['version'] == 2 else 48
    data = open(os.path.join(folder, 'geometry.bytes'), 'rb').read()
    magic, n = struct.unpack_from('<ii', data, 0)
    if magic != 0x52323357 or n != m['chunks']:
        raise ValueError('geometry header ' + folder)
    off, chunks = 8, []
    for c in range(n):
        mat, flags, vis, nv, ni = struct.unpack_from('<iiiii', data, off); off += 20
        raw = np.frombuffer(data, dtype='<f4', count=nv * stride // 4, offset=off).reshape(nv, stride // 4); off += nv * stride
        idx = np.frombuffer(data, dtype='<i4', count=ni, offset=off).reshape(-1, 3); off += ni * 4
        chunks.append(dict(i=c, mat=m['materials'][mat]['name'], flags=flags, vis=vis,
                           pos=raw[:, 0:3].astype(np.float64), col=raw[:, 8:12].astype(np.float64), idx=idx))
    return m, chunks


def comp_rows(P):
    """Connected components of triangles P (T,3,3), vertices welded at 1 mm. Unity coordinates."""
    if len(P) == 0:
        return []
    q = np.round(P.reshape(-1, 3) * 1000).astype(np.int64)
    _, inv = np.unique(q, axis=0, return_inverse=True)
    inv = inv.reshape(-1, 3)
    par = np.arange(inv.max() + 1)
    a = np.concatenate([inv[:, 0], inv[:, 0]]); b = np.concatenate([inv[:, 1], inv[:, 2]])
    while True:
        pa, pb = par[a], par[b]
        if (pa == pb).all():
            break
        mn = np.minimum(pa, pb)
        np.minimum.at(par, pa, mn); np.minimum.at(par, pb, mn)
        while True:
            nxt = par[par]
            if (nxt == par).all():
                break
            par = nxt
    lab = par[inv[:, 0]]
    ul, inv2 = np.unique(lab, return_inverse=True)
    order = np.argsort(inv2, kind='stable'); bounds = np.searchsorted(inv2[order], np.arange(len(ul) + 1))
    rows = []
    for k in range(len(ul)):
        tri = order[bounds[k]:bounds[k + 1]]
        T = P[tri]; lo = T.min(axis=(0, 1)); hi = T.max(axis=(0, 1))
        rows.append(dict(tri=tri, n=len(tri), lo=lo, hi=hi, size=hi - lo, center=(lo + hi) / 2))
    rows.sort(key=lambda r: int(r['tri'][0]))      # source order: first triangle
    return rows


def sub(ch, pred):
    Ts, src, tix = [], [], []
    for ck in ch:
        if pred(ck) and len(ck['idx']):
            T = ck['pos'][ck['idx']]; Ts.append(T); src += [ck['i']] * len(T); tix += list(range(len(T)))
    if not Ts:
        return np.zeros((0, 3, 3)), np.zeros(0, int), np.zeros(0, int)
    return np.vstack(Ts), np.array(src), np.array(tix)


def nverts(P, r):
    return len(np.unique(np.round(P[r['tri']].reshape(-1, 3) * 1000).astype(np.int64), axis=0))


def plane(pts):
    c = pts.mean(0); n = np.linalg.svd(pts - c)[2][2]
    if n[1] < 0:
        n = -n
    d = (pts - c) @ n
    return c, n, float(d.max() - d.min())


# ------------------------------------------------------------------ detectors (Unity coordinates)
def isring(r):
    return 150 <= r['n'] <= 220 and r['size'][1] < .08 and min(r['size'][0], r['size'][2]) > 2.8 and max(r['size'][0], r['size'][2]) < 3.7


def isquestion(r):
    thin, w = min(r['size'][0], r['size'][2]), max(r['size'][0], r['size'][2])
    return (r['n'] in (107, 112) and abs(r['size'][1] - .39) < .015 and thin < .07 and w < .30) or \
           (r['n'] == 28 and abs(r['size'][1] - .1) < .015 and thin < .07 and w < .12)


def red_caches(ch):
    """Red hexagon rings = weapon cache sites; question glyphs and parts near rings = marker triangles."""
    P, src, tix = sub(ch, lambda c: c['mat'] == RED)
    rows = comp_rows(P)
    rings = [r for r in rows if isring(r)]
    mark = np.zeros(len(P), bool)
    for r in rings + [r for r in rows if isquestion(r)]:
        mark[r['tri']] = True
    rc = np.array([r['center'] for r in rings]) if rings else np.zeros((0, 3))
    for r in rows:
        if mark[r['tri']].all() or not len(rc):
            continue
        if r['size'][1] < .5 and max(r['size'][0], r['size'][2]) < .4:
            d = np.hypot(rc[:, 0] - r['center'][0], rc[:, 2] - r['center'][2]); dy = np.abs(rc[:, 1] - r['center'][1])
            if ((d < 2) & (dy < .6)).any():
                mark[r['tri']] = True
    return rings, src, tix, mark


def spawn_markers(ch, mat, dark):
    """White annulus + arrow = warm-up marker; dark annulus + arrow + humanoid head = deathmatch marker."""
    P, src, tix = sub(ch, lambda c: c['mat'] == mat)
    rows = comp_rows(P); out = []; mark = np.zeros(len(P), bool)
    for r in rows:
        if not (r['n'] == 104 and r['size'][1] < .1 and .95 < r['size'][0] < 1.5 and .95 < r['size'][2] < 1.5):
            continue
        parts = [q for q in rows if q['lo'][1] >= r['lo'][1] - .07 and q['hi'][1] < r['center'][1] + 2.15 and max(q['size']) < 2.15
                 and np.hypot(q['center'][0] - r['center'][0], q['center'][2] - r['center'][2]) < 1.25]
        arrows = [q for q in parts if q['n'] == 6 and q['size'][1] < .1 and .4 < np.linalg.norm(q['center'] - r['center']) < 1.1]
        head = any(q['n'] == 26 and .15 < q['size'][1] < .35 and 1.3 < q['center'][1] - r['center'][1] < 1.95 for q in parts)
        if not arrows or (dark and not head):
            continue
        d = arrows[0]['center'] - r['center']; d[1] = 0; d /= max(np.linalg.norm(d), 1e-9)
        out.append(dict(center=r['center'], forward=d))
        for q in ([r] + arrows + (parts if dark else [])):
            mark[q['tri']] = True
    return out, src, tix, mark


GOLD = lambda col: (col[:, :, 0] > .99) & (np.abs(col[:, :, 1] - .77647) < .012) & (col[:, :, 2] < .012)


def gold_comps(ch):
    """Gold (vertex colour) triangles of the 'vc' material: P, components, and each triangle's (chunk, index)."""
    Ts, src, tix = [], [], []
    for ck in ch:
        if ck['mat'] != 'vc' or not len(ck['idx']):
            continue
        T = ck['pos'][ck['idx']]; col = ck['col'][ck['idx']]; g = np.all(GOLD(col), axis=1)
        Ts.append(T[g]); src += [ck['i']] * int(g.sum()); tix += np.flatnonzero(g).tolist()
    P = np.vstack(Ts) if Ts else np.zeros((0, 3, 3))
    return P, comp_rows(P), np.array(src, int), np.array(tix, int)


def health_stations(ch, gold):
    P, rows, _, _ = gold
    bars = [r for r in rows if r['n'] == 12 and max(r['size']) < 1]
    G, _, _ = sub(ch, lambda c: c['mat'] == GREEN)
    greens = [r for r in comp_rows(G) if r['n'] == 6 and nverts(G, r) == 7 and 1 < max(r['size']) < 2.7]
    bases = []
    for bm in BASES:
        B, _, _ = sub(ch, lambda c, bm=bm: c['mat'] == bm)
        bases += [r for r in comp_rows(B) if r['n'] in (108, 126)]
    out = []
    for g in greens:
        cr = [r for r in bars if g['center'][1] + .25 < r['center'][1] < g['center'][1] + 1.5
              and np.hypot(r['center'][0] - g['center'][0], r['center'][2] - g['center'][2]) < .22]
        pair = any(np.linalg.norm(a['center'] - b['center']) < .08 and abs(a['size'][1] - b['size'][1]) > .25
                   for i, a in enumerate(cr) for b in cr[i + 1:])
        if pair and any(np.linalg.norm(b['center'] - g['center']) < .35 for b in bases):
            c, n, _ = plane(G[g['tri']].reshape(-1, 3))
            out.append(dict(center=c, normal=n, radius=1.12, decor=np.concatenate([r['tri'] for r in cr])))
    return out


def jump_pads(ch, gold):
    P, rows, _, _ = gold
    arrows = [r for r in rows if r['n'] == 52 and nverts(P, r) == 15 and max(r['size']) < 2]
    R, _, _ = sub(ch, lambda c: c['mat'] == RIM); rim = comp_rows(R)
    out, used = [], set()
    for i, a in enumerate(arrows):
        if i in used:
            continue
        pa = plane(P[a['tri']].reshape(-1, 3))
        if pa[2] > .16:
            continue
        pair, best = -1, 9
        for j in range(i + 1, len(arrows)):
            if j in used:
                continue
            b = arrows[j]; d = np.linalg.norm(a['center'] - b['center'])
            if d < .35 or d > .85:
                continue
            pb = plane(P[b['tri']].reshape(-1, 3))
            if abs(pa[1] @ pb[1]) < .985 or abs((b['center'] - a['center']) @ pa[1]) > .08:
                continue
            if d < best:
                best, pair = d, j
        if pair < 0 or not any(np.linalg.norm(r['center'] - a['center']) < 2 for r in rim):
            continue
        b = arrows[pair]; pb = plane(P[b['tri']].reshape(-1, 3))
        center, normal, face = (pa[0] + pb[0]) / 2, pa[1], 'chevron-plane fallback'
        fr = [(r, plane(R[r['tri']].reshape(-1, 3))) for r in rim if r['n'] == 12 and nverts(R, r) == 12 and np.linalg.norm(r['center'] - center) < 1.7]
        rm = sorted([f for f in fr if f[1][2] < .008 and max(f[0]['size']) > 1.2], key=lambda f: np.linalg.norm(f[0]['center'] - center))
        if rm:
            center, normal, face = rm[0][1][0], rm[0][1][1], 'thin hexagonal launch rim'
        else:
            sup = [f for f in fr if .04 < f[1][2] < .09]
            if len(sup) == 3:
                center, normal, _ = plane(np.array([f[1][0] for f in sup])); face = 'three triangular launch-frame supports'
        if normal[1] < .001:
            front = (a['center'] + b['center']) / 2
            if normal @ (front - center) < 0:
                normal = -normal; normal[1] = abs(normal[1]); face += '; chevron-front facing'
        used |= {i, pair}
        out.append(dict(decor=np.concatenate([a['tri'], b['tri']]), center=center, normal=normal, radius=1.3, speed=45, evidence='paired gold52-triangle chevrons +jumppad material; ' + face, tilted=bool(normal[1] < .985)))
    return out


def weapon_boards(ch):
    """Backing board (12-triangle box) carrying seven letter components W,E,A,P,O,N,S in a row."""
    signs = {}
    for mat in set(c['mat'] for c in ch):
        P, _, _ = sub(ch, lambda c, mat=mat: c['mat'] == mat)
        if not len(P):
            continue
        rows = [r for r in comp_rows(P) if r['n'] in (48, 44, 104, 176, 36, 236) and r['size'][1] < 2.5]
        for w in [r for r in rows if r['n'] == 48]:
            for axis in (0, 2):
                other = 2 - axis
                for sgn in (1, -1):
                    chain, ok = [w], True
                    for need in SIGN_LETTERS[1:]:
                        prev = chain[-1]['center'][axis]
                        nxt = [r for r in rows if r['n'] == need and abs(r['center'][1] - w['center'][1]) < .2
                               and abs(r['center'][other] - w['center'][other]) < .25 and .5 < (r['center'][axis] - prev) * sgn < 3.5]
                        if not nxt:
                            ok = False; break
                        chain.append(min(nxt, key=lambda r: (r['center'][axis] - prev) * sgn))
                    if ok:
                        lo = np.min([r['lo'] for r in chain], axis=0); hi = np.max([r['hi'] for r in chain], axis=0)
                        signs[tuple(np.round(np.r_[lo, hi], 2))] = dict(mat=mat, lo=lo, hi=hi, axis=axis)
    if not signs:
        return []
    P, src, _ = sub(ch, lambda c: True)
    cen = P.mean(axis=1); out = []
    for s in signs.values():
        c = (s['lo'] + s['hi']) / 2; ax = s['axis']; ot = 2 - ax
        sel = np.where(np.linalg.norm(cen - c, axis=1) < 9)[0]
        if not len(sel):
            continue
        mats = np.array([ch[k]['mat'] for k in src[sel]]); best = None
        for mm in set(mats.tolist()):
            for r in comp_rows(P[sel][mats == mm]):
                if r['n'] != 12 or not (s['hi'][ax] - s['lo'][ax] - .05 <= r['size'][ax] <= s['hi'][ax] - s['lo'][ax] + 3):
                    continue
                if r['size'][ot] > .9 or abs(r['center'][ax] - c[ax]) > 1.5 or abs(r['center'][1] - c[1]) > 1.2 or r['size'][1] < s['hi'][1] - s['lo'][1] - .05:
                    continue
                d = np.linalg.norm(r['center'] - c)
                if best is None or d < best[0]:
                    best = (d, r)
        if best is None:
            continue
        r = best[1]; normal = np.zeros(3); normal[ot] = 1 if c[ot] - r['center'][ot] >= 0 else -1
        out.append(dict(lo=r['lo'], hi=r['hi'], normal=normal))
    out.sort(key=lambda b: (round(float(b['lo'][1]), 1), tuple(np.round(b['lo'], 1))))
    return out


def glass_panels(ch, glass):
    """Glass window candidates (archive_semantics_r8 rule): count only; panel extraction is not ported."""
    count = 0
    for name in glass:
        P, _, _ = sub(ch, lambda c, name=name: c['mat'] == name)
        for r in comp_rows(P):
            wide = max(r['size'][0], r['size'][2])
            if .6 < wide <= 8 and .9 <= r['size'][1] < 5 and r['n'] <= 48:
                count += 1
    return count


def collision_arrays(ch, exclude=None):
    """Collision triangles; `exclude` = {chunk: set(triangle index)} drops decorative marker triangles."""
    A, B, C = [], [], []
    for ck in ch:
        if ck['flags'] and len(ck['idx']):
            T = ck['pos'][ck['idx']]
            if exclude and ck['i'] in exclude:
                T = T[~np.isin(np.arange(len(T)), list(exclude[ck['i']]))]
            A.append(T[:, 0]); B.append(T[:, 1]); C.append(T[:, 2])
    A, B, C = np.vstack(A), np.vstack(B), np.vstack(C)
    return A, B, C, np.minimum(np.minimum(A, B), C), np.maximum(np.maximum(A, B), C)


def surfaces_at(col, x, z):
    """Heights of every collision surface crossing the vertical line at (x,z)."""
    A, B, C, lo, hi = col
    sel = np.where((lo[:, 0] <= x) & (hi[:, 0] >= x) & (lo[:, 2] <= z) & (hi[:, 2] >= z))[0]
    if not len(sel):
        return np.zeros(0)
    a, b, c = A[sel], B[sel], C[sel]
    d = (b[:, 2] - c[:, 2]) * (a[:, 0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (a[:, 2] - c[:, 2])
    ok = np.abs(d) > 1e-12; d = np.where(ok, d, 1)
    l1 = ((b[:, 2] - c[:, 2]) * (x - c[:, 0]) + (c[:, 0] - b[:, 0]) * (z - c[:, 2])) / d
    l2 = ((c[:, 2] - a[:, 2]) * (x - c[:, 0]) + (a[:, 0] - c[:, 0]) * (z - c[:, 2])) / d
    l3 = 1 - l1 - l2
    inside = ok & (np.minimum(np.minimum(l1, l2), l3) >= -1e-6)
    return (l1 * a[:, 1] + l2 * b[:, 1] + l3 * c[:, 1])[inside]


def marker_viable(col, p):
    """Authored marker `p` (Unity coords): a floor 6 m below to 1 m above it with 1.9 m of clear headroom (as the runtime requires)."""
    ys = surfaces_at(col, p[0], p[2])
    floors = ys[(ys >= p[1] - 6) & (ys <= p[1] + 1)]
    if not len(floors):
        return False
    f = floors.max()
    return not ((ys > f + .25) & (ys < f + 1.9)).any()


def pad_obstructed(col, pad):
    """Contact (R23PadContacts) needs a clear segment from the player's feet to the pad centre. A source collision
    surface 0.05-0.75 m above the face (a cover plate, a forcefield, a floor over the hole) makes the pad dead:
    ice-and-fire pad 0/3 were walked onto in the rig and never launched. Magnum 3 pads were excluded the same way."""
    c, n = pad['center'], pad['normal']
    for dx, dz in ((0, 0), (.6, 0), (-.6, 0), (0, .6), (0, -.6)):
        ys = surfaces_at(col, c[0] + dx, c[2] + dz)
        if ((ys > c[1] + .05) & (ys < c[1] + .75)).any():
            return True
    return False


def features(folder):
    m, ch = load(folder)
    rings, rsrc, rtix, rmark = red_caches(ch)
    wu, wsrc, wtix, wmark = spawn_markers(ch, WHITE, False)
    dm, dsrc, dtix, dmark = spawn_markers(ch, DARK, True)
    gold = gold_comps(ch); health = health_stations(ch, gold); jump = jump_pads(ch, gold)
    # chevrons and cross bars stay visible but lose their collision (Godot decorative_collision_parts)
    dmask = np.zeros(len(gold[0]), bool)
    for x in health + jump:
        dmask[x['decor']] = True
    decor = (gold[2], gold[3], dmask)
    exclude = collections.defaultdict(set)
    for c, t_ in zip(decor[0][dmask], decor[1][dmask]):
        exclude[int(c)].add(int(t_))
    return dict(manifest=m, chunks=ch, rings=rings, red=(rsrc, rtix, rmark), warmup=wu, white=(wsrc, wtix, wmark),
                deathmatch=dm, dark=(dsrc, dtix, dmark), health=health, jump=jump, decor=decor, decorExclude=dict(exclude), boards=weapon_boards(ch))


# ------------------------------------------------------------------ marker ranges
def marker_parts(ch, sets, collision_only=False):
    """Per chunk, consecutive triangle ranges to hide. sets = [(src, tix, mark)]."""
    per = collections.defaultdict(set)
    for src, tix, mark in sets:
        for c, t in zip(src[mark], tix[mark]):
            per[int(c)].add(int(t))
    rows = []
    for c in sorted(per):
        tris = sorted(per[c]); ranges = []; start = last = tris[0]
        for t in tris[1:]:
            if t == last + 1:
                last = t
            else:
                ranges += [start, last - start + 1]; start = last = t
        ranges += [start, last - start + 1]
        rows.append(dict(chunk=c, material=ch[c]['mat'], triangles=int(len(ch[c]['idx'])), ranges=ranges, **(dict(collisionOnly=True) if collision_only else {})))
    return rows


# ------------------------------------------------------------------ io helpers
def rd(p):
    return json.load(open(p))


def wr_compact(p, v, crlf=True):
    s = json.dumps(v, separators=(',', ':'))
    open(p, 'w', newline='').write(s + ('\r\n' if crlf else '\n'))


def wr_indent(p, v, indent, crlf):
    s = json.dumps(v, indent=indent) + '\n'
    open(p, 'w', newline='').write(s.replace('\n', '\r\n') if crlf else s)


def src_pt(v):          # Unity -> source-local Godot (Z reflected once)
    return [round(float(v[0]), 5), round(float(v[1]), 5), round(float(-v[2]), 5)]


def r18_ids(repo):
    return [l.strip() for l in open(os.path.join(HERE, 'r18-mapsite-ids.txt')) if l.strip()]


def pick(args, repo):
    ids = None
    if '--ids' in args:
        ids = args[args.index('--ids') + 1].split(',')
    elif '--r18' in args:
        ids = r18_ids(repo)
    if not ids:
        sys.exit('select maps with --ids slug,slug or --r18')
    return ids


def jsonable(v):
    if isinstance(v, dict):
        return {k: jsonable(x) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [jsonable(x) for x in v]
    if isinstance(v, np.ndarray):
        return [round(float(x), 5) for x in v]
    if isinstance(v, (np.floating, np.integer)):
        return v.item()
    return v


# ------------------------------------------------------------------ commands
def cmd_scan(repo, ids, out):
    sa, _ = paths(repo); os.makedirs(out, exist_ok=True); res = {}
    for s in ids:
        f = features(os.path.join(sa, 'Maps', s))
        res[s] = dict(rings=len(f['rings']), warmup=len(f['warmup']), deathmatch=len(f['deathmatch']), jump=len(f['jump']),
                      health=len(f['health']), weaponBoards=len(f['boards']),
                      hiddenMarkerTriangles=int(sum(m[2].sum() for m in (f['red'], f['white'], f['dark']))))
        print(s, res[s], flush=True)
    json.dump(res, open(os.path.join(out, 'scan.json'), 'w'), indent=1)


def cmd_validate(repo):
    """Detectors vs the stored data of the original catalog maps (geometry still contains the marker families)."""
    sa, _ = paths(repo)
    boards = {r['mapId']: r for r in rd(os.path.join(sa, 'Loot/weapon-boards-r18.json'))['maps']}
    bad = 0
    for s in sorted(os.listdir(os.path.join(sa, 'Maps'))):
        f = os.path.join(sa, 'Maps', s)
        sp = os.path.join(f, 'source-semantics.json')
        if not os.path.exists(sp) or not os.path.exists(os.path.join(f, 'geometry.bytes')):
            continue
        sem = rd(sp)['semantics']
        if sem.get('coverage', {}).get('detector'):
            continue                                  # r19 rows come from this detector; compare original captures only
        if not sem.get('jump') and not sem.get('health') and not sem.get('warmup'):
            continue
        ft = features(f)
        got = dict(warmup=len(ft['warmup']), deathmatch=len(ft['deathmatch']), jump=len(ft['jump']), health=len(ft['health']))
        want = {k: len(sem.get(k, [])) for k in got}
        ok = got == want
        bad += not ok
        print(('OK  ' if ok else 'DIFF'), s, got, want if not ok else '', flush=True)
    print('validate: %d maps differ' % bad)
    return bad


def cmd_apply(repo, ids):
    sa, res = paths(repo)
    caches_p = os.path.join(sa, 'Loot/caches.json'); caches = rd(caches_p)
    brd_p = os.path.join(sa, 'Loot/weapon-boards-r18.json'); brd = rd(brd_p)
    sta_p = os.path.join(sa, 'HealthStations/stations.json'); sta = rd(sta_p)
    rec_p = os.path.join(sa, 'TeamLaunch/ArchiveRecovery/maps.json'); rec = rd(rec_p)
    mk_p = os.path.join(res, 'R26MapAuthoringMarkers.json'); mk = rd(mk_p)
    launch = {r['id']: r for r in rd(os.path.join(sa, 'TeamLaunch/launch-maps.json'))['rows']}
    cache_row = {m['mapId']: m for m in caches['maps']}
    sta_row = {m['mapId']: m for m in sta['maps']}
    rec_row = {m['id']: m for m in rec['rows']}
    brd_rows = {m['mapId']: m for m in brd['maps']}
    mk_rows = {m['id']: m for m in mk['maps']}
    report = {}
    for s in ids:
        mid = 'archive:' + s; folder = os.path.join(sa, 'Maps', s)
        ft = features(folder); m = ft['manifest']; sha = m['sourceSha256']; ch = ft['chunks']
        fp = launch[mid]['cacheFingerprint']
        note = report.setdefault(mid, {})
        # sidecar semantics (source-local Godot coordinates, same shape the importer wrote for old maps)
        sc = rd(os.path.join(folder, 'source-semantics.json'))
        if sc['semantics'].get('source_sha256') != sha:
            raise SystemExit('semantic source hash mismatch ' + mid)
        fwd = lambda v: [round(float(v[0]), 5), 0, round(float(-v[2]), 5)]
        wu = [dict(center=src_pt(x['center']), normal=[0, 1, 0], forward=fwd(x['forward']),
                   evidence='104-triangle white annulus +6-triangle direction arrow') for x in ft['warmup']]
        dm = [dict(center=src_pt(x['center']), normal=[0, 1, 0], forward=fwd(x['forward']),
                   evidence='104-triangle dark annulus +direction arrow +26-triangle humanoid head') for x in ft['deathmatch']]
        col0 = collision_arrays(ch, ft['decorExclude'])
        blocked = [x for x in ft['jump'] if pad_obstructed(col0, x)]
        ft['jump'] = [x for x in ft['jump'] if not pad_obstructed(col0, x)]
        note['padsObstructedBySourceCollision'] = len(blocked)
        jp = [dict(center=src_pt(x['center']), normal=src_pt(x['normal']), radius=x['radius'], speed=x['speed'], evidence=x['evidence'], tilted=x['tilted']) for x in ft['jump']]
        hp = [dict(center=src_pt(x['center']), normal=src_pt(x['normal']), radius=x['radius'], health_per_second=10,
                   evidence='green6-triangle hexagonal face +black108/126-triangle base +two gold12-triangle cross bars') for x in ft['health']]
        sc['semantics'].update(warmup=wu, deathmatch=dm, jump=jp, health=hp, marker_parts=[], decorative_collision_parts=[], source_tags_present=False,
                               coverage=dict(warmup=len(wu), deathmatch=len(dm), jump=len(jp), health=len(hp), detector='MAPKIT r26_map_treatments.py'))
        sc['caches'] = dict(cache_fingerprint=fp, sites=[src_pt(r['center']) for r in ft['rings']], marker_parts=[])
        open(os.path.join(folder, 'source-semantics.json'), 'w', newline='').write(json.dumps(sc, indent=1) + '\n')
        # caches: hexagon rings replace the grid seeds
        if ft['rings']:
            row = cache_row[mid]
            if row['sourceSha256'] != sha:
                raise SystemExit('cache row source mismatch ' + mid)
            row['sites'] = [dict(sourceIndex=i, position=[round(float(v), 5) for v in r['center']]) for i, r in enumerate(ft['rings'])]
        note['cacheSites'] = len(ft['rings'])
        # weapon boards
        if ft['boards']:
            rows = []
            for i, b in enumerate(ft['boards']):
                lo = np.array([b['lo'][0], b['lo'][1], -b['hi'][2]]); size = b['hi'] - b['lo']
                n = [float(b['normal'][0]), 0.0, float(-b['normal'][2])]
                rows.append(dict(id='r19/weapons_board_%d' % i, min=[round(float(v), 5) for v in lo], size=[round(float(v), 5) for v in size],
                                 normal=n, hasFloorHint=False, floorHint=0.0))
            brd_rows[mid] = dict(mapId=mid, sourceSha256=sha, cacheFingerprint=fp, boards=rows)
        else:
            brd_rows.pop(mid, None)
        note['weaponBoards'] = len(ft['boards'])
        # health stations
        sta_row[mid] = dict(mapId=mid, sourceSha256=sha, cacheFingerprint=fp,
                            stations=[dict(sourceIndex=i, center=[round(float(v), 5) for v in h['center']], normal=[0, 1, 0] if abs(h['normal'][1]) > .999 else [round(float(v), 5) for v in h['normal']],
                                           radius=h['radius'], healthPerSecond=10) for i, h in enumerate(ft['health'])])
        note['healthStations'] = len(ft['health'])
        # recovery rows: authored warm-up / deathmatch hints
        r = rec_row[mid]
        pt = lambda v: dict(zip('xyz', [round(float(x), 5) for x in src_pt(v)]))
        col = collision_arrays(ch)
        for key in ('warmup', 'deathmatch'):
            good = [x for x in ft[key] if marker_viable(col, x['center'])]
            if good:
                r[key] = [pt(x['center']) for x in good]
            # no usable authored marker: the audited importer / MAPSITE-pruned sites in the row stay untouched
            note['authored_' + key] = '%d viable of %d found' % (len(good), len(ft[key]))
        # hidden markers: triangle ranges
        parts = marker_parts(ch, [ft['red'], ft['white'], ft['dark']]) + marker_parts(ch, [ft['decor']], True)
        if parts:
            mk_rows[mid] = dict(id=mid, sourceSha256=sha, chunks=[], materials=[], triangles=[], parts=parts)
        else:
            mk_rows.pop(mid, None)
        note['markerChunks'] = len(parts); note['markerTriangles'] = int(sum(sum(p['ranges'][1::2]) for p in parts if not p.get('collisionOnly')))
        note['decorativeCollisionTriangles'] = int(sum(sum(p['ranges'][1::2]) for p in parts if p.get('collisionOnly')))
        print(mid, note, flush=True)
    # write back preserving file order
    def merge(rows, key, byid):
        order = {r[key]: i for i, r in enumerate(rows)}
        keep = [byid[r[key]] for r in rows if r[key] in byid]
        keep += [v for k, v in byid.items() if k not in order]
        return keep
    caches['maps'] = merge(caches['maps'], 'mapId', cache_row)
    brd['maps'] = merge(brd['maps'], 'mapId', brd_rows)
    sta['maps'] = merge(sta['maps'], 'mapId', sta_row)
    mk['maps'] = merge(mk['maps'], 'id', mk_rows)
    mk['rule'] = mk['rule'].split(' MAPKIT')[0] + ' MAPKIT r19: rows may carry `parts` = per-chunk triangle ranges [start,count,...] of marker components (red hexagon rings and glyphs, warm-up/deathmatch rings, arrows, figures), including colliding chunks; the runtime drops exactly those triangles (material and triangle count must match).'
    mk['generator'] = 'Tools/PlayableR26/Maps/r26_map_treatments.py apply (r19 rows); earlier rows ~/ashfall/campaign/staging/MAPS/R26MapData.py'
    wr_compact(caches_p, caches); wr_compact(brd_p, brd); wr_indent(sta_p, sta, 2, True); wr_compact(rec_p, rec)
    open(mk_p, 'w', newline='').write(json.dumps(mk, indent=1) + '\n')
    json.dump(report, open(os.path.join(HERE, 'r26_map_treatments_report.json'), 'w'), indent=1)
    print('wrote data for', len(ids), 'maps; run r26_map_data.py <repo> <ids> for jump pads')


def roof_above(A, B, C, lo, hi, p, rise=7.0):
    """True when collision lies at least `rise` m above point p (barycentric test in XZ, Unity coordinates)."""
    sel = np.where((lo[:, 0] <= p[0]) & (hi[:, 0] >= p[0]) & (lo[:, 2] <= p[2]) & (hi[:, 2] >= p[2]) & (hi[:, 1] > p[1] + rise))[0]
    if not len(sel):
        return False
    a, b, c = A[sel], B[sel], C[sel]
    d = (b[:, 2] - c[:, 2]) * (a[:, 0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (a[:, 2] - c[:, 2])
    ok = np.abs(d) > 1e-12
    d = np.where(ok, d, 1)
    l1 = ((b[:, 2] - c[:, 2]) * (p[0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (p[2] - c[:, 2])) / d
    l2 = ((c[:, 2] - a[:, 2]) * (p[0] - c[:, 0]) + (a[:, 0] - c[:, 0]) * (p[2] - c[:, 2])) / d
    l3 = 1 - l1 - l2
    inside = ok & (np.minimum(np.minimum(l1, l2), l3) >= -1e-6)
    y = l1 * a[:, 1] + l2 * b[:, 1] + l3 * c[:, 1]
    return bool((inside & (y > p[1] + rise)).any())


def pod_evidence(folder):
    """Roof coverage over the verified match sites and a 9x9 probe grid over the central third of the play bounds."""
    m, ch = load(folder)
    proof = rd(os.path.join(folder, 'spawn-proof.json'))
    sites = np.array(proof['finalRuntimeValidation']['matchSites'])
    A, B, C = [], [], []
    for ck in ch:
        if ck['flags'] and len(ck['idx']):
            T = ck['pos'][ck['idx']]; A.append(T[:, 0]); B.append(T[:, 1]); C.append(T[:, 2])
    A, B, C = np.vstack(A), np.vstack(B), np.vstack(C)
    lo = np.minimum(np.minimum(A, B), C); hi = np.maximum(np.maximum(A, B), C)
    covered = sum(roof_above(A, B, C, lo, hi, p) for p in sites)
    bmin, bmax = np.array(m['boundsMin']), np.array(m['boundsMax']); mid = (bmin + bmax) / 2
    half = (bmax - bmin)[[0, 2]] * .33 / 2
    probes = [[mid[0] + x, float(np.median(sites[:, 1])), mid[2] + z] for x in np.linspace(-half[0], half[0], 9) for z in np.linspace(-half[1], half[1], 9)]
    arena = sum(roof_above(A, B, C, lo, hi, p) for p in probes)
    return int(covered), len(sites), int(arena), len(probes)


def cmd_pods(repo, ids):
    """Add a pod-policy row (OPEN / BOXED / UNSURE) for selected maps that have none; never edits reviewed rows."""
    sa, res = paths(repo)
    p = os.path.join(res, 'R26MapPodPolicy.json'); data = rd(p); rows = {r['id']: r for r in data['maps']}
    for s in ids:
        mid = 'archive:' + s; folder = os.path.join(sa, 'Maps', s); sha = rd(os.path.join(folder, 'map.json'))['sourceSha256']
        if mid in rows and rows[mid].get('sourceSha256') == sha:
            print(mid, 'row exists:', rows[mid]['verdict']); continue
        cov, tot, arena, probes = pod_evidence(folder)
        verdict = 'BOXED' if cov >= .9 * tot else 'OPEN' if cov <= .25 * tot else 'UNSURE'
        if verdict == 'BOXED':
            print(mid, 'BOXED roof needs interior spawn hints and a visual review; add it with the BOXES tools', cov, tot); continue
        row = rows.setdefault(mid, dict(id=mid)); order = [r['id'] for r in data['maps']]
        row.update(pods=True, verdict=verdict, sourceSha256=sha, source='MAPKIT r19 roof probe',
                   evidence='Collision roof above %d/%d qualified match sites and %d/%d central arena probes (r26_map_treatments.py pods). %s' % (
                       cov, tot, arena, probes, 'No continuous ceiling over the play space; drop pods stay.' if verdict == 'OPEN' else 'Mixed cover: pods keep their previous decision until a visual review.'),
                   hasWarmupLocal=False, hasGameLocal=False, spawns=[])
        if mid not in order:
            data['maps'].append(row)
        print(mid, verdict, cov, tot, arena, probes)
    raw = open(p, 'rb').read(); nl = '\r\n' if b'\r\n' in raw else '\n'
    head = {k: v for k, v in data.items() if k != 'maps'}
    lines = ['{'] + [' %s:%s,' % (json.dumps(k), json.dumps(v, ensure_ascii=False)) for k, v in head.items()] + [' "maps":[']
    lines += ['  ' + json.dumps(r, ensure_ascii=False, separators=(',', ':')) + (',' if i < len(data['maps']) - 1 else '') for i, r in enumerate(data['maps'])]
    open(p, 'w', newline='').write(nl.join(lines + [' ]', '}']) + nl)


# Art-direction defaults for maps that have no Styles/styles.json row (preset, ground texture). The 61 earlier maps were
# assigned by hand from the map's theme; keep doing that and extend this table (the author can retune any row).
STYLE_DEFAULTS = {
    'army-of-pop1-gods-thor': ('overcast-ash', 'storm-slate'), 'cold-castle-gods-ghostlymostly88': ('glacier-morning', 'glacier-frost'),
    'dark-void-gods-alamo-hank': ('deep-space', 'moon-regolith'), 'doom-gods-pitdevil': ('ember-dusk', 'basalt-ember'),
    'ember-gods-voodoo-01': ('ember-dusk', 'ember-ash'), 'eternal-gods-pitdevil': ('moonlit-blue', 'night-plain'),
    'fall-guys-with-swords-dirtymonkee78': ('candy-pop', 'candy-turf'), 'g0ds-of-war-gods-badbar': ('storm-front', 'storm-slate'),
    'garden-of-eden-gods-crazytrain': ('botanical-haze', 'botanical-moss'), 'havoc-gods-pitdevil': ('storm-front', 'basalt-ember'),
    'ice-and-fire-tdm-go-kc': ('glacier-morning', 'glacier-frost'), 'meli-cafe-gods-meli': ('golden-hour', 'golden-meadow'),
    'meliworld-gods-meli': ('candy-pop', 'candy-turf'), 'mini-w-a-m-muniibunii-gfy': ('clear-noon', 'meadow'),
    'rush-gods-chriso': ('neon-night', 'night-plain'), 'spherical-war-swt-estephen08': ('deep-space', 'moon-regolith'),
    'warehouse-gods-delicious-b-mma--d65941d0-0359-4189-8f2a-80be809d495c': ('overcast-ash', 'storm-slate'),
    'warpath-gods-pitdevil': ('desert-bloom', 'pale-dune'),
}


def cmd_styles(repo, ids):
    """Add a Styles/styles.json row for selected maps that have none (preset + ground from STYLE_DEFAULTS, else clear-noon/meadow)."""
    sa, _ = paths(repo); p = os.path.join(sa, 'Styles/styles.json'); data = rd(p)
    for s in ids:
        mid = 'archive:' + s
        if mid in data['maps']:
            print(mid, 'styled:', data['maps'][mid]['preset']); continue
        preset, ground = STYLE_DEFAULTS.get(s, ('clear-noon', 'meadow'))
        assert preset in data['presets'] and os.path.exists(os.path.join(sa, 'Styles/ground', ground + '.jpg')), (s, preset, ground)
        data['maps'][mid] = dict(preset=preset, saturation=1.0, fogDensity=0.0, shadeStrength=0.1 if preset == 'clear-noon' else 0.16,
                                 groundTexture='Styles/ground/%s.jpg' % ground)
        print(mid, 'added', preset, ground)
    wr_indent(p, data, 2, True)


def cmd_audit(repo, ids, out):
    """Found (detector over the geometry) versus applied (runtime data files) per map and treatment."""
    sa, res = paths(repo); os.makedirs(out, exist_ok=True)
    caches = {m['mapId']: m for m in rd(os.path.join(sa, 'Loot/caches.json'))['maps']}
    boards = {m['mapId']: m for m in rd(os.path.join(sa, 'Loot/weapon-boards-r18.json'))['maps']}
    stations = {m['mapId']: m for m in rd(os.path.join(sa, 'HealthStations/stations.json'))['maps']}
    pads = {m['mapId']: m for m in rd(os.path.join(sa, 'JumpPads/pads.json'))['maps']}
    rec = {m['id']: m for m in rd(os.path.join(sa, 'TeamLaunch/ArchiveRecovery/maps.json'))['rows']}
    mk = {m['id']: m for m in rd(os.path.join(res, 'R26MapAuthoringMarkers.json'))['maps']}
    pod = {m['id']: m for m in rd(os.path.join(res, 'R26MapPodPolicy.json'))['maps']}
    sb = {m['id']: m for m in rd(os.path.join(res, 'R26MapScoreboards.json'))['maps']}
    styles = rd(os.path.join(sa, 'Styles/styles.json'))
    rows = {}
    authored = lambda found, listed: sum(1 for x in found if any(abs(x['center'][0] - p['x']) < .01 and abs(-x['center'][2] - p['z']) < .01 for p in listed))
    for s in ids:
        mid = 'archive:' + s; folder = os.path.join(sa, 'Maps', s); ft = features(folder); sha = ft['manifest']['sourceSha256']
        bound = lambda d, key='sourceSha256': mid in d and d[mid].get(key) == sha
        ranges = sum(sum(p['ranges'][1::2]) for p in mk[mid]['parts'] if not p.get('collisionOnly')) if bound(mk) and mk[mid].get('parts') else 0
        ring_sites = {tuple(np.round(r['center'], 2)) for r in ft['rings']}
        applied_sites = {tuple(np.round(p['position'], 2)) for p in caches[mid]['sites']} if bound(caches) else set()
        parts_ok = True
        if bound(mk):                               # the runtime's FilterAuthoringMarkerTriangles contract, replayed on the payload
            for p in mk[mid].get('parts', []):
                c = ft['chunks'][p['chunk']] if 0 <= p['chunk'] < len(ft['chunks']) else None
                parts_ok &= bool(c) and c['mat'] == p['material'] and len(c['idx']) == p['triangles'] and len(p['ranges']) % 2 == 0 \
                    and all(0 <= p['ranges'][i] and p['ranges'][i] + p['ranges'][i + 1] <= p['triangles'] for i in range(0, len(p['ranges']), 2))
        rows[s] = dict(
            marker_parts_valid=bool(parts_ok),
            hexagon_caches=dict(found=len(ft['rings']), applied=len(ring_sites & applied_sites)),
            weapon_signs=dict(found=len(ft['boards']), applied=len(boards[mid]['boards']) if bound(boards) else 0),
            jump_pads=dict(found=len(ft['jump']), applied=len(pads[mid]['pads']) if bound(pads) else 0),
            health_stations=dict(found=len(ft['health']), applied=len(stations[mid]['stations']) if bound(stations) else 0),
            warmup_markers=dict(found=len(ft['warmup']), applied=authored(ft['warmup'], rec.get(mid, {}).get('warmup', []))),
            deathmatch_markers=dict(found=len(ft['deathmatch']), applied=authored(ft['deathmatch'], rec.get(mid, {}).get('deathmatch', []))),
            hidden_marker_triangles=dict(found=int(sum(m[2].sum() for m in (ft['red'], ft['white'], ft['dark']))), applied=int(ranges)),
            pod_policy=pod[mid]['verdict'] if bound(pod) else 'none',
            style=styles['maps'].get(mid, {}).get('preset', 'none'),
            scoreboard_frames=dict(status=sb[mid]['status'], rectangles=len(sb[mid].get('rectangles', []))) if mid in sb and sb[mid].get('sourceSha256') == sha else 'none',
        )
        print(s, json.dumps(rows[s]), flush=True)
    json.dump(rows, open(os.path.join(out, 'audit.json'), 'w'), indent=1)
    return rows


def main(argv):
    if len(argv) < 2:
        sys.exit(__doc__)
    repo = os.path.abspath(argv[argv.index('--repo') + 1]) if '--repo' in argv else REPO
    out = argv[argv.index('--out') + 1] if '--out' in argv else os.path.expanduser('~/ashfall/campaign/tmp/MAPKIT/out')
    cmd = argv[1]
    if cmd == 'validate':
        return cmd_validate(repo)
    ids = pick(argv, repo)
    if cmd == 'scan':
        cmd_scan(repo, ids, out)
    elif cmd == 'apply':
        cmd_apply(repo, ids)
    elif cmd == 'pods':
        cmd_pods(repo, ids)
    elif cmd == 'styles':
        cmd_styles(repo, ids)
    elif cmd == 'audit':
        cmd_audit(repo, ids, out)
    else:
        sys.exit(__doc__)


if __name__ == '__main__':
    sys.exit(main(sys.argv) or 0)
