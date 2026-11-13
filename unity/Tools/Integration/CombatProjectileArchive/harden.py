from pathlib import Path
p=Path('unity/Assets/Ashfall/Modules/Combat/ProjectileArchiveRecords.cs');s=p.read_text().replace('using(var m=new MemoryStream())using(var w=new BinaryWriter(m,new UTF8Encoding(false,true)))','using(var m=new ProjectileArchiveStream())using(var w=new ProjectileArchiveWriter(m))');at='        static readonly ConditionalWeakTable<CombatRules,List<LaunchedProjectileCapability>>'
insert='''        sealed class ProjectileArchiveStream:MemoryStream
        {
            public override void Write(byte[] buffer,int offset,int count){RecoveryWire.Require(count>=0&&Position<=RecoveryWire.Limit-count);base.Write(buffer,offset,count);}
            public override void WriteByte(byte value){RecoveryWire.Require(Position<RecoveryWire.Limit);base.WriteByte(value);}
            public override void SetLength(long value){RecoveryWire.Require(value>=0&&value<=RecoveryWire.Limit);base.SetLength(value);}
        }
        sealed class ProjectileArchiveWriter:BinaryWriter
        {
            internal ProjectileArchiveWriter(Stream stream):base(stream,new UTF8Encoding(false,true)){}
            public override void Write(string value){RecoveryWire.Require(!String.IsNullOrEmpty(value)&&value.Length<=512);base.Write(value);}
        }
'''
s=s.replace(at,insert+at);p.write_text(s)
p=Path('unity/Tools/Integration/CombatProjectileArchive/ArchiveTests.cs');s=p.read_text().replace('var caps=archive.Launches;', '''var alternate=new byte[b.Length+1];Array.Copy(b,0,alternate,0,8);alternate[8]=(byte)(b[8]|128);alternate[9]=0;Array.Copy(b,9,alternate,10,b.Length-9);Reject(()=>Restore(alternate),"overlong noncanonical string prefix");
   var caps=archive.Launches;''');p.write_text(s)
