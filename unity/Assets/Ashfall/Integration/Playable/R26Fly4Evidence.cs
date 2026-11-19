#if UNITY_6000_0_OR_NEWER
using System;
using System.Globalization;
using System.IO;
using Ashfall.Modules.Flight;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    // FLY4 evidence only: the route bot runs off a structure with its arms spread, as a seated
    // headset player would. Inert unless -ashfallFly4Arms <metres between the hands> is given
    // together with -ashfallNetBot (XR disabled). One line per bot frame goes to -ashfallFly4Log.
    //   -ashfallFly4HandDrop <m>     hands this far below the tracked head (default .25, shoulder height)
    //   -ashfallFly4Rules <list>     desktop step-down and inferred-pose evidence switches. Use the r15 binary to compare stick cancellation.
    //   -ashfallFly4Untracked <s>    seconds after leaving the ground at which both controllers lose optical tracking
    //   -ashfallFly4Release <s>      seconds after leaving the ground at which the stick is released
    //   -ashfallFly4Repush <s>       seconds after leaving the ground at which the stick is pushed forward again
    public static class R26Fly4Evidence
    {
        static float spread, drop = .25f, untracked = -1, release = -1, repush = -1, air = -1, last;
        static StreamWriter log; static int sample; static uint noise = 88172645;
        static string Arg(string name)
        { string[] a = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < a.Length; i++) if (a[i] == name) return a[i + 1]; return ""; }
        static bool Number(string name, ref float value)
        { string text = Arg(name); return text.Length == 0 || Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value); }
        static float Noise() { noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5; return (noise & 0xffff) / 32767.5f - 1; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Arg("-ashfallNetBot").Length == 0 || Arg("-ashfallFly4Arms").Length == 0) return;
            if (!Number("-ashfallFly4Arms", ref spread) || !Number("-ashfallFly4HandDrop", ref drop) || !Number("-ashfallFly4Untracked", ref untracked)
                || !Number("-ashfallFly4Release", ref release) || !Number("-ashfallFly4Repush", ref repush) || spread < 0 || spread > 2) return;
            string rules = Arg("-ashfallFly4Rules");
            if (rules.Length > 0)
            {
                R26GlideEntry.StepDownRule = rules.Contains("step"); R26GlideEntry.InferredPoseRule = rules.Contains("inferred");
            }
            string path = Arg("-ashfallFly4Log");
            if (path.Length > 0) { log = new StreamWriter(path, false); log.AutoFlush = true; log.WriteLine("# n frame t x y z supported mode stickY spread tracked vy | gravity=" + R26FreefallTuning.Gravity.ToString(CultureInfo.InvariantCulture) + " rules=" + (rules.Length == 0 ? "all" : rules)); }
            R23VRRig.NetBotPose = Pose;
        }
        // Called inside R23VRRig.NetBotSample before Advance: the rig state is the result of the previous frame.
        static void Pose(R23VRRig rig, ref Vector2 move, ref Vector3 left, ref Vector3 right, ref bool tracked)
        {
            float now = Time.unscaledTime; var c = CultureInfo.InvariantCulture;
            if (rig.Supported) air = -1; else if (air < 0) air = now;
            float airborne = air < 0 ? -1 : now - air;
            if (airborne >= 0 && release >= 0 && airborne >= release && (repush < 0 || airborne < repush)) move = Vector2.zero;
            else if (airborne >= 0 && repush >= 0 && airborne >= repush) move = new Vector2(0, 1);
            if (airborne >= 0 && untracked >= 0 && airborne >= untracked) tracked = false;
            // Arms out to the sides at shoulder height, a little behind the shoulder line, with a few mm of tremor.
            Vector3 head = rig.Head.transform.position; Quaternion q = rig.transform.rotation;
            left = head + q * new Vector3(-spread * .5f + Noise() * .004f, -drop + Noise() * .004f, -.05f + Noise() * .004f);
            right = head + q * new Vector3(spread * .5f + Noise() * .004f, -drop + Noise() * .004f, -.05f + Noise() * .004f);
            if (log == null) return;
            Vector3 p = rig.transform.position; float vy = now > last ? rig.MotionVelocity.y : 0; last = now;
            log.WriteLine((sample++).ToString(c) + " " + Time.frameCount.ToString(c) + " " + now.ToString("F4", c) + " " + p.x.ToString("F3", c) + " " + p.y.ToString("F3", c) + " " + p.z.ToString("F3", c)
                + " " + (rig.Supported ? 1 : 0) + " " + (rig.Activities == null ? "None" : rig.Activities.Mode.ToString()) + " " + move.y.ToString("F2", c) + " " + Vector3.ProjectOnPlane(left - right, Vector3.up).magnitude.ToString("F3", c)
                + " " + (tracked ? 1 : 0) + " " + vy.ToString("F2", c));
        }
    }
}
#endif
