extends SceneTree
## External, read-only source adapter. Run only against an isolated reference copy.
## No terrain/math substitutes: the actual source builders and resources do the work.

class RangePlayer:
	extends Node3D
	func reset_motion():
		pass

class RangeInventory:
	extends RefCounted
	var pickups = {}
	var review_owned_props = {}
	var review_positions = {}

class RangeContext:
	extends Node
	var player
	var world
	var weapon_inventory
	var active_sandbox_map
	var training = null
	func _begin_chapter():
		pass

var request = {}
var out_dir = ""
var payload
var started = 0
var failed = false
var document = {}
var resource_ids = {}
var resources = []
var mesh_ids = {}
var meshes = []
var pending_textures = []
var discovered = {}
var source_hashes = {}
var unsupported = []
var warnings = []
var nodes_seen = 0
var bounds_all = AABB()
var bounds_visible = AABB()
var have_bounds_all = false
var have_bounds_visible = false
var texture_bytes = 0
var captured_batches = {}

func _initialize():
	call_deferred("_run")

func _fail(message):
	if failed:
		return
	failed = true
	push_error(message)
	if payload != null:
		payload.close()
		payload = null
	if not out_dir.is_empty() and DirAccess.dir_exists_absolute(out_dir):
		_write_json(out_dir.path_join("failure.json"), {"complete": false, "error": message})
	quit(1)

