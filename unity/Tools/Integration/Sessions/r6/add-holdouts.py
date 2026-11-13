from pathlib import Path
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Tools\Integration\Sessions\r6\Tests.cs');s=p.read_text();pos=s.index(' public static int Main()');s=s[:pos]+''' static void PendingAndCapacity()
 {
  var f=new Fixture();var n=new AuthorityHost("r6",H,0,1,2,1,100,2);var lease=Admit(n,"a","a");RuleResult result;
  Yes(n.Receive(lease,Packet("a",1,"x"),tick));Yes(n.Receive(lease,Packet("a",2,"y"),tick));No(n.Receive(lease,Packet("a",3,"x"),tick));
  Check(n.DispatchOne(f,out result),"first mutation");Yes(result);
  // A pending new command cannot evict the most recently completed receipt.
  Yes(n.Receive(lease,Packet("a",3,"z"),tick));No(n.Receive(lease,Packet("a",4,"x"),tick));
  Check(n.DispatchOne(f,out result),"second mutation");Yes(result);Check(n.DispatchOne(f,out result),"third mutation");Yes(result);
  Yes(n.Receive(lease,Packet("a",4,"x"),tick));Check(n.DispatchOne(f,out result),"old bounded receipt reusable");Yes(result);
  Yes(n.Release(lease));lease=Admit(n,"b","b");Yes(n.Release(lease));
  PeerReservation denied;No(n.PrepareAdmission("p","c",Hello("c"),tick,Int64.MaxValue,out denied));Check(n.IdentityCount==2,"history saturation never evicts");
  lease=Admit(n,"a","a");Yes(n.Receive(lease,Packet("a",1,"x"),tick));Check(n.DispatchOne(f,out result),"seq1 at history bound");Yes(result);
  Yes(n.Release(lease));PeerReservation expired;Yes(n.PrepareAdmission("p","a",Hello("a"),tick,tick+1,out expired));No(n.Validate(expired,tick+1));Check(n.ReservedCount==0,"expiry frees only reservation");
 }
'''+s[pos:];s=s.replace('Prepared();Leases();','Prepared();Leases();PendingAndCapacity();');p.write_text(s)
