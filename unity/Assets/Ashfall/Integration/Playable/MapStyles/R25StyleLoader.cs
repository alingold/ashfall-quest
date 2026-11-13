#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.MapStyles
{
    public static class R25StyleLoader
    {
        static bool attempted, loading;
        static R25MapStyles styles;
        public static R25StyleProfile Resolve(string mapId)
        { return Expansion.R25Features.MapStyles && styles != null ? styles.Resolve(mapId) : null; }
        public static string Url(string path)
        {
            string url = Application.streamingAssetsPath + "/PlayableR23/" + path;
            return url.Contains("://") ? url : new Uri(url).AbsoluteUri;
        }
        public static IEnumerator Load()
        {
            while (loading) yield return null;
            if (attempted) yield break;
            loading = true;
            try
            {
                using (var request = UnityWebRequest.Get(Url("Styles/styles.json")))
                {
                    request.timeout = 30;
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success) styles = R25MapStyles.Parse(request.downloadHandler.text);
                    if (styles == null) Debug.LogWarning("Map styles unavailable or invalid; using original archive look.");
                }
            }
            finally { attempted = true; loading = false; }
        }
    }
}
#endif
