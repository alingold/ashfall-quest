#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Input;
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        sealed class CatalogCard {public string Kind,Value,Label,Icon;}
        bool creativeCatalog;int catalogRow,catalogStars=1;float catalogScroll;
        int ownedBackpackCount;bool canDropOwnedBackpack;
        public event Action BackpackDropRequested;
        public void SetOwnedBackpacks(int count,bool canDrop)
        {
            count=Math.Max(0,Math.Min(3,count));canDrop=canDrop&&count>0;
            if(ownedBackpackCount==count&&canDropOwnedBackpack==canDrop)return;
            ownedBackpackCount=count;canDropOwnedBackpack=canDrop;if(Page==R23UiPage.Loadout)Build();
        }
        public bool CreativeCatalogEnabled {get{return creativeCatalog;}}
        public event Action<R24CreativeRequest> CreativeItemRequested;
        public void SetCreativeCatalog(bool enabled)
        {if(creativeCatalog==enabled)return;creativeCatalog=enabled;catalogRow=0;catalogScroll=0;loadoutGesture.Reset();if(Page==R23UiPage.Loadout)Build();}
        List<CatalogCard> CreativeCards()
        {
            var cards=new List<CatalogCard>();
            foreach(var id in R24CreativeCatalog.Weapons())cards.Add(new CatalogCard{Kind="weapon",Value=id,Label=Ashfall.Integration.Playable.Explosives.R23GrenadeState.ValidKind(id)?Ashfall.Integration.Playable.Explosives.R23GrenadeState.KindLabel(id):R23HandsWeapons.WeaponDisplayName(id),Icon=id});
            foreach(var family in R24CreativeCatalog.Supplies())cards.Add(new CatalogCard{Kind="supply",Value=family,Label=family=="rifle"?"ASSAULT +32":family=="shells"?"SHOTGUN +6":family=="pistol"?"PISTOL +16":family=="smg"?"SMG +20":family=="sniper"?"SNIPER +5":"BUILDS +6",Icon="supply/"+family});
            foreach(var kind in R24CreativeCatalog.Items())cards.Add(new CatalogCard{Kind="item",Value=kind,Label=kind=="backpack"?"BACKPACK +1 SLOT":kind.ToUpperInvariant(),Icon=kind});
            cards.Add(new CatalogCard{Kind="utility",Value="revive",Label="REVIVE PADDLES",Icon="revive"});return cards;
        }
        public void ScrollBackpack(float axis,float seconds,bool eligible)
        {
            if(!creativeCatalog||Page!=R23UiPage.Loadout||!eligible||float.IsNaN(axis)||float.IsInfinity(axis)||float.IsNaN(seconds)||float.IsInfinity(seconds)||Mathf.Abs(axis)<=.2f)return;
            int maximum=Math.Max(0,(CreativeCards().Count+3)/4-5);
            loadoutGesture.CancelPendingClicks();ApplyBackpackScrollPixels(Mathf.Clamp(catalogScroll-axis*650*Mathf.Clamp(seconds,0,.1f),0,maximum*124));
        }
        bool PublishCreative(R23LoadoutIntent intent)
        {
            if(intent.Id.StartsWith("catalograrity/",StringComparison.Ordinal))
            {if(creativeCatalog){int stars;if(Int32.TryParse(intent.Id.Substring(14),out stars)&&stars>=1&&stars<=5){catalogStars=stars;Build();}}return true;}
            if(!intent.Id.StartsWith("catalog/",StringComparison.Ordinal))return false;
            if(!creativeCatalog)return true;string[] parts=intent.Id.Split('/');if(parts.Length!=3||!R24CreativeCatalog.Contains(parts[1],parts[2]))return true;
            bool drop=intent.Action==R23LoadoutAction.Drop;
            if(!drop&&parts[1]!="supply")CloseBackpack();
            var callback=CreativeItemRequested;if(callback!=null)callback(new R24CreativeRequest(parts[1],parts[2],intent.Hand,catalogStars,drop));return true;
        }
        void BuildCreativeCatalog()
        {
            if(!creativeCatalog)return;
            for(int stars=1;stars<=5;stars++)
            {var b=AddButton(panel,new string('\u2605',stars),-.46f+(stars-1)*.23f,.608f,.22f,.0525f,null,.023f,ownedId:"catalograrity/"+stars);b.SelectionEdgeOnly=true;SetButtonStyle(b,Hex(stars==catalogStars?R25Theme.RaisedHover:R25Theme.Raised),Hex(R23InventoryPresentation.RarityColor(stars)));SetHovered(b,false);}
            Text(panel,"ALL ITEMS  /  TEST CATALOG",new Vector3(-.025f,.09f,-.01f),.018f,1.12f,Hex(R25Theme.Text));
            var cards=CreativeCards();int start=catalogRow*4;
            for(int cell=0;cell<24&&start+cell<cards.Count;cell++)
            {
                var row=cards[start+cell];bool tiered=row.Kind=="weapon"&&R23InventoryPresentation.Tiered(row.Value);
                var button=AddButton(panel,"",-.43875f+cell%4*.2925f,-.006f-cell/4*.155f,.2825f,.145f,null,.0144f,true,"catalog/"+row.Kind+"/"+row.Value);button.SelectionEdgeOnly=true;backpackCatalogSurfaces.Add(button.Surface);
                if(row.Kind=="utility")Icon(button.Surface,"backpack_r23_revive",new Vector3(0,.019375f,-.006f),new Vector2(.10f,.07f));
                else InventoryIcon(button.Surface,row.Icon,new Vector3(0,.019375f,-.006f),.2675f,.09375f,tiered?catalogStars:1);
                var caption=Text(button.Surface,row.Label+(tiered?" "+new string('\u2605',catalogStars):""),new Vector3(0,-.0325f,-.007f),.0144f,.2675f,tiered?Hex(R23InventoryPresentation.RarityColor(catalogStars)):Hex(R25Theme.Text));caption.anchor=TextAnchor.UpperCenter;
            }
            RefreshBackpackCatalogPositions();
        }
    }
}
#endif
