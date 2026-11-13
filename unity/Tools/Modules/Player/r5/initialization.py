from pathlib import Path
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Assets\Ashfall\Modules\Player\Engine\UnityPlayerDriver.cs');s=p.read_text().replace('public bool InitializationLook, InputRequested;','''public bool InitializationLook, InputRequested;
        public void BeginDesktopInitializationLook(bool requested)
        {DisarmPublished();InitializationLook=true;InputRequested=requested;if(requested&&!UseXr)desktopLook.InitializeCapture();}''')
s=s.replace('            InteractionsAllowed = step.InteractionsAllowed;','''            if(!UseXr&&InitializationLook)
                step=new MotionStep {OwnsYaw=InputRequested&&!TurningBlocked,YawRadians=desktopYaw};
            InteractionsAllowed = step.InteractionsAllowed;''')
s=s.replace('Paused = Paused || stalled || Time.timeScale == 0, MoveX','Paused = Paused || stalled || Time.timeScale == 0 || (!UseXr&&InitializationLook), MoveX')
p.write_text(s)
