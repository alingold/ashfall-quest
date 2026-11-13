from slot_disk_guard import SlotDiskGuard as G,GIB
assert G.refusal(5*GIB,GIB//2,True,'C:') is None
assert G.refusal(5*GIB-1,9*GIB,True,'C:')
assert G.refusal(9*GIB,GIB//2-1,True,'C:')
assert G.refusal(4*GIB,2*GIB,True,'D:') is None
assert G.refusal(4*GIB-1,9*GIB,True,'D:')
assert G.refusal(9*GIB,2*GIB-1,True,'D:')
for destination in ('C:','D:'):
    assert G.refusal(GIB,GIB//2,False,destination) is None
    assert G.refusal(GIB-1,9*GIB,False,destination)
    assert G.refusal(9*GIB,GIB//2-1,False,destination)
assert G.refusal(99*GIB,99*GIB,True,'E:')
print('PASS destination-aware initial gates, unchanged live abort thresholds; no files removed/moved.')
