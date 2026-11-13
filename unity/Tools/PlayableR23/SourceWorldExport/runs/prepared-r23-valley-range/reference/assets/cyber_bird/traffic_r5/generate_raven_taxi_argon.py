"""Original CYBER BIRD avian traffic model generator — Gemini Argon variant.
Python standard library only.
Coordinates: metres, +Y up, forward -Z, ground contact Y=0; wheel pivots rotate around local +X.
All triangle winding is outward CCW, all materials opaque, no external textures or embedded lights.
"""
import hashlib
import json
import math
import struct
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE
OUT.mkdir(parents=True, exist_ok=True)

# 11 small opaque PBR materials (<= 12 limit), matching the CYBER BIRD palette contract.
PALETTE = [
    ('Paint_Obsidian', (0.036, 0.046, 0.073), 0.78, 0.25, None),
    ('Paint_Plum',     (0.210, 0.052, 0.220), 0.72, 0.26, None),
    ('Paint_Ivory',    (0.720, 0.765, 0.735), 0.58, 0.30, None),
    ('Paint_Amber',    (0.820, 0.280, 0.050), 0.65, 0.30, None),
    ('Metal_Chassis',  (0.130, 0.165, 0.220), 0.85, 0.28, None),
    ('Rubber',         (0.018, 0.022, 0.030), 0.05, 0.86, None),
    ('Glass_Opaque',   (0.022, 0.095, 0.145), 0.90, 0.11, None),
    ('Neon_Cyan',      (0.040, 0.820, 0.940), 0.35, 0.22, (0.04, 2.8, 3.6)),
    ('Neon_Magenta',   (0.720, 0.045, 0.440), 0.35, 0.24, (2.8, 0.04, 1.4)),
    ('Lamp_Amber',     (0.960, 0.620, 0.150), 0.28, 0.25, (3.4, 1.55, 0.18)),
    ('Lamp_Red',       (0.760, 0.025, 0.030), 0.28, 0.28, (2.5, 0.02, 0.02)),
]
MAT = {x[0]: i for i, x in enumerate(PALETTE)}


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def length(a):
    return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])


def norm(a):
    d = length(a)
    if d < 1e-12:
        return (0.0, 1.0, 0.0)
    return (a[0] / d, a[1] / d, a[2] / d)


def centroid(verts):
    n = len(verts)
    return (
        sum(v[0] for v in verts) / n,
        sum(v[1] for v in verts) / n,
        sum(v[2] for v in verts) / n,
    )


def ensure_outward_faces(verts, faces):
    """Ensure closed convex/star-shaped part faces wind outward from centroid and drop degenerate faces."""
    c = centroid(verts)
    clean = []
    signed_score = 0.0
    for i0, i1, i2 in faces:
        if i0 == i1 or i1 == i2 or i2 == i0:
            continue
        a, b, d = verts[i0], verts[i1], verts[i2]
        n = cross(sub(b, a), sub(d, a))
        area2 = length(n)
        if area2 < 1e-9:
            continue
        fc = ((a[0] + b[0] + d[0]) / 3.0, (a[1] + b[1] + d[1]) / 3.0, (a[2] + b[2] + d[2]) / 3.0)
        signed_score += dot(n, sub(fc, c))
        clean.append((i0, i1, i2))
    if signed_score < 0.0:
        clean = [(i0, i2, i1) for i0, i1, i2 in clean]
    return clean


