#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.WorldScores;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        R24WorldScoreState survivalResultsState;
        readonly TextMesh[] survivalResultsLabels=new TextMesh[8];readonly Label[] survivalResultsLayout=new Label[8];
        void BuildSurvivalResults()
        {
            if(survivalResultsState==null||!survivalResultsState.TeamRevive)return;
            for(int i=0;i<survivalResultsLabels.Length;i++){survivalResultsLabels[i]=Text(panel,"",new Vector3(i%2==0?-.29f:.29f,.16f-(i/2)*.045f,-.01f),.019f,.55f,Hex(R25Theme.Text));survivalResultsLayout[i]=labels[labels.Count-1];}
            RefreshSurvivalResults();
        }
        void RefreshSurvivalResults()
        {
            for(int i=0;i<survivalResultsLabels.Length;i++)if(survivalResultsLabels[i]!=null){var s=survivalResultsState;var row=s!=null&&s.TeamRevive&&i<s.Rows.Length?s.Rows[i]:null;survivalResultsLabels[i].text=row==null?"":row.Name+"  "+R24WorldScoreState.Number(row.Alive)+" ALIVE / "+R24WorldScoreState.Number(row.Downed)+" DOWNED";FitText(survivalResultsLayout[i]);}
        }
    }
}
#endif
