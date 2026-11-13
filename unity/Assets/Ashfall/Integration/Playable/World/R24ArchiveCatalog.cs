#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using UnityEngine;
namespace Ashfall.Integration.Playable.World
{
    // Only converted, independently checked source maps enter this runtime list.
    public static class R24ArchiveCatalog
    {
        [Serializable]sealed class Row {public string id="",title="",relativeFolder="";}
        [Serializable]sealed class Data {public string sourceContentId="";public Row[] maps=null;}
        static bool installed;
        public static void Install()
        {
            if(installed)return;var text=Resources.Load<TextAsset>("R24ArchiveCatalog");
            if(text==null)throw new InvalidDataException("Converted R6 archive catalog missing");
            var data=JsonUtility.FromJson<Data>(text.text);
            if(data==null||data.sourceContentId!="23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520"||data.maps==null||data.maps.Length>105)throw new InvalidDataException("Invalid converted archive catalog");
            foreach(var row in data.maps)R23MapCatalog.Add(row.id,row.title,row.relativeFolder);
            installed=true;
        }
    }
}
#endif
