from pathlib import Path
T=Path(__file__).resolve().parent
p=T/'Staged/R23HandsWeapons.Blades.cs';s=p.read_text().replace('bool shieldWasActive;','').replace('shieldWasActive=shield;','');p.write_text(s)
p=T/'Candidate/R23HandsWeapons.cs';s=p.read_text().replace('void UpdateWeaponPresentation() {','void UpdateWeaponPresentation() {\n            SyncBladePresentation();');p.write_text(s)
p=T/'Candidate/R23HandsWeapons.Knife.cs';s=p.read_text().replace('knifeReady&&IsMelee','knifeReady&&(ActiveWeaponId=="knife"||bladeReady)&&IsMelee');p.write_text(s)
p=T/'Staged/R24BladeArt.cs';s=p.read_text();at=s.index('        public bool IsTransient')
s=s[:at]+'''        // Godot TorusMesh(inner .028,outer .032,rings16,ring_segments5), reflected once.
        public static Mesh SourceCue(){const int rings=16,sides=5;var vertices=new Vector3[(rings+1)*(sides+1)];var normals=new Vector3[vertices.Length];var indices=new int[rings*sides*6];int cursor=0;for(int i=0;i<=rings;i++)for(int j=0;j<=sides;j++){float u=i*Mathf.PI*2/rings,v=j*Mathf.PI*2/sides;int k=i*(sides+1)+j;normals[k]=new Vector3(Mathf.Cos(u)*Mathf.Cos(v),Mathf.Sin(v),-Mathf.Sin(u)*Mathf.Cos(v));vertices[k]=new Vector3(.030f*Mathf.Cos(u),0,-.030f*Mathf.Sin(u))+normals[k]*.002f;if(i<rings&&j<sides){indices[cursor++]=k;indices[cursor++]=k+1;indices[cursor++]=k+sides+1;indices[cursor++]=k+1;indices[cursor++]=k+sides+2;indices[cursor++]=k+sides+1;}}var mesh=new Mesh();mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateBounds();return mesh;}
'''+s[at:];p.write_text(s)
