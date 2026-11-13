#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public static class R23QuestBuildReport
    {
        [Serializable]sealed class Message{public string type,content;}
        [Serializable]sealed class Step{public string name;public double seconds;public List<Message> messages=new List<Message>();}
        [Serializable]sealed class Receipt{public string result,platform,outputPath,startedUtc,endedUtc;public int errors,warnings;public ulong bytes;public List<Step> steps=new List<Step>();}
        public static void Export(string path)
        {
            if(File.Exists(path))throw new Exception("Preserve earlier BuildReport receipt");
            var report=BuildReport.GetLatestReport();if(report==null)throw new Exception("Latest actual BuildReport unavailable");
            var summary=report.summary;var record=new Receipt{result=summary.result.ToString(),platform=summary.platform.ToString(),outputPath=summary.outputPath,startedUtc=summary.buildStartedAt.ToUniversalTime().ToString("o"),endedUtc=summary.buildEndedAt.ToUniversalTime().ToString("o"),errors=summary.totalErrors,warnings=summary.totalWarnings,bytes=summary.totalSize};
            foreach(var source in report.steps){var step=new Step{name=source.name,seconds=source.duration.TotalSeconds};foreach(var message in source.messages)step.messages.Add(new Message{type=message.type.ToString(),content=message.content});record.steps.Add(step);}
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(record,true));Debug.Log("Exported actual BuildReport: "+path+" result="+record.result+" errors="+record.errors);
        }
    }
}
#endif
