extends RefCounted
## Hash-bound recovery of:
## 1) The 8 upper perimeter gates plus R21 ten octagonal mid-height hatches across the 9 verified
##    original-map variants (Alpha/Classic/Evolving/Cont/Statmaster).
## 2) Source-native residual spawn & weapon-cache marker assemblies (starting with
##    Afterlife's 20 white warmup rings + 20 arrows where single-vertex welding joined 19 rings).
## Clones details.panels and semantics_r14.data before appending so cached resources/catalogs
## are never mutated. Fails closed with zero mutations if any expected spec or triangle is missing.

const Catalog = preload("res://scripts/archive_catalog1003.gd")
const Components = preload("res://scripts/archive_semantics_r8.gd")
const DATA_PATH := "res://assets/data/map_fixtures_r18.json"
const SOUND_R18 := "res://assets/audio/sfx/r18/metal_grate.wav"
const FIXTURE_KIND := "tower_gate_grate"

static var _catalog: Dictionary = {}

static func vector(value: Array) -> Vector3:
	return Vector3(float(value[0]), float(value[1]), float(value[2]))

static func aabb_from_min_max(min_arr: Array, max_arr: Array) -> AABB:
	var low := vector(min_arr)
	var high := vector(max_arr)
	return AABB(low, high - low)

static func resolve_slug(map_id_or_slug: String) -> String:
	var s := Catalog.slug(map_id_or_slug)
	if not s.is_empty():
		return s
	return map_id_or_slug.trim_prefix("archive:")

static func catalog() -> Dictionary:
	if _catalog.is_empty() and FileAccess.file_exists(DATA_PATH):
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
		if parsed is Dictionary:
			_catalog = parsed
	return _catalog

static func for_map(map_id_or_slug: String, fingerprint: String) -> Dictionary:
	if fingerprint.is_empty():
		return {}
	var slug := resolve_slug(map_id_or_slug)
	var row: Dictionary = catalog().get("maps", {}).get(slug, {})
	if row.is_empty() or str(row.get("cache_fingerprint", "")) != fingerprint:
		return {}
	return row

static func unsupported_reason(map_id_or_slug: String) -> Dictionary:
	var slug := resolve_slug(map_id_or_slug)
	return catalog().get("unsupported_maps", {}).get(slug, {})

static func _relative_transform(root_node: Node, child: Node) -> Transform3D:
	var xf := Transform3D.IDENTITY
	var cur: Node = child
	while cur != null and cur != root_node:
		if cur is Node3D:
			xf = (cur as Node3D).transform * xf
		cur = cur.get_parent()
	return xf

static func _edge_components(mesh: Mesh) -> Dictionary:
	var faces: PackedVector3Array = mesh.get_faces()
	var num_tris: int = faces.size() / 3
	var parents: Array[int] = []
	parents.resize(num_tris)
	for t in num_tris:
		parents[t] = t
	var v_ids: Dictionary = {}
	var next_vid := 0
	var edge_owner: Dictionary = {}
	for t in num_tris:
		var tv0 := 0
		var tv1 := 0
		var tv2 := 0
		for k in 3:
			var v: Vector3 = faces[t * 3 + k]
			var q := Vector3i((v * 1000.0).round())
			var vid: int
			if v_ids.has(q):
				vid = int(v_ids[q])
			else:
				vid = next_vid
				v_ids[q] = vid
				next_vid += 1
			if k == 0:
				tv0 = vid
			elif k == 1:
				tv1 = vid
			else:
				tv2 = vid
		var edges := [
			Vector2i(mini(tv0, tv1), maxi(tv0, tv1)),
			Vector2i(mini(tv1, tv2), maxi(tv1, tv2)),
			Vector2i(mini(tv2, tv0), maxi(tv2, tv0))
		]
		for ek in edges:
			if ek.x == ek.y:
				continue
			if edge_owner.has(ek):
				var other_t: int = int(edge_owner[ek])
				parents[Components._root(parents, t)] = Components._root(parents, other_t)
			else:
				edge_owner[ek] = t
	var rows: Dictionary = {}
	var tri_roots: Array[int] = []
	tri_roots.resize(num_tris)
	for t in num_tris:
		var r: int = Components._root(parents, t)
		tri_roots[t] = r
		if not rows.has(r):
			rows[r] = {"box": AABB(faces[t * 3], Vector3.ZERO), "triangles": 0}
		var b: AABB = rows[r].box
		b = b.expand(faces[t * 3])
		b = b.expand(faces[t * 3 + 1])
		b = b.expand(faces[t * 3 + 2])
		rows[r].box = b
		rows[r].triangles += 1
	return {"rows": rows, "triangle_roots": tri_roots, "faces": faces}

