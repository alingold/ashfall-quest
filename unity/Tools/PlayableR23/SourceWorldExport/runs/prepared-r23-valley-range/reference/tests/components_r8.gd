extends SceneTree
var output:String
func _initialize():call_deferred("run")
func find_root(parent:Array[int],index:int)->int:
	while parent[index]!=index:
		parent[index]=parent[parent[index]];index=parent[index]
	return index
func components(mesh:Mesh)->Array:
	var faces:PackedVector3Array=mesh.get_faces();var parents:Array[int]=[];var ids:Dictionary={};var points:Array[Vector3]=[];var tris:Array[int]=[]
	for i in range(0,faces.size(),3):
		var corners:Array[int]=[]
		for k in 3:
			var v:Vector3=faces[i+k];var key:Vector3i=Vector3i((v*1000).round())
			if not ids.has(key):ids[key]=parents.size();parents.append(parents.size());points.append(v)
			corners.append(ids[key])
		for k in [1,2]:parents[find_root(parents,corners[k])]=find_root(parents,corners[0])
		tris.append(corners[0])
	var rows:Dictionary={}
	for i in points.size():
		var key:int=find_root(parents,i)
		if not rows.has(key):rows[key]={"box":AABB(points[i],Vector3.ZERO),"triangles":0}
		rows[key].box=rows[key].box.expand(points[i])
	for i in tris:rows[find_root(parents,i)].triangles+=1
	var result:Array=[]
	for row in rows.values():
		var box:AABB=row.box
		result.append({"center":[box.get_center().x,box.get_center().y,box.get_center().z],"size":[box.size.x,box.size.y,box.size.z],"triangles":row.triangles})
	return result
func run():
	output=OS.get_cmdline_user_args()[0];preload("res://scripts/archive_catalog1003.gd").catalog();var rows:Array=[]
	for slug in ["banana-land-gods-pitdevil","bamboo-gods-pitdevil","atrocious-gods-trashpandakiller","battlefield-paintball-gods-pitdevil","classic-bigbox-vr"]:
		var scene:Node=load("res://local_archive/maps/"+slug+"/converted/map.scn").instantiate();var result:Array=[]
		for view in scene.find_children("*","MeshInstance3D",true,false):
			if view.name=="WalkThrough" or "_vc_" in view.name or "_t60_" in view.name or "_p2a61c272_" in view.name or "_t54_" in view.name or "_t132_" in view.name:
				result.append({"node":str(view.name),"parts":components(view.mesh)})
		rows.append({"slug":slug,"nodes":result});scene.free();print("COMPONENTS_R8 ",slug)
	var f=FileAccess.open(output+"/COMPONENTS-RED-R8.json",FileAccess.WRITE);f.store_string(JSON.stringify(rows,"\t"));quit()