func _run():
	started = Time.get_ticks_msec()
	var request_path = ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--request="):
			request_path = arg.trim_prefix("--request=")
	request = JSON.parse_string(FileAccess.get_file_as_string(request_path))
	if not request is Dictionary or request.get("version") != 1:
		_fail("Expected external --request=<absolute request.json> version1")
		return
	var project_root = ProjectSettings.globalize_path("res://").replace("\\", "/").trim_suffix("/")
	if project_root.to_lower() != str(request.get("referenceRoot", "")).replace("\\", "/").trim_suffix("/").to_lower():
		_fail("--path must name the disposable referenceRoot, never the original source")
		return
	var original_root = str(request.get("originalRoot", "")).replace("\\", "/").trim_suffix("/")
	if project_root.to_lower() == original_root.to_lower():
		_fail("Original source execution is prohibited")
		return
	var candidate_output = str(request.get("output", "")).replace("\\", "/").simplify_path()
	if original_root.is_empty() or not candidate_output.is_absolute_path() or candidate_output.to_lower().begins_with(project_root.to_lower() + "/") or candidate_output.to_lower().begins_with(original_root.to_lower() + "/"):
		_fail("Output must be absolute and outside source/reference projects")
		return
	if DirAccess.dir_exists_absolute(candidate_output) and (not DirAccess.get_files_at(candidate_output).is_empty() or not DirAccess.get_directories_at(candidate_output).is_empty()):
		_fail("Output is already populated; choose a fresh run directory")
		return
	out_dir = candidate_output
	if DirAccess.make_dir_recursive_absolute(out_dir.path_join("textures")) != OK or DirAccess.make_dir_recursive_absolute(out_dir.path_join("shaders")) != OK:
		_fail("Could not create external output folders")
		return
	for relative in request.get("criticalSourceHashes", {}):
		var actual = FileAccess.get_sha256("res://" + relative)
		if actual != request.criticalSourceHashes[relative]:
			_fail("Pinned source mismatch: " + relative)
			return
		source_hashes[relative] = actual
	var map_id = request.get("map", "")
	if map_id not in ["valley", "range"]:
		_fail("Only genuine valley/range builders are supported")
		return
	create_timer(float(request.get("timeoutSeconds", 180))).timeout.connect(func(): _fail("Export deadline exceeded"))
	var source_script = load("res://scripts/world.gd" if map_id == "valley" else "res://scripts/sandbox_map_091.gd")
	if source_script == null:
		_fail("Actual builder script could not load")
		return
	# Dummy RenderingServer cannot round-trip MultiMesh transforms. Observe the
	# actual builder's input before its normal flush clears it; do not edit the
	# pinned world.gd or reconstruct poses from collision/material ordering.
	if map_id == "valley":
		var observer = GDScript.new()
		observer.source_code = "extends \"res://scripts/world.gd\"\nvar export_batches = {}\nfunc _flush_batches() -> void:\n\tfor material in batches:\n\t\tassert(not export_batches.has(material), \"Duplicate source batch flush\")\n\t\texport_batches[material] = batches[material].duplicate()\n\tsuper._flush_batches()\n"
		if observer.reload() != OK:
			_fail("Read-only source batch observer could not compile")
			return
		source_script = observer
	var world = source_script.new()
	root.add_child(world)
	var spawn
	if map_id == "valley":
		world.build()
		captured_batches = world.export_batches
		spawn = world.player_spawn
	else:
		# Invoke the actual enter() unchanged. These stubs absorb player/inventory
		# side effects only; all geometry/material/collision/placement comes from source.
		var context = RangeContext.new()
		context.weapon_inventory = RangeInventory.new()
		context.player = RangePlayer.new()
		context.world = Node3D.new()
		root.add_child(context)
		context.add_child(context.player)
		context.add_child(context.world)
		world.enter(context)
		spawn = world.spawn
	_freeze(world)
	await process_frame
	_discover(world)
	await _wait_for_generated_images()
	if failed:
		return
	payload = FileAccess.open(out_dir.path_join("payload.bin"), FileAccess.WRITE)
	if payload == null:
		_fail("Cannot create payload.bin")
		return
	document = {
		"version": 1, "map": map_id, "complete": false,
		"coordinateSystem": "Raw Godot RH Y-up, forward -Z; untouched clockwise source faces; no reflection, rebasing or winding edits",
		"transformEncoding": "basis columns X,Y,Z followed by origin XYZ; column-major3x4, positions local to mesh/shape resource",
		"binaryEncoding": "Native Godot packed-array to_byte_array bytes; little-endian source host; array descriptor records Variant type, count, components, scalar bytes, offset and length",
		"byteOrder": "little" if PackedInt32Array([0x01020304]).to_byte_array()[0] == 4 else "big",
		"godot": Engine.get_version_info(), "sourceBaselineSha256": request.get("baselineSha256"),
		"sourceTreeDigest": request.get("sourceTreeDigest"), "referenceRoot": project_root,
		"worldTransform": _transform(world.global_transform), "spawn": _v3(spawn),
		"meshInstances": [], "multiMeshes": [], "staticBodies": [], "lights": [], "environments": [], "labels": [],
		"sourceBuilder": "scripts/world.gd::build" if map_id == "valley" else "scripts/sandbox_map_091.gd::enter",
		"rangeContext": "Geometry-only side-effect stubs; source enter and its +600Z placement are unchanged" if map_id == "range" else null
	}
	if map_id == "valley":
		document["authoredMarkers"] = {}
		for property in ["extraction_position", "extraction_spawn_points", "objective_positions", "enemy_spawn_points", "encounter_spawns"]:
			document.authoredMarkers[property] = _encode(world.get(property))
	else:
		document["environmentInheritance"] = "sandbox_map_091 defines no environment; actual main retains Valley atmosphere while hiding Valley geometry. Use the genuine Valley environment export."
	_walk(world)
	if failed:
		return
	document["meshes"] = meshes
	document["resources"] = resources
	document["sourceHashes"] = source_hashes
	document["unsupported"] = unsupported
	document["warnings"] = warnings
	document["boundsAll"] = _aabb(bounds_all) if have_bounds_all else null
	document["boundsVisible"] = _aabb(bounds_visible) if have_bounds_visible else null
	document["bytes"] = payload.get_position()
	payload.close()
	payload = null
	document["payloadSha256"] = FileAccess.get_sha256(out_dir.path_join("payload.bin"))
	document["elapsedMilliseconds"] = Time.get_ticks_msec() - started
	document["complete"] = true
	_write_json(out_dir.path_join("world.json"), document)
	_write_json(out_dir.path_join("complete.json"), {"complete": true, "map": map_id, "worldSha256": FileAccess.get_sha256(out_dir.path_join("world.json")), "payloadSha256": document.payloadSha256, "unsupportedCount": unsupported.size()})
	print("SOURCE_WORLD_EXPORT complete map=", map_id, " meshes=", meshes.size(), " unsupported=", unsupported.size(), " output=", out_dir)
	world.queue_free()
	await process_frame
	quit(0)