static func apply(map: Node) -> Dictionary:
	if map == null:
		return {"supported": false, "status": "null_map"}
	var geometry: Node = map.get("_geometry")
	var details: Dictionary = map.get("_details_r8") if map.get("_details_r8") is Dictionary else {}
	var semantics_r14: Node = map.get("semantics_r14")
	var map_id: String = str(map.get("map_id"))
	var meta: Dictionary = map.get("_meta") if map.get("_meta") is Dictionary else {}
	var fingerprint: String = str(meta.get("cache_fingerprint", ""))
	var is_live := false
	var game: Node = map.get("_game")
	if game != null and game.get("network_bridge") != null:
		is_live = bool(game.network_bridge.live())

	var grate_stats := apply_tower_grates(geometry, details, map_id, fingerprint)
	var marker_stats := apply_markers(geometry, semantics_r14, map_id, fingerprint, is_live)

	if map.get("statistics") is Dictionary:
		map.statistics["tower_grates_r18"] = grate_stats
		if bool(marker_stats.get("supported", false)):
			map.statistics["markers_r18"] = marker_stats

	var combined := grate_stats.duplicate(true)
	combined["markers_r18"] = marker_stats
	if not bool(grate_stats.get("supported", false)) and bool(marker_stats.get("supported", false)):
		combined["supported"] = true
		combined["status"] = str(marker_stats.get("status", "ok"))
	return combined

