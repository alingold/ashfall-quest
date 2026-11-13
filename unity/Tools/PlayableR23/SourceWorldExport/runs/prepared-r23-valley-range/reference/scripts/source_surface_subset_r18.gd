extends RefCounted
## Compact an index subset without re-encoding source positions, normals, colors or UVs.
## Static single-surface archive meshes only. The source quantization AABB/UV scale remain intact.
static func build(source:ArrayMesh,indices:PackedInt32Array)->ArrayMesh:
 var encoded:Array=source.get("_surfaces")
 if encoded.size()!=1 or indices.is_empty():return null
 var surface:Dictionary=encoded[0].duplicate();var format:int=surface.format;var old_count:int=surface.vertex_count
 if format&Mesh.ARRAY_FORMAT_BONES or format&Mesh.ARRAY_FORMAT_WEIGHTS or not format&Mesh.ARRAY_FORMAT_INDEX:return null
 var vertex_stride:int=RenderingServer.mesh_surface_get_format_vertex_stride(format,old_count)
 var normal_stride:int=RenderingServer.mesh_surface_get_format_normal_tangent_stride(format,old_count)
 var attribute_stride:int=RenderingServer.mesh_surface_get_format_attribute_stride(format,old_count)
 var old_vertices:PackedByteArray=surface.vertex_data;var old_attributes:PackedByteArray=surface.get("attribute_data",PackedByteArray())
 if old_vertices.size()!=old_count*(vertex_stride+normal_stride) or old_attributes.size()!=old_count*attribute_stride:return null
 var lookup={};var originals:PackedInt32Array=[];var remapped:PackedInt32Array=[]
 for index in indices:
  if index<0 or index>=old_count:return null
  if not lookup.has(index):lookup[index]=originals.size();originals.append(index)
  remapped.append(lookup[index])
 var count:int=originals.size();var vertices=PackedByteArray();var normals=PackedByteArray();var attributes=PackedByteArray()
 for index in originals:
  vertices.append_array(old_vertices.slice(index*vertex_stride,(index+1)*vertex_stride))
  if normal_stride>0:normals.append_array(old_vertices.slice(old_count*vertex_stride+index*normal_stride,old_count*vertex_stride+(index+1)*normal_stride))
  if attribute_stride>0:attributes.append_array(old_attributes.slice(index*attribute_stride,(index+1)*attribute_stride))
 vertices.append_array(normals)
 var stride:int=RenderingServer.mesh_surface_get_format_index_stride(format,count)
 var index_bytes=PackedByteArray();index_bytes.resize(remapped.size()*stride)
 for i in remapped.size():
  if stride==2:index_bytes.encode_u16(i*2,remapped[i])
  elif stride==4:index_bytes.encode_u32(i*4,remapped[i])
  else:return null
 surface.vertex_count=count;surface.vertex_data=vertices;surface.attribute_data=attributes;surface.index_count=remapped.size();surface.index_data=index_bytes
 surface.erase("lods")
 var mesh=ArrayMesh.new();mesh.set("_surfaces",[surface])
 return mesh if mesh.get_surface_count()==1 else null
