extends SceneTree

const FixturesR18 = preload("res://scripts/map_fixtures_r18.gd")
const PanelR8 = preload("res://scripts/archive_panel_r8.gd")
const SemR14 = preload("res://scripts/map_semantics_r14.gd")

class DummyNetNode extends Node:
	var active: bool = false
	var server: bool = true
	var stage: String = "match"
	var poses: Dictionary = {}
	var archive_broken_r8: Dictionary = {}
	func _publish() -> void:
		pass

class DummyBridge extends Node:
	var net: DummyNetNode = null
	var is_live: bool = false
	func _init() -> void:
		net = DummyNetNode.new()
		add_child(net)
	func live() -> bool:
		return is_live

class DummyPlayer extends Node3D:
	var input_enabled: bool = false

class DummyGame extends Node:
	var network_bridge: DummyBridge = null
	var player: DummyPlayer = null
	var front_end_active: bool = false
	func _init() -> void:
		network_bridge = DummyBridge.new()
		add_child(network_bridge)
		player = DummyPlayer.new()
		add_child(player)

class DummyMap extends Node3D:
	var map_id: String = ""
	var map_ready: bool = false
	var _meta: Dictionary = {}
	var _geometry: Node3D = null
	var _details_r8: Dictionary = {}
	var semantics_r14: Node3D = null
	var statistics: Dictionary = {}
	var _game: DummyGame = null
	func _init() -> void:
		_game = DummyGame.new()
		add_child(_game)
	func _outside(_p: Vector3) -> bool:
		return false
	func _floor(from: Vector3, depth: float) -> Vector3:
		var q := PhysicsRayQueryParameters3D.create(from, from - Vector3.UP * depth, 1)
		var hit: Dictionary = get_world_3d().direct_space_state.intersect_ray(q)
		return hit.position if not hit.is_empty() and hit.normal.y > 0.7 else Vector3.INF
	func _room(_p: Vector3) -> bool:
		return true
	func safe_spawn_r8(_p: Vector3) -> bool:
		return true

func _init() -> void:
	ProjectSettings.load_resource_pack("D:/Codex/ASHFALL-R15-Quest3/candidate/builds/ASHFALL-Local-Maps.pck", false)
	ProjectSettings.load_resource_pack("D:/Codex/ASHFALL-R15-Quest3/candidate/builds/ASHFALL-Map-Adapters-R10.pck", false)
	call_deferred("_run_tests")

func _fail(msg: String) -> void:
	printerr("FAIL: ", msg)
	quit(1)

func _assert(cond: bool, msg: String) -> void:
	if not cond:
		_fail(msg)

