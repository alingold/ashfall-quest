import copy,json,itertools,sys,unittest
from pathlib import Path
import build_graph as b
from boundaries import path_errors
from ports import Assembly,Graph
ROOT=b.STAGE/(sys.argv.pop() if len(sys.argv)==2 else 'tests-r7'); ROOT.mkdir()
class R7(unittest.TestCase):
    def test_authorized_identity_and_indirect_closure(self):
        self.assertTrue(b.validate_authorized_plan({}, ROOT, ROOT, None))
        nodes={n:Assembly(n,n,'neutral',references=refs) for n,refs in [('A',[]),('B',['A']),('C',['B'])]}
        self.assertEqual(['A','B'],b.dependency_closure(nodes,'C'))
        nodes['A'].references=['C']
        self.assertEqual(['A','B'],b.dependency_closure(nodes,'C'))
    def test_reserved_components_case_extensions(self):
        names=['CON','PRN','AUX','NUL','CONIN$','CONOUT$']+[p+d for p in ('COM','LPT') for d in '123456789\u00b9\u00b2\u00b3']
        for n,ext,slot in itertools.product(names,['','.cs','.x.y'],range(3)):
            for name in (n,n.lower(),n.swapcase()):
                parts=['safe','nested','File.cs']; parts[slot]=name+ext
                self.assertTrue(path_errors(['/'.join(parts)]),parts)
        self.assertEqual([],path_errors(['A/COM0.cs','A/COM10.cs','A/Console.cs','A/LPT0.cs']))
    def fixture(self):
        r=ROOT/self._testMethodName; r.mkdir()
        p=r/'unity/Assets/Ashfall/Core/C.cs'; p.parent.mkdir(parents=True); p.write_text('namespace Ashfall.Core { public class C {} }')
        meta=r/'unity/Documentation/CONTRACTS-v3.md';meta.parent.mkdir(parents=True);meta.write_text('contract')
        g,_=b.generate(r);g.inputs=[{'path':x.relative_to(r).as_posix(),'sha256':b.digest(x.read_bytes())} for x in (p,meta)]
        out=r/'staged';b.stage(g,out);plan=b.proposal_plan(g,r,out)
        return r,meta,out,plan
    def test_metadata_deletion_and_seal_tamper(self):
        r,meta,out,plan=self.fixture(); seal=b.digest((out/'input-inventory.json').read_bytes())
        self.assertEqual([],b.validate_plan(plan,r,out,seal))
        self.assertEqual([],b.validate_authorized_plan(plan,r,out,seal))
        meta.write_text('changed')
        changed=copy.deepcopy(plan);changed['preconditions'].pop()
        self.assertIn('sealed-precondition-inventory-mismatch',b.validate_plan(changed,r,out,seal))
        b.dump(out/'input-inventory.json',changed['preconditions'])
        self.assertIn('trusted-inventory-identity-mismatch',b.validate_plan(changed,r,out,seal))
        (out/'input-inventory.json').unlink()
        self.assertTrue(b.validate_plan(changed,r,out,seal))
    def test_precondition_each_deleted(self):
        r,meta,out,plan=self.fixture()
        for i in range(len(plan['preconditions'])):
            changed=copy.deepcopy(plan);del changed['preconditions'][i]
            self.assertTrue(b.validate_plan(changed,r,out))
    def test_malformed_assembly_fields(self):
        root=ROOT/self._testMethodName;root.mkdir()
        p=root/'A.asmdef';m=root/'A.asmdef.meta';m.write_text('guid: '+'a'*32)
        for payload in ('{','[]','{"name":1}','{"name":"A","references":"B"}','{"name":"A","references":[null]}','{"name":"A","references":["B","B"]}'):
            p.write_text(payload); self.assertTrue(b.validate_asmdefs(root))
        p.write_text('{"name":"A"}')
        ref=root/'B'/'B.asmref';ref.parent.mkdir()
        for payload in ('{','[]','{"reference":42}','{"reference":"GUID:bad"}'):
            ref.write_text(payload);self.assertTrue(b.validate_asmdefs(root))
    def test_platform_reference_domain(self):
        for include,exclude in ((['Editor'],['Android']),(['Unknown'],[]),('Android',[]),([],['Android','Android'])):
            a=Assembly('A','unity/Assets/Ashfall/A','neutral',guid='a'*32,definition={'includePlatforms':include,'excludePlatforms':exclude})
            self.assertTrue(b.validate(Graph([a])))
unittest.main(verbosity=2)
