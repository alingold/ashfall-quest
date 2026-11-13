using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using Ashfall.Integration.WorldWeaponTransfers;
public static class HistoryCutTests
{
 static int checks;static void A(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static List<byte[]> Frames(byte[] bytes){var list=new List<byte[]>();using(var r=new BinaryReader(new MemoryStream(bytes))){while(r.BaseStream.Position<r.BaseStream.Length){int n=r.ReadInt32();list.Add(r.ReadBytes(n));r.ReadBytes(32);}}return list;}
 static byte[] Pack(List<byte[]> frames){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){foreach(var f in frames){w.Write(f.Length);w.Write(f);using(var s=SHA256.Create())w.Write(s.ComputeHash(f));}w.Flush();return m.ToArray();}}
 static void Deny(byte[] b,string reason){bool denied=false;try{WorldTransferHistory.Decode(b);}catch(InvalidDataException){denied=true;}catch(EndOfStreamException){denied=true;}A(denied,reason);}
 public static void Main(string[] args){byte[] bytes=File.ReadAllBytes(args[1]);var frames=Frames(bytes);
  if(args[0]=="prefix"){
   var h=WorldTransferHistory.Decode(bytes);A(h.Sequence==Int64.Parse(args[2]),"exact cut publication sequence");A(h.Pending.Length==Int32.Parse(args[3]),"exact cut ACK set");A(h.UnfinishedPrepare==Boolean.Parse(args[4])&&h.UnresolvedDecision==Boolean.Parse(args[5]),"cut reconciliation disposition");Console.WriteLine("PASS "+checks+" cold flushed cut assertions");return;
  }
  if(args[0]=="cuts"){
   long sequence=0;int pending=0;bool prepared=false,decision=false;var list=new List<byte[]>();
   foreach(var f in frames){list.Add(f);if(f[0]==1){prepared=true;decision=false;}if(f[0]==2)decision=true;if(f[0]==3){sequence++;pending++;prepared=false;decision=false;}if(f[0]==4){prepared=false;decision=false;}if(f[0]==5)pending--;
    var h=WorldTransferHistory.Decode(Pack(list));A(h.Sequence==sequence&&h.Pending.Length==pending,"flushed prefix state");A(h.UnfinishedPrepare==prepared&&h.UnresolvedDecision==decision,"uncertain cuts never inferred committed");
   }
   A(sequence==3&&pending==2,"successful receipts survive sparse ACK");
  }else{
   var shortBytes=new byte[bytes.Length-1];Array.Copy(bytes,shortBytes,shortBytes.Length);Deny(shortBytes,"torn flush");
   var corrupt=(byte[])bytes.Clone();corrupt[10]^=1;Deny(corrupt,"physical hash corruption");
   var duplicate=new List<byte[]>(frames);byte[] ack=null;foreach(var f in frames)if(f[0]==5)ack=f;duplicate.Add(ack);Deny(Pack(duplicate),"duplicate ACK");
   var orphan=new List<byte[]>();orphan.Add(ack);Deny(Pack(orphan),"orphan ACK");
   var first=new List<byte[]>();foreach(var f in frames){if(f[0]==1){first.Add(f);break;}}
   byte[] marker=new byte[9];marker[0]=3;marker[1]=1;first.Add(marker);Deny(Pack(first),"published without decision");
   marker=(byte[])marker.Clone();marker[0]=99;Deny(Pack(new List<byte[]>{marker}),"unknown stage");
   marker=(byte[])ack.Clone();marker[1]=99;Deny(Pack(new List<byte[]>{marker}),"future ACK");
   var appended=new byte[frames[0].Length+1];Array.Copy(frames[0],appended,frames[0].Length);Deny(Pack(new List<byte[]>{appended}),"frame trailing data");
  }
  Console.WriteLine("PASS "+checks+" journal history "+args[0]+" assertions");
 }
}