func _run_tests() -> void:
	await process_frame
	print("=== TEST 1: Fingerprint mismatch, unsupported Phoenix, & source_sha256 gates ===")
	var cat: Dictionary = FixturesR18.catalog()
	var maps_cat: Dictionary = cat.get("maps", {})
	_assert(int(cat.get("supported_tower_map_count", 0)) == 9, "Expected 9 supported tower maps")
	_assert(maps_cat.size() >= 10, "Expected at least 10 supported maps in map_fixtures_r18.json, got %d" % maps_cat.size())
	for slug in maps_cat:
		var m_row: Dictionary = maps_cat[slug]
		var sha: String = str(m_row.get("source_sha256", ""))
		_assert(sha.length() == 64, "Map %s must have a 64-hex source_sha256 from source/map.glb, got '%s'" % [slug, sha])
		if not m_row.get("tower_gate_grates", []).is_empty():
			_assert(int(m_row.get("expected_member_specs", 0)) == 56, "Tower map %s must expect 56 member specs" % slug)
			_assert(int(m_row.get("expected_triangles", 0)) == 368, "Tower map %s must expect 368 triangles" % slug)

	var alpha_res: Resource = load("res://local_archive_r10/alfa-aglae-souza.res")
	_assert(alpha_res != null, "Failed to load alfa-aglae-souza.res")
	var alpha_fp: String = str(alpha_res.get("source_fingerprint"))
	var original_source_panel_count: int = alpha_res.details.panels.size()

	var dummy_mismatch := DummyMap.new()
	dummy_mismatch.map_id = "archive:alfa-aglae-souza"
	dummy_mismatch._meta = {"cache_fingerprint": "0000_wrong_fingerprint"}
	dummy_mismatch._geometry = alpha_res.get("geometry").instantiate()
	dummy_mismatch._details_r8 = alpha_res.details.duplicate(false)
	root.add_child(dummy_mismatch)
	dummy_mismatch.add_child(dummy_mismatch._geometry)
	await process_frame

	var bad_res: Dictionary = FixturesR18.apply(dummy_mismatch)
	_assert(bad_res.get("supported", true) == false, "Mismatch fingerprint must return supported=false")
	_assert(int(bad_res.get("extracted_openings", -1)) == 0, "Mismatch fingerprint must extract 0 openings")
	_assert(int(bad_res.get("removed_visual_triangles", -1)) == 0, "Mismatch fingerprint must cut 0 visual triangles")
	_assert(int(bad_res.get("removed_collision_triangles", -1)) == 0, "Mismatch fingerprint must cut 0 collision triangles")
	dummy_mismatch.free()

	var phx_check: Dictionary = FixturesR18.unsupported_reason("archive:phoenix-bigbox-vr")
	_assert(str(phx_check.get("status", "")) == "unsupported_different_template", "Phoenix must be explicitly labeled unsupported_different_template")
	print("  PASS: Fingerprint mismatch, Phoenix unsupported, and 64-hex source_sha256 verified.")

	print("=== TEST 2: Shallow resource.details.duplicate(false) safety across multiple map instantiations ===")
	var alpha_map := DummyMap.new()
	alpha_map.map_id = "archive:alfa-aglae-souza"
	alpha_map._meta = {"cache_fingerprint": alpha_fp}
	alpha_map._geometry = alpha_res.geometry.instantiate()
	alpha_map._details_r8 = alpha_res.details.duplicate(false)
	root.add_child(alpha_map)
	alpha_map.add_child(alpha_map._geometry)
	await process_frame

	var stats1: Dictionary = FixturesR18.apply(alpha_map)
	_assert(bool(stats1.get("supported", false)) == true, "Alpha must be supported")
	_assert(str(stats1.get("status", "")) == "ok", "Alpha status must be ok")
	_assert(int(stats1.get("expected_member_specs", 0)) == 56, "Alpha must expect 56 member specs")
	_assert(int(stats1.get("matched_member_specs", 0)) == 56, "Alpha must match all 56 member specs")
	_assert(int(stats1.get("extracted_openings", 0)) == 8, "Alpha must extract 8 openings")
	_assert(int(stats1.get("removed_visual_triangles", 0)) == 368, "Alpha must cut 368 visual triangles")
	_assert(int(stats1.get("removed_collision_triangles", 0)) == 368, "Alpha must cut 368 collision triangles")
	_assert(int(stats1.get("appended_panels", 0)) == 8, "Alpha must append 8 panels")
	_assert(alpha_map._details_r8.panels.size() == original_source_panel_count + 8, "First map details.panels must equal original_count + 8")
	_assert(alpha_res.details.panels.size() == original_source_panel_count, "Cached resource.details.panels MUST remain unchanged (%d)" % original_source_panel_count)

	var stats2: Dictionary = FixturesR18.apply(alpha_map)
	_assert(bool(stats2.get("idempotent_skip", false)) == true, "Second apply on same geometry must set idempotent_skip=true")
	_assert(int(stats2.get("appended_panels", -1)) == 0, "Second apply must append 0 panels")
	_assert(alpha_map._details_r8.panels.size() == original_source_panel_count + 8, "Second apply must not duplicate panels")

	var alpha_map_second := DummyMap.new()
	alpha_map_second.map_id = "archive:alfa-aglae-souza"
	alpha_map_second._meta = {"cache_fingerprint": alpha_fp}
	alpha_map_second._geometry = alpha_res.geometry.instantiate()
	alpha_map_second._details_r8 = alpha_res.details.duplicate(false)
	root.add_child(alpha_map_second)
	alpha_map_second.add_child(alpha_map_second._geometry)
	await process_frame

	var stats_second_entry: Dictionary = FixturesR18.apply(alpha_map_second)
	_assert(str(stats_second_entry.get("status", "")) == "ok", "Second map entry must succeed with status=ok")
	_assert(int(stats_second_entry.get("matched_member_specs", 0)) == 56, "Second map entry must match all 56 member specs")
	_assert(int(stats_second_entry.get("appended_panels", 0)) == 8, "Second map entry must append 8 panels")
	_assert(alpha_map_second._details_r8.panels.size() == original_source_panel_count + 8, "Second map entry details.panels must be original_count + 8 (not +16)")
	_assert(alpha_res.details.panels.size() == original_source_panel_count, "Cached resource.details.panels MUST STILL remain unchanged (%d)" % original_source_panel_count)
	alpha_map_second.free()
	print("  PASS: Shallow resource.details.duplicate(false) safety verified across repeated map entries.")

	print("=== TEST 3: Fail-closed verification on incomplete/corrupted geometry ===")
	var corrupted_map := DummyMap.new()
	corrupted_map.map_id = "archive:alfa-aglae-souza"
	corrupted_map._meta = {"cache_fingerprint": alpha_fp}
	corrupted_map._geometry = alpha_res.geometry.instantiate()
	corrupted_map._details_r8 = alpha_res.details.duplicate(false)
	root.add_child(corrupted_map)
	corrupted_map.add_child(corrupted_map._geometry)
	await process_frame

	for view in corrupted_map._geometry.find_children("*", "MeshInstance3D", true, false):
		if view.material_override != null and view.material_override.resource_name == "s15":
			view.mesh = ArrayMesh.new()
			break

	var corrupt_stats: Dictionary = FixturesR18.apply(corrupted_map)
	_assert(str(corrupt_stats.get("status", "")) == "error_incomplete_visual_match", "Corrupted map must fail closed with error_incomplete_visual_match")
	_assert(int(corrupt_stats.get("appended_panels", -1)) == 0, "Corrupted map must append 0 panels")
	_assert(int(corrupt_stats.get("removed_visual_triangles", -1)) == 0, "Corrupted map must cut 0 visual triangles")
	_assert(int(corrupt_stats.get("removed_collision_triangles", -1)) == 0, "Corrupted map must cut 0 collision triangles")
	_assert(corrupted_map._details_r8.panels.size() == original_source_panel_count, "Corrupted map panels must remain at original_source_panel_count")
	corrupted_map.free()
	print("  PASS: Fail-closed behavior verified (zero partial mutations on missing parts).")

	print("=== TEST 4: Alpha collision transfer, contact-path break, take_damage break, and adjacent-floor rays ===")
	var grate_panels: Array[Node] = []
	for i in range(original_source_panel_count, alpha_map._details_r8.panels.size()):
		var row: Dictionary = alpha_map._details_r8.panels[i]
		_assert(bool(row.get("glass", true)) == false, "Grate row must have glass=false")
		_assert(str(row.get("sound_r18", "")) == "res://assets/audio/sfx/r18/metal_grate.wav", "Grate row must have sound_r18 contract")
		_assert(row.get("attachments", []).size() == 6, "Grate row must have 6 attachments (1 inner plane + 4 bars + 1 cap)")
		var panel := PanelR8.new()
		alpha_map.add_child(panel)
		panel.setup(alpha_map, str(i), row)
		panel.set_physics_process(false)
		grate_panels.append(panel)

	await physics_frame
	await physics_frame

	var space: PhysicsDirectSpaceState3D = alpha_map.get_world_3d().direct_space_state

	for idx in grate_panels.size():
		var panel: Node = grate_panels[idx]
		var row: Dictionary = alpha_map._details_r8.panels[original_source_panel_count + idx]
		var c: Vector3 = (row.box as AABB).get_center()
		var cardinal: String = str(row.get("cardinal_r18", ""))
		var dir := Vector3(0, 0, 1.5) if cardinal in ["north", "south"] else Vector3(1.5, 0, 0)
		var q := PhysicsRayQueryParameters3D.create(c - dir, c + dir, 1)
		q.hit_back_faces = true
		var hit: Dictionary = space.intersect_ray(q)
		_assert(not hit.is_empty(), "Pre-break ray must hit grate opening %s at %s" % [row.get("opening_id_r18"), str(c)])
		_assert(hit.collider == panel, "Pre-break ray at %s hit %s instead of BreakablePassageR8 panel" % [row.get("opening_id_r18"), str(hit.collider)])

	var floor_probes: Array[Vector3] = [
		Vector3(-157.72, 22.0, 81.5),
		Vector3(-146.68, 22.0, 81.5),
		Vector3(-156.31, 22.0, -43.0),
		Vector3(-212.0, 22.0, 29.77),
		Vector3(-93.5, 22.0, 18.91)
	]
	for fp_pt in floor_probes:
		var fq := PhysicsRayQueryParameters3D.create(fp_pt, fp_pt + Vector3.DOWN * 4.0, 1)
		var fhit: Dictionary = space.intersect_ray(fq)
		_assert(not fhit.is_empty(), "Pre-break adjacent floor control ray at %s must hit floor" % str(fp_pt))

	# Test authoritative walk-through contact dispatch via panel._physics_process on panel 0
	var first_panel: Node = grate_panels[0]
	var first_center: Vector3 = (alpha_map._details_r8.panels[original_source_panel_count].box as AABB).get_center()
	alpha_map._game.network_bridge.net.active = true
	alpha_map._game.network_bridge.net.server = true
	alpha_map._game.network_bridge.net.stage = "match"
	alpha_map._game.network_bridge.net.poses = {
		"peer_1": {
			"root": Transform3D(Basis.IDENTITY, first_center - Vector3.UP * 0.9),
			"head": Transform3D(Basis.IDENTITY, first_center)
		}
	}
	first_panel._physics_process(0.2)
	_assert(first_panel._broken == true and alpha_map._game.network_bridge.net.archive_broken_r8.has(first_panel.key), "Authoritative contact pose must break grate panel 0 and record key in archive_broken_r8")
	alpha_map._game.network_bridge.net.active = false

	# Break remaining 7 grate panels via take_damage
	for idx in range(1, grate_panels.size()):
		var panel: Node = grate_panels[idx]
		panel.take_damage(1.0)
		_assert(panel._broken == true and panel.view.visible == false and panel.collision_layer == 0, "Panel must be broken and hidden after take_damage")

	await physics_frame
	await physics_frame

	for idx in grate_panels.size():
		var row: Dictionary = alpha_map._details_r8.panels[original_source_panel_count + idx]
		var c: Vector3 = (row.box as AABB).get_center()
		var cardinal: String = str(row.get("cardinal_r18", ""))
		var dir := Vector3(0, 0, 1.5) if cardinal in ["north", "south"] else Vector3(1.5, 0, 0)
		var q := PhysicsRayQueryParameters3D.create(c - dir, c + dir, 1)
		q.hit_back_faces = true
		var hit: Dictionary = space.intersect_ray(q)
		_assert(hit.is_empty(), "Post-break ray through %s at %s must be clear, but hit %s" % [row.get("opening_id_r18"), str(c), str(hit.get("collider"))])

	for fp_pt in floor_probes:
		var fq := PhysicsRayQueryParameters3D.create(fp_pt, fp_pt + Vector3.DOWN * 4.0, 1)
		var fhit: Dictionary = space.intersect_ray(fq)
		_assert(not fhit.is_empty(), "Post-break adjacent floor control ray at %s must STILL hit floor" % str(fp_pt))

	alpha_map.free()
	print("  PASS: Alpha 8 upper grate assemblies verified (contact dispatch, damage hook, clear passage, adjacent floors preserved).")

	print("=== TEST 5: Verify all 9 supported tower-gate map variants (56/56 specs, 368/368 tris, cached resource unmutated) ===")
	for slug in maps_cat.keys():
		if maps_cat[slug].get("tower_gate_grates", []).is_empty():
			continue
		var res_path := "res://local_archive_r10/%s.res" % slug
		var res: Resource = load(res_path)
		_assert(res != null, "Missing adapter resource for %s" % slug)
		var orig_panels: int = res.details.panels.size()
		var m := DummyMap.new()
		m.map_id = "archive:" + slug
		m._meta = {"cache_fingerprint": str(res.get("source_fingerprint"))}
		m._geometry = res.get("geometry").instantiate()
		m._details_r8 = res.details.duplicate(false)
		root.add_child(m)
		m.add_child(m._geometry)
		await process_frame
		var st: Dictionary = FixturesR18.apply(m)
		print("  Variant %-32s status=%s specs=%d/%d openings=%d vis_tris=%d col_tris=%d batch=%s" % [
			slug,
			str(st.get("status", "")),
			int(st.get("matched_member_specs", 0)),
			int(st.get("expected_member_specs", 0)),
			int(st.get("extracted_openings", 0)),
			int(st.get("removed_visual_triangles", 0)),
			int(st.get("removed_collision_triangles", 0)),
			str(st.get("template_batch", ""))
		])
		_assert(str(st.get("status", "")) == "ok", "%s status must be ok" % slug)
		_assert(int(st.get("matched_member_specs", 0)) == 56 and int(st.get("expected_member_specs", 0)) == 56, "%s must match 56/56 member specs" % slug)
		_assert(int(st.get("extracted_openings", 0)) == 8, "%s must extract 8 openings" % slug)
		_assert(int(st.get("removed_visual_triangles", 0)) == 368, "%s must cut 368 visual triangles" % slug)
		_assert(int(st.get("removed_collision_triangles", 0)) == 368, "%s must cut 368 collision triangles" % slug)
		_assert(int(st.get("appended_panels", 0)) == 8, "%s must append 8 panels" % slug)
		_assert(m._details_r8.panels.size() == orig_panels + 8, "%s details.panels must be orig + 8" % slug)
		_assert(res.details.panels.size() == orig_panels, "%s cached resource.details.panels must remain unchanged" % slug)
		m.free()

	print("=== TEST 6: Afterlife (afterlife-gods-alamo-hank) 20/20 warmup symbols, floor preservation, READY/hosted visibility, & cached resource safety ===")
	var al_res: Resource = load("res://local_archive_r10/afterlife-gods-alamo-hank.res")
	_assert(al_res != null, "Failed to load afterlife-gods-alamo-hank.res")
	var al_fp: String = str(al_res.get("source_fingerprint"))
	var r14_static_warmup_count: int = SemR14.catalog().get("maps", {}).get("afterlife-gods-alamo-hank", {}).get("warmup", []).size()
	_assert(r14_static_warmup_count == 1, "Expected static SemR14 catalog for Afterlife to have 1 isolated warmup ring before R18")

	var al_map := DummyMap.new()
	al_map.map_id = "archive:afterlife-gods-alamo-hank"
	al_map._meta = {"cache_fingerprint": al_fp}
	al_map._geometry = al_res.geometry.instantiate()
	al_map._details_r8 = al_res.details.duplicate(false)
	al_map._game.network_bridge.is_live = false # READY / offline inspection mode first
	root.add_child(al_map)
	al_map.add_child(al_map._geometry)
	al_map.semantics_r14 = SemR14.new()
	al_map.add_child(al_map.semantics_r14)
	await process_frame

	al_map.semantics_r14.setup(al_map)
	_assert(al_map.semantics_r14.removed_visual_triangles == 116, "SemR14 must remove 116 visual triangles (1 ring + 2 arrows) on Afterlife")
	_assert(al_map.semantics_r14.removed_collision_triangles == 116, "SemR14 must remove 116 collision triangles on Afterlife")

	var al_combined: Dictionary = FixturesR18.apply(al_map)
	var al_mstats: Dictionary = al_combined.get("markers_r18", {})
	print("  Afterlife markers_r18 apply #1: ", JSON.stringify(al_mstats))
	_assert(bool(al_mstats.get("supported", false)) == true, "Afterlife marker recovery must be supported")
	_assert(str(al_mstats.get("status", "")) == "ok", "Afterlife marker recovery status must be ok")
	_assert(int(al_mstats.get("expected_parts", 0)) == 37 and int(al_mstats.get("matched_parts", 0)) == 37, "Afterlife must match all 37/37 residual edge-connected marker parts")
	_assert(int(al_mstats.get("removed_visual_triangles", 0)) == 2084, "Afterlife must remove 2084 residual visual triangles (19 rings * 104 + 18 arrows * 6)")
	_assert(int(al_mstats.get("removed_collision_triangles", 0)) == 2084, "Afterlife must remove 2084 residual collision triangles")
	_assert(int(al_mstats.get("total_handled_marker_parts", 0)) == 40, "Afterlife total handled marker parts (R14 + R18) must be 40")
	_assert(int(al_mstats.get("total_handled_marker_triangles", 0)) == 2200, "Afterlife total handled marker triangles (R14 + R18) must be 2200")
	_assert(int(al_mstats.get("total_warmup_symbols", 0)) == 20 and int(al_mstats.get("recovered_warmup_symbols", 0)) == 19, "Afterlife must recover 19 warmup symbols for 20 total")
	_assert(al_map.semantics_r14.data.get("warmup", []).size() == 20, "al_map.semantics_r14.data.warmup must have all 20 warmup symbols")
	_assert(SemR14.catalog().get("maps", {}).get("afterlife-gods-alamo-hank", {}).get("warmup", []).size() == 1, "Static SemR14._catalog MUST remain unmutated (1 warmup symbol)")

	# Verify 0 remaining p054b1b60 triangles in al_map._geometry
	for view in al_map._geometry.find_children("*", "MeshInstance3D", true, false):
		if view.material_override != null and view.material_override.resource_name == "p054b1b60":
			var rem_faces: int = view.mesh.get_faces().size() if view.mesh != null else 0
			_assert(rem_faces == 0, "Expected 0 remaining p054b1b60 faces in _geometry, got %d" % rem_faces)

	# Verify READY inspection visibility vs Hosted visibility under semantics_r14.marker_visuals
	var marker_vis: Node3D = al_map.semantics_r14.marker_visuals
	_assert(marker_vis != null and marker_vis.get_child_count() == 2, "OriginalSpawnMarkersR14 must hold 2 chunks (1 from R14 + 1 from R18)")
	var total_vis_tris := 0
	for ch in marker_vis.get_children():
		if ch is MeshInstance3D and ch.mesh != null:
			total_vis_tris += ch.mesh.get_faces().size() / 3
	_assert(total_vis_tris == 2200, "OriginalSpawnMarkersR14 must contain all 2200 warmup marker triangles for READY inspection, got %d" % total_vis_tris)
	_assert(marker_vis.visible == true, "OriginalSpawnMarkersR14 must be visible in READY / offline inspection (live=false)")

	al_map._game.network_bridge.is_live = true
	al_map.semantics_r14.set_markers_visible(not al_map._game.network_bridge.live())
	_assert(marker_vis.visible == false, "OriginalSpawnMarkersR14 must be hidden in hosted play (live=true)")

	await physics_frame
	await physics_frame

	# Verify all 20 warmup spawns validate against the preserved pb8a19331 lobby floor (top Y = 417.76)
	var validated_warmup: Array = al_map.semantics_r14.validated_spawns("warmup")
	_assert(validated_warmup.size() == 20, "All 20 warmup spawns on Afterlife must validate against the preserved lobby floor, got %d" % validated_warmup.size())
	var al_space: PhysicsDirectSpaceState3D = al_map.get_world_3d().direct_space_state
	for w_site in al_map.semantics_r14.data.get("warmup", []):
		var wc: Vector3 = FixturesR18.vector(w_site.center)
		# Raycast down through the ring rim (offset +0.55m X from ring center) where the ring collision used to sit at Y~417.85
		var rim_probe := wc + Vector3(0.55, 1.0, 0.0)
		var rq := PhysicsRayQueryParameters3D.create(rim_probe, rim_probe + Vector3.DOWN * 3.0, 1)
		var rhit: Dictionary = al_space.intersect_ray(rq)
		_assert(not rhit.is_empty(), "Lobby floor raycast under ring rim at %s must hit floor" % str(rim_probe))
		_assert(absf(rhit.position.y - 417.76) < 0.03, "Lobby floor raycast at %s must hit floor at Y=417.76 (not ring at 417.85), got Y=%.4f" % [str(rim_probe), rhit.position.y])

	# Verify same-geometry idempotence on Afterlife
	var al_idem: Dictionary = FixturesR18.apply(al_map).get("markers_r18", {})
	_assert(bool(al_idem.get("idempotent_skip", false)) == true, "Second apply on Afterlife must set idempotent_skip=true")
	_assert(al_map.semantics_r14.data.get("warmup", []).size() == 20, "Second apply on Afterlife must not duplicate warmup spawns")
	al_map.free()

	# Instantiate a SECOND Afterlife map from the same cached al_res and prove cached resource meshes/collision were NOT mutated
	var al_map2 := DummyMap.new()
	al_map2.map_id = "archive:afterlife-gods-alamo-hank"
	al_map2._meta = {"cache_fingerprint": al_fp}
	al_map2._geometry = al_res.geometry.instantiate()
	al_map2._details_r8 = al_res.details.duplicate(false)
	root.add_child(al_map2)
	al_map2.add_child(al_map2._geometry)
	al_map2.semantics_r14 = SemR14.new()
	al_map2.add_child(al_map2.semantics_r14)
	await process_frame

	al_map2.semantics_r14.setup(al_map2)
	_assert(al_map2.semantics_r14.removed_visual_triangles == 116 and al_map2.semantics_r14.removed_collision_triangles == 116, "Second Afterlife load from cached Resource must still have all 116 R14 triangles intact")
	var al_mstats2: Dictionary = FixturesR18.apply(al_map2).get("markers_r18", {})
	print("  Afterlife markers_r18 on 2nd map entry from cached resource: ", JSON.stringify(al_mstats2))
	_assert(str(al_mstats2.get("status", "")) == "ok", "Second Afterlife load must succeed with status=ok")
	_assert(int(al_mstats2.get("matched_parts", 0)) == 37 and int(al_mstats2.get("removed_visual_triangles", 0)) == 2084 and int(al_mstats2.get("removed_collision_triangles", 0)) == 2084, "Second Afterlife load must match 37/37 parts and cut 2084 visual & 2084 collision triangles")
	_assert(al_map2.semantics_r14.data.get("warmup", []).size() == 20, "Second Afterlife load must have 20 warmup spawns")
	_assert(SemR14.catalog().get("maps", {}).get("afterlife-gods-alamo-hank", {}).get("warmup", []).size() == 1, "Static SemR14._catalog must still have 1 warmup symbol")
	al_map2.free()
	print("  PASS: Afterlife 20/20 warmup symbols, READY/hosted visibility, floor preservation, and cached resource non-mutation verified.")

	await process_frame
	print("=== ALL R18 TOWER-GATE GRATE & AFTERLIFE MARKER HELPER TESTS PASSED (ZERO ERRORS) ===")
	quit(0)
