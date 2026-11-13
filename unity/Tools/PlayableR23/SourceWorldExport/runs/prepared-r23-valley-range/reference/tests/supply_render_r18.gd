extends SceneTree
const Supply=preload("res://scripts/supply_catalog_088.gd")
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func capture(model:Node3D)->Dictionary:
 var rows:Dictionary={};var draws=0;var glyphs:Array=[];var triangles=0
 for node in model.get_children():
  if node is Sprite3D:glyphs.append([node.texture.resource_path,node.transform,node.pixel_size]);continue
  if not node is MeshInstance3D:continue
  for surface in node.mesh.get_surface_count():
   draws+=1
   var material:StandardMaterial3D=node.material_override if node.material_override!=null else node.mesh.surface_get_material(surface)
   var arrays:Array=node.mesh.surface_get_arrays(surface);var vertices:PackedVector3Array=arrays[Mesh.ARRAY_VERTEX];var normals:PackedVector3Array=arrays[Mesh.ARRAY_NORMAL];var indices=arrays[Mesh.ARRAY_INDEX]
   var count:int=indices.size() if indices!=null and not indices.is_empty() else vertices.size()
   for i in range(0,count,3):
    var key:String=str(material.albedo_color)+str(material.metallic)+str(material.roughness);var tri_normals:Array=[]
    for corner in 3:
     var index:int=indices[i+corner] if indices!=null and not indices.is_empty() else i+corner
     key+=str(node.transform*vertices[index]);tri_normals.append((node.basis*normals[index]).normalized())
    if not rows.has(key):rows[key]=[]
    rows[key].append(tri_normals);triangles+=1
 return {"rows":rows,"draws":draws,"glyphs":glyphs,"triangles":triangles}
func run():
 for family in Supply.AMOUNTS:
  var original:Node3D=Supply._unbatched_r18(family);var batched:Node3D=Supply.model(family);var shared:Node3D=Supply.model(family)
  var before:Dictionary=capture(original);var after:Dictionary=capture(batched)
  check(before.triangles==after.triangles and before.rows.size()==after.rows.size(),family+" source triangle count and materials retained")
  var same=true;var normals_safe=true
  for key in before.rows:
   if not after.rows.has(key) or before.rows[key].size()!=after.rows[key].size():same=false;continue
   for triangle in before.rows[key].size():
    for corner in 3:
     if before.rows[key][triangle][corner].dot(after.rows[key][triangle][corner])<.999999:normals_safe=false
  check(same,family+" complete ordered triangle/material multiset retained")
  check(normals_safe,family+" packed normals retain authored shading")
  check(before.glyphs==after.glyphs,family+" unchanged original ammo glyph/transform/size")
  check(after.draws==3 and after.draws<before.draws,family+" opaque surfaces reduced to three")
  check(shared.get_node("ModelSurfaces").mesh==batched.get_node("ModelSurfaces").mesh,family+" cached mesh/materials shared between items")
  check(batched.find_children("*","CollisionObject3D",true,false).is_empty(),family+" no new gameplay collision")
  facts.append({"family":family,"before_surfaces":before.draws,"after_surfaces":after.draws,"triangles":after.triangles})
  original.free();batched.free();shared.free()
 var game=load("res://scripts/main.gd").new();root.add_child(game)
 game.weapon.reserves.rifle=0;game.field_builder.charges=0
 var box:Node3D=game.ammo_pickups.spawn_box("rifle",Vector3.ZERO)
 check(game.ammo_pickups.collect_box(box)==32 and game.weapon.reserves.rifle==32,"batched rifle item awards existing32round quantity")
 check(not box.visible and game.ammo_pickups.collect_box(box)==0,"collected supply hides and cannot award twice")
 var builds:Node3D=game.ammo_pickups.spawn_box("builds",Vector3.ZERO)
 check(game.ammo_pickups.collect_box(builds)==6 and game.field_builder.charges==6,"batched build item awards existing six builds")
 game._quitting=true;game.queue_free();for i in 6:await process_frame
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/SUPPLY-RENDER-R18.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"Exact source supply geometry/material/glyph preservation, shared render resources and actual award path. XR off; native counters and Quest acceptance separate."},"\t"));f.close()
 print("SUPPLY_RENDER_R18 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
