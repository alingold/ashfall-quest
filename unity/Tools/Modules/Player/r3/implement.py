from pathlib import Path
r=Path('C:/Users/aling/Documents/ashfall'); p=r/'unity/Assets/Ashfall/Modules/Player'
f=p/'Locomotion.cs'; s=f.read_text();s=s.replace('velocity = new Float3(); jumpHeld', 'stamina = 6;\n            CancelMotion();\n        }\n        // Pause/collision cancellation must not refill stamina.\n        public void CancelMotion()\n        {\n            velocity = new Float3(); jumpHeld',1)
s=s.replace('!PlayerInput.Finite(world.HeadingRadians))','!PlayerInput.Finite(world.HeadingRadians) ||\n                !PlayerInput.Finite(world.DesktopYawRadians) || Math.Abs(world.DesktopYawRadians) > Math.PI)')
s=s.replace('ResetMotion();\n                return new MotionStep();','if (!input.Tracked) ResetMotion(); else CancelMotion();\n                return new MotionStep();')
s=s.replace('float yaw = 0;', 'float yaw = world.IsXr ? 0 : world.DesktopYawRadians;')
s=s.replace('turnNeedsNeutral = false; yaw = Turn(', 'turnNeedsNeutral = false; yaw += Turn(')
s=s.replace('double heading = (double)world.HeadingRadians + yaw;','// Reduce BEFORE Framework trig: very large finite arguments are unsafe on this runtime.\n            double heading = ((double)world.HeadingRadians % (2 * Math.PI) + yaw) % (2 * Math.PI);')
s=s.replace('velocity = new Float3(vx, vy, vz);','Float3 next = new Float3(vx, vy, vz);\n            Float3 displacement = new Float3(vx * dt, vy * dt, vz * dt);\n            if (!next.IsFinite || !displacement.IsFinite)\n            { CancelMotion(); return new MotionStep(); }\n            velocity = next;')
s=s.replace('Displacement = new Float3(vx * dt, vy * dt, vz * dt)', 'Displacement = displacement'); f.write_text(s)
f=p/'PlayerPorts.cs';s=f.read_text().replace('public float HeadingRadians;', 'public float HeadingRadians;\n        public float DesktopYawRadians;');f.write_text(s)
f=p/'PlayerInput.cs';s=f.read_text().replace('private long sequence', 'public readonly HandActionOwnership Ownership = new HandActionOwnership();\n        private long sequence')
s=s.replace('left.Primary |= sample.Jump; left.Click |= sample.Sprint;\n                right.Secondary |= sample.Reload || sample.Ping;', '''bool leftWeapon = Ownership.Dominant == DominantHand.Left;
                if (leftWeapon) { right.Primary |= sample.Jump; left.Secondary |= sample.Reload || sample.Ping; }
                else { left.Primary |= sample.Jump; right.Secondary |= sample.Reload || sample.Ping; }
                left.Click |= sample.Sprint;''')
s=s.replace('frame.Jump = sample.Jump; frame.Sprint = sample.Sprint;', 'frame.Sprint = sample.Sprint;',1)
s=s.replace('frame.Reload = sample.Reload; frame.Ping = sample.Ping;','',1)
s=s.replace('''            }
            else
            {''','''                frame.Jump = sample.Jump && (leftWeapon ? rightReady : leftReady);
                frame.Reload = sample.Reload && (leftWeapon ? leftReady : rightReady);
                frame.Ping = sample.Ping && (leftWeapon ? leftReady : rightReady);
            }
            else
            {''',1)
f.write_text(s)
