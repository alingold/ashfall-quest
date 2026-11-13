extends SceneTree
var game:Node
func _initialize():call_deferred("run")
func stage_ready(net:Node,stage:String)->bool:
 var deadline=Time.get_ticks_msec()+60000
 while Time.get_ticks_msec()<deadline:
  await process_frame
  if net.stage==stage and not game.network_bridge._preparing and is_instance_valid(game.active_sandbox_map) and game.active_sandbox_map.map_ready:return true
 return false
func run():
 game=load("res://scripts/main.gd").new();root.add_child(game)
 var net=game.network_bridge.net
 assert(net.host("Score fixture",25918,"score-r18")==OK)
 net.request("rules",{"map":"archive:melipop-gods-poplightning","mode":"respawn","teams":2,"zone_enabled":false,"time_limit":120,"score_limit":25,"protection":0.0,"catalog":true})
 assert(await stage_ready(net,"lobby"))
 net.request("start")
 assert(await stage_ready(net,"match"))
 var board=game.active_sandbox_map.find_child("AuthoredWorldScoreR9",true,false)
 assert(board!=null and board.canvas.game.network_bridge.net==net and board.faces.size()==4)
 # Synthetic victim in the actual listen-server authority, not an additional peer.
 net._add_player(2,"Score victim","")
 net.players[1].team=0;net.players[2].team=1
 net.players[2].life="alive";net.players[2].loaded=true;net.players[2].protection_until=0
 net.hurt(2,10000,1,false,true,0,"fixture")
 var state=load("res://scripts/world_score_state_r18.gd").snapshot(board.canvas.game.network_bridge.net)
 assert(state.active and state.cap==25 and state.scores[0]==1 and state.scores[1]==0)
 assert(state.seconds>=110 and state.seconds<=120)
 board._process(1)
 assert(board.viewport.render_target_update_mode==SubViewport.UPDATE_ONCE)
 print("R18_SCORE_LISTEN_AUTHORITY_PASS team0=",state.scores[0]," cap=",state.cap," seconds=",state.seconds," faces=",board.faces.size())
 net.close(false);game._quitting=true;quit()
