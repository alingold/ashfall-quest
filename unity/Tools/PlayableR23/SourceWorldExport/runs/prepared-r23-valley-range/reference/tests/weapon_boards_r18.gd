extends SceneTree
const Boards=preload("res://scripts/map_weapon_boards_r18.gd")
var checks=0
func check(value:bool,message:String):
 checks+=1
 assert(value,message)
func _initialize():call_deferred("run")
func run():
 var maps:Dictionary=Boards.catalog().maps;var total=0;var count=0
 for slug in maps:
  var source:Array=maps[slug].get("weapon_boards_r18",{}).get("boards",[])
  if source.is_empty():continue
  var rows:Array=Boards.merge([],"archive:"+slug,maps[slug].cache_fingerprint)
  check(rows.size()==source.size(),"all source-proven signs retained: "+slug)
  check(Boards.merge([],"archive:"+slug,"wrong").is_empty(),"wrong fingerprint refuses boards: "+slug)
  total+=rows.size();count+=1
 check(count==37 and total==1344,"expected complete source scan scope")
 var slug="melipop-gods-poplightning";var fingerprint:String=maps[slug].cache_fingerprint
 var catalog=preload("res://scripts/archive_catalog1003.gd");catalog.catalog()
 var packed=load("res://local_archive_r10/"+slug+".res")
 var inherited:Array=packed.details.weapon_boards_r9;var size:int=inherited.size();var first:Dictionary=inherited[0].duplicate(true)
 var merged:Array=Boards.merge(inherited,"archive:"+slug,fingerprint)
 check(size==2 and merged.size()==31,"two inherited MellyPop signs deduplicate against31source boards")
 check(inherited.size()==size and inherited[0]==first,"cached details remain untouched")
 var face:Dictionary=merged[0].duplicate(true);face.normal=-face.normal
 var double:Array=Boards.merge([merged[0],face],"archive:"+slug,fingerprint)
 check(double.size()==32,"opposite-facing sign remains distinct")
 merged[0].normal=Vector3.ZERO
 check(inherited[0].normal==first.normal,"returned dictionaries are isolated")
 var f=FileAccess.open(OS.get_cmdline_user_args()[0]+"/WEAPON-BOARDS-R18.json",FileAccess.WRITE)
 f.store_string(JSON.stringify({"passed":true,"checks":checks,"source_maps":count,"source_boards":total,"scope":"Hash-bound source row conversion/dedup; physical floor access validated at runtime near each sign, not all1344placements preaccepted."},"\t"));f.close()
 print("WEAPON_BOARDS_R18 checks=",checks," maps=",count," source_boards=",total);quit()
