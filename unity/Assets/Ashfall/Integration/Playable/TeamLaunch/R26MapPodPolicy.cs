using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.TeamLaunch
{
    // BOXES: reviewed per-map pod and interior spawn decisions. One JSON row per
    // shipped launch payload in World/Resources/R26MapPodPolicy.json. BOXED always
    // disables pods. Interior hints are source-local feet points, source-hash bound,
    // and the native planner still validates floor, standing room and hazards.
    // OPEN/UNSURE retain their previous pod decision; unknown ids retain GroundTeams.
    // Regenerate with ~/ashfall/tools/boxes/R26MapData.py --boxes <repo root>.
    public static class R26MapPodPolicy
    {
        [Serializable]sealed class Point{public float x=0,y=0,z=0;public Float3 V(){return new Float3(x,y,z);}}
        [Serializable]sealed class Row{public string id="",source="",evidence="",verdict="",sourceSha256="";public bool pods=true;public Point[] spawns=null;public Point warmupLocal=null,gameLocal=null;public bool hasWarmupLocal=false,hasGameLocal=false;}
        [Serializable]sealed class Data{public int version=0;public Row[] maps=null;}
        static Dictionary<string,bool> rows;static readonly Dictionary<string,Row> interiors=new Dictionary<string,Row>(StringComparer.Ordinal);
        // BOXES rows use source-local coordinates (Godot Z), never world/map-top rays.
        public static bool InteriorSpawn(string mapId,string sourceSha256)
        {Rows();Row row;return mapId!=null&&interiors.TryGetValue(mapId,out row)&&row.sourceSha256==sourceSha256;}
        public static Float3[] InteriorHints(string mapId,string sourceSha256)
        {if(!InteriorSpawn(mapId,sourceSha256))return new Float3[0];var row=interiors[mapId];if(row.spawns==null)return new Float3[0];var result=new Float3[row.spawns.Length];for(int i=0;i<result.Length;i++)result[i]=row.spawns[i].V();return result;}
        static readonly Dictionary<string,Row> reviewed=new Dictionary<string,Row>(StringComparer.Ordinal);
        // These two entry defaults are represented Unity-local feet, just like map.json.
        public static bool ReviewedDefault(string id,string source,bool warmup,out Float3 point){
            Rows();Row row;point=new Float3();if(id==null||!id.StartsWith("archive:",StringComparison.Ordinal)||!reviewed.TryGetValue(id,out row)||source!=row.sourceSha256||!(warmup?row.hasWarmupLocal:row.hasGameLocal))return false;
            var p=warmup?row.warmupLocal:row.gameLocal;if(p==null)return false;point=p.V();return true;
        }
        public static string Failure {get;private set;}
        public static bool UsesPods(string mapId)
        {
            if(String.IsNullOrEmpty(mapId))return false;
            bool pods;if(Rows().TryGetValue(mapId,out pods))return pods;
            return !Ashfall.Integration.Playable.Match.R24ArchiveRoundPolicy.GroundTeams(mapId);
        }
        public static bool HasExplicitOrAutoRow(string mapId){bool pods;return mapId!=null&&Rows().TryGetValue(mapId,out pods);}
        static Dictionary<string,bool> Rows()
        {
            if(rows!=null)return rows;var result=new Dictionary<string,bool>(StringComparer.Ordinal);
#if UNITY_6000_0_OR_NEWER
            try
            {
                var text=UnityEngine.Resources.Load<UnityEngine.TextAsset>("R26MapPodPolicy");
                var data=text==null?null:UnityEngine.JsonUtility.FromJson<Data>(text.text);
                if(data==null||data.version!=1||data.maps==null)Failure="R26MapPodPolicy resource missing or invalid";
                else foreach(var row in data.maps)if(row!=null&&!String.IsNullOrEmpty(row.id)){result[row.id]=row.verdict=="BOXED"?false:row.pods;if(row.verdict=="BOXED")interiors[row.id]=row;if(row.hasWarmupLocal||row.hasGameLocal)reviewed[row.id]=row;}
            }
            catch(Exception error){Failure=error.Message;result.Clear();interiors.Clear();}
#endif
            rows=result;return rows;
        }
    }
}
