extends SceneTree
func _initialize():
 load("res://scripts/archive_catalog1003.gd").catalog()
 var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate()
 var view=scene.find_children("*","MeshInstance3D",true,false)[0]
 var surfaces=view.mesh.get("_surfaces")
 print("SERIAL_SURFACES ",typeof(surfaces)," ",surfaces[0].keys() if surfaces!=null else [])
 print("SERVER_SURFACE ",RenderingServer.mesh_get_surface(view.mesh.get_rid(),0).keys())
 var clone=ArrayMesh.new();clone.set("_surfaces",surfaces)
 print("CLONE ",clone.get_surface_count()," normal_equal=",clone.surface_get_arrays(0)[Mesh.ARRAY_NORMAL]==view.mesh.surface_get_arrays(0)[Mesh.ARRAY_NORMAL])
 scene.free();quit()
