from pathlib import Path
import hashlib,json,uuid
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable'
STAGE=HERE/'Staged'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rows=[]
def change(path,replacements):
 p=LIVE/path;t=p.read_text(encoding='utf-8-sig')
 for old,new in replacements:
  assert t.count(old)==1,(path,old,t.count(old));t=t.replace(old,new)
 q=STAGE/path;q.parent.mkdir(parents=True,exist_ok=True);q.write_text(t,encoding='utf-8')
 rows.append(dict(path=path,baselineSha256=sha(p),stagedSha256=sha(q)))
change('Climbing/R23HeadCollision.cs',[
 ('        bool Relevant(Collider c)','        Func<Collider,bool> sourceGhostActorFilter;\n        public void SetSourceGhostActorFilter(Func<Collider,bool> actualActorLayer4){sourceGhostActorFilter=actualActorLayer4;}\n        bool Relevant(Collider c)'),
 ('c!=hull&&c.enabled','c!=hull&&(sourceGhostActorFilter==null||!sourceGhostActorFilter(c))&&c.enabled')])
change('R23VRRig.cs',[
 ('                Activities.TickConstruction(frame,','                var constructionFrame=frame;if(SourceGhostMotion)constructionFrame.Paused=true;\n                Activities.TickConstruction(constructionFrame,')])
change('Appearance/R23PlayerAppearance.cs',[
 ('        public Material CurrentGloveMaterial','        // Actual selected world-rooted art; no selector preview or mesh duplication.\n        public Transform CurrentVisualRoot {get{return current==null||current.Root==null?null:current.Root.transform;}}\n        public Material CurrentGloveMaterial')])
change('R23PlayableLoop.Match.cs',[
 ('public bool MatchLocalInputAllowed{get{return ActualMatchRules==null||MatchActive&&MatchLocalAlive;}}','public bool MatchLocalInputAllowed{get{return MatchLocalItemsAllowed;}}'),
 ('void PublishMatchLife(bool force){var state=Gameplay.LocalMatchHealth;', 'void PublishMatchLife(bool force){SyncMatchGhost();var state=Gameplay.LocalMatchHealth;'),
 ('previousMatchLife=state.Life;if(state.Life!=LifeState.Alive){','previousMatchLife=state.Life;if(state.Life!=LifeState.Alive){Rig.DisarmMotion();'),
 ('if(ActualMatchRules==null)return;ActualMatchRules=null;UI.SetLocalMatchLifeState(null,0);','if(ActualMatchRules==null){RestoreMatchGhost();return;}RestoreMatchGhost();ActualMatchRules=null;UI.SetLocalMatchLifeState(null,0);'),
 ('public void DisposeMatch(){','public void DisposeMatch(){DisposeMatchGhost();')])
for p in STAGE.rglob('*'):
 if not p.is_file()or p.suffix=='.meta':continue
 rel=p.relative_to(STAGE).as_posix()
 if not any(r['path']==rel for r in rows):
  live=LIVE/rel;assert not live.exists(),rel
  rows.append(dict(path=rel,baselineSha256=None,stagedSha256=sha(p)))
 meta=p.with_name(p.name+'.meta')
 liveMeta=(LIVE/rel).with_name(p.name+'.meta')
 if liveMeta.exists():meta.write_bytes(liveMeta.read_bytes())
 elif not meta.exists():
  guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall/R6/ghost/'+rel).hex
  kind='ShaderImporter'if p.suffix=='.shader'else'MonoImporter'
  meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+kind+':\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
(HERE/'candidate.json').write_text(json.dumps(dict(root=str(LIVE),files=sorted(rows,key=lambda r:r['path'])),indent=2)+'\n')
print('Staged',len(rows),'ghost source files; no Assets writes.')
