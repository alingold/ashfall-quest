const fs = require('fs');
const p = require('path');
const crypto = require('crypto');
const S = require('./r14/semantic_scan.cjs');
const J = require('./r14/semantic_catalog.cjs');

const TOWER_MAPS = [
  'alfa-aglae-souza',
  'bigbabyjesuz-classic-kiki-80',
  'classic-bigbox-vr',
  'classic-bw-187norvis',
  'cont-cl-er-ssd-cjcoops',
  'cont-cl-ssd-cjcoops',
  'evolving-map-bigbox',
  'statmaster-classic-soulsmight',
  'statmaster-evolving-soulsmight'
];

const UNSUPPORTED_MAPS = {
  'phoenix-bigbox-vr': {
    status: 'unsupported_different_template',
    template: 'demo-gamemap-3-batched',
    note: 'Phoenix uses demo-gamemap-3-batched without the 9-variant Central Tower compound at (-152.8, Y, 19.2) or axis-aligned 7-letter WEAPONS [48,44,44,104,176,36,236] extruded signs.'
  },
  'ga-fenix-ga-hikesa': {
    status: 'unsupported_different_template',
    template: 'demo-gamemap-3-batched',
    note: 'Phoenix variant using demo-gamemap-3-batched without the 9-variant Central Tower compound or WEAPONS signs.'
  }
};

const WEAPONS_LETTERS = ['W', 'E', 'A', 'P', 'O', 'N', 'S'];
const WEAPONS_SEQ = [48, 44, 44, 104, 176, 36, 236];
const WEAPONS_REV = [...WEAPONS_SEQ].reverse();

const WEAPONS_SIGN_MAPS = [
  'asylum-gods-pitdevil',
  'atrocious-gods-trashpandakiller',
  'badlands-gods-pitdevil',
  'bamboo-gods-pitdevil',
  'banana-land-gods-pitdevil',
  'beast-mode-gods-pitdevil',
  'blue-gods-quietowl',
  'chem-x-template',
  'chill-gods-pitdevil',
  'cold-hearted-gods-pitdevil',
  'dark-carnival-gods-pitdevil',
  'ereptile-disfunction-gods-pitdevil',
  'flash-gods-pitdevil',
  'fortress-gods-putangfreyja',
  'g-spot-gods-pitdevil',
  'g0dtham-gods-poplightning',
  'high-ground-gods-thor',
  'hildrs-echo-gods-pitdevil',
  'holy-ground-gods-pitdevil',
  'melipop-gods-poplightning',
  'melis-cafe-gods-pitdevil',
  'mushy-gods-putangfreyja',
  'oppression-gods-thor',
  'phantom-gods-badbar',
  'plan-b-gods-pitdevil',
  'quantum-city-gods-pitdevil',
  'quietowl-gods-pitdevil',
  'realtree-gods-poplightning',
  'robotics-2-gods-pitdevil',
  'suffering-gods-thor',
  'the-citadel-gods-thor',
  'the-powerpuff-g0ds-gods-meli',
  'the-shining-gods-ghostlymostly88',
  'the-white-house-gods-osprey-kat',
  'thor-city-gods-thor',
  'trauma-gods-pitdevil',
  'unbroken-gods-thor'
];

function round3(arr) {
  return arr.map(v => Math.round(v * 1000) / 1000);
}

function round5(arr) {
  return arr.map(v => Math.round(v * 100000) / 100000);
}

function classifySide(center) {
  const [x, , z] = center;
  if (z > 70) return x < -152 ? 'south_west' : 'south_east';
  if (z < -30) return x < -150 ? 'north_west' : 'north_east';
  if (x < -200) return z > 24 ? 'west_south' : 'west_north';
  if (x > -100) return z < 24 ? 'east_north' : 'east_south';
  return 'unknown';
}

function cardinalOf(openingId) {
  return openingId.split('_')[0];
}