class Car:
    def __init__(self, name):
        self.name = name
        self.parts = []

    def mesh(self, name, vertices, faces, mat, position=(0.0, 0.0, 0.0), verify_outward=True):
        if verify_outward:
            faces = ensure_outward_faces(vertices, faces)
        else:
            valid = []
            for i0, i1, i2 in faces:
                if i0 == i1 or i1 == i2 or i2 == i0:
                    continue
                if length(cross(sub(vertices[i1], vertices[i0]), sub(vertices[i2], vertices[i0]))) >= 1e-9:
                    valid.append((i0, i1, i2))
            faces = valid
        if faces:
            self.parts.append(
                dict(
                    name=name,
                    vertices=list(vertices),
                    faces=list(faces),
                    mat=MAT[mat],
                    position=tuple(position),
                )
            )

    def sym_mesh(self, name, vertices, faces, mat, verify_outward=True):
        """Add right (+X) mesh and its exact mirror-symmetric left (-X) counterpart with outward CCW winding."""
        self.mesh(name + '_R', vertices, faces, mat, (0.0, 0.0, 0.0), verify_outward=verify_outward)
        m_verts = [(-v[0], v[1], v[2]) for v in vertices]
        m_faces = [(i0, i2, i1) for i0, i1, i2 in faces]
        self.mesh(name + '_L', m_verts, m_faces, mat, (0.0, 0.0, 0.0), verify_outward=verify_outward)

    def loft(self, name, sections, mat, position=(0.0, 0.0, 0.0), verify_outward=True):
        """Loft 2D (x, y) CCW rings along increasing Z with bilateral-symmetric quad splitting."""
        n = len(sections[0][1])
        v = [(x, y, z) for z, ring in sections for (x, y) in ring]
        f = []
        for j in range(len(sections) - 1):
            for i in range(n):
                a = j * n + i
                b = j * n + (i + 1) % n
                c = (j + 1) * n + (i + 1) % n
                d = (j + 1) * n + i
                cx = 0.25 * (v[a][0] + v[b][0] + v[c][0] + v[d][0])
                if cx >= 0.0:
                    f.extend([(a, b, c), (a, c, d)])
                else:
                    f.extend([(a, b, d), (b, c, d)])
        for i in range(1, n - 1):
            f.append((0, i + 1, i))
            base = (len(sections) - 1) * n
            f.append((base, base + i, base + i + 1))
        self.mesh(name, v, f, mat, position, verify_outward=verify_outward)

    def loft_3d(self, name, rings_3d, mat, position=(0.0, 0.0, 0.0), cap_ends=True, symmetric=False, verify_outward=True):
        """Loft arbitrary 3D rings with optional mirror symmetry across X=0."""
        n = len(rings_3d[0])
        v = [pt for ring in rings_3d for pt in ring]
        f = []
        for j in range(len(rings_3d) - 1):
            for i in range(n):
                a = j * n + i
                b = j * n + (i + 1) % n
                c = (j + 1) * n + (i + 1) % n
                d = (j + 1) * n + i
                cx = 0.25 * (v[a][0] + v[b][0] + v[c][0] + v[d][0])
                if cx >= 0.0:
                    f.extend([(a, b, c), (a, c, d)])
                else:
                    f.extend([(a, b, d), (b, c, d)])
        if cap_ends:
            for i in range(1, n - 1):
                f.append((0, i + 1, i))
                base = (len(rings_3d) - 1) * n
                f.append((base, base + i, base + i + 1))
        if symmetric:
            self.sym_mesh(name, v, f, mat, verify_outward=verify_outward)
        else:
            self.mesh(name, v, f, mat, position, verify_outward=verify_outward)

    def box(self, name, c, size, mat, bevel=0.0, symmetric=False):
        x, y, z = size
        if bevel > 0.0:
            b = min(bevel, x * 0.28, y * 0.28)
            ring = [
                (-x / 2 + b, -y / 2),
                (x / 2 - b, -y / 2),
                (x / 2, -y / 2 + b),
                (x / 2, y / 2 - b),
                (x / 2 - b, y / 2),
                (-x / 2 + b, y / 2),
                (-x / 2, y / 2 - b),
                (-x / 2, -y / 2 + b),
            ]
        else:
            ring = [(-x / 2, -y / 2), (x / 2, -y / 2), (x / 2, y / 2), (-x / 2, y / 2)]
        rings_3d = [[(px + c[0], py + c[1], sz + c[2]) for px, py in ring] for sz in (-z / 2, z / 2)]
        self.loft_3d(name, rings_3d, mat, cap_ends=True, symmetric=symmetric, verify_outward=True)

    def beam(self, name, a, b, width, mat, depth=None, symmetric=False, chamfer=True):
        """Structural/light beam between endpoints a and b with optional 6-point hexagonal bevel."""
        d = norm(sub(b, a))
        ref = (0.0, 1.0, 0.0) if abs(d[1]) < 0.92 else (0.0, 0.0, 1.0)
        u = norm(cross(d, ref))
        w = norm(cross(d, u))
        r = width / 2.0
        s = (depth if depth is not None else width) / 2.0
        if chamfer:
            profile = [
                (-r * 0.65, -s),
                (r * 0.65, -s),
                (r, 0.0),
                (r * 0.65, s),
                (-r * 0.65, s),
                (-r, 0.0),
            ]
        else:
            profile = [(-r, -s), (r, -s), (r, s), (-r, s)]
        rings_3d = []
        for end in (a, b):
            rings_3d.append([
                (
                    end[0] + qu * u[0] + tw * w[0],
                    end[1] + qu * u[1] + tw * w[1],
                    end[2] + qu * u[2] + tw * w[2],
                )
                for qu, tw in profile
            ])
        self.loft_3d(name, rings_3d, mat, cap_ends=True, symmetric=symmetric, verify_outward=True)

    def wheel_pair(self, name_l, name_r, x_abs, r=0.34, z_axle=0.0, width=0.22):
        """Build right wheel (+X) and mirror across X=0 for left wheel (-X) so all normals are outward CCW.
        Uses 3 materials per wheel (Rubber, Metal_Chassis, Paint_Ivory) and n=16 for <5,000 triangle budget.
        """
        n = 16  # Even count -> i=n//2 has cos(pi) = -1.0 -> exact Y = 0.0 ground contact
        c_r = (x_abs, r, z_axle)
        c_l = (-x_abs, r, z_axle)

        def add_mirrored_wheel_part(part_suffix, verts_r, faces_r, mat, verify=False):
            if verify:
                faces_r = ensure_outward_faces(verts_r, faces_r)
            self.mesh(name_r + '/' + part_suffix, verts_r, faces_r, mat, c_r, verify_outward=False)
            verts_l = [(-v[0], v[1], v[2]) for v in verts_r]
            faces_l = [(i0, i2, i1) for i0, i1, i2 in faces_r]
            self.mesh(name_l + '/' + part_suffix, verts_l, faces_l, mat, c_l, verify_outward=False)

        # 1. Sculpted Tire (Rubber): closed 6-ring bevelled tread + sidewall torus
        tire_stations = [
            (-width * 0.46, r * 0.66),
            (-width * 0.50, r * 0.88),
            (-width * 0.36, r * 1.00),
            (width * 0.36, r * 1.00),
            (width * 0.50, r * 0.88),
            (width * 0.46, r * 0.66),
        ]
        tv = []
        for x, rad in tire_stations:
            for i in range(n):
                ang = 2.0 * math.pi * i / n
                tv.append((x, rad * math.cos(ang), rad * math.sin(ang)))
        tf = []
        num_s = len(tire_stations)
        for j in range(num_s):
            jn = (j + 1) % num_s
            for i in range(n):
                a = j * n + i
                b = j * n + (i + 1) % n
                d = jn * n + i
                e = jn * n + (i + 1) % n
                tf.extend([(a, b, e), (a, e, d)])
        add_mirrored_wheel_part('Tire', tv, tf, 'Rubber', verify=False)

        # 2. Stepped Deep-Dish Alloy Wheel Rim & Brake Drum (Metal_Chassis)
        # Profile goes from inner drum cap (-0.42*w) to outer stepped dish (+0.45*w) -> outward normal is +X
        rim_stations = [
            (-0.42 * width, r * 0.66),
            (0.24 * width, r * 0.66),
            (0.45 * width, r * 0.68),
            (0.46 * width, r * 0.60),
            (0.26 * width, r * 0.54),
            (0.24 * width, r * 0.22),
            (0.43 * width, r * 0.18),
        ]
        rv = []
        for x, rad in rim_stations:
            for i in range(n):
                ang = 2.0 * math.pi * i / n
                rv.append((x, rad * math.cos(ang), rad * math.sin(ang)))
        rf = []
        for j in range(len(rim_stations) - 1):
            for i in range(n):
                a = j * n + i
                b = j * n + (i + 1) % n
                d = (j + 1) * n + i
                e = (j + 1) * n + (i + 1) % n
                rf.extend([(a, b, e), (a, e, d)])
        # Inner drum cap (-X normal) and outer hub boss cap (+X normal)
        last_base = (len(rim_stations) - 1) * n
        for i in range(1, n - 1):
            rf.append((0, i + 1, i))
            rf.append((last_base, last_base + i, last_base + i + 1))
        add_mirrored_wheel_part('Rim', rv, rf, 'Metal_Chassis', verify=False)

        # 3. Five Directional Raven-Talon Aero-Turbine Spokes & Center Cap (Paint_Ivory + Metal_Chassis)
        x_inner = width * 0.25
        x_outer = width * 0.45
        x_mid = width * 0.38
        n_spokes = 5
        for i in range(n_spokes):
            t0 = i * 2.0 * math.pi / n_spokes
            for suffix, a_pt, b_pt, w_sp, d_sp, mat in [
                (
                    f'SpokeMain_{i}',
                    (x_inner, r * 0.16 * math.cos(t0), r * 0.16 * math.sin(t0)),
                    (x_outer, r * 0.62 * math.cos(t0 + 0.28), r * 0.62 * math.sin(t0 + 0.28)),
                    0.044,
                    0.022,
                    'Paint_Ivory',
                ),
                (
                    f'SpokeSub_{i}',
                    (x_mid, r * 0.23 * math.cos(t0 - 0.18), r * 0.23 * math.sin(t0 - 0.18)),
                    (x_outer * 0.94, r * 0.58 * math.cos(t0 + 0.15), r * 0.58 * math.sin(t0 + 0.15)),
                    0.026,
                    0.018,
                    'Metal_Chassis',
                ),
            ]:
                d_vec = norm(sub(b_pt, a_pt))
                u_vec = norm(cross(d_vec, (1.0, 0.0, 0.0)))
                w_vec = norm(cross(d_vec, u_vec))
                rw, sw = w_sp * 0.5, d_sp * 0.5
                prof = [(-rw, -sw), (rw, -sw), (rw, sw), (-rw, sw)]
                sv = [
                    (
                        end[0] + qu * u_vec[0] + tw * w_vec[0],
                        end[1] + qu * u_vec[1] + tw * w_vec[1],
                        end[2] + qu * u_vec[2] + tw * w_vec[2],
                    )
                    for end in (a_pt, b_pt)
                    for qu, tw in prof
                ]
                sf = [
                    (0, 1, 5), (0, 5, 4),
                    (1, 2, 6), (1, 6, 5),
                    (2, 3, 7), (2, 7, 6),
                    (3, 0, 4), (3, 4, 7),
                    (0, 2, 1), (0, 3, 2),
                    (4, 5, 6), (4, 6, 7),
                ]
                add_mirrored_wheel_part(suffix, sv, sf, mat, verify=True)

        # Faceted Octagonal Center-Lock Cap (Paint_Ivory) strictly within tire width
        cap_n = 8
        x_cap0 = width * 0.37
        x_cap1 = width * 0.46
        cv = [
            (x_cap0, r * 0.14 * math.cos(2 * math.pi * i / cap_n), r * 0.14 * math.sin(2 * math.pi * i / cap_n))
            for i in range(cap_n)
        ] + [
            (x_cap1, r * 0.09 * math.cos(2 * math.pi * i / cap_n), r * 0.09 * math.sin(2 * math.pi * i / cap_n))
            for i in range(cap_n)
        ]
        cf = []
        for i in range(cap_n):
            a, b = i, (i + 1) % cap_n
            d, e = cap_n + i, cap_n + (i + 1) % cap_n
            cf.extend([(a, b, e), (a, e, d)])
        for i in range(1, cap_n - 1):
            cf.append((0, i + 1, i))
            cf.append((cap_n, cap_n + i, cap_n + i + 1))
        add_mirrored_wheel_part('CenterCap', cv, cf, 'Paint_Ivory', verify=True)

    def export(self):
        """Merge rigid parts by material within Body and each Wheel_* pivot, validate, and write GLB."""
        grouped = {}
        for part in self.parts:
            group = part['name'].split('/')[0] if '/' in part['name'] else 'Body'
            key = (group, part['mat'])
            if key not in grouped:
                part_name = (
                    group + '/' + PALETTE[part['mat']][0]
                    if group != 'Body'
                    else 'Body_' + PALETTE[part['mat']][0]
                )
                grouped[key] = {
                    'name': part_name,
                    'vertices': [],
                    'faces': [],
                    'mat': part['mat'],
                    'position': part['position'] if group != 'Body' else (0.0, 0.0, 0.0),
                }
            merged = grouped[key]
            offset = len(merged['vertices'])
            px, py, pz = part['position']
            mx, my, mz = merged['position']
            dx, dy, dz = px - mx, py - my, pz - mz
            merged['vertices'].extend((v[0] + dx, v[1] + dy, v[2] + dz) for v in part['vertices'])
            merged['faces'].extend((f[0] + offset, f[1] + offset, f[2] + offset) for f in part['faces'])

        self.parts = list(grouped.values())
        g = {
            'asset': {
                'version': '2.0',
                'generator': 'CYBER BIRD Gemini Argon Original Procedural Generator (CC0-1.0)',
                'copyright': 'CC0-1.0 Universal. Baseline by Codex; original Raven Taxi Argon variant by Gemini Argon (antigravity / gemini-4-argon-high).',
            },
            'scene': 0,
            'scenes': [{'nodes': [0]}],
            'nodes': [{'name': self.name, 'children': []}],
            'meshes': [],
            'materials': [],
            'buffers': [],
            'bufferViews': [],
            'accessors': [],
            'extensionsUsed': ['KHR_materials_emissive_strength'],
        }
        for name, rgb, metal, rough, em in PALETTE:
            m = {
                'name': name,
                'pbrMetallicRoughness': {
                    'baseColorFactor': [rgb[0], rgb[1], rgb[2], 1.0],
                    'metallicFactor': metal,
                    'roughnessFactor': rough,
                },
                'alphaMode': 'OPAQUE',
                'doubleSided': False,
            }
            if em:
                m['emissiveFactor'] = [v / max(em) for v in em]
                m['extensions'] = {'KHR_materials_emissive_strength': {'emissiveStrength': max(em)}}
            g['materials'].append(m)

        data = bytearray()
        triangles = 0
        degenerate_dropped = 0
        mins = [1e9, 1e9, 1e9]
        maxs = [-1e9, -1e9, -1e9]
        groups = {}

        def accessor(values, components, typ, minmax=False):
            while len(data) % 4:
                data.append(0)
            start = len(data)
            flat = [t for v in values for t in v] if components > 1 else values
            fmt = '<' + ('f' if typ == 5126 else 'I') * len(flat)
            data.extend(struct.pack(fmt, *flat))
            bv = len(g['bufferViews'])
            g['bufferViews'].append({'buffer': 0, 'byteOffset': start, 'byteLength': len(data) - start})
            a = {
                'bufferView': bv,
                'componentType': typ,
                'count': len(values),
                'type': {1: 'SCALAR', 3: 'VEC3'}[components],
            }
            if minmax:
                a['min'] = [min(v[k] for v in values) for k in range(3)]
                a['max'] = [max(v[k] for v in values) for k in range(3)]
            ai = len(g['accessors'])
            g['accessors'].append(a)
            return ai

        for part in self.parts:
            vertices = []
            normals = []
            pos = part['position']
            for face in part['faces']:
                a, b, c = (part['vertices'][face[0]], part['vertices'][face[1]], part['vertices'][face[2]])
                raw_n = cross(sub(b, a), sub(c, a))
                if length(raw_n) < 1e-9:
                    degenerate_dropped += 1
                    continue
                normal = norm(raw_n)
                for v in (a, b, c):
                    vertices.append(v)
                    normals.append(normal)
                    for k in range(3):
                        world_k = v[k] + pos[k]
                        mins[k] = min(mins[k], world_k)
                        maxs[k] = max(maxs[k], world_k)

            triangles += len(vertices) // 3
            p_acc = accessor(vertices, 3, 5126, True)
            n_acc = accessor(normals, 3, 5126, False)
            i_acc = accessor(list(range(len(vertices))), 1, 5125, False)
            mi = len(g['meshes'])
            g['meshes'].append({
                'name': part['name'],
                'primitives': [{
                    'attributes': {'POSITION': p_acc, 'NORMAL': n_acc},
                    'indices': i_acc,
                    'material': part['mat'],
                }],
            })
            node = {'name': part['name'].split('/')[-1], 'mesh': mi}
            parent = 0
            if '/' in part['name']:
                group = part['name'].split('/')[0]
                if group not in groups:
                    groups[group] = len(g['nodes'])
                    g['nodes'].append({'name': group, 'translation': [round(x, 6) for x in pos], 'children': []})
                    g['nodes'][0]['children'].append(groups[group])
                parent = groups[group]
            else:
                node['translation'] = [round(x, 6) for x in pos]
            idx = len(g['nodes'])
            g['nodes'].append(node)
            g['nodes'][parent]['children'].append(idx)

        g['buffers'] = [{'byteLength': len(data)}]
        js = json.dumps(g, separators=(',', ':')).encode('utf-8')
        js += b' ' * ((-len(js)) % 4)
        data += b'\0' * ((-len(data)) % 4)
        path = OUT / (self.name + '.glb')
        glb_bytes = (
            struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(data))
            + struct.pack('<II', len(js), 0x4E4F534A)
            + js
            + struct.pack('<II', len(data), 0x004E4942)
            + data
        )
        path.write_bytes(glb_bytes)

        dims = [round(maxs[k] - mins[k], 4) for k in range(3)]
        return {
            'file': path.name,
            'path': str(path),
            'sha256': hashlib.sha256(glb_bytes).hexdigest(),
            'triangles': triangles,
            'degenerate_faces': degenerate_dropped,
            'mesh_parts': len(self.parts),
            'materials_defined': len(PALETTE),
            'materials_used': len({p['mat'] for p in self.parts}),
            'dimensions_m': dims,
            'width_m': dims[0],
            'height_m': dims[1],
            'length_m': dims[2],
            'bounds_min': [round(mins[k], 5) for k in range(3)],
            'bounds_max': [round(maxs[k], 5) for k in range(3)],
            'wheel_pivots': ['Wheel_FL', 'Wheel_FR', 'Wheel_RL', 'Wheel_RR'],
            'bytes': path.stat().st_size,
        }