func _freeze(node):
	node.set_process(false)
	node.set_physics_process(false)
	for child in node.get_children():
		_freeze(child)

func _discover(value):
	if value is Object:
		var key = value.get_instance_id()
		if discovered.has(key):
			return
		discovered[key] = true
		if value is Texture2D:
			pending_textures.append(value)
		if value is MeshInstance3D and value.mesh != null:
			for i in value.mesh.get_surface_count():
				_discover(value.get_active_material(i))
		elif value is MultiMeshInstance3D and value.multimesh != null and value.multimesh.mesh != null:
			for i in value.multimesh.mesh.get_surface_count():
				_discover(value.multimesh.mesh.surface_get_material(i))
		for prop in value.get_property_list():
			if int(prop.usage) & PROPERTY_USAGE_STORAGE and prop.name not in ["script", "_surfaces"]:
				var content = value.get(prop.name)
				if content is Resource or content is Array or content is Dictionary:
					_discover(content)
		if value is Node:
			for child in value.get_children():
				_discover(child)
	elif value is Array:
		for item in value:
			_discover(item)
	elif value is Dictionary:
		for key in value:
			_discover(value[key])

func _wait_for_generated_images():
	while not failed:
		var remaining = []
		for texture in pending_textures:
			if texture is NoiseTexture2D and texture.get_image() == null:
				remaining.append(texture)
		if remaining.is_empty():
			return
		if Time.get_ticks_msec() - started > int(request.get("timeoutSeconds", 180)) * 1000:
			_fail("Generated NoiseTexture2D image not ready before deadline")
			return
		await create_timer(.05).timeout

