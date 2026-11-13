using System;
using System.Reflection;
using System.Collections.Generic;
using Ashfall.Modules.Combat;
class Inventory
{
 static void Main(){foreach(var t in new[]{typeof(CombatRules),typeof(WeaponLease),typeof(HostEvent),typeof(HostHit),typeof(SourceRadialExplosion),typeof(SourceRadialProvenance)}){var fs=new List<string>();foreach(var f in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))fs.Add(f.Name+":"+f.FieldType);fs.Sort(StringComparer.Ordinal);Console.WriteLine(t.Name+"|"+String.Join("\\n",fs.ToArray()));}}
}
