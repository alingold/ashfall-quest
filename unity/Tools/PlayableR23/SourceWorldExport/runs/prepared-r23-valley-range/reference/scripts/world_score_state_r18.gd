extends RefCounted
## Presentation-only snapshot. Authority stores/scoring/rules are never mutated here.
static func _score(scores:Dictionary,id:int)->int:return int(scores.get(id,scores.get(str(id),0)))
static func _rank(rows:Array,teams:bool=false)->Array:
 rows.sort_custom(func(a,b):
  if a.score!=b.score:return a.score>b.score
  if not teams:
   var order:int=str(a.name).nocasecmp_to(str(b.name))
   if order!=0:return order<0
  return a.id<b.id)
 for i in rows.size():rows[i].rank=i+1
 return rows
static func snapshot(net:Node)->Dictionary:
 var limit:int=maxi(0,int(net.rules.score_limit));var time_limit:int=maxi(0,int(net.rules.time_limit));var teams:int=maxi(0,int(net.rules.get("teams",2)))
 var seconds:int=maxi(0,int(float(time_limit)-(net.now()-net.match_started))) if net.stage=="match" and time_limit>0 else time_limit
 var players:Array=[];var team_ids:Dictionary={};var totals:Dictionary={};var legacy_scores:Array=[]
 for team in teams:team_ids[team]=true;legacy_scores.append(_score(net.match_scores,team))
 for peer in net.players:
  var row:Dictionary=net.players[peer]
  if bool(row.get("spectator",false)):continue
  var id:int=int(peer);var team:int=int(row.get("team",-1))
  if teams>0 and team>=0:team_ids[team]=true
  var score:int=_score(net.match_scores,id) if teams==0 else int(row.get("score",row.get("kills",0)))
  players.append({"id":id,"name":str(row.get("name","Player")),"score":score,"team":team,"kills":int(row.get("kills",0)),"deaths":int(row.get("deaths",0))})
 var rows:Array=[]
 if teams>0:
  for team in team_ids:
   totals[team]=_score(net.match_scores,team);rows.append({"id":int(team),"name":"TEAM %d"%(int(team)+1),"score":totals[team],"team":int(team)})
  _rank(rows,true)
 else:
  for player in players:totals[player.id]=player.score
  rows=players.duplicate(true);_rank(rows)
 _rank(players)
 return {"active":bool(net.active),"stage":str(net.stage),"mode":str(net.rules.get("mode","respawn")),"teams":teams,"seconds":seconds,"time_unlimited":time_limit==0,"cap":limit,"score_unlimited":limit==0,"scores":legacy_scores,"scores_by_id":totals,"rows":rows,"players":players,"header":"TEAM SCORES" if teams>0 else "PLAYER SCORES"}