func _walk(node):
	if failed:
		return
	nodes_seen += 1
	if nodes_seen > int(request.get("maxNodes", 200000)):
		_fail("Node export bound exceeded")
		return
	if node is MeshInstance3D and node.mesh != null:
		var row = _node(node)
		row["mesh"] = _mesh(node.mesh)
		row["materialOverride"] = _resource(node.material_override)
		row["materialOverlay"] = _resource(node.material_overlay)
		row["activeSurfaceMaterials"] = []
		for surface in node.mesh.get_surface_count():
			row.activeSurfaceMaterials.append(_resource(node.get_active_material(surface)))
		row["properties"] = _properties(node, ["mesh", "material_override", "material_overlay", "skin"])
		row["skin"] = _resource(node.skin)
		if node.skin != null:
			unsupported.append({"node": str(node.get_path()), "reason": "Skinned render instance; rest arrays and skin retained, no deformed snapshot claimed"})
		document.meshInstances.append(row)
		_add_bounds(node.global_transform * node.mesh.get_aabb(), node.is_visible_in_tree())
	elif node is MultiMeshInstance3D and node.multimesh != null:
		var mm = node.multimesh
		var row = _node(node)
		row["mesh"] = _mesh(mm.mesh)
		row["materialOverride"] = _resource(node.material_override)
		row["materialOverlay"] = _resource(node.material_overlay)
		row["transformFormat"] = mm.transform_format
		row["instanceCount"] = mm.instance_count
		row["visibleInstanceCount"] = mm.visible_instance_count
		row["useColors"] = mm.use_colors
		row["useCustomData"] = mm.use_custom_data
		row["sourceBuffer"] = _encode(mm.buffer)
		row["customAabb"] = _aabb(mm.custom_aabb)
		row["properties"] = _properties(node, ["multimesh", "material_override", "material_overlay"])
		row["instances"] = []
		if mm.transform_format == MultiMesh.TRANSFORM_3D:
			var source_transforms = []
			if request.get("map") == "valley":
				var batch_key = str(node.name).trim_prefix("Batched_")
				if not captured_batches.has(batch_key) or captured_batches[batch_key].size() != mm.instance_count:
					_fail("Missing actual source batch input for " + str(node.get_path()))
					return
				source_transforms = captured_batches[batch_key]
				row["transformSource"] = "original _flush_batches input capture"
			else:
				row["transformSource"] = "RenderingServer MultiMesh getter"
				if mm.instance_count > 0 and mm.buffer.is_empty():
					_fail("MultiMesh renderer has no retained instance buffer")
					return
			for index in mm.instance_count:
				var local = source_transforms[index] if not source_transforms.is_empty() else mm.get_instance_transform(index)
				var shown = node.is_visible_in_tree() and (mm.visible_instance_count < 0 or index < mm.visible_instance_count)
				var instance = {"index": index, "localTransform": _transform(local), "globalTransform": _transform(node.global_transform * local), "visible": shown}
				if mm.use_colors:
					instance["color"] = _color(mm.get_instance_color(index))
				if mm.use_custom_data:
					instance["customData"] = _color(mm.get_instance_custom_data(index))
				row.instances.append(instance)
				if mm.mesh != null:
					_add_bounds(node.global_transform * local * mm.mesh.get_aabb(), shown)
		else:
			unsupported.append({"node": str(node.get_path()), "reason": "2D MultiMesh in3D source export"})
		document.multiMeshes.append(row)
	if node is StaticBody3D:
		var row = _node(node)
		row["collisionLayer"] = node.collision_layer
		row["collisionMask"] = node.collision_mask
		row["physicsMaterial"] = _resource(node.physics_material_override)
		row["shapes"] = []
		for owner in node.get_shape_owners():
			var owner_node = node.shape_owner_get_owner(owner)
			var local = node.shape_owner_get_transform(owner)
			for index in node.shape_owner_get_shape_count(owner):
				row.shapes.append({"owner": str(owner_node.get_path()) if owner_node is Node else str(owner), "ownerTransform": _transform(local), "globalTransform": _transform(node.global_transform * local), "disabled": node.is_shape_owner_disabled(owner), "shape": _resource(node.shape_owner_get_shape(owner, index))})
		document.staticBodies.append(row)
	if node is Light3D:
		var row = _node(node)
		row["properties"] = _properties(node)
		document.lights.append(row)
	elif node is WorldEnvironment:
		document.environments.append({"node": str(node.get_path()), "environment": _resource(node.environment), "cameraAttributes": _resource(node.camera_attributes)})
	elif node is Label3D:
		var row = _node(node)
		row["properties"] = _properties(node)
		document.labels.append(row)
		unsupported.append({"node": str(node.get_path()), "reason": "Label3D text/font recipe preserved; no Unity glyph atlas or rendered text mesh exported"})
	for child in node.get_children():
		_walk(child)

func _node(node):
	var metadata = {}
	for key in node.get_meta_list():
		metadata[str(key)] = _encode(node.get_meta(key))
	return {"path": str(node.get_path()), "class": node.get_class(), "globalTransform": _transform(node.global_transform), "localTransform": _transform(node.transform), "visible": node.is_visible_in_tree(), "groups": _encode(node.get_groups()), "metadata": metadata}