def sculpted_body_ring(w_bot, w_lower, w_shoulder, w_belt, w_top, y_bot, y_lower, y_shoulder, y_belt, y_top, crown=0.025):
    """12-point bevelled automotive cross-section ring (CCW in XY plane)."""
    return [
        (-w_bot * 0.5, y_bot),
        (w_bot * 0.5, y_bot),
        (w_lower * 0.5, y_lower),
        (w_shoulder * 0.5, y_shoulder),
        (w_belt * 0.5, y_belt),
        (w_top * 0.5, y_top),
        (w_top * 0.18, y_top + crown),
        (-w_top * 0.18, y_top + crown),
        (-w_top * 0.5, y_top),
        (-w_belt * 0.5, y_belt),
        (-w_shoulder * 0.5, y_shoulder),
        (-w_lower * 0.5, y_lower),
    ]


def canopy_ring(w_base, w_mid, w_top, y_base, y_mid, y_top, crown=0.022):
    """8-point chamfered wraparound visor cross-section (CCW in XY plane)."""
    return [
        (-w_base * 0.5, y_base),
        (w_base * 0.5, y_base),
        (w_mid * 0.5, y_mid),
        (w_top * 0.5, y_top),
        (w_top * 0.22, y_top + crown),
        (-w_top * 0.22, y_top + crown),
        (-w_top * 0.5, y_top),
        (-w_mid * 0.5, y_mid),
    ]


