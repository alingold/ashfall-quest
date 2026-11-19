#if UNITY_6000_0_OR_NEWER
using System;using System.IO;using System.Reflection;using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.WorldScores;
namespace Ashfall.Integration.Playable
{
    public sealed class R26MaxPlayersEvidence:MonoBehaviour
    {
        [Serializable] sealed class Row{public string actor;public int team,kills,deaths,score,rank;public bool spawn,scored,equipped;public Vector3 feet;}
        [Serializable] sealed class Sample{public float time;public int members,loaded,scoreRows;public string stage;public Row[] rows;}
        R23PlayableLoop root;float next;string path;int captures;bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(R25NetBot.Arg("-ashfallMaxPlayersEvidence")=="true")new GameObject("MAXPLAYERS evidence").AddComponent<R26MaxPlayersEvidence>();}
        void Update(){if(root==null)root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root==null||root.LanSession==null||Time.unscaledTime<next)return;next=Time.unscaledTime+3;
            var lobby=root.LanSession.Lobby;if(lobby==null)return;if(path==null)path=Path.ChangeExtension(R25NetBot.Arg("-ashfallNetBotLog"),"maxplayers.jsonl");
            var rows=new List<Row>();int scores=0;foreach(var m in lobby.Members){R24LanSpawn spawn;R24LanScoreRow score;R26RemoteEquipment equipment;var row=new Row{actor=m.Actor,team=m.Team,spawn=lobby.TryReadSpawn(m.Actor,out spawn),scored=lobby.TryReadScore(m.Actor,out score),equipped=lobby.TryReadRemoteEquipment(m.Actor,out equipment)};
                if(spawn!=null)row.feet=new Vector3(spawn.Feet.X,spawn.Feet.Y,spawn.Feet.Z);if(score!=null){scores++;row.kills=score.Kills;row.deaths=score.Deaths;row.score=score.Score;}var result=lobby.MatchEndResults==null?null:lobby.MatchEndResults.ForActor(m.Actor);if(result!=null)row.rank=result.Rank;rows.Add(row);}
            File.AppendAllText(path,JsonUtility.ToJson(new Sample{time=Time.unscaledTime,members=lobby.MemberCount,loaded=lobby.LoadedCount,scoreRows=scores,stage=lobby.MapStage.ToString(),rows=rows.ToArray()})+"\n");
            if(lobby.State==R24LanLobbyState.Hosting&&lobby.RoundElapsed>65&&!finished&&R25NetBot.Arg("-ashfallMaxPlayersFinish")=="true")finished=lobby.HostFinishMatchWithCelebration();
            if(lobby.State==R24LanLobbyState.Hosting&&root.MatchActive&&captures<3&&lobby.RoundElapsed>10+captures*20){CaptureScores(captures++);if(captures==1)CaptureFixtures();}
        }
        void CaptureFixtures(){var board=root.WorldScores;if(board==null||!board.Ready||board.SharedCanvas==null)return;var flags=BindingFlags.Instance|BindingFlags.NonPublic;var draw=typeof(R24WorldScoreboards).GetMethod("Draw",flags);var camera=(Camera)typeof(R24WorldScoreboards).GetField("canvasCamera",flags).GetValue(board);
            foreach(int teams in new[]{0,2,8}){var players=new R24WorldScoreRow[24];for(int i=0;i<24;i++)players[i]=new R24WorldScoreRow{Id="fixture"+i,Name="PLAYER "+(i+1)+" LONG NAME",Rank=i+1,Team=teams==0?-1:i%teams,Score=24-i,Kills=24-i,Deaths=i,Life="ALIVE"};
                var state=new R24WorldScoreState{Active=true,Stage="match",Teams=teams,Seconds=180,Cap=100,Rows=players};if(teams>0){state.Players=players;state.Rows=new R24WorldScoreRow[teams];for(int i=0;i<teams;i++)state.Rows[i]=new R24WorldScoreRow{Id="team"+i,Name="TEAM "+(i+1),Rank=i+1,Team=i,Score=100-i};}
                draw.Invoke(board,new object[]{state});var letters=(Material)typeof(R24WorldScoreboards).GetField("letters",flags).GetValue(board);var font=(Font)typeof(R24WorldScoreboards).GetField("font",flags).GetValue(board);letters.mainTexture=font.material.mainTexture;camera.Render();SaveCanvas(board.SharedCanvas,Path.ChangeExtension(path,"fixture-24-teams-"+teams+".png"));}board.Refresh();}
        static void SaveCanvas(RenderTexture target,string file){var active=RenderTexture.active;Texture2D pixels=null;try{RenderTexture.active=target;pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(file,pixels.EncodeToPNG());}finally{RenderTexture.active=active;if(pixels!=null)Destroy(pixels);}}
        void CaptureScores(int index){foreach(var board in UnityEngine.Object.FindObjectsByType<R24WorldScoreboards>()){if(board.SharedCanvas==null||board.LastState==null)continue;var active=RenderTexture.active;Texture2D image=null;try{RenderTexture.active=board.SharedCanvas;image=new Texture2D(board.SharedCanvas.width,board.SharedCanvas.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();File.WriteAllBytes(Path.ChangeExtension(path,"score-"+index+".png"),image.EncodeToPNG());}finally{RenderTexture.active=active;if(image!=null)Destroy(image);}break;}}
    }
}
#endif
