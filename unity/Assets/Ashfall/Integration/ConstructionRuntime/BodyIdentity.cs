using System;
using System.Globalization;
using Ashfall.Modules.World;
namespace Ashfall.Integration.ConstructionRuntime
{
    // Length-prefixed ordinal tuples, disjoint typed domains. No hashing, culture
    // folding, delimiters, engine instance numbers or semantic-owner substitution.
    public static class BodyIdentity
    {
        public static string Part(string value)
        {if(String.IsNullOrWhiteSpace(value))throw new ArgumentException("unsupported_body_identity");return value.Length.ToString(CultureInfo.InvariantCulture)+":"+value;}
        public static string Map(ContentIdentity map,string sourceBody)
        {if(map==null)throw new ArgumentException("map_identity");return "map/"+Part(map.MapId)+Part(map.Version)+Part(map.PayloadSha256)+Part(sourceBody);}
        public static string ConstructedScope(string session,long epoch,string host)
        {if(epoch<0)throw new ArgumentException("host_epoch");return Part(session)+Part(epoch.ToString(CultureInfo.InvariantCulture))+Part(host);}
        public static string Constructed(string scopedSerial){return "constructed/"+Part(scopedSerial);}
    }
}
