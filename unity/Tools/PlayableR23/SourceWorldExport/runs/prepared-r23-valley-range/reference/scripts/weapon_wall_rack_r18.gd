extends Node3D
## Real red-rarity gun instances on an original rack aligned to a recovered source sign.
const Rarity=preload("res://scripts/weapon_rarity_r6.gd")
var map:Node
var ids:Array[int]=[]
var mounts=0
var anchors_r18:Array[Dictionary]=[]
var boards_r18:Array=[]
var active_boards_r18:Dictionary={}
var spawn_items_r18=true
var game:Node
var catalog:Array=[]
var metal:StandardMaterial3D
var clock_r18=0.0
func setup(owner_map:Node,boards:Array,spawn_items:bool=true):
 map=owner_map;game=map._game;name="WeaponWallRacksR18";boards_r18=boards.duplicate();spawn_items_r18=spawn_items
 catalog=game.weapon.ACTIVE_WEAPON_INDICES.filter(func(id):return Rarity.tiered(id))
 metal=StandardMaterial3D.new();metal.albedo_color=Color("1a242b");metal.metallic=.65;metal.roughness=.65
 activate_near_r18([game.player.global_position])
func _physics_process(delta:float):
 clock_r18+=delta
 if clock_r18<.25 or not map.map_ready or game.front_end_active:return
 clock_r18=0
 var points:Array=[game.player.global_position]
 if game.network_bridge.live() and game.network_bridge.net.server:
  for pose in game.network_bridge.net.poses.values():points.append(pose.root.origin)
 activate_near_r18(points)
func activate_near_r18(points:Array,budget:int=2):
 var before:int=ids.size();var activated=0
 for board_index in boards_r18.size():
  if active_boards_r18.has(board_index):continue
  var center:Vector3=map.global_position+boards_r18[board_index].box.get_center();var near=false
  for point in points:
   if center.distance_squared_to(point)<70*70:near=true;break
  if not near:continue
  active_boards_r18[board_index]=true;_activate_board_r18(board_index);activated+=1
  if activated>=budget:break
 map.statistics["weapon_board_candidates_r18"]=boards_r18.size();map.statistics["weapon_boards_validated_r18"]=anchors_r18.size();map.statistics["mounted_red_board_guns_r18"]=mounts
 if is_instance_valid(get_parent()) and "display_ids_r9" in get_parent():get_parent().display_ids_r9.assign(ids)
 if ids.size()>before and game.network_bridge.playing() and game.network_bridge.net.server:game.network_bridge.register_archive_loot_r8(ids.slice(before))
func _activate_board_r18(board_index:int):
 var board:Dictionary=boards_r18[board_index];var normal:Vector3=(board.normal as Vector3).normalized();normal.y=0
 if normal.length_squared()<.5:return
 normal=normal.normalized()
 var anchor:Dictionary=_anchor_r18(board,normal)
 if anchor.is_empty():return
 normal=anchor.normal;var right:Vector3=Vector3.UP.cross(normal).normalized()
 var width:float=maxf(7.6,minf(18.0,maxf(board.box.size.x,board.box.size.z)))
 var columns:int=clampi(floori(width/1.55),5,11);var rows:int=ceili(float(catalog.size())/columns)
 var floor_y:float=anchor.floor_y
 var center:Vector3=map.global_position+board.box.get_center();center.y=floor_y
 var row_step:float=minf(.55,1.5/maxi(1,rows-1))
 var positions:Array[Vector3]=[]
 # A clear center alone is insufficient: a sign can border a stairwell, narrow
 # platform or wall. Validate every grip position before creating a full rack.
 for slot in catalog.size():
  var desired:Vector3=center+right*((slot%columns)-(columns-1)*.5)*1.55+Vector3.UP*(.7+(rows-1-floori(float(slot)/columns))*row_step)+normal*.38
  var hit:Dictionary=get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(desired+normal*1.8,desired-normal*2.5,1))
  if not hit.is_empty() and absf(hit.normal.dot(normal))>.75:desired=hit.position+normal*.36
  var approach:Vector3=map._floor(desired+normal*.8+Vector3.UP*.5,5)
  if not approach.is_finite() or absf(approach.y-floor_y)>.35 or not map.safe_spawn_r8(approach+Vector3.UP*.12) or not game.weapon_inventory._line_clear(desired+normal*.8,desired) or not game.weapon_inventory._line_clear(approach+Vector3.UP*1.5,desired):
   map.statistics["weapon_boards_rejected_access_r18"]=int(map.statistics.get("weapon_boards_rejected_access_r18",0))+1
   return
  positions.append(desired)
 anchors_r18.append({"center":center,"normal":normal,"width":columns*1.55})
 var back=MeshInstance3D.new();var box=BoxMesh.new();box.size=Vector3(columns*1.55,.11,.08);back.mesh=box;back.material_override=metal;add_child(back);back.global_transform=Transform3D(Basis(right,Vector3.UP,normal),center+Vector3.UP*(.9+rows*.32)+normal*.08)
 var hooks=SurfaceTool.new();hooks.begin(Mesh.PRIMITIVE_TRIANGLES)
 for slot in catalog.size():
  var desired:Vector3=positions[slot]
  mounts+=1
  if spawn_items_r18:
   var index:int=game.weapon.create_instance(int(catalog[slot]),5,true);game.weapon_inventory._spawn_pickup(index,desired)
   var prop:Node3D=game.weapon_inventory.pickups[index];prop.set_meta("wall_mount_r6",true);prop.set_meta("weapon_board_r9",true);prop.set_meta("archive_site_r8",-10000-board_index*100-slot)
   prop.global_basis=Basis(-normal,Vector3.UP,right);prop.global_position=desired;ids.append(index)
  for side in [-.32,.32]:
   var a:Vector3=to_local(desired+right*side-normal*.15-Vector3.UP*.12-right*.025)
   var b:Vector3=a+Vector3.UP*.11;var c:Vector3=b+right*.05;var d:Vector3=a+right*.05
   for v in [a,b,c,a,c,d]:hooks.set_normal(normal);hooks.add_vertex(v)
 var hardware=MeshInstance3D.new();hardware.mesh=hooks.commit();hardware.material_override=metal;add_child(hardware)

func _anchor_r18(board:Dictionary,normal:Vector3)->Dictionary:
 # Exported spawn-height hints are not floors. Recover a real standable apron
 # beside the sign before mounting anything; try both sides of a two-sided sign.
 var center:Vector3=map.global_position+board.box.get_center()
 var hint:float=map.global_position.y+float(board.get("floor_hint",board.box.position.y-3.0))
 var start_y:float=maxf(map.global_position.y+board.box.position.y-.03,hint+1.0)
 for side in [normal,-normal]:
  for distance in [.85,1.3,2.0]:
   var point:Vector3=center+side*distance;point.y=start_y
   var floor:Vector3=map._floor(point,maxf(16,start_y-hint+5))
   if not floor.is_finite() or not map.safe_spawn_r8(floor+Vector3.UP*.12):continue
   var clear:Dictionary=get_world_3d().direct_space_state.intersect_ray(PhysicsRayQueryParameters3D.create(floor+Vector3.UP*1.4,Vector3(center.x,floor.y+1.4,center.z)+side*.36,1))
   if clear.is_empty():return {"floor_y":floor.y,"normal":side}
 return {}
