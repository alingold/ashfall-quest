"""Publish already decoded/qualified new maps only after the root releases Assets."""
import argparse,subprocess,sys
import prepare as p
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--release-assets',action='store_true');args=parser.parse_args()
    assert args.release_assets,'Explicit capture-hold release must precede publication'
    assert not(p.HERE/'STOP').exists(),'Root capture Assets hold is active'
    p.bindings()
    for f in sorted((p.HERE/'results').glob('*.json')):
        if(p.HERE/'STOP').exists():print('Stopped before next publication.',flush=True);break
        row=p.load(f)
        if row.get('published')or row.get('refused'):continue
        slug=row['slug'];assert slug not in p.EXCLUDED
        out=p.STAGE/slug;r=p.load(out/'provenance.json')
        assert r['sourceContentId']==p.CONTENT_ID and p.sha(out/'geometry.bytes')==r['geometrySha256']
        subprocess.run([sys.executable,str(p.HERE/'check.py'),'--slug',slug,'--staged'],check=True)
        dest=p.publish(out,slug);row.update(published=True,destination=str(dest));p.write(f,row)
        rows=[p.load(x)for x in sorted((p.HERE/'results').glob('*.json'))]
        p.write(p.HERE/'catalog-delta.json',dict(sourceContentId=p.CONTENT_ID,maps=[r for r in rows if r.get('published')],refused=[r for r in rows if r.get('refused')]))
        print('Published '+slug,flush=True)
if __name__=='__main__':main()
