extends Node
## Opt-in, ephemeral proximity voice. 16 kHz mono PCM16 / 20 ms / 640 bytes.
## No recordings, local microphone monitoring, or third-party voice service.
const HZ:=16000
const FRAMES:=320
const BYTES:=640
var game:Node
var net:Node
var enabled=false
var listening=true
var muted:Dictionary={}
var player_volumes:Dictionary={}
var speaking_until:Dictionary={}
var _activity_sent:Dictionary={}
var speakers:Dictionary={}
var _seen:Dictionary={}
var _rates:Dictionary={}
var _capture:AudioEffectCapture
var _microphone:AudioStreamPlayer
var _sequence=0
var _hangover=0.0
var _indicator:Label3D
var received_packets=0
var sent_packets=0
var rejected_packets=0
var _session_epoch=-1
var _android_microphone_requested=false
func setup(g:Node,n:Node)->void:
	game=g;net=n;name="ProximityVoiceR4"
	_indicator=Label3D.new();_indicator.name="MicrophoneState";_indicator.no_depth_test=true;_indicator.font_size=28;_indicator.pixel_size=.00028;_indicator.position=Vector3(.213,-.425,-.794);_indicator.layers=1<<19;_indicator.render_priority=118;game.combat_hud.add_child(_indicator)
func set_enabled(value:bool)->void:
	enabled=value
	if not value:_stop_capture();_android_microphone_requested=false
func toggle_mute(id:int)->void:
	if muted.has(id):muted.erase(id)
	else:muted[id]=true
	if speakers.has(id):_remove_speaker(id)
func in_map()->bool:
	return net.active and not net.dedicated and net.stage in ["lobby","countdown","match"] and net.players.has(net.me()) and net.players[net.me()].loaded
func _start_capture()->void:
	# Offline fixtures never open the real input device, even with synthetic voice enabled.
	if DisplayServer.get_name()=="headless" or "--voice-test" in OS.get_cmdline_user_args():return
	if OS.get_name()=="Android" and not "android.permission.RECORD_AUDIO" in OS.get_granted_permissions():
		if not _android_microphone_requested:
			_android_microphone_requested=true
			OS.request_permission("android.permission.RECORD_AUDIO")
		return
	if AudioServer.get_bus_index("MicrophoneR4")<0:
		AudioServer.add_bus();var index:=AudioServer.bus_count-1;AudioServer.set_bus_name(index,"MicrophoneR4");AudioServer.set_bus_mute(index,true)
	var bus:=AudioServer.get_bus_index("MicrophoneR4")
	_capture=AudioEffectCapture.new();_capture.buffer_length=.12;AudioServer.add_bus_effect(bus,_capture)
	_microphone=AudioStreamPlayer.new();_microphone.bus="MicrophoneR4";_microphone.stream=AudioStreamMicrophone.new();add_child(_microphone);_microphone.play()
func _stop_capture()->void:
	if is_instance_valid(_microphone):_microphone.stop();_microphone.queue_free()
	_microphone=null;_capture=null
	var bus:=AudioServer.get_bus_index("MicrophoneR4")
	if bus>=0:AudioServer.remove_bus(bus)
func _process(delta:float)->void:
	var active:=in_map()
	if _session_epoch!=int(net.epoch) or not active:
		for id in speakers.keys():_remove_speaker(id)
		_seen.clear();_rates.clear();speaking_until.clear();_activity_sent.clear();_session_epoch=int(net.epoch)
	if enabled and active:
		if not is_instance_valid(_microphone):_start_capture()
	else:_stop_capture()
	_indicator.visible=false
	_indicator.text=""
	_indicator.modulate=Color("69f7b5") if enabled else Color("95aebd")
	_hangover=maxf(0,_hangover-delta)
	if _capture!=null:
		var input_frames:=roundi(AudioServer.get_mix_rate()*.02)
		# Discard stale input after a stalled frame, never transmit a growing backlog.
		if _capture.get_frames_available()>input_frames*5:_capture.clear_buffer()
		for i in 4:
			if not _capture.can_get_buffer(input_frames):break
			var data:=encode(_capture.get_buffer(input_frames))
			var peak:=0.0
			for j in FRAMES:peak=maxf(peak,absf(float(data.decode_s16(j*2))/32767.0))
			if peak>.012:_hangover=.3
			if _hangover>0:transmit(data)
	for id in speakers.keys():
		if not net.players.has(id) or muted.has(id) or not listening:_remove_speaker(id);continue
		var row:Dictionary=speakers[id];var player:AudioStreamPlayer3D=row.player
		if net.poses.has(id):player.global_position=net.poses[id].head.origin
		if Time.get_ticks_msec()-int(row.last)>1500:_remove_speaker(id);continue
		var playback:AudioStreamGeneratorPlayback=player.get_stream_playback()
		while not row.queue.is_empty() and playback.get_frames_available()>=FRAMES:
			playback.push_buffer(row.queue.pop_front())
static func encode(input:PackedVector2Array)->PackedByteArray:
	var packet:=PackedByteArray();packet.resize(BYTES)
	if input.is_empty():return packet
	# Box-filter downsampling at arbitrary output mix rates (44.1/48/96 kHz).
	for i in FRAMES:
		var begin:=floori(float(i)*input.size()/FRAMES);var end:=maxi(begin+1,floori(float(i+1)*input.size()/FRAMES));var sample:=0.0
		for j in range(begin,mini(end,input.size())):sample+=(input[j].x+input[j].y)*.5
		sample/=maxi(1,end-begin)
		packet.encode_s16(i*2,roundi(clampf(sample,-1,1)*32767))
	return packet
