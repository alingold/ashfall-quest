using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public struct R23M1014Contact
    {
        public readonly int Pellet;public readonly string TargetId;public readonly Float3 Point;public readonly bool Head;
        public R23M1014Contact(int pellet,string target,Float3 point,bool head=false){Pellet=pellet;TargetId=target;Point=point;Head=head;}
    }
}