func _mesh(mesh):
	if mesh == null:
		return null
	var identity = mesh.get_instance_id()
	if mesh_ids.has(identity):
		return mesh_ids[identity]
	var id = "mesh_%05d" % meshes.size()
	mesh_ids[identity] = id
	var row = {"id": id, "class": mesh.get_class(), "source": mesh.resource_path, "aabb": _aabb(mesh.get_aabb()), "surfaces": []}
	meshes.append(row)
	_hash_source(mesh.resource_path)
	for index in mesh.get_surface_count():
		var raw = RenderingServer.mesh_get_surface(mesh.get_rid(), index)
		var arrays = mesh.surface_get_arrays(index)
		if arrays.is_empty() or arrays[Mesh.ARRAY_VERTEX] == null or arrays[Mesh.ARRAY_VERTEX].is_empty():
			_fail("Source mesh has no CPU vertex arrays; no empty geometry export accepted: " + mesh.resource_path)
			return id
		var surface = {"index": index, "material": _resource(mesh.surface_get_material(index)), "arrays": _encode(arrays), "blendShapes": _encode(mesh.surface_get_blend_shape_arrays(index)), "serverSurface": _encode(raw)}
		if mesh is ArrayMesh:
			surface["primitive"] = mesh.surface_get_primitive_type(index)
			surface["formatMask"] = str(mesh.surface_get_format(index))
			surface["name"] = mesh.surface_get_name(index)
		elif mesh is PrimitiveMesh:
			surface["primitive"] = Mesh.PRIMITIVE_TRIANGLES
		else:
			surface["primitive"] = raw.get("primitive", -1)
		row.surfaces.append(surface)
	return id

func _resource(resource):
	if resource == null:
		return null
	if resource is Mesh:
		return {"meshRef": _mesh(resource)}
	var identity = resource.get_instance_id()
	if resource_ids.has(identity):
		return {"resourceRef": resource_ids[identity]}
	var id = "resource_%05d" % resources.size()
	resource_ids[identity] = id
	var row = {"id": id, "class": resource.get_class(), "source": resource.resource_path, "name": resource.resource_name}
	resources.append(row)
	_hash_source(resource.resource_path)
	if resource is Script:
		return {"resourceRef": id}
	if resource is Texture2D:
		row["image"] = _texture_image(resource, id)
	elif resource is Texture:
		unsupported.append({"resource": id, "reason": "Non-Texture2D image type; properties retained without invented pixels"})
	if resource is Shader:
		var path = "shaders/" + id + ".gdshader"
		_write_text(out_dir.path_join(path), resource.code)
		row["shaderFile"] = path
		row["shaderSha256"] = FileAccess.get_sha256(out_dir.path_join(path))
	if resource is ShaderMaterial:
		row["uniforms"] = {}
		if resource.shader != null:
			for uniform in resource.shader.get_shader_uniform_list():
				row.uniforms[uniform.name] = _encode(resource.get_shader_parameter(uniform.name))
		unsupported.append({"resource": id, "reason": "Source ShaderMaterial code and effective uniform values exported; Unity shader translation not supplied"})
	if resource is ConcavePolygonShape3D:
		row["faces"] = _encode(resource.get_faces())
		row["backfaceCollision"] = resource.backface_collision
	elif resource is ConvexPolygonShape3D:
		row["points"] = _encode(resource.points)
	elif resource is HeightMapShape3D:
		row["mapData"] = _encode(resource.map_data)
	elif resource is Shape3D and not (resource is BoxShape3D or resource is SphereShape3D or resource is CapsuleShape3D or resource is CylinderShape3D or resource is WorldBoundaryShape3D or resource is SeparationRayShape3D):
		unsupported.append({"resource": id, "reason": "Unknown collision recipe; properties preserved, no substitute hull"})
	row["properties"] = _properties(resource, ["code", "_data"] if resource is Shader or resource is Texture2D else [])
	return {"resourceRef": id}

