from pathlib import Path
import json,hashlib
S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source/assets/data');R=Path(__file__).resolve().parents[4]
a=S/'one_star_050.json';b=S/'weapon_rarity_r6.json';rates=json.loads(a.read_text())['profiles'];damage=json.loads(b.read_text())['weapons']
text='using System;\nnamespace Ashfall.Integration.Playable.Networking {\n public sealed class R24LanBallisticProfile {\n  public readonly string Id;public readonly float Rate;public readonly int Pellets;readonly float[] body,head;\n  R24LanBallisticProfile(string id,float rate,int pellets,float[] body,float[] head){Id=id;Rate=rate;Pellets=pellets;this.body=body;this.head=head;}\n  public float Damage(int stars,bool headshot,float distance){if(stars<1||stars>5||distance<0||distance>180)return 0;float fall=Pellets>1?1-.75f*Math.Max(0,Math.Min(1,(distance-8)/17)):1;return (headshot?head:body)[stars-1]/Pellets*fall;}\n'
text+='  // Frozen R6 one_star_050 SHA '+hashlib.sha256(a.read_bytes()).hexdigest()+'; rarity SHA '+hashlib.sha256(b.read_bytes()).hexdigest()+'.\n'
text+='  public static bool TryRead(string id,out R24LanBallisticProfile result){switch(id){\n'
for p in rates:
    cat=p['index'];key=p['id'];alias={11:'uzis',12:'matadors'}.get(cat,key)
    if cat in [14,15,16,26,27,28,29]:continue
    d=damage[key];rate=p['rounds_per_second']*(1.2 if cat==12 else 1)
    arr=lambda v:'new float[]{'+','.join(format(float(x),'.9g')+'f' for x in v)+'}'
    text+='   case "'+alias+'":result=new R24LanBallisticProfile(id,'+format(rate,'.9g')+'f,'+str(d['pellets'])+','+arr(d['body'])+','+arr(d['head'])+');return true;\n'
text+='   default:result=null;return false;}}\n }\n}\n'
(R/'unity/Assets/Ashfall/Integration/Playable/Networking/R24LanBallisticProfile.cs').write_text(text)
print('Generated exact frozen source cadence and rarity damage for firearm profiles.')