static func apply_tower_grates(geometry: Node, details: Dictionary, map_id_or_slug: String, fingerprint: String) -> Dictionary:
	var slug := resolve_slug(map_id_or_slug)
	var map_row := for_map(slug, fingerprint)
	var assemblies: Array = map_row.get("tower_gate_grates", []) if not map_row.is_empty() else []
	if geometry == null or map_row.is_empty() or assemblies.is_empty():
		var unsup := unsupported_reason(slug)
		var st := str(unsup.get("status", "fingerprint_mismatch_or_unsupported"))
		if not map_row.is_empty() and assemblies.is_empty():
			st = "no_tower_grates_on_map"
		return {
			"supported": false,
			"slug": slug,
			"status": st,
			"expected_member_specs": 0,
			"matched_member_specs": 0,
			"extracted_openings": 0,
			"removed_visual_triangles": 0,
			"removed_collision_triangles": 0,
			"appended_panels": 0
		}

	if details.has("panels") and details.panels is Array:
		details.panels = details.panels.duplicate()
	else:
		details["panels"] = []

	var existing_grates := 0
	for panel in details.panels:
		if panel is Dictionary and str(panel.get("fixture_kind_r18", "")) == FIXTURE_KIND:
			existing_grates += 1

	if geometry.has_meta("tower_grates_r18"):
		var cached: Dictionary = geometry.get_meta("tower_grates_r18").duplicate(true)
		if existing_grates == 0 and geometry.has_meta("tower_grate_rows_r18"):
			var saved_rows: Array = geometry.get_meta("tower_grate_rows_r18")
			for r in saved_rows:
				details.panels.append(r.duplicate(true))
			cached["appended_panels"] = saved_rows.size()
		else:
			cached["appended_panels"] = 0
		cached["idempotent_skip"] = true
		return cached

	var wanted_keys: Dictionary = {}
	var opening_buckets: Array = []
	var member_specs: Array = []
	var tower_bounds := AABB()
	var has_bounds := false
	var expected_triangles := 0

	for o_idx in assemblies.size():
		var assembly: Dictionary = assemblies[o_idx]
		var o_box := aabb_from_min_max(assembly.min, assembly.max)
		if not has_bounds:
			tower_bounds = o_box
			has_bounds = true
		else:
			tower_bounds = tower_bounds.merge(o_box)
		var part_slots: Array = []
		var parts: Array = assembly.get("parts", [])
		for p_idx in parts.size():
			var part: Dictionary = parts[p_idx]
			var key := str(part.key)
			wanted_keys[key] = true
			part_slots.append({
				"role": str(part.role),
				"key": key,
				"extracted": []
			})
			var members: Array = part.get("members", [part])
			for m_idx in members.size():
				var m: Dictionary = members[m_idx]
				var m_tris := int(m.triangles)
				expected_triangles += m_tris
				member_specs.append({
					"opening_idx": o_idx,
					"part_idx": p_idx,
					"member_idx": m_idx,
					"role": str(part.role),
					"key": str(m.key),
					"triangles": m_tris,
					"center": vector(m.center),
					"size": vector(m.size),
					"box": aabb_from_min_max(m.min, m.max),
					"matched": false
				})
		opening_buckets.append(part_slots)

	var expected_member_specs: int = member_specs.size()
	tower_bounds = tower_bounds.grow(0.5)

	var removed_triangles: Dictionary = {}
	var collision_vertices: Dictionary = {}
	var collision_boxes: Dictionary = {}
	var collision_component_count := 0
	var planned_visual_triangles := 0
	var matched_member_specs := 0
	var pending_mesh_updates: Array = []

	for view in geometry.find_children("*", "MeshInstance3D", true, false):
		var material: Material = view.material_override
		if material == null or view.mesh == null or view.mesh.get_surface_count() != 1:
			continue
		var key: String = material.resource_name
		if not wanted_keys.has(key):
			continue
		var relative: Transform3D = _relative_transform(geometry, view)
		var mesh_box: AABB = relative * view.mesh.get_aabb()
		if not tower_bounds.intersects(mesh_box):
			continue

		var groups: Dictionary = Components._components(view.mesh)
		var matched_comp_to_spec: Dictionary = {}
		for comp_id in groups.rows:
			var comp: Dictionary = groups.rows[comp_id]
			var comp_box: AABB = relative * comp.box
			if not tower_bounds.intersects(comp_box):
				continue
			var comp_center: Vector3 = comp_box.get_center()
			var comp_size: Vector3 = comp_box.size
			var comp_tris: int = int(comp.triangles)
			for spec_idx in member_specs.size():
				var spec: Dictionary = member_specs[spec_idx]
				if spec.matched or spec.key != key or spec.triangles != comp_tris:
					continue
				if comp_center.distance_to(spec.center) <= 0.04 and (comp_size - spec.size).length() <= 0.05:
					spec.matched = true
					matched_member_specs += 1
					matched_comp_to_spec[comp_id] = spec_idx
					break

		if matched_comp_to_spec.is_empty():
			continue

		var arrays: Array = view.mesh.surface_get_arrays(0).duplicate()
		var old_indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] != null else PackedInt32Array()
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals = arrays[Mesh.ARRAY_NORMAL]
		var uvs = arrays[Mesh.ARRAY_TEX_UV]
		var colors = arrays[Mesh.ARRAY_COLOR]
		var keep_indices := PackedInt32Array()

		var comp_tools: Dictionary = {}
		var comp_faces: Dictionary = {}
		var comp_col_ids: Dictionary = {}
		var comp_vertex_tags: Dictionary = {}

		for comp_id in matched_comp_to_spec:
			var st := SurfaceTool.new()
			st.begin(Mesh.PRIMITIVE_TRIANGLES)
			comp_tools[comp_id] = st
			comp_faces[comp_id] = PackedVector3Array()
			var col_id := collision_component_count
			collision_component_count += 1
			comp_col_ids[comp_id] = col_id
			var spec: Dictionary = member_specs[matched_comp_to_spec[comp_id]]
			collision_boxes[col_id] = (spec.box as AABB).grow(0.025)

		for tri_idx in groups.triangle_roots.size():
			var root_id: int = groups.triangle_roots[tri_idx]
			if not matched_comp_to_spec.has(root_id):
				for corner in 3:
					var vi: int = old_indices[tri_idx * 3 + corner] if not old_indices.is_empty() else tri_idx * 3 + corner
					keep_indices.append(vi)
				continue

			var st: SurfaceTool = comp_tools[root_id]
			var f_arr: PackedVector3Array = comp_faces[root_id]
			var col_id: int = comp_col_ids[root_id]
			var tri_pts: Array[Vector3] = []

			for corner in 3:
				var vi: int = old_indices[tri_idx * 3 + corner] if not old_indices.is_empty() else tri_idx * 3 + corner
				var p_world: Vector3 = relative * vertices[vi]
				tri_pts.append(p_world)
				f_arr.append(p_world)
				if normals != null and normals.size() > vi:
					st.set_normal((relative.basis * normals[vi]).normalized())
				if uvs != null and uvs.size() > vi:
					st.set_uv(uvs[vi])
				if colors != null and colors.size() > vi:
					st.set_color(colors[vi])
				st.add_vertex(p_world)

				var v_bin := Vector3i((p_world * 250.0).round())
				var v_tag := str(col_id) + ":" + str(v_bin)
				if not comp_vertex_tags.has(v_tag):
					comp_vertex_tags[v_tag] = true
					if not collision_vertices.has(v_bin):
						collision_vertices[v_bin] = []
					collision_vertices[v_bin].append({"point": p_world, "component": col_id})

			var tri_center: Vector3 = (tri_pts[0] + tri_pts[1] + tri_pts[2]) / 3.0
			var tri_cross: Vector3 = (tri_pts[1] - tri_pts[0]).cross(tri_pts[2] - tri_pts[0])
			var c_bin := Vector3i((tri_center * 250.0).round())
			if not removed_triangles.has(c_bin):
				removed_triangles[c_bin] = []
			removed_triangles[c_bin].append({
				"center": tri_center,
				"normal": tri_cross.normalized(),
				"area": tri_cross.length()
			})
			planned_visual_triangles += 1

		var replacement_mesh := ArrayMesh.new()
		if not keep_indices.is_empty():
			arrays[Mesh.ARRAY_INDEX] = keep_indices
			replacement_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		pending_mesh_updates.append({"view": view, "mesh": replacement_mesh})

		for comp_id in matched_comp_to_spec:
			var spec: Dictionary = member_specs[matched_comp_to_spec[comp_id]]
			var st: SurfaceTool = comp_tools[comp_id]
			var committed: ArrayMesh = st.commit()
			opening_buckets[spec.opening_idx][spec.part_idx].extracted.append({
				"mesh": committed,
				"material": material,
				"faces": comp_faces[comp_id],
				"box": committed.get_aabb()
			})

	if matched_member_specs != expected_member_specs or planned_visual_triangles != expected_triangles:
		return {
			"supported": true,
			"slug": slug,
			"status": "error_incomplete_visual_match",
			"expected_member_specs": expected_member_specs,
			"matched_member_specs": matched_member_specs,
			"expected_triangles": expected_triangles,
			"extracted_openings": 0,
			"removed_visual_triangles": 0,
			"removed_collision_triangles": 0,
			"appended_panels": 0
		}

	var occupied_cells: Dictionary = {}
	for bucket in removed_triangles.values():
		for tri in bucket:
			occupied_cells[Vector3i((tri.center / 4.0).floor())] = true
	var collision_cells: Dictionary = {}
	for cell in occupied_cells:
		for x in range(-1, 2):
			for y in range(-1, 2):
				for z in range(-1, 2):
					collision_cells[cell + Vector3i(x, y, z)] = true

	var planned_collision_triangles := 0
	var pending_collision_updates: Array = []

	for col_node in geometry.find_children("*", "CollisionShape3D", true, false):
		if not (col_node.shape is ConcavePolygonShape3D):
			continue
		var relative: Transform3D = _relative_transform(geometry, col_node)
		var shape_box: AABB = _shape_bounds(col_node.shape, relative)
		if not tower_bounds.intersects(shape_box):
			continue
		var faces: PackedVector3Array = col_node.shape.get_faces()
		var keep_faces := PackedVector3Array()
		for i in range(0, faces.size(), 3):
			var a: Vector3 = relative * faces[i]
			var b: Vector3 = relative * faces[i + 1]
			var c: Vector3 = relative * faces[i + 2]
			var center: Vector3 = (a + b + c) / 3.0
			var cell4 := Vector3i((center / 4.0).floor())
			if collision_cells.has(cell4):
				var cross: Vector3 = (b - a).cross(c - a)
				if _matches_removed_triangle(removed_triangles, center, cross.normalized(), cross.length()) or _matches_removed_component(collision_vertices, collision_boxes, a, b, c, center):
					planned_collision_triangles += 1
					continue
			keep_faces.append(faces[i])
			keep_faces.append(faces[i + 1])
			keep_faces.append(faces[i + 2])
		if keep_faces.size() == faces.size():
			continue
		pending_collision_updates.append({"node": col_node, "keep_faces": keep_faces})

	if planned_collision_triangles != expected_triangles:
		return {
			"supported": true,
			"slug": slug,
			"status": "error_incomplete_collision_match",
			"expected_member_specs": expected_member_specs,
			"matched_member_specs": matched_member_specs,
			"expected_triangles": expected_triangles,
			"planned_collision_triangles": planned_collision_triangles,
			"extracted_openings": 0,
			"removed_visual_triangles": 0,
			"removed_collision_triangles": 0,
			"appended_panels": 0
		}

	var built_rows: Array = []
	for o_idx in assemblies.size():
		var assembly: Dictionary = assemblies[o_idx]
		var part_slots: Array = opening_buckets[o_idx]
		var flat_parts: Array = []
		for slot in part_slots:
			if slot.extracted.is_empty():
				return {
					"supported": true,
					"slug": slug,
					"status": "error_missing_opening_part",
					"expected_member_specs": expected_member_specs,
					"matched_member_specs": matched_member_specs,
					"extracted_openings": 0,
					"removed_visual_triangles": 0,
					"removed_collision_triangles": 0,
					"appended_panels": 0
				}
			for ext in slot.extracted:
				flat_parts.append(ext)
		var primary: Dictionary = flat_parts[0]
		var attachments: Array = []
		var merged_box: AABB = primary.box
		for p_i in range(1, flat_parts.size()):
			var att: Dictionary = flat_parts[p_i]
			attachments.append({
				"mesh": att.mesh,
				"material": att.material,
				"faces": att.faces
			})
			merged_box = merged_box.merge(att.box)
		var panel_row := {
			"mesh": primary.mesh,
			"faces": primary.faces,
			"box": merged_box,
			"material": primary.material,
			"glass": false,
			"sound_r18": SOUND_R18,
			"attachments": attachments,
			"fixture_kind_r18": str(assembly.get("fixture_kind",FIXTURE_KIND)),
			"opening_id_r18": str(assembly.get("opening_id", "")),
			"cardinal_r18": str(assembly.get("cardinal", ""))
		}
		built_rows.append(panel_row)

	if built_rows.size() != assemblies.size():
		return {
			"supported": true,
			"slug": slug,
			"status": "error_incomplete_openings",
			"expected_member_specs": expected_member_specs,
			"matched_member_specs": matched_member_specs,
			"extracted_openings": 0,
			"removed_visual_triangles": 0,
			"removed_collision_triangles": 0,
			"appended_panels": 0
		}

	for m_upd in pending_mesh_updates:
		(m_upd.view as MeshInstance3D).mesh = m_upd.mesh

	for c_upd in pending_collision_updates:
		var col_node: CollisionShape3D = c_upd.node
		var keep_faces: PackedVector3Array = c_upd.keep_faces
		if keep_faces.is_empty():
			col_node.disabled = true
		else:
			var new_shape := ConcavePolygonShape3D.new()
			new_shape.backface_collision = true
			new_shape.set_faces(keep_faces)
			col_node.shape = new_shape

	if existing_grates == 0:
		for panel_row in built_rows:
			details.panels.append(panel_row)

	var result := {
		"supported": true,
		"slug": slug,
		"status": "ok",
		"fixture_label": str(catalog().get("fixture_label", "tower-gate grate")),
		"template_batch": str(map_row.get("template_batch", "")),
		"expected_member_specs": expected_member_specs,
		"matched_member_specs": matched_member_specs,
		"extracted_openings": built_rows.size(),
		"removed_visual_triangles": planned_visual_triangles,
		"removed_collision_triangles": planned_collision_triangles,
		"appended_panels": built_rows.size() if existing_grates == 0 else 0,
		"idempotent_skip": false
	}
	geometry.set_meta("tower_grates_r18", result.duplicate(true))
	geometry.set_meta("tower_grate_rows_r18", built_rows)
	return result