def add_wheel_arch_flare(car, name, x_inner, x_outer, yc, zc, r_in, r_out, mat_flare, mat_liner):
    """Sculpted 6-segment bevelled wheel arch eyebrow flare + dark recessed wheel-house liner."""
    segs = 6
    flare_rings = []
    liner_rings = []
    for s in range(segs + 1):
        ang = math.radians(-10.0 + 200.0 * s / segs)
        ca = math.cos(ang)
        sa = math.sin(ang)
        z_in = zc - r_in * ca
        y_in = max(0.24, yc + r_in * sa)
        z_out = zc - r_out * ca
        y_out = max(0.25, yc + r_out * sa)
        z_mid = 0.5 * (z_in + z_out)
        y_mid = 0.5 * (y_in + y_out) + 0.015 * sa

        flare_rings.append([
            (x_inner, y_in, z_in),
            (x_outer - 0.015, y_in - 0.012, z_in),
            (x_outer, y_mid - 0.008, z_mid),
            (x_outer, y_mid + 0.014, z_mid),
            (x_outer - 0.025, y_out, z_out),
            (x_inner, y_out, z_out),
        ])
        liner_rings.append([
            (x_inner - 0.24, y_in - 0.02, z_in * 0.98 + zc * 0.02),
            (x_inner + 0.01, y_in - 0.02, z_in),
            (x_inner + 0.01, y_out - 0.01, z_out),
            (x_inner - 0.24, y_out - 0.01, z_out * 0.98 + zc * 0.02),
        ])
    car.loft_3d(name + '_Flare', flare_rings, mat_flare, cap_ends=True, symmetric=True, verify_outward=True)
    car.loft_3d(name + '_Liner', liner_rings, mat_liner, cap_ends=True, symmetric=True, verify_outward=True)


