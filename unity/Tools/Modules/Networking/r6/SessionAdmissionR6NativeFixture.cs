#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Networking;
// Coordinator-only callable fixture. Copy into isolated actual Editor probe project,
// resolve centrally selected Transport6.6.0, execute under engine_lane.py. Never run in a leaf.
public static class SessionAdmissionR6NativeFixture
{
    public static void Run()
    {
        var transport=new UnityTransportAdapter(1,8);
        try
        {
            transport.Listen(0); // ephemeral native UDP listener, no fixed service port
            transport.Pump();
            string peer; byte[] bytes;
            if(transport.TryReceive(out peer,out bytes))throw new InvalidOperationException("unexpected-native-ingress");
        }
        finally { transport.Dispose(); }
        transport.Dispose();
        bool rejected=false;
        try { transport.Pump(); } catch(ObjectDisposedException) { rejected=true; }
        if(!rejected)throw new InvalidOperationException("disposed-native-pump");
        UnityEngine.Debug.Log("PASS SessionAdmissionR6 native adapter API/resource smoke only");
    }
}
#endif
