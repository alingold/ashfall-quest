using System;using System.Collections.Generic;using System.IO;using System.Text;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable.Networking
{
    // Fixed rule order/types come from the existing validated source-rule owner.
    // No field names, unbounded dictionaries or arbitrary object deserialization.
    public static class R24LanRoundCodec
    {
        static readonly string[] Keys=R24PlayableMatchRules.SourceKeys;static readonly R24PlayableMatchRules Types=R24PlayableMatchRules.ForPreset("team_revive","range");
        public static void Write(BinaryWriter writer,R24PlayableMatchRules rules)
        {
            writer.Write((byte)1);writer.Write((byte)(Keys.Length-1));foreach(string key in Keys)if(key!="map"){
                object value=rules.ReadSourceValue(key);if(value is bool)writer.Write((bool)value);else if(value is int)writer.Write((int)value);else if(value is float)writer.Write((float)value);else if(value is string){byte[] bytes=Encoding.UTF8.GetBytes((string)value);if(bytes.Length<1||bytes.Length>32)throw new InvalidDataException("Rule string size");writer.Write((byte)bytes.Length);writer.Write(bytes);}else throw new InvalidDataException("Rule type");
            }
        }
        public static R24PlayableMatchRules Read(BinaryReader reader,string map)
        {
            if(reader.ReadByte()!=1||reader.ReadByte()!=Keys.Length-1)throw new InvalidDataException("Rule schema");var values=new Dictionary<string,object>(StringComparer.Ordinal);values.Add("map",map);
            foreach(string key in Keys)if(key!="map"){
                object type=Types.ReadSourceValue(key),value;
                if(type is bool){byte b=reader.ReadByte();if(b>1)throw new InvalidDataException("Rule boolean");value=b==1;}else if(type is int)value=reader.ReadInt32();else if(type is float)value=reader.ReadSingle();else{int n=reader.ReadByte();if(n<1||n>32||n>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Rule text");value=new UTF8Encoding(false,true).GetString(reader.ReadBytes(n));}values.Add(key,value);
            }
            var result=R24PlayableMatchRules.FromSourceValues(values,delegate(string id){return id==map;});foreach(var value in values)if(!Object.Equals(value.Value,result.ReadSourceValue(value.Key)))throw new InvalidDataException("Noncanonical rule value");return result;
        }
    }
    // Explicit seed shared by the host, not a reset of Unity's global RNG.
    public sealed class R24LanRoundRandom
    {
        uint state;public R24LanRoundRandom(int seed){if(seed<=0)throw new ArgumentOutOfRangeException("seed");state=(uint)seed;}
        public double Next(){state=unchecked(state*1664525u+1013904223u);return state/4294967296.0;}
    }
    public sealed class R24LanHostClock
    {
        double offset,bestRoundTrip=Double.PositiveInfinity,lastSample;public bool Synchronized{get;private set;}public double RoundTrip{get{return bestRoundTrip;}}
        static bool Finite(double n){return !Double.IsNaN(n)&&!Double.IsInfinity(n);}
        public void Observe(double hostTime,double localTime){if(!Synchronized&&Finite(hostTime)&&Finite(localTime)&&hostTime>=0&&localTime>=0)offset=hostTime-localTime;}
        public bool Reply(double sent,double host,double received)
        {if(!Finite(sent)||!Finite(host)||!Finite(received)||sent<0||host<0||received<sent||received-sent>5)return false;if(received-lastSample>10)bestRoundTrip=Double.PositiveInfinity;double trip=received-sent;if(trip>bestRoundTrip+.01)return false;bestRoundTrip=Math.Min(bestRoundTrip,trip);offset=host-(sent+received)*.5;lastSample=received;Synchronized=true;return true;}
        public double Now(double local){if(!Finite(local)||local<0)throw new ArgumentOutOfRangeException("local");return Math.Max(0,local+offset);}
    }
}
