#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        [Serializable] sealed class WeaponCredit
        {
            public string id="",title="",author="",authorUrl="",sourceUrl="",license="",licenseUrl="",changes="",notice="";
        }
        [Serializable] sealed class WeaponCredits
        {
            public WeaponCredit[] entries=new WeaponCredit[0];
        }
        WeaponCredits weaponCredits;
        int creditsPage;
        const int CreditsPerPage=2;

        public void OpenCredits() { creditsPage=0;OpenSubpage(R23UiPage.Credits); }

        void BuildCredits()
        {
            if(weaponCredits==null)
            {
                var asset=Resources.Load<TextAsset>("R23WeaponCredits");
                weaponCredits=asset==null?new WeaponCredits():JsonUtility.FromJson<WeaponCredits>(asset.text);
                if(weaponCredits==null||weaponCredits.entries==null)weaponCredits=new WeaponCredits();
            }
            int pages=Mathf.Max(1,(weaponCredits.entries.Length+CreditsPerPage-1)/CreditsPerPage);
            creditsPage=Mathf.Clamp(creditsPage,0,pages-1);
            Quad(panel,"Credits backdrop",Vector3.zero,new Vector2(2.2f,1.375f),Hex(R25Theme.Panel));
            title.text="";
            FrontText("ASHFALL / CREDITS",640,48,32,1150).color=Hex(R25Theme.Text);
            FrontText("WEAPON MODEL DESIGNERS  /  "+(creditsPage+1)+" OF "+pages,640,96,22,1150);
            if(weaponCredits.entries.Length==0)FrontText("Weapon credits could not be loaded.",640,310,26,1100);
            for(int slot=0;slot<CreditsPerPage;slot++)
            {
                int index=creditsPage*CreditsPerPage+slot;if(index>=weaponCredits.entries.Length)break;
                var credit=weaponCredits.entries[index];float y=165+slot*260;
                FrontText(CreditLines(credit.title,65),640,y,28,1160).color=Hex(R25Theme.Teal);
                FrontText("By "+credit.author,640,y+50,26,1120);
                FrontText(credit.license+"  /  Ashfall: "+credit.id,640,y+84,22,1120);
                FrontText(CreditLines(credit.changes,82),640,y+128,22,1120);
                CreditLink("ORIGINAL MODEL",credit.sourceUrl,180,y+175,300);
                CreditLink("DESIGNER",credit.authorUrl,500,y+175,280);
                CreditLink("LICENSE",credit.licenseUrl,800,y+175,280);
            }
            FrontText("Models adapted for Ashfall. No endorsement implied.",640,683,19,1120).color=Hex(R25Theme.TextDim);
            FrontButton("BACK",40,720,170,58,delegate{SetPage(returnPage);},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            var previous=FrontButton("PREVIOUS",390,720,220,58,delegate{creditsPage--;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);previous.Enabled=creditsPage>0;
            var next=FrontButton("NEXT",670,720,220,58,delegate{creditsPage++;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);next.Enabled=creditsPage+1<pages;
        }
        void CreditLink(string caption,string url,float x,float y,float width)
        {
            Uri target;bool valid=Uri.TryCreate(url,UriKind.Absolute,out target)&&target.Scheme==Uri.UriSchemeHttps;
            var button=FrontButton(caption,x,y,width,48,delegate{Application.OpenURL(url);},Hex(R25Theme.Raised),Hex(R25Theme.Text),21);
            button.Enabled=valid;if(!valid)SetButtonStyle(button,Hex(R25Theme.Disabled),Hex(R25Theme.TextDisabled));
        }
        static string CreditLines(string value,int width)
        {
            if(String.IsNullOrEmpty(value))return "";
            int start=0;var lines=new System.Collections.Generic.List<string>();
            while(value.Length-start>width)
            {
                int end=value.LastIndexOf(' ',start+width,width);
                if(end<=start)end=start+width;
                lines.Add(value.Substring(start,end-start));start=end;
                while(start<value.Length&&value[start]==' ')start++;
            }
            lines.Add(value.Substring(start));return String.Join("\n",lines.ToArray());
        }
    }
}
#endif
