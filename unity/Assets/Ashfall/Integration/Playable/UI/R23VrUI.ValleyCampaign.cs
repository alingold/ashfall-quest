#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        public Func<bool> ValleyCampaignAvailable,ValleyCheckpointAvailable;
        public event Action NewValleyCampaignRequested,ContinueValleyCampaignRequested;
        void BuildValleyCampaignActions()
        {
            if(ValleyCampaignAvailable==null||!ValleyCampaignAvailable())return;
            FrontButton("NEW CAMPAIGN",970,490,270,58,delegate{if(NewValleyCampaignRequested!=null)NewValleyCampaignRequested();},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            var resume=FrontButton("CONTINUE",970,562,270,58,delegate{if(ContinueValleyCampaignRequested!=null)ContinueValleyCampaignRequested();},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);resume.Enabled=ValleyCheckpointAvailable!=null&&ValleyCheckpointAvailable();if(!resume.Enabled)SetButtonStyle(resume,Hex(R25Theme.Disabled),Hex(R25Theme.TextDisabled));
        }
    }
}
#endif
