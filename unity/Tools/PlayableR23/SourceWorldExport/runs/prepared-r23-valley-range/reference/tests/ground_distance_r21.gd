extends SceneTree
const Policy=preload("res://scripts/ground_item_visibility_r21.gd")
var game:Node
var checks=0
var failures:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func geometry(node:Node)->Array:
 var result:Array=[]
 if node is GeometryInstance3D:result.append(node)
 for child in node.get_children():result.append_array(geometry(child))
 return result
func bounded(node:Node)->bool:
 var parts=geometry(node)
 if parts.is_empty():return false
 for part in parts:
  if part.visibility_range_end<=0 or part.visibility_range_end>60:return false
 return true
func green_pixels(image:Image)->int:
 var count=0
 for y in image.get_height():
  for x in image.get_width():
   var c=image.get_pixel(x,y)
   if c.g>.7 and c.r<.2 and c.b<.2:count+=1
 return count
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game)
 var prop=game.weapon_inventory._create_prop(0)
 check(bounded(prop),"ground gun meshes and rarity labels capped at60m")
 var source_parts=geometry(game.weapon.weapons[0].model);var held_clear=true
 for part in source_parts:held_clear=held_clear and part.visibility_range_end==0
 check(held_clear,"held gun model untouched")
 var old_owned=game.weapon_inventory.owned.duplicate()
 for family in game.ammo_pickups.families:
  var box=game.ammo_pickups.spawn_box(family,Vector3(0,0,100))
  check(bounded(box),"ammo/build meshes and glyphs capped "+family)
  check(not box.get_meta("collected") and int(box.get_meta("amount"))>0,"supply authority/quantity retained "+family)
 for kind in ["soda","shaker","banana","backpack"]:
  var ground=game.field_consumables.make_prop(kind,true);root.add_child(ground)
  var held=game.field_consumables.make_prop(kind);root.add_child(held)
  check(bounded(ground),"ground consumable capped "+kind)
  var clear=true
  for part in geometry(held):clear=clear and part.visibility_range_end==0
  check(clear,"held/pouch consumable untouched "+kind)
  ground.queue_free();held.queue_free()
 check(game.weapon_inventory.owned==old_owned,"render limit does not change inventory ownership")
 var native=DisplayServer.get_name()!="headless"
 if native:
  var view=SubViewport.new();view.size=Vector2i(400,400);view.own_world_3d=true;view.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(view)
  var world=Node3D.new();view.add_child(world)
  var camera=Camera3D.new();camera.far=200;world.add_child(camera);camera.make_current()
  var near=MeshInstance3D.new();near.mesh=BoxMesh.new();near.mesh.size=Vector3.ONE*5;near.position=Vector3(-6,0,-50);world.add_child(near)
  var far=MeshInstance3D.new();far.mesh=near.mesh;far.position=Vector3(6,0,-70);world.add_child(far)
  for row in [[near,Color.RED],[far,Color.GREEN]]:
   var mat=StandardMaterial3D.new();mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;mat.albedo_color=row[1];row[0].material_override=mat
  for i in 8:await process_frame
  await RenderingServer.frame_post_draw
  var before=view.get_texture().get_image();check(green_pixels(before)>100,"70m control object actually renders before limit")
  Policy.apply(near);Policy.apply(far)
  for i in 8:await process_frame
  await RenderingServer.frame_post_draw
  var after=view.get_texture().get_image();check(green_pixels(after)==0,"native Mobile renderer culls70m geometry with60m limit")
  check(far.is_inside_tree() and far.visible,"culling preserves actual world entity")
  before.save_png(OS.get_cmdline_user_args()[0]+"/R21-items-before.png");after.save_png(OS.get_cmdline_user_args()[0]+"/R21-items-after.png")
  view.queue_free()
 var file=FileAccess.open(OS.get_cmdline_user_args()[0]+"/GROUND-DISTANCE-R21.json",FileAccess.WRITE)
 file.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"native_renderer_checked":native,"distance_m":60,"scope":"Actual ground factories and unchanged held models/ownership; native viewport pixel comparison when available. XRoff, not Quest acceptance."},"\t"));file.close()
 prop.queue_free();game._quitting=true;game.queue_free();for i in 6:await process_frame
 print("GROUND_DISTANCE_R21 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
