from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall')
p=R/'unity/Assets/Ashfall/Modules/Networking/PeerLeases.cs';s=p.read_text();s=s.replace('internal readonly AuthorityHost Owner;','internal readonly AuthorityHost Owner;\n        internal long IngressSequence;\n        internal readonly System.Collections.Generic.HashSet<string> IngressSeen=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);\n        internal readonly System.Collections.Generic.Queue<string> IngressOrder=new System.Collections.Generic.Queue<string>();',1);p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/Networking/Authority.cs';s=p.read_text();s=s.replace('public long Sequence, Revision;', 'public long Sequence, Revision, Tick;');s=s.replace('public Packet Packet; public Actor Owner;', 'public Packet Packet; public Actor Owner; public string Receipt; public long Tick;')
a=s.index('            Actor a=actors[leases[transportPeer].ReplayIdentity];');b=s.index('            return RuleResult.Accept();',a)
s=s[:a]+'''            PeerLease lease=leases[transportPeer];
            Actor a=actors[lease.ReplayIdentity];
            if(a.Revision==long.MaxValue || a.Sequence==long.MaxValue) return RuleResult.Reject("history-revision-exhausted");
            if(p.Sequence<=lease.IngressSequence || lease.IngressSeen.Contains(p.CommandId)) return RuleResult.Reject("replay");
            // Trusted, injective normalization occurs only after exact lease admission.
            // Canonical history survives departure; raw ingress numbering belongs to the lease.
            string receipt="network-generation/"+lease.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+p.CommandId.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+p.CommandId;
            long tick=Math.Max(a.Tick,p.Tick);
            if(!commands.TryAdd(new Work { Peer=transportPeer,Generation=lease.Generation,Packet=p,Owner=a,Receipt=receipt,Tick=tick })) return RuleResult.Reject("backpressure");
            lease.IngressSequence=p.Sequence; lease.IngressSeen.Add(p.CommandId); lease.IngressOrder.Enqueue(p.CommandId);
            while(lease.IngressOrder.Count>replayWindow) lease.IngressSeen.Remove(lease.IngressOrder.Dequeue());
            a.Sequence++; a.Tick=tick; a.Pending.Add(receipt); a.Revision++;
'''+s[b:]
# pending raw IDs must remain protected even if completed-window rotates; normalized pending checks
s=s.replace('if(p.Sequence<=lease.IngressSequence || lease.IngressSeen.Contains(p.CommandId))','if(p.Sequence<=lease.IngressSequence || lease.IngressSeen.Contains(p.CommandId))')
s=s.replace('p.CommandId,p.Tick,Epoch,true)','w.Receipt,w.Tick,Epoch,true)');s=s.replace('a.Pending.Remove(p.CommandId); a.Seen.Add(p.CommandId); a.Order.Enqueue(p.CommandId);','a.Pending.Remove(w.Receipt); a.Seen.Add(w.Receipt); a.Order.Enqueue(w.Receipt);');p.write_text(s)