def build_raven_taxi_argon():
    c = Car('raven_taxi_argon')

    # --- 1. UNDERBODY & AERO CHASSIS FLOOR (Metal_Chassis & Paint_Obsidian) ---
    c.loft(
        'AeroUndertray',
        [
            (-2.37, sculpted_body_ring(1.06, 1.24, 1.28, 1.24, 1.12, 0.175, 0.19, 0.22, 0.26, 0.28, 0.0)),
            (-1.92, sculpted_body_ring(1.36, 1.56, 1.62, 1.58, 1.48, 0.180, 0.20, 0.25, 0.30, 0.34, 0.0)),
            (-1.12, sculpted_body_ring(1.42, 1.62, 1.66, 1.62, 1.50, 0.195, 0.21, 0.27, 0.34, 0.38, 0.0)),
            (1.12,  sculpted_body_ring(1.44, 1.64, 1.68, 1.64, 1.52, 0.195, 0.21, 0.27, 0.34, 0.38, 0.0)),
            (1.90,  sculpted_body_ring(1.38, 1.56, 1.60, 1.56, 1.44, 0.210, 0.23, 0.29, 0.36, 0.40, 0.0)),
            (2.36,  sculpted_body_ring(1.14, 1.32, 1.36, 1.32, 1.20, 0.250, 0.28, 0.34, 0.40, 0.43, 0.0)),
        ],
        'Metal_Chassis',
    )

    # --- 2. MULTI-STATION SCULPTED RAVEN BODY MONOCOQUE (Paint_Plum) ---
    c.loft(
        'SculptedRavenBody',
        [
            (-2.35, sculpted_body_ring(1.04, 1.18, 1.24, 1.16, 0.94, 0.23, 0.32, 0.56, 0.68, 0.72, 0.035)),
            (-2.14, sculpted_body_ring(1.30, 1.50, 1.58, 1.48, 1.26, 0.23, 0.33, 0.58, 0.73, 0.79, 0.042)),
            (-1.82, sculpted_body_ring(1.48, 1.68, 1.76, 1.66, 1.44, 0.24, 0.35, 0.62, 0.79, 0.85, 0.040)),
            (-1.48, sculpted_body_ring(1.42, 1.64, 1.80, 1.70, 1.48, 0.38, 0.48, 0.68, 0.83, 0.89, 0.036)),
            (-1.08, sculpted_body_ring(1.50, 1.68, 1.77, 1.67, 1.48, 0.23, 0.35, 0.64, 0.84, 0.91, 0.030)),
            (-0.55, sculpted_body_ring(1.52, 1.66, 1.73, 1.64, 1.46, 0.22, 0.34, 0.62, 0.85, 0.92, 0.022)),
            (0.15,  sculpted_body_ring(1.52, 1.65, 1.72, 1.64, 1.46, 0.22, 0.34, 0.63, 0.86, 0.93, 0.022)),
            (0.82,  sculpted_body_ring(1.54, 1.70, 1.79, 1.70, 1.48, 0.23, 0.36, 0.67, 0.90, 0.97, 0.025)),
            (1.46,  sculpted_body_ring(1.44, 1.68, 1.82, 1.72, 1.48, 0.39, 0.50, 0.72, 0.93, 1.01, 0.028)),
            (1.92,  sculpted_body_ring(1.42, 1.62, 1.74, 1.64, 1.40, 0.28, 0.40, 0.69, 0.90, 0.97, 0.024)),
            (2.22,  sculpted_body_ring(1.26, 1.48, 1.58, 1.48, 1.28, 0.32, 0.44, 0.68, 0.85, 0.91, 0.018)),
            (2.34,  sculpted_body_ring(1.16, 1.36, 1.44, 1.36, 1.18, 0.36, 0.46, 0.66, 0.80, 0.85, 0.012)),
        ],
        'Paint_Plum',
    )

    # --- 3. SCULPTED FENDER FLARES & DARK WHEEL HOUSINGS ---
    add_wheel_arch_flare(c, 'FrontArch', 0.78, 0.915, 0.34, -1.48, 0.365, 0.445, 'Paint_Plum', 'Rubber')
    add_wheel_arch_flare(c, 'RearArch',  0.79, 0.922, 0.34,  1.46, 0.365, 0.455, 'Paint_Plum', 'Rubber')

    # --- 4. FOUR INDEPENDENT WHEEL PIVOTS (Wheel_FL, Wheel_FR, Wheel_RL, Wheel_RR) ---
    r_wheel = 0.34
    c.wheel_pair('Wheel_FL', 'Wheel_FR', 0.775, r=r_wheel, z_axle=-1.48, width=0.22)
    c.wheel_pair('Wheel_RL', 'Wheel_RR', 0.785, r=r_wheel, z_axle= 1.46, width=0.23)

    # --- 5. PREDATORY RAVEN-BEAK FRONT NOSE, HOOD SPINE, VENTS & BADGE ---
    c.loft(
        'HoodBeakSpine',
        [
            (-2.365, [(-0.06, 0.71), (0.06, 0.71), (0.09, 0.745), (0.0, 0.765), (-0.09, 0.745)]),
            (-2.05,  [(-0.18, 0.79), (0.18, 0.79), (0.22, 0.835), (0.0, 0.855), (-0.22, 0.835)]),
            (-1.55,  [(-0.25, 0.86), (0.25, 0.86), (0.29, 0.910), (0.0, 0.930), (-0.29, 0.910)]),
            (-1.12,  [(-0.31, 0.90), (0.31, 0.90), (0.34, 0.940), (0.0, 0.955), (-0.34, 0.940)]),
        ],
        'Paint_Obsidian',
    )

    # Recessed Hood Heat-Extractor Louvers (3 angled aero slats per side, flush on sloped hood)
    for j in range(3):
        z_vent = -1.84 + j * 0.17
        y_in = 0.875 + j * 0.016
        y_out = 0.858 + j * 0.016
        c.beam(
            f'HoodExtractor_{j}',
            (0.24, y_in, z_vent - 0.02),
            (0.50, y_out, z_vent + 0.05),
            0.055,
            'Paint_Obsidian',
            0.026,
            symmetric=True,
            chamfer=False,
        )

    # Readable Raven Silhouette / Feather Crest Badge on Front Prow
    c.loft(
        'FrontRavenQuill',
        [
            (-2.378, [(-0.014, 0.67), (0.014, 0.67), (0.020, 0.72), (0.0, 0.755), (-0.020, 0.72)]),
            (-2.250, [(-0.034, 0.74), (0.034, 0.74), (0.044, 0.785), (0.0, 0.805), (-0.044, 0.785)]),
        ],
        'Paint_Ivory',
    )
    for k in range(2):
        yk = 0.725 + k * 0.028
        zk = -2.366 + k * 0.020
        c.beam(
            f'FrontBadgeWing_{k}',
            (0.022, yk - 0.008, zk),
            (0.19 + k * 0.04, yk + 0.024, zk + 0.058),
            0.024,
            'Neon_Cyan',
            0.020,
            symmetric=True,
            chamfer=False,
        )

    c.box('FrontGrilleCavity', (0.0, 0.42, -2.345), (0.92, 0.21, 0.06), 'Rubber', 0.02)
    for bar_y, bar_w in [(0.35, 0.88), (0.42, 0.82), (0.49, 0.74)]:
        c.box(f'FrontGrilleBar_{int(bar_y*100)}', (0.0, bar_y, -2.362), (bar_w, 0.026, 0.035), 'Metal_Chassis', 0.0)
    c.loft(
        'FrontSplitterBlade',
        [
            (-2.380, [(-0.62, 0.175), (0.62, 0.175), (0.66, 0.215), (-0.66, 0.215)]),
            (-2.160, [(-0.82, 0.180), (0.82, 0.180), (0.84, 0.225), (-0.84, 0.225)]),
        ],
        'Paint_Obsidian',
    )
    c.beam('FrontCheekFang', (0.44, 0.54, -2.350), (0.58, 0.28, -2.30), 0.065, 'Paint_Obsidian', 0.045, symmetric=True)
    c.beam('FrontAirCurtain', (0.63, 0.32, -2.27), (0.70, 0.56, -2.21), 0.026, 'Neon_Cyan', 0.028, symmetric=True, chamfer=False)

    c.beam('HeadlampHousing', (0.28, 0.65, -2.33), (0.72, 0.70, -2.14), 0.085, 'Paint_Obsidian', 0.065, symmetric=True)
    c.beam('HeadlampAmberIn', (0.30, 0.65, -2.342), (0.50, 0.67, -2.255), 0.052, 'Lamp_Amber', 0.038, symmetric=True, chamfer=False)
    c.beam('HeadlampAmberOut', (0.52, 0.675, -2.245), (0.70, 0.695, -2.165), 0.048, 'Lamp_Amber', 0.038, symmetric=True, chamfer=False)
    c.beam('HeadlampCyanBrow', (0.25, 0.705, -2.345), (0.76, 0.755, -2.06), 0.026, 'Neon_Cyan', 0.028, symmetric=True, chamfer=False)

    # --- 6. RAKED DARK REFLECTIVE OPAQUE CANOPY & ARCHITECTURAL ROOF FRAMING ---
    c.loft(
        'RakedReflectiveCanopy',
        [
            (-1.24, canopy_ring(1.36, 1.32, 1.18, 0.90, 0.93, 0.96, 0.015)),
            (-0.92, canopy_ring(1.44, 1.34, 1.14, 0.91, 1.08, 1.24, 0.022)),
            (-0.52, canopy_ring(1.46, 1.30, 1.08, 0.92, 1.20, 1.44, 0.025)),
            (0.12,  canopy_ring(1.46, 1.28, 1.06, 0.93, 1.21, 1.44, 0.025)),
            (0.68,  canopy_ring(1.42, 1.24, 1.00, 0.95, 1.18, 1.38, 0.022)),
            (1.24,  canopy_ring(1.32, 1.14, 0.88, 0.98, 1.11, 1.22, 0.018)),
            (1.68,  canopy_ring(1.16, 1.02, 0.78, 0.99, 1.02, 1.05, 0.010)),
        ],
        'Glass_Opaque',
    )

    c.loft(
        'WindshieldCowl',
        [
            (-1.32, [(-0.66, 0.89), (0.66, 0.89), (0.62, 0.935), (-0.62, 0.935)]),
            (-1.14, [(-0.70, 0.90), (0.70, 0.90), (0.66, 0.960), (-0.66, 0.960)]),
        ],
        'Paint_Obsidian',
    )

    c.loft(
        'SculptedRoofCrown',
        [
            (-0.56, [(-0.52, 1.43), (0.52, 1.43), (0.46, 1.475), (-0.46, 1.475)]),
            (-0.10, [(-0.53, 1.44), (0.53, 1.44), (0.48, 1.488), (-0.48, 1.488)]),
            (0.58,  [(-0.50, 1.39), (0.50, 1.39), (0.45, 1.440), (-0.45, 1.440)]),
            (0.95,  [(-0.44, 1.29), (0.44, 1.29), (0.38, 1.330), (-0.38, 1.330)]),
        ],
        'Paint_Obsidian',
    )

    # Flush A-Pillar, Cantrail, Multi-station Curved C-Pillar Buttress & B-Pillar
    c.beam('APillar', (0.67, 0.92, -1.20), (0.54, 1.44, -0.52), 0.068, 'Paint_Obsidian', 0.052, symmetric=True)
    c.beam('RoofCantrail', (0.54, 1.44, -0.54), (0.50, 1.38, 0.68), 0.068, 'Paint_Plum', 0.052, symmetric=True)
    # 3-station curved C-Pillar Flying Buttress hugging the fastback profile
    c.loft_3d(
        'CPillarButtress',
        [
            [(0.46, 1.36, 0.66), (0.55, 1.36, 0.66), (0.54, 1.41, 0.66), (0.46, 1.41, 0.66)],
            [(0.42, 1.20, 1.24), (0.54, 1.20, 1.24), (0.52, 1.25, 1.24), (0.42, 1.25, 1.24)],
            [(0.52, 0.98, 1.74), (0.66, 0.98, 1.74), (0.64, 1.04, 1.74), (0.52, 1.04, 1.74)],
        ],
        'Paint_Plum',
        cap_ends=True,
        symmetric=True,
        verify_outward=True,
    )
    # 3-station curved B-Pillar hugging the side glass tumblehome
    c.loft_3d(
        'BPillar',
        [
            [(0.71, 0.92, 0.06), (0.745, 0.92, 0.06), (0.745, 0.92, 0.14), (0.71, 0.92, 0.14)],
            [(0.62, 1.21, 0.05), (0.655, 1.21, 0.05), (0.655, 1.21, 0.13), (0.62, 1.21, 0.13)],
            [(0.51, 1.43, 0.04), (0.545, 1.43, 0.04), (0.545, 1.43, 0.12), (0.51, 1.43, 0.12)],
        ],
        'Paint_Obsidian',
        cap_ends=True,
        symmetric=True,
        verify_outward=True,
    )
    c.beam('WindowBeltline', (0.72, 0.915, -1.16), (0.70, 0.995, 1.42), 0.038, 'Metal_Chassis', 0.034, symmetric=True, chamfer=False)

    for idx, (zl, yl, wl) in enumerate([
        (0.84, 1.355, 0.86),
        (1.06, 1.285, 0.82),
        (1.28, 1.205, 0.76),
        (1.50, 1.115, 0.70),
    ]):
        c.box(f'RearLouver_{idx}', (0.0, yl, zl), (wl, 0.026, 0.11), 'Paint_Obsidian', 0.0)

    # --- 7. SLEEK AERODYNAMIC AMBER TAXI ROOF BEACON POD ---
    c.loft(
        'TaxiPodCradle',
        [
            (-0.46, [(-0.28, 1.46), (0.28, 1.46), (0.24, 1.525), (-0.24, 1.525)]),
            (-0.18, [(-0.34, 1.47), (0.34, 1.47), (0.31, 1.545), (-0.31, 1.545)]),
            (0.18,  [(-0.26, 1.46), (0.26, 1.46), (0.22, 1.520), (-0.22, 1.520)]),
        ],
        'Metal_Chassis',
    )
    c.loft(
        'TaxiPodAmberGlow',
        [
            (-0.38, [(-0.24, 1.52), (0.24, 1.52), (0.21, 1.620), (-0.21, 1.620)]),
            (-0.16, [(-0.29, 1.53), (0.29, 1.53), (0.26, 1.635), (-0.26, 1.635)]),
            (0.08,  [(-0.22, 1.52), (0.22, 1.52), (0.19, 1.610), (-0.19, 1.610)]),
        ],
        'Lamp_Amber',
    )
    c.loft(
        'TaxiPodVisorCap',
        [
            (-0.40, [(-0.22, 1.615), (0.22, 1.615), (0.18, 1.648), (-0.18, 1.648)]),
            (-0.14, [(-0.27, 1.628), (0.27, 1.628), (0.22, 1.658), (-0.22, 1.658)]),
            (0.12,  [(-0.18, 1.605), (0.18, 1.605), (0.14, 1.632), (-0.14, 1.632)]),
        ],
        'Paint_Obsidian',
    )
    c.beam('TaxiPodWinglet', (0.25, 1.58, -0.30), (0.39, 1.55, 0.06), 0.032, 'Neon_Cyan', 0.055, symmetric=True, chamfer=False)
    for gx in (-0.11, 0.0, 0.11):
        c.box(f'TaxiGlyph_{int(gx*100)}', (gx, 1.572, -0.39), (0.045, 0.058, 0.022), 'Paint_Ivory', 0.0)

    # --- 8. SIDE ROCKER SILLS, FENDER GILLS, CYAN FEATHER NEON & MIRRORS (FLUSH MOUNTED) ---
    c.beam('SideRockerSkirt', (0.835, 0.28, -1.04), (0.845, 0.29, 1.02), 0.095, 'Metal_Chassis', 0.11, symmetric=True)
    c.beam('RockerNeonStrip', (0.875, 0.255, -0.96), (0.885, 0.265, 0.94), 0.022, 'Neon_Cyan', 0.022, symmetric=True, chamfer=False)

    # Flush Front Fender Gill & Flank Wing Recess Panel hugging the sculpted door/haunch surface
    c.beam('FrontFenderGill', (0.855, 0.56, -1.02), (0.840, 0.72, -0.82), 0.055, 'Paint_Obsidian', 0.11, symmetric=True, chamfer=False)
    c.beam('FlankWingPanel', (0.840, 0.62, -0.68), (0.865, 0.78, 0.92), 0.048, 'Paint_Obsidian', 0.16, symmetric=True)

    # Contoured 3-station Cyan Shoulder Neon hugging the exact beltline curve
    c.loft_3d(
        'ShoulderCyanSpine',
        [
            [(0.82, 0.825, -1.12), (0.852, 0.825, -1.12), (0.852, 0.850, -1.12), (0.82, 0.850, -1.12)],
            [(0.80, 0.845,  0.10), (0.836, 0.845,  0.10), (0.836, 0.870,  0.10), (0.80, 0.870,  0.10)],
            [(0.84, 0.915,  1.46), (0.875, 0.915,  1.46), (0.875, 0.940,  1.46), (0.84, 0.940,  1.46)],
        ],
        'Neon_Cyan',
        cap_ends=True,
        symmetric=True,
        verify_outward=True,
    )

    # Flush-mounted Raven Primary Feather Vanes + Amber Taxi Accent Vane along the flank/haunch
    feather_specs = [
        ('FlankFeatherCyan0', 0.852, 0.58, -0.25, 0.864, 0.75, 0.42, 0.036, 'Neon_Cyan'),
        ('FlankFeatherCyan1', 0.856, 0.65,  0.12, 0.878, 0.83, 0.82, 0.036, 'Neon_Cyan'),
        ('FlankFeatherCyan2', 0.868, 0.73,  0.50, 0.888, 0.90, 1.18, 0.034, 'Neon_Cyan'),
        ('FlankFeatherAmber', 0.854, 0.51, -0.05, 0.872, 0.68, 0.68, 0.032, 'Lamp_Amber'),
    ]
    for fname, x0, y0, z0, x1, y1, z1, fw, fmat in feather_specs:
        c.beam(fname, (x0, y0, z0), (x1, y1, z1), fw, fmat, 0.026, symmetric=True, chamfer=False)

    # Aerodynamic Winglet Side Mirrors anchored firmly into the A-pillar/door beltline
    c.beam('MirrorArm', (0.70, 0.95, -0.78), (0.84, 1.02, -0.84), 0.038, 'Metal_Chassis', 0.028, symmetric=True, chamfer=False)
    c.beam('MirrorPod', (0.81, 1.02, -0.76), (0.895, 1.04, -0.94), 0.058, 'Paint_Obsidian', 0.082, symmetric=True)
    c.beam('MirrorSignal', (0.85, 1.03, -0.78), (0.908, 1.042, -0.92), 0.020, 'Neon_Cyan', 0.020, symmetric=True, chamfer=False)

    # --- 9. WING-SHAPED REAR DETAILS, SPOILER, TAIL LAMPS & VENTURI DIFFUSER ---
    c.beam('RearWingFinOuter', (0.74, 0.98, 1.42), (0.68, 1.11, 2.26), 0.075, 'Paint_Obsidian', 0.14, symmetric=True)
    c.beam('RearWingFinInner', (0.62, 0.99, 1.56), (0.56, 1.13, 2.28), 0.065, 'Paint_Plum', 0.11, symmetric=True)

    c.beam('SpoilerPylon', (0.42, 0.92, 1.98), (0.44, 1.15, 2.14), 0.045, 'Metal_Chassis', 0.095, symmetric=True, chamfer=False)
    c.loft(
        'RearBiplaneSpoiler',
        [
            (1.98, [(-0.74, 1.130), (0.74, 1.130), (0.72, 1.168), (-0.72, 1.168)]),
            (2.16, [(-0.78, 1.145), (0.78, 1.145), (0.76, 1.188), (-0.76, 1.188)]),
            (2.29, [(-0.70, 1.155), (0.70, 1.155), (0.68, 1.190), (-0.68, 1.190)]),
        ],
        'Paint_Obsidian',
    )
    c.box('SpoilerTrailingNeon', (0.0, 1.174, 2.30), (1.36, 0.022, 0.028), 'Neon_Cyan', 0.0)

    c.box('RearVisorPanel', (0.0, 0.77, 2.335), (1.36, 0.17, 0.04), 'Paint_Obsidian', 0.02)
    c.box('RearLightBarTop', (0.0, 0.835, 2.355), (1.32, 0.032, 0.028), 'Lamp_Red', 0.0)
    for k in range(3):
        yk = 0.715 + k * 0.036
        c.beam(
            f'RearFeatherLamp_{k}',
            (0.24 + k * 0.03, yk, 2.352),
            (0.65 - k * 0.02, yk + 0.022, 2.325),
            0.028,
            'Lamp_Red',
            0.026,
            symmetric=True,
            chamfer=False,
        )

    c.box('RearBadgeQuill', (0.0, 0.765, 2.360), (0.036, 0.095, 0.024), 'Paint_Ivory', 0.0)
    c.beam('RearBadgeWing', (0.022, 0.755, 2.360), (0.14, 0.790, 2.352), 0.022, 'Neon_Cyan', 0.018, symmetric=True, chamfer=False)

    c.loft(
        'RearVenturiDiffuser',
        [
            (1.95, [(-0.62, 0.21), (0.62, 0.21), (0.66, 0.36), (-0.66, 0.36)]),
            (2.36, [(-0.58, 0.26), (0.58, 0.26), (0.62, 0.46), (-0.62, 0.46)]),
        ],
        'Paint_Obsidian',
    )
    for sx in (0.14, 0.34, 0.54):
        c.box(f'DiffuserStrake_{int(sx*100)}', (sx, 0.32, 2.22), (0.028, 0.14, 0.31), 'Metal_Chassis', 0.0, symmetric=True)

    return c


