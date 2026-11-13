extends SceneTree
var checks=0
var failures:Array=[]
var facts:Array=[]
func _initialize():call_deferred("run")
func check(ok:bool,label:String):
 checks+=1
 if not ok:failures.append(label);print("FAIL ",label)
func colliders(scene:Node)->Array:
 var values:Array=[]
 for node in scene.find_children("*","CollisionShape3D",true,false):values.append([node.get_instance_id(),node.shape.get_instance_id(),node.transform,node.disabled])
 return values
func triangles(scene:Node)->int:
 var count=0
 for node in scene.find_children("*","MeshInstance3D",true,false):
  if node.mesh!=null:count+=node.mesh.get_faces().size()/3
 return count
func run():
 var tex:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/mobile_texture_index_r17.json"));var artifacts:Dictionary={}
 check(tex.maps.size()==105,"compressed map textures cover105maps")
 for row in tex.records.values():artifacts[row.path]=row
 check(artifacts.size()==519 and tex.records.size()==821,"deduplicated texture count")
 for path in artifacts:
  var row:Dictionary=artifacts[path]
  check(FileAccess.get_sha256(path)==row.sha256,"compressed artifact hash "+path)
  var texture:PortableCompressedTexture2D=load(path)
  check(texture!=null and texture.get_compression_mode()==PortableCompressedTexture2D.COMPRESSION_MODE_BASIS_UNIVERSAL,"portable GPU transcode payload")
  check(maxi(texture.get_width(),texture.get_height())<=1024 and texture.get_width()==int(row.size[0]),"bounded dimensions")
 var index:Dictionary=JSON.parse_string(FileAccess.get_file_as_string("res://assets/data/art_index_r17.json"))
 for row in index.maps.values():check(FileAccess.get_sha256(row.path)==row.sha256,"surface selector artifact hash")
 var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.catalog()
 for slug in ["alfa-aglae-souza","classic-bigbox-vr","phoenix-bigbox-vr"]:
  var scene:Node=load("res://local_archive_r10/"+slug+".res").geometry.instantiate();preload("res://scripts/map_art_repair_r14.gd").apply(scene,"archive:"+slug)
  var before:Array=colliders(scene);var count:int=triangles(scene)
  var pass_r17=preload("res://scripts/map_art_r17.gd").new();var stats:Dictionary=pass_r17.apply(scene,"archive:"+slug,str(catalog.manifest("archive:"+slug).cache_fingerprint))
  check(stats.get("guard_skips",-1)==0,"all selected geometry guards match "+slug)
  check(int(stats.get("truck",0))>=7,"truck family applied "+slug)
  check(colliders(scene)==before,"all source collision identities/transforms retained "+slug)
  check(triangles(scene)==count,"surface repair transfers exact source triangle counts "+slug)
  check(pass_r17.apply(scene,"archive:"+slug,str(catalog.manifest("archive:"+slug).cache_fingerprint))==stats,"idempotent art pass "+slug)
  facts.append({"map":slug,"stats":stats,"triangles":count,"collision_nodes":before.size()});scene.free()
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/RESOURCES-R17.json",FileAccess.WRITE);f.store_string(JSON.stringify({"passed":failures.is_empty(),"checks":checks,"failures":failures,"facts":facts,"scope":"Offline resource/hash/collision preservation; native PC screenshots separate from Quest acceptance."},"\t"));f.close();print("RESOURCES_R17 checks=",checks," failures=",failures.size());quit(0 if failures.is_empty() else 1)