function scanEdgeConnected(glb, keyFilter) {
  const out = [];
  for (const node of glb.json.nodes) {
    if (node.mesh === undefined || !/^(?:world|roof)_/.test(node.name)) continue;
    const key = node.name.match(/^(?:world|roof)_-?\d+_-?\d+_(.+)$/)[1];
    if (!keyFilter(key)) continue;
    for (const prim of glb.json.meshes[node.mesh].primitives) {
      const pos = S.accessor(glb, prim.attributes.POSITION);
      const rawIdx = prim.indices !== undefined ? S.accessor(glb, prim.indices) : pos.map((_, i) => i);
      const numTris = rawIdx.length / 3;
      const parent = Array.from({ length: numTris }, (_, i) => i);
      const root = (i) => { while (parent[i] !== i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; };
      const vKey = (pt) => pt.map(x => Math.round(x * 1000)).join(',');
      const edgeMap = new Map();
      for (let t = 0; t < numTris; t++) {
        const vs = [vKey(pos[rawIdx[t * 3]]), vKey(pos[rawIdx[t * 3 + 1]]), vKey(pos[rawIdx[t * 3 + 2]])];
        for (let e = 0; e < 3; e++) {
          const a = vs[e], b = vs[(e + 1) % 3];
          const ek = a < b ? `${a}|${b}` : `${b}|${a}`;
          if (edgeMap.has(ek)) {
            parent[root(t)] = root(edgeMap.get(ek));
          } else {
            edgeMap.set(ek, t);
          }
        }
      }
      const comps = new Map();
      for (let t = 0; t < numTris; t++) {
        const r = root(t);
        if (!comps.has(r)) comps.set(r, { tris: 0, min: [Infinity, Infinity, Infinity], max: [-Infinity, -Infinity, -Infinity] });
        const c = comps.get(r);
        c.tris++;
        for (let k = 0; k < 3; k++) {
          const pt = pos[rawIdx[t * 3 + k]];
          for (let d = 0; d < 3; d++) {
            c.min[d] = Math.min(c.min[d], pt[d]);
            c.max[d] = Math.max(c.max[d], pt[d]);
          }
        }
      }
      for (const c of comps.values()) {
        const center = c.min.map((v, d) => 0.5 * (v + c.max[d]));
        const size = c.min.map((v, d) => c.max[d] - v);
        out.push({ node: node.name, key, triangles: c.tris, center, size, min: c.min, max: c.max });
      }
    }
  }
  return out;
}

function ensureMapRecord(outMaps, slug) {
  if (outMaps[slug]) return outMaps[slug];
  const dir = p.join(S.source, 'maps', slug);
  const manifest = JSON.parse(fs.readFileSync(p.join(dir, 'converted/manifest.json'), 'utf8'));
  const glbPath = p.join(dir, 'source/map.glb');
  const computedSourceSha256 = crypto.createHash('sha256').update(fs.readFileSync(glbPath)).digest('hex');
  if (manifest.source_sha256 && manifest.source_sha256 !== computedSourceSha256) {
    throw new Error(`source_sha256 mismatch on ${slug}: manifest=${manifest.source_sha256} vs computed=${computedSourceSha256}`);
  }
  outMaps[slug] = {
    slug,
    map_id: manifest.map_id || `archive:${slug}`,
    cache_fingerprint: manifest.cache_fingerprint,
    source_sha256: computedSourceSha256,
    tower_gate_grates: []
  };
  return outMaps[slug];
}

function scanWeaponsBoardsForMap(slug, rows, joined, prov) {
  const cands = joined.filter(r =>
    WEAPONS_SEQ.includes(r.triangles) &&
    r.size[1] > 0.8 && r.size[1] < 4.0 &&
    Math.min(r.size[0], r.size[2]) < 0.8 &&
    Math.max(r.size[0], r.size[2]) > 0.6 && Math.max(r.size[0], r.size[2]) < 3.5
  );

  const visited = new Set();
  const boards = [];
  for (let i = 0; i < cands.length; i++) {
    if (visited.has(i)) continue;
    const seed = cands[i];
    const planeAxis = seed.size[2] < seed.size[0] ? 2 : 0; // 2 = thin in Z (normal Z), 0 = thin in X (normal X)
    const lineAxis = planeAxis === 2 ? 0 : 2;
    const group = [seed];
    visited.add(i);
    let changed = true;
    while (changed) {
      changed = false;
      for (let j = 0; j < cands.length; j++) {
        if (visited.has(j)) continue;
        const c = cands[j];
        if (c.key !== seed.key) continue;
        if (Math.abs(c.center[1] - seed.center[1]) > 0.5) continue;
        if (Math.abs(c.center[planeAxis] - seed.center[planeAxis]) > 0.4) continue;
        if (group.some(m => Math.abs(c.center[lineAxis] - m.center[lineAxis]) < 2.8)) {
          group.push(c);
          visited.add(j);
          changed = true;
        }
      }
    }
    if (group.length !== 7) continue;
    group.sort((a, b) => a.center[lineAxis] - b.center[lineAxis]);
    const seq = group.map(g => g.triangles);
    const isFwd = seq.every((t, k) => t === WEAPONS_SEQ[k]);
    const isRev = seq.every((t, k) => t === WEAPONS_REV[k]);
    if (!isFwd && !isRev) continue;

    const lMin = [Infinity, Infinity, Infinity], lMax = [-Infinity, -Infinity, -Infinity];
    for (const g of group) {
      for (let d = 0; d < 3; d++) {
        lMin[d] = Math.min(lMin[d], g.min[d]);
        lMax[d] = Math.max(lMax[d], g.max[d]);
      }
    }
    const lCenter = lMin.map((v, d) => 0.5 * (v + lMax[d]));
    const lSize = lMin.map((v, d) => lMax[d] - v);

    // Outward normal determined from W->E->A->P->O->N->S left-to-right reading order in Godot right-handed coords
    const normal = planeAxis === 2
      ? (isFwd ? [0, 0, 1] : [0, 0, -1])
      : (isFwd ? [-1, 0, 0] : [1, 0, 0]);

    // Find backing board box (prefer 12-triangle 3D board box with thickness > 0.2m and height ~ 2.9..3.0m)
    const allBacking = joined.filter(r =>
      r.triangles <= 48 &&
      r.size[lineAxis] >= lSize[lineAxis] * 0.8 && r.size[lineAxis] <= lSize[lineAxis] * 2.5 &&
      r.size[1] >= lSize[1] * 1.1 && r.size[1] <= lSize[1] * 5.0 &&
      r.size[planeAxis] < 2.0 &&
      Math.abs(r.center[lineAxis] - lCenter[lineAxis]) < 3.0 &&
      Math.abs(r.center[1] - lCenter[1]) < 3.0 &&
      Math.abs(r.center[planeAxis] - lCenter[planeAxis]) < 1.2
    ).sort((a, b) => {
      const aScore = (a.triangles === 12 && a.size[planeAxis] > 0.2 ? 0 : 10) + Math.abs(a.size[1] - 2.93) + Math.abs(a.center[planeAxis] - lCenter[planeAxis]);
      const bScore = (b.triangles === 12 && b.size[planeAxis] > 0.2 ? 0 : 10) + Math.abs(b.size[1] - 2.93) + Math.abs(b.center[planeAxis] - lCenter[planeAxis]);
      return aScore - bScore;
    });

    const backing = allBacking[0] || null;
    let boardMin, boardMax, boardSelector;
    if (backing) {
      boardMin = round3(backing.min);
      boardMax = round3(backing.max);
      boardSelector = {
        kind: 'backing_board_with_7_letter_weapons_glyphs',
        board_key: backing.key,
        board_material: prov.materials?.[backing.key]?.name || backing.key,
        board_triangles: backing.triangles,
        letter_key: seed.key,
        letter_material: prov.materials?.[seed.key]?.name || seed.key,
        letter_triangles: WEAPONS_SEQ,
        total_letter_triangles: 688
      };
    } else {
      // Synthesize a standard 2.93m-tall, 0.50m-thick backing box centered behind the 7 letters
      const synthMin = [...lMin];
      const synthMax = [...lMax];
      const padLine = 0.27;
      synthMin[lineAxis] -= padLine;
      synthMax[lineAxis] += padLine;
      synthMin[1] = lCenter[1] - 2.01;
      synthMax[1] = lCenter[1] + 0.92;
      if (normal[planeAxis] > 0) {
        synthMin[planeAxis] = lMax[planeAxis] - 0.54;
        synthMax[planeAxis] = lMax[planeAxis] - 0.04;
      } else {
        synthMin[planeAxis] = lMin[planeAxis] + 0.04;
        synthMax[planeAxis] = lMin[planeAxis] + 0.54;
      }
      boardMin = round3(synthMin);
      boardMax = round3(synthMax);
      boardSelector = {
        kind: 'wall_mounted_7_letter_weapons_glyphs',
        board_key: null,
        board_material: null,
        board_triangles: 0,
        letter_key: seed.key,
        letter_material: prov.materials?.[seed.key]?.name || seed.key,
        letter_triangles: WEAPONS_SEQ,
        total_letter_triangles: 688
      };
    }

    const boardCenter = round3(boardMin.map((v, d) => 0.5 * (v + boardMax[d])));
    const boardSize = round3(boardMin.map((v, d) => boardMax[d] - v));

    // Advisory floor_hint from highest floor top Y under probe in front of board
    const probePos = [
      lCenter[0] + normal[0] * 2.5,
      lCenter[1],
      lCenter[2] + normal[2] * 2.5
    ];
    const floors = joined.filter(r =>
      r.max[1] <= lMin[1] + 0.5 && r.max[1] >= lMin[1] - 15.0 &&
      r.min[0] - 1.5 <= probePos[0] && r.max[0] + 1.5 >= probePos[0] &&
      r.min[2] - 1.5 <= probePos[2] && r.max[2] + 1.5 >= probePos[2] &&
      Math.max(r.size[0], r.size[2]) > 2.5
    ).sort((a, b) => b.max[1] - a.max[1]);

    const floorHint = floors.length > 0
      ? Math.round(floors[0].max[1] * 1000) / 1000
      : Math.round((boardMin[1] - 1.5) * 1000) / 1000;

    // Order letters W-E-A-P-O-N-S
    const orderedLetters = isFwd ? group : [...group].reverse();
    const letterSpecs = orderedLetters.map((g, k) => ({
      glyph: WEAPONS_LETTERS[k],
      key: g.key,
      triangles: g.triangles,
      center: round3(g.center),
      size: round3(g.size)
    }));

    const inheritsR9 = slug === 'melipop-gods-poplightning' && (
      Math.hypot(boardCenter[0] - (-219.49), boardCenter[1] - 198.42, boardCenter[2] - (-96.23)) < 1.0 ||
      Math.hypot(boardCenter[0] - 123.02, boardCenter[1] - 202.46, boardCenter[2] - 233.44) < 1.0
    );

    boards.push({
      board_id: `weapons_board_${boards.length}`,
      word: 'WEAPONS',
      box_min: boardMin,
      box_max: boardMax,
      center: boardCenter,
      size: boardSize,
      normal,
      floor_hint: floorHint,
      floor_hint_advisory: true,
      inherits_r9_board: inheritsR9,
      reading_order: isFwd ? 'min_to_max_along_axis' : 'max_to_min_along_axis',
      letter_box_min: round3(lMin),
      letter_box_max: round3(lMax),
      letter_center: round3(lCenter),
      letter_size: round3(lSize),
      selector: boardSelector,
      letters: letterSpecs
    });
  }

  // Sort deterministically by Y, then X, then Z, then normal
  boards.sort((a, b) =>
    (a.center[1] - b.center[1]) ||
    (a.center[0] - b.center[0]) ||
    (a.center[2] - b.center[2]) ||
    (a.normal[0] - b.normal[0]) ||
    (a.normal[2] - b.normal[2])
  );
  boards.forEach((b, idx) => { b.board_id = `weapons_board_${idx}`; });
  return boards;
}

const r14Data = JSON.parse(fs.readFileSync(p.join(__dirname, '../assets/data/map_semantics_r14.json'), 'utf8'));
const outMaps = {};

// 1. Build Milestone 1: 9-variant Central Tower perimeter-gate grates
for (const slug of TOWER_MAPS) {
  const mapRec = ensureMapRecord(outMaps, slug);
  const { rows } = S.scan(slug, () => true);
  const joined = J.joinChunks(rows);

  const inTowerGateBand = (r) =>
    r.center[0] >= -218 && r.center[0] <= -88 &&
    r.center[2] >= -48 && r.center[2] <= 86 &&
    r.center[1] >= 24.0 && r.center[1] <= 27.5;

  const grateCaps = joined.filter(r =>
    inTowerGateBand(r) &&
    r.triangles === 2 &&
    r.size[1] < 0.02 &&
    Math.abs(Math.max(r.size[0], r.size[2]) - 2.92) < 0.05 &&
    Math.abs(Math.min(r.size[0], r.size[2]) - 0.27) < 0.05
  );

  const gratePlanes = joined.filter(r =>
    inTowerGateBand(r) &&
    r.triangles === 2 &&
    Math.abs(r.size[1] - 1.92) < 0.05 &&
    Math.abs(Math.max(r.size[0], r.size[2]) - 2.92) < 0.05 &&
    Math.min(r.size[0], r.size[2]) < 0.12
  );

  const grateBars = joined.filter(r =>
    inTowerGateBand(r) &&
    r.triangles === 10 &&
    Math.abs(r.size[1] - 2.09) < 0.05 &&
    Math.max(r.size[0], r.size[2]) < 0.25
  );

  if (grateCaps.length !== 8 || gratePlanes.length !== 16 || grateBars.length !== 32) {
    throw new Error(`Unexpected upper tower-gate grate counts on ${slug}: caps=${grateCaps.length}, planes=${gratePlanes.length}, bars=${grateBars.length}`);
  }

  const assemblies = [];
  for (const cap of grateCaps) {
    const openingId = classifySide(cap.center);
    const cardinal = cardinalOf(openingId);
    const planes = gratePlanes
      .filter(pl => Math.hypot(pl.center[0] - cap.center[0], pl.center[2] - cap.center[2]) < 0.5 && Math.abs(pl.center[1] - (cap.center[1] - 0.96)) < 0.2)
      .sort((a, b) => {
        const da = Math.hypot(a.center[0] - (-152.8), a.center[2] - 19.2);
        const db = Math.hypot(b.center[0] - (-152.8), b.center[2] - 19.2);
        return db - da;
      });
    const bars = grateBars
      .filter(b => Math.hypot(b.center[0] - cap.center[0], b.center[2] - cap.center[2]) < 1.6 && Math.abs(b.center[1] - (cap.center[1] - 0.97)) < 0.2)
      .sort((a, b) => (a.center[0] - b.center[0]) || (a.center[2] - b.center[2]));

    if (planes.length !== 2 || bars.length !== 4) {
      throw new Error(`Invalid assembly at ${openingId} on ${slug}: planes=${planes.length}, bars=${bars.length}`);
    }

    const formatMembers = (item) => (item.members || [item]).map(m => ({
      node: m.node,
      key: m.key,
      triangles: m.triangles,
      center: round3(m.center),
      size: round3(m.size),
      min: round3(m.min),
      max: round3(m.max)
    }));

    const parts = [
      ...planes.map((pl, idx) => ({
        role: idx === 0 ? 'outer_backing_plane' : 'inner_backing_plane',
        key: pl.key,
        triangles: pl.triangles,
        center: round3(pl.center),
        size: round3(pl.size),
        min: round3(pl.min),
        max: round3(pl.max),
        members: formatMembers(pl)
      })),
      ...bars.map((b, idx) => ({
        role: `vertical_bar_${idx}`,
        key: b.key,
        triangles: b.triangles,
        center: round3(b.center),
        size: round3(b.size),
        min: round3(b.min),
        max: round3(b.max),
        members: formatMembers(b)
      })),
      {
        role: 'top_cap',
        key: cap.key,
        triangles: cap.triangles,
        center: round3(cap.center),
        size: round3(cap.size),
        min: round3(cap.min),
        max: round3(cap.max),
        members: formatMembers(cap)
      }
    ];

    let min = [Infinity, Infinity, Infinity];
    let max = [-Infinity, -Infinity, -Infinity];
    for (const pt of parts) {
      for (let d = 0; d < 3; d++) {
        min[d] = Math.min(min[d], pt.min[d]);
        max[d] = Math.max(max[d], pt.max[d]);
      }
    }

    const sigHash = crypto.createHash('sha256').update(JSON.stringify(parts)).digest('hex').slice(0, 16);

    assemblies.push({
      opening_id: openingId,
      cardinal,
      fixture_kind: 'tower_gate_grate',
      center: round3(min.map((v, d) => 0.5 * (v + max[d]))),
      size: round3(min.map((v, d) => max[d] - v)),
      min: round3(min),
      max: round3(max),
      total_triangles: parts.reduce((acc, item) => acc + item.triangles, 0),
      signature_sha256_16: sigHash,
      parts
    });
  }

  const order = [
    'north_west', 'north_east',
    'east_north', 'east_south',
    'south_west', 'south_east',
    'west_north', 'west_south'
  ];
  assemblies.sort((a, b) => order.indexOf(a.opening_id) - order.indexOf(b.opening_id));

  mapRec.expected_member_specs = assemblies.reduce((acc, a) => acc + a.parts.reduce((pa, pt) => pa + pt.members.length, 0), 0);
  mapRec.expected_triangles = assemblies.reduce((acc, a) => acc + a.total_triangles, 0);
  mapRec.template_batch = gratePlanes[0].key === 's3' ? 'demo-gamemap-2-batched' : 'demo-gamemap-batched';
  mapRec.material_keys = {
    backing_plane: gratePlanes[0].key,
    vertical_bar: grateBars[0].key,
    top_cap: grateCaps[0].key
  };
  mapRec.tower_gate_grates = assemblies;
}

// 2. Build Milestone 2: Afterlife (afterlife-gods-alamo-hank) edge-connected warmup marker recovery
{
  const slug = 'afterlife-gods-alamo-hank';
  const mapRec = ensureMapRecord(outMaps, slug);
  const dir = p.join(S.source, 'maps', slug);
  const glb = S.glb(p.join(dir, 'source/map.glb'));
  const edgeComps = scanEdgeConnected(glb, k => k === 'p054b1b60');
  const rings = edgeComps.filter(c => c.triangles === 104 && c.size[1] < 0.1 && c.size[0] > 0.95 && c.size[0] < 1.5 && c.size[2] > 0.95 && c.size[2] < 1.5);
  const arrows = edgeComps.filter(c => c.triangles === 6 && c.size[1] < 0.1);
  if (rings.length !== 20 || arrows.length !== 20) {
    throw new Error(`Expected 20 warmup rings and 20 arrows on ${slug}, got rings=${rings.length}, arrows=${arrows.length}`);
  }

  const r14Map = r14Data.maps[slug] || {};
  const r14Parts = r14Map.marker_parts || [];
  const isHandledByR14 = (comp) => r14Parts.some(p =>
    p.key === comp.key &&
    p.triangles === comp.triangles &&
    Math.hypot(p.center[0] - comp.center[0], p.center[1] - comp.center[1], p.center[2] - comp.center[2]) < 0.01
  );

  const sortXZ = (a, b) => (a.center[0] - b.center[0]) || (a.center[2] - b.center[2]);
  rings.sort(sortXZ);
  arrows.sort(sortXZ);

  const usedArrows = new Set();
  const warmupSpawns = [];
  for (const ring of rings) {
    let bestIdx = -1, bestDist = Infinity;
    arrows.forEach((arr, idx) => {
      if (usedArrows.has(idx)) return;
      const d = Math.hypot(arr.center[0] - ring.center[0], arr.center[1] - ring.center[1], arr.center[2] - ring.center[2]);
      if (d < bestDist) { bestDist = d; bestIdx = idx; }
    });
    usedArrows.add(bestIdx);
    const arr = arrows[bestIdx];
    const dx = arr.center[0] - ring.center[0], dz = arr.center[2] - ring.center[2];
    const len = Math.hypot(dx, dz) || 1;
    const handled = isHandledByR14(ring);
    warmupSpawns.push({
      center: round5(ring.center),
      normal: [0, 1, 0],
      forward: round5([dx / len, 0, dz / len]),
      arrow_center: round5(arr.center),
      handled_by_r14: handled,
      evidence: '104-triangle edge-connected white annulus + 6-triangle direction arrow'
    });
  }

  const markerParts = [
    ...rings.map((r, i) => ({
      part_id: `warmup_ring_${i}`,
      role: 'warmup_ring',
      key: r.key,
      triangles: r.triangles,
      center: round5(r.center),
      size: round5(r.size),
      min: round5(r.min),
      max: round5(r.max),
      handled_by_r14: isHandledByR14(r)
    })),
    ...arrows.map((a, i) => ({
      part_id: `warmup_arrow_${i}`,
      role: 'warmup_arrow',
      key: a.key,
      triangles: a.triangles,
      center: round5(a.center),
      size: round5(a.size),
      min: round5(a.min),
      max: round5(a.max),
      handled_by_r14: isHandledByR14(a)
    }))
  ];

  const residualParts = markerParts.filter(p => !p.handled_by_r14);
  const r14HandledParts = markerParts.filter(p => p.handled_by_r14);

  mapRec.marker_recovery_r18 = {
    grouping_mode: 'edge_connected_1000',
    total_warmup_symbols: warmupSpawns.length,
    r14_warmup_symbols: warmupSpawns.filter(s => s.handled_by_r14).length,
    recovered_warmup_symbols: warmupSpawns.filter(s => !s.handled_by_r14).length,
    total_marker_parts: markerParts.length,
    r14_marker_parts: r14HandledParts.length,
    residual_marker_parts: residualParts.length,
    total_marker_triangles: markerParts.reduce((s, p) => s + p.triangles, 0),
    r14_marker_triangles: r14HandledParts.reduce((s, p) => s + p.triangles, 0),
    residual_marker_triangles: residualParts.reduce((s, p) => s + p.triangles, 0),
    warmup_spawns: warmupSpawns,
    marker_parts: markerParts,
    recovered_weapon_sites: []
  };
}

// 3. Build Milestone 3: 7-letter WEAPONS [48,44,44,104,176,36,236] extruded 3D sign boards across all 37 matched maps
let totalWeaponBoards = 0;
for (const slug of WEAPONS_SIGN_MAPS) {
  const mapRec = ensureMapRecord(outMaps, slug);
  const { rows, provenance } = S.scan(slug, () => true);
  const joined = J.joinChunks(rows);
  const boards = scanWeaponsBoardsForMap(slug, rows, joined, provenance);
  if (boards.length === 0) {
    throw new Error(`Expected >=1 WEAPONS sign board on ${slug}, got 0`);
  }
  totalWeaponBoards += boards.length;
  mapRec.weapon_boards_r18 = {
    glyph_signature: WEAPONS_SEQ,
    total_signs: boards.length,
    inherited_r9_count: boards.filter(b => b.inherits_r9_board).length,
    coverage_limit_note: 'Matches axis-aligned 7-letter extruded 3D WEAPONS glyphs [48,44,44,104,176,36,236] (688 triangles). Floor hints are advisory source AABB tops; parent owns physics/headroom validation and streamed rack mounting.',
    boards
  };
}

const payload = {
  schema: 'ashfall-r18-map-fixtures-v3',
  fixture_label: 'tower-gate grate + source-native residual markers + WEAPONS 3D glyph boards',
  semantic_note: 'Covers (1) the 8 proven upper N/E/S/W Central Tower perimeter-gate grate assemblies across the 9 verified original-map variants, (2) source-native edge-connected residual marker recovery (Afterlife 20/20 warmup rings + 20/20 arrows), and (3) 1,344 axis-aligned 7-letter 3D WEAPONS [48,44,44,104,176,36,236] sign boards across 37 shipped maps (including all 31 signs on melipop-gods-poplightning). 68 maps without this known axis-aligned 7-letter font signature have no WEAPONS board entries. Floor hints are advisory source AABB tops, not physics-validated rack proofs.',
  sound_r18: 'res://assets/audio/sfx/r18/metal_grate.wav',
  supported_tower_map_count: TOWER_MAPS.length,
  supported_marker_map_count: 1,
  supported_weapon_board_map_count: WEAPONS_SIGN_MAPS.length,
  total_weapon_boards_r18: totalWeaponBoards,
  unsupported_maps: UNSUPPORTED_MAPS,
  maps: outMaps
};

const outPath = p.join(__dirname, '../assets/data/map_fixtures_r18.json');
fs.writeFileSync(outPath, JSON.stringify(payload, null, 2) + '\n', 'utf8');
console.log(`Wrote ${outPath} with ${Object.keys(outMaps).length} maps.`);
console.log(`  Tower maps: ${TOWER_MAPS.length}, Marker maps: 1, WEAPONS board maps: ${WEAPONS_SIGN_MAPS.length} (${totalWeaponBoards} total signs, melipop=${outMaps['melipop-gods-poplightning'].weapon_boards_r18.total_signs}).`);
