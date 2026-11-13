extends SceneTree
func _initialize():
 load("res://scripts/archive_catalog1003.gd").catalog()
 var scene=load("res://local_archive_r10/alfa-aglae-souza.res").geometry.instantiate()
 for view in scene.find_children("*","MeshInstance3D",true,false):
  if view.material_override==null or view.material_override.resource_name!="vc":continue
  var d=RenderingServer.mesh_get_surface(view.mesh.get_rid(),0);var format:int=d.format;var count:int=d.vertex_count
  print("FORMAT ",format," count ",count," keys ",d.keys())
  for slot in [Mesh.ARRAY_VERTEX,Mesh.ARRAY_NORMAL,Mesh.ARRAY_TANGENT,Mesh.ARRAY_COLOR,Mesh.ARRAY_TEX_UV,Mesh.ARRAY_TEX_UV2]:print("OFFSET ",slot," ",RenderingServer.mesh_surface_get_format_offset(format,count,slot))
  print("STRIDES vertex ",RenderingServer.mesh_surface_get_format_vertex_stride(format,count)," normal ",RenderingServer.mesh_surface_get_format_normal_tangent_stride(format,count)," attr ",RenderingServer.mesh_surface_get_format_attribute_stride(format,count)," index ",RenderingServer.mesh_surface_get_format_index_stride(format,count))
  print("BYTES vertex ",d.vertex_data.size()," attr ",d.attribute_data.size()," index ",d.index_data.size());break
 scene.free();quit()
