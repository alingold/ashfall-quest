using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ashfall.Integration.Playable.World
{
    // MAP-SPAWN-MARKERS (playtest 2026-11-15, Magnum video 27:40/30:00): source map-editor
    // authoring symbols (spawn humanoids + rings, warmup rings/arrows) were imported as
    // visible, non-colliding chunks. World/Resources/R26MapAuthoringMarkers.json lists, per
    // map, the chunks made only of source-semantics marker_parts triangles
    // (~/ashfall/campaign/staging/MAPS/R26MapData.py). They are skipped like retired chunks.
    // Every listed chunk must still match its audited material, triangle count and
    // render-only role; colliding and unmatched chunks are always left intact.
    public sealed partial class R23PlayableMap
    {
        [Serializable]sealed class AuthoringMarkerPart{public int chunk=0;public string material="";public int triangles=0;public int[] ranges=null;public bool collisionOnly=false;}
        [Serializable]sealed class AuthoringMarkerMap{public string id="",sourceSha256="";public int[] chunks=null;public string[] materials=null;public int[] triangles=null;public AuthoringMarkerPart[] parts=null;}
        [Serializable]sealed class AuthoringMarkerData{public int version=0;public AuthoringMarkerMap[] maps=null;}
        static Dictionary<string,AuthoringMarkerMap> authoringMarkerMaps;
        public static string AuthoringMarkerFailure {get;private set;}
        AuthoringMarkerMap authoringMarkers;
        public int HiddenMarkerChunks {get;private set;}public int HiddenMarkerTriangles {get;private set;}
        void BeginAuthoringMarkers()
        {
            HiddenMarkerChunks=HiddenMarkerTriangles=0;authoringMarkers=null;
            if(authoringMarkerMaps==null)authoringMarkerMaps=LoadAuthoringMarkerMaps();
            AuthoringMarkerMap row;
            if(manifest!=null&&MapId!=null&&authoringMarkerMaps.TryGetValue(MapId,out row)&&row.sourceSha256==manifest.sourceSha256)authoringMarkers=row;
        }
        static Dictionary<string,AuthoringMarkerMap> LoadAuthoringMarkerMaps()
        {
            var result=new Dictionary<string,AuthoringMarkerMap>(StringComparer.Ordinal);
            try
            {
                var text=Resources.Load<TextAsset>("R26MapAuthoringMarkers");if(text==null){AuthoringMarkerFailure="R26MapAuthoringMarkers resource missing";return result;}
                var data=JsonUtility.FromJson<AuthoringMarkerData>(text.text);if(data==null||data.version!=1||data.maps==null){AuthoringMarkerFailure="Invalid R26MapAuthoringMarkers data";return result;}
                foreach(var row in data.maps)
                    if(row!=null&&!String.IsNullOrEmpty(row.id)&&row.chunks!=null&&row.materials!=null&&row.triangles!=null&&row.chunks.Length==row.materials.Length&&row.chunks.Length==row.triangles.Length)result[row.id]=row;
            }
            catch(Exception error){AuthoringMarkerFailure=error.Message;result.Clear();}
            return result;
        }
        // r19 MAPKIT: marker triangles that share a chunk with real geometry (or sit in a colliding chunk) are listed as
        // per-chunk triangle ranges [start,count,...] in a row's `parts`. A part drops those triangles from the render
        // mesh and the collider (red hexagon rings, warm-up/deathmatch rings, arrows, figures) or, with collisionOnly,
        // from the collider alone (jump-pad chevrons and health-station cross bars stay visible but must not block the
        // pad's contact ray). Returns true when the chunk has an audited entry. kept = indices without hidden triangles
        // (empty = the whole chunk is markers, null = unchanged); colliderKept = collider indices (null = unchanged).
        // The entry must match the chunk's material and triangle count exactly, so a different capture never loses geometry.
        bool FilterAuthoringMarkerTriangles(int chunk,int material,int[] indices,out int[] kept,out int[] colliderKept)
        {
            kept=null;colliderKept=null;
            if(authoringMarkers==null||authoringMarkers.parts==null||material<0||material>=manifest.materials.Length)return false;
            int triangles=indices.Length/3;bool[] hide=null,decor=null;int hidden=0,decorative=0;
            foreach(var part in authoringMarkers.parts)
            {
                if(part==null||part.chunk!=chunk||part.ranges==null||part.ranges.Length%2!=0)continue;
                if(part.triangles!=triangles||manifest.materials[material].name!=part.material)return false;
                bool[] mask;if(part.collisionOnly){if(decor==null)decor=new bool[triangles];mask=decor;}else{if(hide==null)hide=new bool[triangles];mask=hide;}
                for(int r=0;r<part.ranges.Length;r+=2)for(int t=part.ranges[r];t<part.ranges[r]+part.ranges[r+1]&&t<triangles;t++)if(t>=0&&!mask[t]){mask[t]=true;if(part.collisionOnly)decorative++;else hidden++;}
            }
            if(hidden==0&&decorative==0)return false;
            if(hidden>0){HiddenMarkerTriangles+=hidden;if(hidden==triangles)HiddenMarkerChunks++;kept=Without(indices,hide,triangles,hidden);}
            if(decorative>0)
            {
                // Collider = original triangles minus hidden minus collision-only; the render mesh keeps `kept`.
                var both=new bool[triangles];int count=0;
                for(int t=0;t<triangles;t++){both[t]=(hide!=null&&hide[t])||(decor!=null&&decor[t]);if(both[t])count++;}
                CollisionOnlyMarkerTriangles+=decorative;colliderKept=Without(indices,both,triangles,count);
            }
            return true;
        }
        public int CollisionOnlyMarkerTriangles {get;private set;}
        static int[] Without(int[] indices,bool[] drop,int triangles,int dropped)
        {
            var result=new int[(triangles-dropped)*3];int write=0;
            for(int t=0;t<triangles;t++)if(drop==null||!drop[t]){result[write++]=indices[t*3];result[write++]=indices[t*3+1];result[write++]=indices[t*3+2];}
            return result;
        }
        bool HideAuthoringMarker(int chunk,int material,int collisionFlags,bool visible,int triangles)
        {
            if(authoringMarkers==null||collisionFlags!=0||!visible||material<0||material>=manifest.materials.Length)return false;
            int at=Array.IndexOf(authoringMarkers.chunks,chunk);
            if(at<0||manifest.materials[material].name!=authoringMarkers.materials[at]||triangles!=authoringMarkers.triangles[at])return false;
            HiddenMarkerChunks++;HiddenMarkerTriangles+=triangles;return true;
        }
    }
}