func _texture_image(texture, id):
	var image = texture.get_image()
	if image == null or image.is_empty():
		unsupported.append({"resource": id, "reason": "Texture2D has no CPU image after bounded generation wait"})
		return null
	var bytes = image.get_data()
	texture_bytes += bytes.size()
	if texture_bytes > int(request.get("maxTextureBytes", 2147483648)):
		_fail("Exact texture image byte bound exceeded")
		return null
	var path = "textures/" + id + ".imagebin"
	var file = FileAccess.open(out_dir.path_join(path), FileAccess.WRITE)
	if file == null:
		_fail("Cannot write exact source texture bytes")
		return null
	file.store_buffer(bytes)
	file.close()
	var result = {"rawFile": path, "width": image.get_width(), "height": image.get_height(), "format": image.get_format(), "mipmaps": image.has_mipmaps(), "mipmapCount": image.get_mipmap_count(), "compressed": image.is_compressed(), "bytes": bytes.size(), "sha256": FileAccess.get_sha256(out_dir.path_join(path))}
	# PNG is convenience only. The exact original format/mipmap imagebin is authoritative.
	var preview = image.duplicate()
	var error = preview.decompress() if preview.is_compressed() else OK
	if error == OK:
		var float_format = image.get_format() in [Image.FORMAT_RF, Image.FORMAT_RGF, Image.FORMAT_RGBF, Image.FORMAT_RGBAF, Image.FORMAT_RH, Image.FORMAT_RGH, Image.FORMAT_RGBH, Image.FORMAT_RGBAH, Image.FORMAT_RGBE9995]
		var view = "textures/" + id + (".exr" if float_format else ".png")
		error = preview.save_exr(out_dir.path_join(view), false) if float_format else preview.save_png(out_dir.path_join(view))
		if error == OK:
			result["viewFile"] = view
	if error != OK:
		warnings.append({"resource": id, "reason": "Convenience texture view unavailable", "error": error})
	return result

func _properties(object, skip = []):
	var result = {}
	for property in object.get_property_list():
		if not (int(property.usage) & PROPERTY_USAGE_STORAGE) or property.name in skip or property.name == "script":
			continue
		result[property.name] = _encode(object.get(property.name))
	return result

func _encode(value):
	# Godot can expose a typed Object Variant whose instance is null. Its JSON
	# value is genuinely null; get_class() on that property must not be called.
	if typeof(value) == TYPE_OBJECT and not is_instance_valid(value):
		return null
	match typeof(value):
		TYPE_NIL, TYPE_BOOL, TYPE_STRING:
			return value
		TYPE_INT:
			return {"int64": str(value)} if value > 9007199254740991 or value < -9007199254740991 else value
		TYPE_FLOAT:
			return value if is_finite(value) else {"float": str(value)}
		TYPE_STRING_NAME, TYPE_NODE_PATH:
			return str(value)
		TYPE_VECTOR2:
			return {"vector2": [value.x, value.y]}
		TYPE_VECTOR2I:
			return {"vector2i": [value.x, value.y]}
		TYPE_VECTOR3:
			return {"vector3": _v3(value)}
		TYPE_VECTOR3I:
			return {"vector3i": [value.x, value.y, value.z]}
		TYPE_VECTOR4, TYPE_VECTOR4I:
			return {"vector4": [value.x, value.y, value.z, value.w]}
		TYPE_COLOR:
			return {"colorUnconverted": _color(value)}
		TYPE_TRANSFORM3D:
			return {"transform3D": _transform(value)}
		TYPE_BASIS:
			return {"basis": _v3(value.x) + _v3(value.y) + _v3(value.z)}
		TYPE_QUATERNION:
			return {"quaternion": [value.x, value.y, value.z, value.w]}
		TYPE_AABB:
			return {"aabb": _aabb(value)}
		TYPE_PLANE:
			return {"plane": _v3(value.normal) + [value.d]}
		TYPE_RECT2, TYPE_RECT2I:
			return {"rect2": [value.position.x, value.position.y, value.size.x, value.size.y]}
		TYPE_TRANSFORM2D:
			return {"transform2D": [value.x.x, value.x.y, value.y.x, value.y.y, value.origin.x, value.origin.y]}
		TYPE_PROJECTION:
			return {"projection": [_encode(value.x), _encode(value.y), _encode(value.z), _encode(value.w)]}
		TYPE_ARRAY:
			var array = []
			for item in value:
				array.append(_encode(item))
			return array
		TYPE_DICTIONARY:
			var entries = []
			for key in value:
				entries.append([_encode(key), _encode(value[key])])
			return {"dictionaryEntries": entries}
		TYPE_OBJECT:
			if value is Resource:
				return _resource(value)
			return {"nodePath": str(value.get_path())} if value is Node else {"objectClass": value.get_class()}
		TYPE_PACKED_STRING_ARRAY:
			return Array(value)
		TYPE_PACKED_BYTE_ARRAY, TYPE_PACKED_INT32_ARRAY, TYPE_PACKED_INT64_ARRAY, TYPE_PACKED_FLOAT32_ARRAY, TYPE_PACKED_FLOAT64_ARRAY, TYPE_PACKED_VECTOR2_ARRAY, TYPE_PACKED_VECTOR3_ARRAY, TYPE_PACKED_COLOR_ARRAY, TYPE_PACKED_VECTOR4_ARRAY:
			return _blob(value)
		_:
			warnings.append({"reason": "Nonportable Variant retained as description", "variantType": typeof(value)})
			return {"unserializedVariantType": typeof(value), "description": str(value)}