static func apply_markers(geometry: Node, semantics_r14: Node, map_id_or_slug: String, fingerprint: String, live_session: bool = false) -> Dictionary:
	var slug := resolve_slug(map_id_or_slug)
	var map_row := for_map(slug, fingerprint)
	var rec: Dictionary = map_row.get("marker_recovery_r18", {}) if not map_row.is_empty() else {}
	if geometry == null or map_row.is_empty() or rec.is_empty():
		return {
			"supported": false,
			"slug": slug,
			"status": "no_marker_recovery_or_fingerprint_mismatch",
			"expected_parts": 0,
			"matched_parts": 0,
			"removed_visual_triangles": 0,
			"removed_collision_triangles": 0,
			"total_warmup_symbols": 0,
			"recovered_warmup_symbols": 0,
			"recovered_weapon_sites_r18": []
		}

	if geometry.has_meta("marker_recovery_r18"):
		var cached: Dictionary = geometry.get_meta("marker_recovery_r18").duplicate(true)
		cached["idempotent_skip"] = true
		_sync_semantics_warmup_and_sites(semantics_r14, rec, live_session)
		return cached

	var r14_already_extracted := false
	if semantics_r14 != null and int(semantics_r14.get("removed_visual_triangles")) > 0:
		r14_already_extracted = true

	var all_parts: Array = rec.get("marker_parts", [])
	var active_specs: Array = []
	var wanted_keys: Dictionary = {}
	var marker_bounds := AABB()
	var has_bounds := false
	var expected_triangles := 0

	for p_idx in all_parts.size():
		var p_row: Dictionary = all_parts[p_idx]
		if r14_already_extracted and bool(p_row.get("handled_by_r14", false)):
			continue
		var key := str(p_row.key)
		wanted_keys[key] = true
		var p_box := aabb_from_min_max(p_row.min, p_row.max)
		if not has_bounds:
			marker_bounds = p_box
			has_bounds = true
		else:
			marker_bounds = marker_bounds.merge(p_box)
		var tris := int(p_row.triangles)
		expected_triangles += tris
		active_specs.append({
			"part_id": str(p_row.get("part_id", str(p_idx))),
			"role": str(p_row.get("role", "marker")),
			"key": key,
			"grouping": str(p_row.get("grouping", "edge")),
			"triangles": tris,
			"center": vector(p_row.center),
			"size": vector(p_row.size),
			"box": p_box,
			"matched": false
		})

	var expected_parts: int = active_specs.size()
	marker_bounds = marker_bounds.grow(0.5)

	var removed_triangles: Dictionary = {}
	var collision_vertices: Dictionary = {}
	var collision_boxes: Dictionary = {}
	var collision_component_count := 0
	var planned_visual_triangles := 0
	var matched_parts := 0
	var pending_mesh_updates: Array = []
	var pending_visual_chunks: Array = []

	for view in geometry.find_children("*", "MeshInstance3D", true, false):
		var material: Material = view.material_override
		if material == null or view.mesh == null or view.mesh.get_surface_count() != 1:
			continue
		var key: String = material.resource_name
		if not wanted_keys.has(key):
			continue
		var relative: Transform3D = _relative_transform(geometry, view)
		var mesh_box: AABB = relative * view.mesh.get_aabb()
		if not marker_bounds.intersects(mesh_box):
			continue

		var edge_groups: Dictionary = _edge_components(view.mesh)
		var matched_comp_to_spec: Dictionary = {}

		for comp_id in edge_groups.rows:
			var comp: Dictionary = edge_groups.rows[comp_id]
			var comp_box: AABB = relative * comp.box
			if not marker_bounds.intersects(comp_box):
				continue
			var comp_center: Vector3 = comp_box.get_center()
			var comp_size: Vector3 = comp_box.size
			var comp_tris: int = int(comp.triangles)
			for spec_idx in active_specs.size():
				var spec: Dictionary = active_specs[spec_idx]
				if spec.matched or spec.key != key or spec.triangles != comp_tris:
					continue
				if comp_center.distance_to(spec.center) <= 0.04 and (comp_size - spec.size).length() <= 0.05:
					spec.matched = true
					matched_parts += 1
					matched_comp_to_spec[comp_id] = spec_idx
					break

		if matched_comp_to_spec.is_empty():
			continue

		var arrays: Array = view.mesh.surface_get_arrays(0).duplicate()
		var old_indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX] if arrays[Mesh.ARRAY_INDEX] != null else PackedInt32Array()
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals = arrays[Mesh.ARRAY_NORMAL]
		var uvs = arrays[Mesh.ARRAY_TEX_UV]
		var colors = arrays[Mesh.ARRAY_COLOR]
		var keep_indices := PackedInt32Array()

		var extracted_st := SurfaceTool.new()
		extracted_st.begin(Mesh.PRIMITIVE_TRIANGLES)
		var comp_col_ids: Dictionary = {}
		var comp_vertex_tags: Dictionary = {}

		for comp_id in matched_comp_to_spec:
			var col_id := collision_component_count
			collision_component_count += 1
			comp_col_ids[comp_id] = col_id
			var spec: Dictionary = active_specs[matched_comp_to_spec[comp_id]]
			collision_boxes[col_id] = (spec.box as AABB).grow(0.025)

		for tri_idx in edge_groups.triangle_roots.size():
			var root_id: int = edge_groups.triangle_roots[tri_idx]
			if not matched_comp_to_spec.has(root_id):
				for corner in 3:
					var vi: int = old_indices[tri_idx * 3 + corner] if not old_indices.is_empty() else tri_idx * 3 + corner
					keep_indices.append(vi)
				continue

			var col_id: int = comp_col_ids[root_id]
			var tri_pts: Array[Vector3] = []
			for corner in 3:
				var vi: int = old_indices[tri_idx * 3 + corner] if not old_indices.is_empty() else tri_idx * 3 + corner
				var v_local: Vector3 = vertices[vi]
				var p_world: Vector3 = relative * v_local
				tri_pts.append(p_world)
				if normals != null and normals.size() > vi:
					extracted_st.set_normal(normals[vi])
				if uvs != null and uvs.size() > vi:
					extracted_st.set_uv(uvs[vi])
				if colors != null and colors.size() > vi:
					extracted_st.set_color(colors[vi])
				extracted_st.add_vertex(v_local)

				var v_bin := Vector3i((p_world * 250.0).round())
				var v_tag := str(col_id) + ":" + str(v_bin)
				if not comp_vertex_tags.has(v_tag):
					comp_vertex_tags[v_tag] = true
					if not collision_vertices.has(v_bin):
						collision_vertices[v_bin] = []
					collision_vertices[v_bin].append({"point": p_world, "component": col_id})

			var tri_center: Vector3 = (tri_pts[0] + tri_pts[1] + tri_pts[2]) / 3.0
			var tri_cross: Vector3 = (tri_pts[1] - tri_pts[0]).cross(tri_pts[2] - tri_pts[0])
			var c_bin := Vector3i((tri_center * 250.0).round())
			if not removed_triangles.has(c_bin):
				removed_triangles[c_bin] = []
			removed_triangles[c_bin].append({
				"center": tri_center,
				"normal": tri_cross.normalized(),
				"area": tri_cross.length()
			})
			planned_visual_triangles += 1

		var replacement_mesh := ArrayMesh.new()
		if not keep_indices.is_empty():
			arrays[Mesh.ARRAY_INDEX] = keep_indices
			replacement_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		pending_mesh_updates.append({"view": view, "mesh": replacement_mesh})
		pending_visual_chunks.append({
			"mesh": extracted_st.commit(),
			"material": material,
			"relative_transform": relative
		})

	if matched_parts != expected_parts or planned_visual_triangles != expected_triangles:
		return {
			"supported": true,
			"slug": slug,
			"status": "error_incomplete_marker_visual_match",
			"expected_parts": expected_parts,
			"matched_parts": matched_parts,
			"expected_triangles": expected_triangles,
			"removed_visual_triangles": 0,
			"removed_collision_triangles": 0,
			"total_warmup_symbols": int(rec.get("total_warmup_symbols", 0)),
			"recovered_warmup_symbols": 0,
			"recovered_weapon_sites_r18": []
		}

	var occupied_cells: Dictionary = {}
	for bucket in removed_triangles.values():
		for tri in bucket:
			occupied_cells[Vector3i((tri.center / 4.0).floor())] = true
	var collision_cells: Dictionary = {}
	for cell in occupied_cells:
		for x in range(-1, 2):
			for y in range(-1, 2):
				for z in range(-1, 2):
					collision_cells[cell + Vector3i(x, y, z)] = true

	var planned_collision_triangles := 0
	var pending_collision_updates: Array = []

	for col_node in geometry.find_children("*", "CollisionShape3D", true, false):
		if not (col_node.shape is ConcavePolygonShape3D):
			continue
		var relative: Transform3D = _relative_transform(geometry, col_node)
		var shape_box: AABB = _shape_bounds(col_node.shape, relative)
		if not marker_bounds.intersects(shape_box):
			continue
		var faces: PackedVector3Array = col_node.shape.get_faces()
		var keep_faces := PackedVector3Array()
		for i in range(0, faces.size(), 3):
			var a: Vector3 = relative * faces[i]
			var b: Vector3 = relative * faces[i + 1]
			var c: Vector3 = relative * faces[i + 2]
			var center: Vector3 = (a + b + c) / 3.0
			var cell4 := Vector3i((center / 4.0).floor())
			if collision_cells.has(cell4):
				var cross: Vector3 = (b - a).cross(c - a)
				if _matches_removed_triangle(removed_triangles, center, cross.normalized(), cross.length()) or _matches_removed_component(collision_vertices, collision_boxes, a, b, c, center):
					planned_collision_triangles += 1
					continue
			keep_faces.append(faces[i])
			keep_faces.append(faces[i + 1])
			keep_faces.append(faces[i + 2])
		if keep_faces.size() == faces.size():
			continue
		pending_collision_updates.append({"node": col_node, "keep_faces": keep_faces})

	if planned_collision_triangles != expected_triangles:
		return {
			"supported": true,
			"slug": slug,
			"status": "error_incomplete_marker_collision_match",
			"expected_parts": expected_parts,
			"matched_parts": matched_parts,
			"expected_triangles": expected_triangles,
			"planned_collision_triangles": planned_collision_triangles,
			"removed_visual_triangles": 0,
			"removed_collision_triangles": 0,
			"total_warmup_symbols": int(rec.get("total_warmup_symbols", 0)),
			"recovered_warmup_symbols": 0,
			"recovered_weapon_sites_r18": []
		}

	# Commit visual mesh cuts
	for m_upd in pending_mesh_updates:
		(m_upd.view as MeshInstance3D).mesh = m_upd.mesh

	# Commit collision cuts
	for c_upd in pending_collision_updates:
		var col_node: CollisionShape3D = c_upd.node
		var keep_faces: PackedVector3Array = c_upd.keep_faces
		if keep_faces.is_empty():
			col_node.disabled = true
		else:
			var new_shape := ConcavePolygonShape3D.new()
			new_shape.backface_collision = true
			new_shape.set_faces(keep_faces)
			col_node.shape = new_shape

	# Attach extracted marker visuals under semantics_r14.marker_visuals (OriginalSpawnMarkersR14)
	var marker_holder: Node3D = null
	if semantics_r14 != null and semantics_r14.get("marker_visuals") is Node3D:
		marker_holder = semantics_r14.marker_visuals
	else:
		marker_holder = geometry.get_node_or_null("OriginalSpawnMarkersR14") as Node3D
		if marker_holder == null:
			marker_holder = Node3D.new()
			marker_holder.name = "OriginalSpawnMarkersR14"
			geometry.add_child(marker_holder)

	for chunk in pending_visual_chunks:
		var vis := MeshInstance3D.new()
		vis.name = "OriginalMarkerChunkR18_" + str(marker_holder.get_child_count())
		vis.mesh = chunk.mesh
		vis.material_override = chunk.material
		vis.transform = chunk.relative_transform
		marker_holder.add_child(vis, true)

	marker_holder.visible = not live_session
	if semantics_r14 != null and semantics_r14.has_method("set_markers_visible"):
		semantics_r14.set_markers_visible(not live_session)

	var sync_info := _sync_semantics_warmup_and_sites(semantics_r14, rec, live_session)

	var result := {
		"supported": true,
		"slug": slug,
		"status": "ok",
		"r14_already_extracted": r14_already_extracted,
		"expected_parts": expected_parts,
		"matched_parts": matched_parts,
		"removed_visual_triangles": planned_visual_triangles,
		"removed_collision_triangles": planned_collision_triangles,
		"total_handled_marker_parts": int(rec.get("total_marker_parts", expected_parts)),
		"total_handled_marker_triangles": int(rec.get("total_marker_triangles", planned_visual_triangles)),
		"total_warmup_symbols": int(rec.get("total_warmup_symbols", 0)),
		"recovered_warmup_symbols": int(sync_info.get("appended_warmup", int(rec.get("recovered_warmup_symbols", 0)))),
		"recovered_weapon_sites_r18": sync_info.get("weapon_sites", []),
		"idempotent_skip": false
	}
	geometry.set_meta("marker_recovery_r18", result.duplicate(true))
	return result