def main():
    car = build_raven_taxi_argon()
    stats = car.export()

    manifest = {
        'title': 'CYBER BIRD original avian traffic asset — Raven Taxi Argon variant',
        'author': 'Gemini Argon',
        'provider': {
            'author': 'Gemini Argon',
            'provider': 'antigravity',
            'model': 'gemini-4-argon-high',
            'baseline_credit': 'Original CC0-1.0 baseline procedural generator and palette by Codex (2026).',
        },
        'license': 'CC0-1.0',
        'coordinate_contract': {
            'units': 'metres',
            'up': '+Y',
            'forward': '-Z',
            'ground_y': 0.0,
            'wheel_rotation_axis': '+X',
            'length_range_m': [4.4, 4.9],
            'max_width_m': 1.90,
            'max_height_m': 1.85,
            'preferred_max_triangles': 5000,
            'hard_max_triangles': 8000,
            'max_mesh_parts': 24,
            'max_materials': 12,
        },
        'materials': {
            'count': len(PALETTE),
            'textures': 0,
            'opaque': True,
            'double_sided': False,
            'dynamic_lights': 0,
            'reuse_by_name': [p[0] for p in PALETTE],
        },
        'model': stats,
        'models': [stats],
    }

    (OUT / 'raven_taxi_argon_stats.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    (OUT / 'ARGON-PROVENANCE.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    (OUT / 'ARGON-LICENSE.txt').write_text(
        'CC0 1.0 Universal\n\n'
        'Original CYBER BIRD Raven Taxi Argon variant geometry and generator by Gemini Argon '
        '(antigravity / gemini-4-argon-high), 2026.\n'
        'Built upon the CC0-1.0 baseline procedural framework and material palette by Codex, 2026.\n'
        'To the extent possible under law, the authors have waived all copyright and related or '
        'neighboring rights to these original assets and generation source under CC0 1.0 Universal.\n'
        'https://creativecommons.org/publicdomain/zero/1.0/\n',
        encoding='utf-8',
    )
    print(json.dumps(stats, indent=2))


if __name__ == '__main__':
    main()
