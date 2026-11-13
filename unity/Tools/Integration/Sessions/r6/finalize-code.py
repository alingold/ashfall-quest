from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall');p=R/'unity/Assets/Ashfall/Integration/Sessions/SessionNetworkCoordinator.cs';s=p.read_text().replace('private bool admitting;', 'private bool admitting;\n        private readonly int hostThread=System.Threading.Thread.CurrentThread.ManagedThreadId;');s=s.replace('if(admitting || applying || dispatching','if(System.Threading.Thread.CurrentThread.ManagedThreadId!=hostThread || admitting || applying || dispatching');p.write_text(s)
p=R/'unity/Assets/Ashfall/Integration/Sessions/Ports.cs';s=p.read_text().replace('// Trusted, synchronous, nonthrowing coordinator ports on one simulation lane.','// Trusted, pure/stable, synchronous, nonthrowing coordinator ports on one host thread.\n    // No callback may reenter the coordinator or mutate admission/domain state.');p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/Sessions/Ports.cs';s=p.read_text().replace('// Trusted coordinator adapters; clients must never supply implementations.','// Trusted coordinator adapters; clients must never supply implementations.\n    // Prepared admission requires one serialized host lane and pure/stable, nonthrowing ports.\n    // Clock is frozen across prepare/validate/publication. Issue may reserve a unique opaque\n    // token externally, but must not mutate Session state or reenter; abort need not reuse it.');p.write_text(s)
p=R/'unity/Tools/Integration/Sessions/r6/run.py';s=p.read_text();pos=s.index("(L/'inputs.json')")
s=s[:pos]+'''for name,t in [('NetworkingR2','NetworkingR2Tests.cs'),('NetworkingR3','NetworkingR3Tests.cs'),('NetworkingAdversarial','AdversarialR2.cs'),('OldPeerPolicy','r5/PeerTests.cs')]:
 build(name,src,copy(R/'unity/Tools/Modules/Networking'/t,'test-'+name))
# Define-OFF includes the real guarded engine source; no Unity references or stubs.
engine=[copy(p,'guarded-engine') for p in R.glob('unity/Assets/Ashfall/Modules/Networking/Engine/*.cs')]
results['DefineOFF']=run('DefineOFF',[r'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(O/'DefineOFF.dll')]+src+engine)
'''+s[pos:];p.write_text(s)
