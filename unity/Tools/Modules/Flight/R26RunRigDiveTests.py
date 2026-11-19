#!/usr/bin/env python3
"""CPU slice: copies production EvaluateMotion/AcceptMotion/Context/F/U and the rig travel-to-flight call.
Unity value/identity-transform stubs replace engine geometry, never input or flight logic.
No production source or maintained Tools files are rewritten.
"""
from pathlib import Path
import argparse,subprocess,json,hashlib
p=argparse.ArgumentParser();p.add_argument('output',type=Path);p.add_argument('--overlay',type=Path);p.add_argument('--baseline',action='store_true');p.add_argument('--detection-only',action='store_true');a=p.parse_args()
repo=Path('/home/al/ashfall/unity');a.output.mkdir(parents=True,exist_ok=False)
def src(r):
    q=a.overlay/r if a.overlay else repo/r
    return q if q.exists() else repo/r
def method(s,needle):
    start=s.index(needle);brace=s.index('{',start);depth=1;end=brace+1
    while depth:
        if s[end]=='{':depth+=1
        elif s[end]=='}':depth-=1
        end+=1
    return s[start:end]
s=src('Assets/Ashfall/Integration/Playable/Activities/R23VRActivities.cs').read_text()
rig=src('Assets/Ashfall/Integration/Playable/R23VRRig.cs').read_text()
methods='\n'.join(method(s,n) for n in ['CommandContext Context(', 'public R23ActivityMotion EvaluateMotion(', 'public void AcceptMotion(', 'public RuleResult ValidateCollision(', 'static Float3 F(', 'static Vector3 U('])
# Use the rig's travel and activity call for the CPU slice. Fixed fixture geometry
# supplies no extra locomotion owner or slope takeoff.
travel_start=rig.index('                var travel=R23TravelHeading.Resolve(')
travel_lines=rig[travel_start:rig.index('                gliding=flight.Mode',travel_start)].strip()
travel_lines=travel_lines.replace(';GlideEntryFacts(ref frame,U(travel.Direction));',';')
travel_lines=travel_lines.replace('Activities.EvaluateMotion(', 'EvaluateMotion(').replace('climb.OwnsMotion||traversal||vehicle||pull,climb.Mantling','false,false')
methods+='''
public R23ActivityMotion EvaluateRigMotion(InputFrame frame, float seconds, bool grounded,
    Float3 previousVelocity, MotionStep step, float heading, PlayerInput input)
{
    bool ceiling=false;
'''+travel_lines+'\n    return flight;\n}\n'
pre='''using System;using Ashfall.Core;using Ashfall.Modules.Flight;using Ashfall.Modules.Player;using UnityEngine;
namespace UnityEngine {
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator *(Vector3 v,float s){return new Vector3(v.x*s,v.y*s,v.z*s);}public static Vector3 operator /(Vector3 v,float s){return new Vector3(v.x/s,v.y/s,v.z/s);}}
public sealed class Transform {public Vector3 position,forward=new Vector3(0,0,1);public Vector3 InverseTransformPoint(Vector3 p){return p;}}
public static class Mathf {public static float Clamp(float v,float lo,float hi){return Math.Max(lo,Math.Min(hi,v));}}
}
namespace Ashfall.Integration.Playable.Activities {
public struct R23ActivityMotion {public bool OwnsMotion;public Vector3 Velocity,Displacement;public FlightMode Mode;}
public sealed class R23VRActivities:IFlightCollisionAuthorization {
public FlightMachine flight=new FlightMachine("r23-playable","player",0,new Gate(),6.5f,1);
public Locomotion Motor=new Locomotion(new PlayerTuning{WalkMultiplier=2.5f});
public Transform root=new Transform(),left=new Transform(),right=new Transform(),head=new Transform();public Vector3 Accepted;public float ViewYaw;long serial,tick;public string MotionStatus;
sealed class Gate:IAuthorityGate {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
'''
gen=a.output/'R26ActivitySlice.cs';gen.write_text(pre+methods+'\n}}\n')
# Exercise the production Quest feature reader as well as the projection, rearm,
# locomotion, activity, flight and accepted-collision path. Only device I/O is stubbed.
native='''using Ashfall.Core;using Ashfall.Modules.Player;using UnityEngine;
namespace UnityEngine {public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}}
public static class CommonUsages {public const string isTracked="tracked",primaryButton="primary",secondaryButton="secondary",primary2DAxisClick="click",menuButton="menu",grip="grip",trigger="trigger",primary2DAxis="axis";}
public struct InputDevice {
public bool isValid;public HandSample Sample;
public bool TryGetFeatureValue(string key,out bool v){v=key==CommonUsages.isTracked?Sample.Tracked:key==CommonUsages.primaryButton?Sample.Primary:key==CommonUsages.secondaryButton?Sample.Secondary:key==CommonUsages.primary2DAxisClick?Sample.Click:Sample.Menu;return isValid;}
public bool TryGetFeatureValue(string key,out float v){v=key==CommonUsages.grip?Sample.Grip:Sample.Trigger;return isValid;}
public bool TryGetFeatureValue(string key,out Vector2 v){v=new Vector2(Sample.StickX,Sample.StickY);return isValid;}
}
public sealed class R26QuestHandReader {Transform transform=new Transform();
public HandSample Read(HandSample sample){return Hand(new InputDevice{isValid=true,Sample=sample},new Transform{position=new Vector3(sample.Position.X,sample.Position.Y,sample.Position.Z)});}
'''
native+=method(rig,'HandSample Hand(')+'\n'+method(s,'static Float3 F(')+'\n}\n'
hand=a.output/'R26QuestHandSlice.cs';hand.write_text(native)
files=[repo/'Assets/Ashfall/Core/Contracts.cs']+sorted((repo/'Assets/Ashfall/Modules/Flight').glob('*.cs'))+sorted((repo/'Assets/Ashfall/Modules/Player').glob('*.cs'))+[repo/'Assets/Ashfall/Integration/Playable/R23InputProjection.cs']
# TravelHeading is another verbatim, pure production class; construction UI is outside this test's scope.
travel=src('Assets/Ashfall/Integration/Playable/Activities/R23ActivityState.cs');t=travel.read_text();heading=a.output/'R26TravelSlice.cs';heading.write_text('using System;using Ashfall.Core;namespace Ashfall.Integration.Playable.Activities {\n'+method(t,'public struct R23TravelVectors')+'\n'+method(t,'public static class R23TravelHeading')+'\n}')
files=[src(str(f.relative_to(repo))) for f in files]+[gen,heading,hand,src('Tools/Modules/Flight/R26RigDiveTests.cs')]
mono=Path('/home/al/Unity/Hub/Editor/6000.6.8f1/Editor/Data/MonoBleedingEdge');exe=a.output/'R26RigDiveTests.exe'
c=subprocess.run([str(mono/'bin/mono'),str(mono/'lib/mono/4.5/csc.exe'),'-nologo','-langversion:5','-warnaserror+','-out:'+str(exe)]+list(map(str,files)),capture_output=True,text=True);(a.output/'compile.log').write_text(c.stdout+c.stderr)
if c.returncode:print(c.stdout+c.stderr);raise SystemExit(c.returncode)
r=subprocess.run([str(mono/'bin/mono'),str(exe)]+(['--baseline'] if a.baseline else [])+(['--detection-only'] if a.detection_only else []),capture_output=True,text=True);(a.output/'run.log').write_text(r.stdout+r.stderr)
(a.output/'manifest.json').write_text(json.dumps({str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in files+[src('Assets/Ashfall/Integration/Playable/Activities/R23VRActivities.cs'),src('Assets/Ashfall/Integration/Playable/R23VRRig.cs'),travel]},indent=2)+'\n')
print('\n'.join(l for l in (r.stdout+r.stderr).splitlines() if 'snapshot axis preserved' not in l));raise SystemExit(r.returncode)
