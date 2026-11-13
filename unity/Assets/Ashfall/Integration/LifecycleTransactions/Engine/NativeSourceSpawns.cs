#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.World;
using Ashfall.Integration.WorldRuntime;
using UnityEngine;
namespace Ashfall.Integration.LifecycleTransactions
{
    public static class NativeSourceSpawns
    {
        // Compiler-only staging. NativeWorld supplies the actual floor raycast,
        // all-layer standing overlap and hazard contacts. No success callbacks.
        // Its public hazard API is fixed at 1.8m, so taller bodies are unsupported.
        public static SourceSpawnWorld Bind(ApprovedWorld source,IWorldApproval approval,IWorldBuildPolicy bounds,NativeWorld contacts,float height,SourceSpawnPolicy[] policies)
        {
            if(source==null||contacts==null||contacts.Quarantined||height!=1.8f||contacts.Identity.MapId!=source.Identity.MapId||contacts.Identity.PayloadSha256!=source.Identity.PayloadSha256||contacts.Identity.Version!=source.Identity.Version||contacts.Identity.SourceRevision!=source.Identity.SourceRevision||contacts.Identity.SourceSha256!=source.Identity.SourceSha256)throw new ArgumentException("exact approved native source and 1.8m body required");
            return new SourceSpawnWorld(source,approval,bounds,contacts,height,policies,LayerMask.LayerToName);
        }
    }
}
#endif
