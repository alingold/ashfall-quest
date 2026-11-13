from pathlib import Path
p=Path('unity/Tools/Integration/CombatProjectileArchive/ArchiveTests.cs');s=p.read_text();needle='   var caps=archive.Launches;'
insert='''   int record=-1;byte[] signature=System.Text.Encoding.UTF8.GetBytes("launch");
   for(int i=0;i<b.Length-signature.Length;i++){bool same=b[i]==6;for(int j=0;j<signature.Length&&same;j++)same=b[i+1+j]==signature[j];if(same)record=i;}
   Need(record>0,"actual capability record located");
   using(var reader=new BinaryReader(new MemoryStream(b,false))){reader.BaseStream.Position=record;reader.ReadString();reader.ReadString();int life=(int)reader.BaseStream.Position;reader.ReadInt64();int connection=(int)reader.BaseStream.Position;reader.ReadInt64();int frame=(int)reader.BaseStream.Position;reader.ReadInt64();int observed=(int)reader.BaseStream.Position;reader.ReadInt64();int boolean=(int)reader.BaseStream.Position;reader.ReadBoolean();reader.ReadString();reader.ReadString();int digest=(int)reader.BaseStream.Position+1;reader.ReadString();int count=(int)reader.BaseStream.Position;reader.ReadInt32();int slots=(int)reader.BaseStream.Position;
    foreach(int position in new[]{life,connection,frame}){var bad=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(-1L),0,bad,position,8);Reject(()=>Restore(bad),"negative capability generation");}
    var futureObserved=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(2L),0,futureObserved,observed,8);Reject(()=>Restore(futureObserved),"future validation observation");
    var badBool=(byte[])b.Clone();badBool[boolean]=2;Reject(()=>Restore(badBool),"noncanonical provenance bool");
    var upper=(byte[])b.Clone();upper[digest]=(byte)'A';Reject(()=>Restore(upper),"noncanonical content digest case");
    var shortCount=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(1),0,shortCount,count,4);Reject(()=>Restore(shortCount),"invalid reserved capacity");
    var duplicate=(byte[])b.Clone();Array.Copy(b,slots,duplicate,slots+8,8);Reject(()=>Restore(duplicate),"duplicate reserved slot alias");
    var foreign=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(1000L),0,foreign,slots,8);Reject(()=>Restore(foreign),"foreign missing event capability");
    var mixed=(byte[])b.Clone();mixed[record+1]=(byte)'x';Reject(()=>Restore(mixed),"capability marker mixed identity");
   }
'''
s=s.replace(needle,insert+needle);p.write_text(s)
