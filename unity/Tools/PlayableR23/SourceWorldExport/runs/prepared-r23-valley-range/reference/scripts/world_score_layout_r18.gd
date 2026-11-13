extends RefCounted
## Pure presentation geometry; no roster truncation for the session's eight active peers.
static func clock_text(state:Dictionary)->String:
 return "UNLIMITED" if state.time_unlimited else "%02d:%02d"%[int(state.seconds)/60,int(state.seconds)%60]
static func cap_text(state:Dictionary)->String:return "UNLIMITED" if state.score_unlimited else str(state.cap)
static func layout(state:Dictionary)->Dictionary:
 var primary_count:int=state.rows.size();var player_count:int=state.players.size() if state.teams>0 else 0
 var primary_preferred:float=64 if state.teams>0 else 88;var player_preferred:float=62
 var gap:float=48 if player_count>0 else 0
 var requested:float=primary_count*primary_preferred+player_count*player_preferred
 var scale:float=minf(1.0,(1240.0-190.0-gap)/maxf(1,requested))
 var primary_height:float=floor(primary_preferred*scale);var player_height:float=floor(player_preferred*scale)
 var y:float=190;var primary:Array=[];var players:Array=[]
 for row in state.rows:primary.append({"row":row,"y":y,"height":primary_height});y+=primary_height
 var header_y:float=y+36 if player_count>0 else -1
 y+=gap
 if player_count>0:
  for row in state.players:players.append({"row":row,"y":y,"height":player_height});y+=player_height
 return {"primary":primary,"players":players,"header_y":header_y,"end_y":y,"primary_height":primary_height,"player_height":player_height}
