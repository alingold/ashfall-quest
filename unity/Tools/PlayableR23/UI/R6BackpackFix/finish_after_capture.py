"""Finite remaining source UI corrections; run only after the root's Assets release."""
from pathlib import Path
import argparse
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--assets-released',action='store_true');args=parser.parse_args();assert args.assets_released
    path=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/R23VrUI.cs';text=path.read_text(encoding='utf-8')
    changes={
      'public void OpenWeapons() { OpenSubpage(R23UiPage.Weapons); }':'public void OpenWeapons() { weaponChoice=0;OpenSubpage(R23UiPage.Weapons); }',
      'FrontButton("LOADOUT",450,712,330,64,OpenWeapons':'FrontButton("LOADOUT",450,712,180,64,OpenWeapons',
      'FrontButton("OUTFITS",970,712,270,64,OpenAppearance':'FrontButton("OUTFITS",650,712,180,64,OpenAppearance',
      'close.SelectionEdgeOnly=true;':'close.SelectionEdgeOnly=true;SetHovered(close,false);',
      'revive.SelectionEdgeOnly=true;':'revive.SelectionEdgeOnly=true;SetHovered(revive,false);',
      'new Vector3(-.14f,-.285f,-.01f),.0144f,.90f':'new Vector3(0,-.249f,-.01f),.0144f,1.16f',
      'paddlesStatusText=Text(panel,paddlesStatus,new Vector3(0,-.249f,-.01f),.0126f,1.1f':'paddlesStatusText=Text(panel,paddlesStatus,new Vector3(-.14f,-.285f,-.01f),.0144f,.90f',
      '            if(title!=null)title.text="BACKPACK";':'            foreach(var card in loadoutCards)SetHovered(card.Button,hovered[0]==card.Button||hovered[1]==card.Button||focusButton==card.Button);\n            if(title!=null)title.text="BACKPACK";'
    }
    for before,after in changes.items():assert text.count(before)==1,before;text=text.replace(before,after,1)
    path.write_text(text,encoding='utf-8')
    print('Applied exact R6 lobby180px controls, gallery index0 admission, and initial backpack edge styling.')
if __name__=='__main__':main()
