extends SceneTree
func _initialize():
 for kind in ["landscape","portrait"]:
  var base="res://assets/art_r21/northline-"+kind
  var image=Image.new()
  assert(image.load_svg_from_string(FileAccess.get_file_as_string(base+".svg"))==OK)
  assert(image.save_png(base+".png")==OK)
  image.generate_mipmaps()
  assert(image.compress(Image.COMPRESS_ETC2)==OK)
  assert(ResourceSaver.save(ImageTexture.create_from_image(image),base+"_texture.res",ResourceSaver.FLAG_COMPRESS)==OK)
  var texture=load(base+"_texture.res")
  assert(texture.get_image().is_compressed())
  assert(texture.get_image().has_mipmaps())
  print("R21_ART_BAKED ",kind," ",texture.get_width(),"x",texture.get_height()," mipmaps=",texture.get_image().get_mipmap_count())
 quit()