static func _sync_semantics_warmup_and_sites(semantics_r14: Node, rec: Dictionary, live_session: bool) -> Dictionary:
	var out_sites: Array[Vector3] = []
	for s in rec.get("recovered_weapon_sites", []):
		out_sites.append(vector(s))
	var appended_warmup := 0
	if semantics_r14 != null:
		if semantics_r14.get("data") is Dictionary:
			# Clone semantics_r14.data and semantics_r14.data["warmup"] before appending so
			# SemR14._catalog is NEVER mutated across repeated map loads.
			var cloned_data: Dictionary = semantics_r14.data.duplicate(false)
			var warmup_arr: Array = cloned_data.get("warmup", []).duplicate()
			for w_row in rec.get("warmup_spawns", []):
				var w_center := vector(w_row.center)
				var exists := false
				for old_w in warmup_arr:
					if vector(old_w.center).distance_to(w_center) < 0.05:
						exists = true
						break
				if not exists:
					warmup_arr.append({
						"center": w_row.center.duplicate(),
						"normal": w_row.normal.duplicate(),
						"forward": w_row.forward.duplicate(),
						"evidence": str(w_row.get("evidence", ""))
					})
					appended_warmup += 1
			cloned_data["warmup"] = warmup_arr
			semantics_r14.data = cloned_data
		if semantics_r14.get("weapon_sites_r17") is Array:
			for site_vec in out_sites:
				var dup := false
				for old_site in semantics_r14.weapon_sites_r17:
					if (old_site as Vector3).distance_to(site_vec) < 0.5:
						dup = true
						break
				if not dup:
					semantics_r14.weapon_sites_r17.append(site_vec)
		if semantics_r14.has_method("set_markers_visible"):
			semantics_r14.set_markers_visible(not live_session)
	return {"appended_warmup": appended_warmup, "weapon_sites": out_sites}

