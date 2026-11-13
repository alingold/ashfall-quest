extends Node
## Test-only phase barrier and fact exchange for real multi-process peers.
## SceneMultiplayer.server_relay is false in the game session, so clients report to
## the host and the host rebroadcasts. Nothing here touches game state.
var reached:Dictionary={} # peer id -> highest phase
var facts:Dictionary={}   # peer id -> {phase -> Dictionary}
func _ready()->void:name="R18Sync"
func report(phase:int,data:Dictionary={})->void:
	var me:int=multiplayer.get_unique_id()
	_store(me,phase,data)
	if multiplayer.is_server():_down.rpc(me,phase,data)
	else:_up.rpc_id(1,phase,data)
@rpc("any_peer","call_remote","reliable",5)
func _up(phase:int,data:Dictionary)->void:
	if not multiplayer.is_server():return
	var sender:int=multiplayer.get_remote_sender_id()
	_store(sender,phase,data);_down.rpc(sender,phase,data)
@rpc("authority","call_remote","reliable",5)
func _down(sender:int,phase:int,data:Dictionary)->void:_store(sender,phase,data)
func _store(id:int,phase:int,data:Dictionary)->void:
	reached[id]=maxi(int(reached.get(id,0)),phase)
	if not facts.has(id):facts[id]={}
	facts[id][phase]=data
func all_reached(ids:Array,phase:int)->bool:
	for id in ids:
		if int(reached.get(int(id),0))<phase:return false
	return true
func fact(id:int,phase:int)->Dictionary:return facts.get(id,{}).get(phase,{})
