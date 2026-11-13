extends SceneTree
class NetFixture extends Node:
 var stage="lobby"
 func is_host():return true
class BridgeFixture extends Node:
 var net:Node
 func live():return true
var game:Node
var checks=0
var failures=[]
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func _initialize():call_deferred("run")
func freeze(node:Node):
 node.set_process(false);node.set_physics_process(false)
 for child in node.get_children():freeze(child)
func shot(label:String):
 if DisplayServer.get_name()=="headless":return
 for i in 8:await process_frame
 await RenderingServer.frame_post_draw
 root.get_texture().get_image().save_png(OS.get_cmdline_user_args()[0]+"/R21-MENU-"+label+".png")
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game);freeze(game);game.hide()
 for layer in game.find_children("*","CanvasLayer",true,false):layer.hide()
 var stage=Node3D.new();root.add_child(stage)
 var environment=WorldEnvironment.new();var env=Environment.new();env.background_mode=Environment.BG_COLOR;env.background_color=Color("354b58");environment.environment=env;stage.add_child(environment)
 var camera=Camera3D.new();stage.add_child(camera);camera.position=Vector3(0,1.5,2);camera.fov=55;camera.look_at(Vector3(0,1.5,0));camera.current=true
 var wall=MeshInstance3D.new();wall.mesh=BoxMesh.new();wall.mesh.size=Vector3(3,2.8,.1);var mat=StandardMaterial3D.new();mat.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;mat.albedo_color=Color("697981");wall.material_override=mat;wall.position=Vector3(0,1.5,.35);stage.add_child(wall)
 var menu=game.field_menu;menu.is_open=true;menu._panel.reparent(stage,false);menu._panel.position=Vector3(0,1.5,0)
 var bridge=BridgeFixture.new();bridge.net=NetFixture.new();bridge.add_child(bridge.net);root.add_child(bridge)
 var real_bridge=menu.network_bridge
 for page in ["pause","root","host-pause"]:
  menu.settings_page="pause" if page=="host-pause" else page;menu.network_bridge=bridge if page=="host-pause" else null;menu._refresh();menu._panel.show();menu._desktop_layer.hide()
  var count=6 if page=="host-pause" else 4
  check(menu._labels().size()==(5 if page=="root" else count),page+" actions preserved")
  for i in 6:
   check(menu._category_cards[i].visible==(i<count),page+" card "+str(i))
   if i>=count:continue
   var card=menu._category_cards[i];check(card.material_override.no_depth_test and menu._category_icons[i].no_depth_test,page+" overlays world "+str(i))
   check(is_equal_approx(card.mesh.size.x,.94) and menu._category_icons[i].texture!=null,page+" shared card/icon "+str(i))
   if i>0:check(menu._category_cards[i-1].position.y-card.position.y>card.mesh.size.y,page+" nonoverlap "+str(i))
  check(menu._spatial_title.text==("SETTINGS" if page=="root" else "ASHFALL"),page+" concise title")
  await shot(page)
 menu.network_bridge=real_bridge
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/MENU-CARDS-R21.json",FileAccess.WRITE);f.store_string(JSON.stringify({"checks":checks,"failures":failures,"headset":false},"  "));f.close()
 game._quitting=true;game.queue_free();stage.queue_free();bridge.queue_free();await process_frame;print("R21_MENU checks=",checks," failures=",failures.size());quit(failures.size())