static func decode(packet:PackedByteArray)->PackedVector2Array:
	var samples:=PackedVector2Array()
	if packet.size()!=BYTES:return samples
	samples.resize(FRAMES)
	for i in FRAMES:samples[i]=Vector2.ONE*(float(packet.decode_s16(i*2))/32767.0)
	return samples
func transmit(packet:PackedByteArray)->void:
	if not in_map() or packet.size()!=BYTES:return
	if multiplayer.multiplayer_peer.get_connection_status()!=MultiplayerPeer.CONNECTION_CONNECTED:return
	_sequence+=1;sent_packets+=1
	if net.server:_accept(net.me(),_sequence,packet,net.epoch)
	else:_voice_up.rpc_id(1,_sequence,packet,net.epoch)
@rpc("any_peer","call_remote","unreliable_ordered",4)
func _voice_up(sequence:int,packet:PackedByteArray,epoch:int)->void:
	if net.server:_accept(multiplayer.get_remote_sender_id(),sequence,packet,epoch)
func _accept(id:int,sequence:int,packet:PackedByteArray,epoch:int)->void:
	if not net.active or not net.server or epoch!=net.epoch or net.stage not in ["lobby","countdown","match"] or not net.players.has(id) or not net.players[id].loaded or net.players[id].get("kicked_r18",false) or packet.size()!=BYTES:
		rejected_packets+=1;return
	var now:=Time.get_ticks_msec()
	var rate:Dictionary=_rates.get(id,{"at":now,"count":0})
	if now-int(rate.at)>=1000:rate={"at":now,"count":0}
	rate.count+=1;_rates[id]=rate
	if int(rate.count)>60 or sequence<=int(_seen.get(id,0)):
		rejected_packets+=1;return
	_seen[id]=sequence
	_note_activity_r5(id,packet)
	if float(speaking_until.get(id,0))>now and now-int(_activity_sent.get(id,0))>=250:
		_activity_sent[id]=now;_activity_down_r5.rpc(id,epoch)
	if id!=net.me():_receive(id,sequence,packet,epoch)
	for peer in net.players:
		if peer==id or peer==net.me() or not net.players[peer].loaded or net.players[peer].get("kicked_r18",false):continue
		# Server routes only within voice distance; never trusts client-supplied positions.
		if net.poses.has(id) and net.poses.has(peer) and net.poses[id].head.origin.distance_to(net.poses[peer].head.origin)<=42:
			_voice_down.rpc_id(int(peer),id,sequence,packet,epoch)
@rpc("authority","call_remote","unreliable_ordered",4)
func _voice_down(id:int,sequence:int,packet:PackedByteArray,epoch:int)->void:
	if not net.server:_receive(id,sequence,packet,epoch)
func _receive(id:int,_sequence_value:int,packet:PackedByteArray,epoch:int)->void:
	if not in_map() or epoch!=net.epoch or id==net.me() or not net.players.has(id) or packet.size()!=BYTES:return
	_note_activity_r5(id,packet)
	if not listening or muted.has(id):return
	if not net.poses.has(id) or game.player.get_view_camera().global_position.distance_to(net.poses[id].head.origin)>42:return
	received_packets+=1
	if not speakers.has(id):
		var player:=AudioStreamPlayer3D.new();player.name="PeerVoice%d"%id;player.bus="Voice";player.max_distance=40;player.unit_size=6;player.volume_db=linear_to_db(maxf(.0001,player_volume(id)));player.doppler_tracking=AudioStreamPlayer3D.DOPPLER_TRACKING_DISABLED
		var stream:=AudioStreamGenerator.new();stream.mix_rate=HZ;stream.buffer_length=.12;player.stream=stream;game.add_child(player);player.play()
		speakers[id]={"player":player,"queue":[],"last":Time.get_ticks_msec()}
	var row:Dictionary=speakers[id];row.last=Time.get_ticks_msec()
	if row.queue.size()>=5:row.queue.pop_front()
	row.queue.append(decode(packet))
func _remove_speaker(id:int)->void:
	if not speakers.has(id):return
	var player:AudioStreamPlayer3D=speakers[id].player
	if is_instance_valid(player):player.stop();player.queue_free()
	speakers.erase(id)
func _exit_tree()->void:
	_stop_capture()
	for id in speakers.keys():_remove_speaker(id)

func player_volume(id:int)->float:return float(player_volumes.get(id,1.0))
func set_player_volume(id:int,value:float)->void:
	player_volumes[id]=clampf(value,0,1)
	if speakers.has(id):speakers[id].player.volume_db=linear_to_db(maxf(.0001,player_volume(id)))
func is_speaking(id:int)->bool:
	return (_hangover>0 and enabled) if id==net.me() else Time.get_ticks_msec()<int(speaking_until.get(id,0))
func _note_activity_r5(id:int,packet:PackedByteArray)->void:
	for i in range(0,packet.size(),16):
		if absf(float(packet.decode_s16(i))/32767.0)>.012:speaking_until[id]=Time.get_ticks_msec()+400;return
@rpc("authority","call_remote","unreliable_ordered",4)
func _activity_down_r5(id:int,epoch:int)->void:
	if in_map() and epoch==net.epoch and net.players.has(id) and id!=net.me():speaking_until[id]=Time.get_ticks_msec()+400