static func _shape_bounds(shape: ConcavePolygonShape3D, relative: Transform3D) -> AABB:
	var faces: PackedVector3Array = shape.get_faces()
	if faces.is_empty():
		return AABB()
	var first: Vector3 = relative * faces[0]
	var box := AABB(first, Vector3.ZERO)
	for i in range(1, faces.size()):
		box = box.expand(relative * faces[i])
	return box

static func _matches_removed_triangle(removed: Dictionary, center: Vector3, normal: Vector3, area: float) -> bool:
	var bin := Vector3i((center * 250.0).round())
	for x in range(-1, 2):
		for y in range(-1, 2):
			for z in range(-1, 2):
				for tri in removed.get(bin + Vector3i(x, y, z), []):
					if center.distance_squared_to(tri.center) > 0.0045 * 0.0045:
						continue
					if absf(normal.dot(tri.normal)) < 0.985:
						continue
					if absf(area - float(tri.area)) > maxf(area, float(tri.area)) * 0.12 + 0.0001:
						continue
					return true
	return false

static func _vertex_components(collision_vertices: Dictionary, point: Vector3) -> Dictionary:
	var result: Dictionary = {}
	var bin := Vector3i((point * 250.0).round())
	for x in range(-1, 2):
		for y in range(-1, 2):
			for z in range(-1, 2):
				for row in collision_vertices.get(bin + Vector3i(x, y, z), []):
					if point.distance_squared_to(row.point) <= 0.0045 * 0.0045:
						result[row.component] = true
	return result

static func _matches_removed_component(collision_vertices: Dictionary, collision_boxes: Dictionary, a: Vector3, b: Vector3, c: Vector3, center: Vector3) -> bool:
	var first := _vertex_components(collision_vertices, a)
	if first.is_empty():
		return false
	var second := _vertex_components(collision_vertices, b)
	if second.is_empty():
		return false
	var third := _vertex_components(collision_vertices, c)
	for comp_id in first:
		if second.has(comp_id) and third.has(comp_id):
			var cbox: AABB = collision_boxes.get(comp_id, AABB())
			if cbox.has_point(center):
				return true
	return false
