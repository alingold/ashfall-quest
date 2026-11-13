extends SceneTree
## Also run against the exported PCK/APK resource set, not just the source checkout.
func _initialize():
 var path="res://assets/art_r19/freight_texture.res"
 assert(ResourceLoader.exists(path,"Texture2D"))
 var texture=load(path) as Texture2D
 assert(texture!=null and texture.get_width()==1024 and texture.get_height()==552)
 var image=texture.get_image()
 assert(image!=null and image.has_mipmaps() and image.is_compressed())
 print("R19_RESOURCE_PATH_PASS ",path," format=",image.get_format()," mipmaps=",image.get_mipmap_count()," raw_png_present=",FileAccess.file_exists("res://assets/art_r19/freight.png"))
 quit()