func _blob(value):
	var kind = typeof(value)
	var bytes = value if kind == TYPE_PACKED_BYTE_ARRAY else value.to_byte_array()
	if payload == null:
		_fail("Packed array requested outside payload stream")
		return {}
	if payload.get_position() + bytes.size() > int(request.get("maxPayloadBytes", 2147483648)):
		_fail("Binary payload bound exceeded")
		return {}
	var offset = payload.get_position()
	payload.store_buffer(bytes)
	var components = 4 if kind in [TYPE_PACKED_COLOR_ARRAY, TYPE_PACKED_VECTOR4_ARRAY] else 3 if kind == TYPE_PACKED_VECTOR3_ARRAY else 2 if kind == TYPE_PACKED_VECTOR2_ARRAY else 1
	var scalar_bytes = bytes.size() / (value.size() * components) if value.size() > 0 else 1 if kind == TYPE_PACKED_BYTE_ARRAY else 8 if kind in [TYPE_PACKED_INT64_ARRAY, TYPE_PACKED_FLOAT64_ARRAY] else 4
	return {"blob": "payload.bin", "variantType": kind, "count": value.size(), "components": components, "scalarBytes": scalar_bytes, "offset": offset, "bytes": bytes.size()}

func _hash_source(path):
	var clean = str(path).split("::")[0]
	if clean.begins_with("res://") and FileAccess.file_exists(clean):
		source_hashes[clean.trim_prefix("res://")] = FileAccess.get_sha256(clean)

func _add_bounds(box, visible):
	bounds_all = bounds_all.merge(box) if have_bounds_all else box
	have_bounds_all = true
	if visible:
		bounds_visible = bounds_visible.merge(box) if have_bounds_visible else box
		have_bounds_visible = true

func _transform(value):
	return _v3(value.basis.x) + _v3(value.basis.y) + _v3(value.basis.z) + _v3(value.origin)

func _v3(value):
	return [value.x, value.y, value.z]

func _color(value):
	return [value.r, value.g, value.b, value.a]

func _aabb(value):
	return {"position": _v3(value.position), "size": _v3(value.size)}

func _write_text(path, content):
	var file = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		_fail("Cannot write " + path)
		return
	file.store_string(content)
	file.close()

func _write_json(path, content):
	_write_text(path, JSON.stringify(content, "\t", true, true))
