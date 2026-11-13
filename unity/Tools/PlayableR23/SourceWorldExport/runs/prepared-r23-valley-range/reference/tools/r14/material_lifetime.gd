extends SceneTree
const Art=preload("res://scripts/map_art_repair_r14.gd")
func _initialize():call_deferred("run")
func run():
	var holder:=Node3D.new();var view:=MeshInstance3D.new();holder.add_child(view);var material:=StandardMaterial3D.new();material.resource_name="vc";view.material_override=material;var tool:=SurfaceTool.new();tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	for point in [Vector3(0,0,0),Vector3(2.63269,0,0),Vector3(0,2.99868,0),Vector3(0,2.99868,0),Vector3(2.63269,0,0),Vector3(2.63269,2.99868,0)]:tool.set_color(Color("324f77"));tool.add_vertex(point)
	view.mesh=tool.commit();print(Art.prepare(holder));holder.free();Art._materials.clear()
	for i in 4:await process_frame
	Node.print_orphan_nodes();quit()
