from pathlib import Path
p=Path('C:/Users/aling/Documents/ashfall/unity/Assets/Ashfall/Modules/Player')
f=p/'RootMotionSafety.cs';s=f.read_text().replace(' || System.Math.Abs(yaw) > System.Math.PI','').replace('double c = System.Math.Cos(yaw), s = System.Math.Sin(yaw);','double reduced = (double)yaw % (2 * System.Math.PI);\n            double c = System.Math.Cos(reduced), s = System.Math.Sin(reduced);');f.write_text(s)
f=p/'PlayerInput.cs';s=f.read_text().replace('private long sequence','private DominantHand previousDominant;\n        private SprintBinding previousSprint;\n        private bool ownershipInitialized;\n        private long sequence')
s=s.replace('''            lastTick = tick;''','''            if (!Enum.IsDefined(typeof(DominantHand), Ownership.Dominant) || !Enum.IsDefined(typeof(SprintBinding), Ownership.Sprint))
                throw new ArgumentException("hand ownership");
            if (ownershipInitialized && (Ownership.Dominant != previousDominant || Ownership.Sprint != previousSprint)) Disarm();
            previousDominant = Ownership.Dominant; previousSprint = Ownership.Sprint; ownershipInitialized = true;
            lastTick = tick;''');f.write_text(s)
f=p/'Engine/UnityPlayerDriver.cs';s=f.read_text().replace('''                desktopYaw = look.YawRadians;''','''                CurrentFrame = ClearDesktopTurn(CurrentFrame);
                desktopYaw = look.YawRadians;''')
s=s.replace('''            raw.HeadPosition = Local(Head);''','''            else if (Mouse.current != null)
            {
                // Capture clicks remain physical held input during neutral rearming.
                raw.Right.Trigger = Mouse.current.leftButton.isPressed ? 1 : 0;
            }
            raw.HeadPosition = Local(Head);''')
s=s.replace('raw.Right.Trigger = Mathf.Max(raw.Right.Trigger, Scalar(TriggerRightAction));','raw.Right.Trigger = Mathf.Max(raw.Right.Trigger, Scalar(TriggerRightAction));')
s=s.replace('''        private static void SetCursor''','''        private static InputFrame ClearDesktopTurn(InputFrame frame)
        { frame.TurnX = frame.TurnY = 0; return frame; }
        private static void SetCursor''');f.write_text(s)
f=p/'PlayerR3Input.cs';s=f.read_text().replace('m.RootUp.IsFinite && m.TrackingUp.IsFinite && m.RootUp.Y > .99999f && m.TrackingUp.Y > .99999f','Upright(m.RootUp) && Upright(m.TrackingUp)');s=s.replace('''        public static bool Valid(RigMeasurements m)''','''        private static bool Upright(Ashfall.Core.Float3 up)
        { return up.IsFinite && up.Y > .99999f && Math.Abs(up.X) < .0045f && Math.Abs(up.Z) < .0045f &&
            Math.Abs((double)up.X * up.X + (double)up.Y * up.Y + (double)up.Z * up.Z - 1) < .00002; }
        public static bool Valid(RigMeasurements m)''');f.write_text(s)
