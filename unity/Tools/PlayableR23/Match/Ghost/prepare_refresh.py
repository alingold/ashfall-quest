from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable';STAGE=HERE/'RecoveryStaged'
p=LIVE/'R23PlayableLoop.Ghost.cs';text=p.read_text();old='if(!localGhostAppearance.SetEnabled(ghost))SetMatchStatus(localGhostAppearance.Failure);';assert text.count(old)==1
(STAGE/p.name).write_text(text.replace(old,old+'\n            if(localGhostAppearance.Active)localGhostAppearance.Refresh();'))
p=LIVE/'Match/R24GhostAppearance.cs';text=p.read_text();text=text.replace('readonly List<Renderer> scratch=new List<Renderer>();','readonly List<Renderer> scratch=new List<Renderer>();readonly List<Material> liveMaterials=new List<Material>();')
old='''{var live=r.View.sharedMaterials;if(live.Length!=r.Replacement.Length){changed=true;break;}for(int i=0;i<live.Length;i++)if(live[i]!=r.Replacement[i]){changed=true;break;}if(changed)break;}'''
new='''{liveMaterials.Clear();r.View.GetSharedMaterials(liveMaterials);if(liveMaterials.Count!=r.Replacement.Length){changed=true;break;}for(int i=0;i<liveMaterials.Count;i++)if(liveMaterials[i]!=r.Replacement[i]){changed=true;break;}if(changed)break;}'''
assert text.count(old)==1;text=text.replace(old,new)
old='''{var live=r.View.sharedMaterials;if(live.Length!=r.Replacement.Length)r.Original=live;else for(int i=0;i<live.Length;i++)if(live[i]!=r.Replacement[i])r.Original[i]=live[i];}'''
new='''{liveMaterials.Clear();r.View.GetSharedMaterials(liveMaterials);if(liveMaterials.Count!=r.Replacement.Length)r.Original=liveMaterials.ToArray();else for(int i=0;i<liveMaterials.Count;i++)if(liveMaterials[i]!=r.Replacement[i])r.Original[i]=liveMaterials[i];}'''
assert text.count(old)==1;text=text.replace(old,new)
p=STAGE/'Match/R24GhostAppearance.cs';p.write_text(text)
print('Staged active Refresh with reusable material list; no Assets writes.')
