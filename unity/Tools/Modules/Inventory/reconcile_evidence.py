"""Audit exact frozen r2 -> captured r3 inputs; no original source writes."""
import difflib
import hashlib
import inventory as tool
from reconciliation import verify_receipt

def main():
    receipt=tool.read(tool.EVIDENCE/'capture-receipt.json')
    verify_receipt(receipt)
    oldroot=tool.ROOT/'docs/agent-runs/20261104-three-chats/unity/Inventory/r2'
    old=tool.read(oldroot/'source-symbols.json');new=tool.read(tool.EVIDENCE/'source-symbols.json')
    a={r['id']:r for r in old};b={r['id']:r for r in new}
    diffs=[{'id':k,'before':a.get(k),'after':b.get(k)} for k in sorted(set(a)|set(b)) if a.get(k)!=b.get(k)]
    tool.write(tool.EVIDENCE/'r2-symbol-diff.json',diffs)
    hud=[]
    for tree, folder in [('quest','quest-r22/source'),('game','game')]:
        rows=[r for r in old if r['source']['tree']==tree and r['source']['path']=='scripts/match_hud_r3.gd']
        source=rows[0]['source'];current=tool.OUT/'inputs'/folder/'scripts/match_hud_r3.gd'
        now=current.read_bytes()
        # Candidate inverse patch is accepted ONLY if the full frozen SHA256 agrees.
        # This recovers exact old bytes, rather than trusting an inferred historical body.
        before=now.replace(b'\t# Clear transient danger feedback when a zone is removed or a round restarts.\n\tclock.modulate=Color.WHITE\n',b'',1)
        assert hashlib.sha256(before).hexdigest()==source['sha256'], 'Historical bytes not recoverable exactly; stop instead of guessing'
        dest=tool.OUT/'baselines/r2'/folder/'scripts/match_hud_r3.gd'
        dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(before)
        patch=''.join(difflib.unified_diff(before.decode('utf-8-sig').splitlines(True),now.decode('utf-8-sig').splitlines(True),fromfile='r2/'+folder+'/scripts/match_hud_r3.gd',tofile='r3/'+folder+'/scripts/match_hud_r3.gd'))
        patchpath=tool.EVIDENCE/(tree+'-hud-r2-r3.diff');patchpath.write_text(patch,encoding='utf-8')
        hud.append({'tree':tree,'before_sha256':source['sha256'],'after_sha256':hashlib.sha256(now).hexdigest(),'before_bytes':str(dest),'after_bytes':str(current),'diff':str(patchpath),'recovery':'inverse patch verified against exact r2 full-file SHA256'})
    hud_diffs=[r for r in diffs if r['after'] and r['after']['source']['path']=='scripts/match_hud_r3.gd']
    additional=[r for r in diffs if r not in hud_diffs]
    assert len(hud_diffs)==10
    tool.write(tool.EVIDENCE/'additional-source-drift.json',additional)
    tool.write(tool.EVIDENCE/'hud-refresh.json',{'changed_hud_rows':len(hud_diffs),'additional_changed_rows':len(additional),'unchanged_rows':sum(a[k]==b.get(k) for k in a),'files':hud,'tree_difference':'Quest life_notice_r13/downed/eliminated logic retained; game retains older feed wording. No baseline merge.'})
    proposals=tool.read(tool.EVIDENCE/'assignment-proposals.json')
    audit=[]
    for row in proposals:
        src=row['source'];p=tool.OUT/'inputs'/Path(src['absolute_path']).relative_to(tool.ROOT)
        data=p.read_bytes();lines=data.decode('utf-8-sig').splitlines()
        assert hashlib.sha256(data).hexdigest()==src['sha256']
        assert row['owner'] and row['acceptance'] and row['status']=='proposed; coordinator confirmation required'
        syms=[b[i] for i in row['symbols']]
        for s in syms:
            assert s['source']==src
            assert lines[s['line']-1]==s['declaration']
            assert hashlib.sha256(('\n'.join(lines[s['line']-1:s['end_line']])+'\n').encode()).hexdigest()==s['body_sha256']
        # Full source is immutable and pinned; declarations/spans are navigable
        # evidence for the retained owner's candidate policy, never owner approval.
        audit.append({'tree':row['tree'],'path':row['path'],'owner':row['owner'],'partners':row['partners'],'acceptance':row['acceptance'],'ambiguity':row['ambiguity'],'source_sha256':src['sha256'],'snapshot':str(p),'preamble':[{'line':i+1,'text':line} for i,line in enumerate(lines[:16])],'symbols':[{'id':s['id'],'line':s['line'],'end_line':s['end_line'],'declaration':s['declaration'],'body_sha256':s['body_sha256']} for s in syms],'disposition':'source-linked proposal confirmed; central assignment and behavioral acceptance OPEN'})
    assert len(audit)==142 and len({r['path'] for r in audit})==76
    ambiguous=[r for r in audit if r['ambiguity']]
    assert len(ambiguous)==8
    assert {(r['tree'],r['path']) for r in ambiguous}=={(t,'scripts/'+n+'.gd') for t in ['quest','game'] for n in ['enemy','guard_routes','main','campaign_extension']}
    tool.write(tool.EVIDENCE/'proposal-source-audit.json',audit)
    tool.write(tool.EVIDENCE/'ambiguous-source-audit.json',ambiguous)
    preserved=[]
    # Copy receipts/metadata, preserving originals and both historical identity versions.
    for revision,root in [('r1',oldroot.parent),('r2',oldroot)]:
        for name in ['candidate-manifest.json','source-symbols.json','assignment-proposals.json','feature-acceptance.json']:
            p=root/name
            if p.is_file():
                dest=tool.OUT/'baselines'/revision/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes())
                preserved.append({'path':str(p),'sha256':tool.sha(p),'snapshot':str(dest)})
    tool.write(tool.EVIDENCE/'baseline-preservation.json',preserved)
    print('PASS exact HUD refresh: 10 rows; additional drift rows='+str(len(additional))+'; unchanged='+str(sum(a[k]==b.get(k) for k in a))+'. 142 proposals / 76 paths / 8 ambiguities source-verified.')

from pathlib import Path
if __name__=='__main__':main()
