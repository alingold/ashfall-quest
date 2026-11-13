from pathlib import Path
p=Path(__file__).parent/'StagedRuntime/R23VrUI.cs';s=p.read_text(encoding='utf-8')
def replace(start,end,body):
    global s
    a=s.index(start);b=s.index(end,a);s=s[:a]+body+s[b:]
replace('        void BuildMaps()\n','        void BuildWeapons()\n','''        void BuildMaps()
        {
            title.text="";FrontText("SELECT MAP",190,65,28,300);
            for(int i=0;i<mapOptions.Count&&i<2;i++)
            {
                var choice=mapOptions[i];float x=90+i*590;
                FrontText(Clean(choice.Label,36),x+220,260,25,440);
                FrontButton("LOBBY SPAWN",x,340,440,65,delegate{if(MapSelected!=null)MapSelected(choice.Id,false);},Hex("0499c4"),Color.white,24);
                FrontButton("GAME SPAWN",x,430,440,65,delegate{if(MapSelected!=null)MapSelected(choice.Id,true);},Hex("101b31"),Color.white,24);
            }
            FrontButton("BACK",40,710,170,58,delegate{SetPage(returnPage);},Hex("101b31"),Color.white,22);
        }
''')
replace('        void BuildWeapons()\n','        void BuildLoadout()\n','''        void BuildWeapons()
        {
            title.text="";FrontText("LOADOUT",180,50,28,300);
            FrontButton("WEAPONS",400,108,230,54,OpenWeapons,Hex("101b31"),Color.white,22);
            FrontButton("CHARACTERS",650,108,230,54,OpenAppearance,Hex("101b31"),Color.white,22);
            if(options.Count>0)
            {
                weaponChoice=Mathf.Clamp(weaponChoice,0,options.Count-1);var option=options[weaponChoice];
                FrontText(Clean(option.Label,44),640,565,25,500);
                FrontButton("<",350,672,210,105,delegate{weaponChoice=(weaponChoice+options.Count-1)%options.Count;Build();},Hex("101b31"),Color.white,46);
                FrontButton(">",720,672,210,105,delegate{weaponChoice=(weaponChoice+1)%options.Count;Build();},Hex("101b31"),Color.white,46);
                var choose=FrontButton("SELECT WEAPON",500,594,280,54,delegate{if(option.Available&&WeaponSelected!=null)WeaponSelected(option.Id);},Hex("0298c3"),Color.white,22);choose.Enabled=option.Available;
            }
            else FrontText("NO WEAPONS AVAILABLE",640,565,24,600);
            FrontButton("BACK",40,650,170,54,delegate{SetPage(returnPage);},Hex("101b31"),Color.white,22);
            FrontButton("BACKPACK",40,725,240,54,OpenLoadout,Hex("101b31"),Color.white,22);
        }
''')
replace('        void BuildLoadout()\n','        void BuildAppearance()\n','''        void BuildLoadout()
        {
            title.text="BACKPACK";title.transform.localPosition=new Vector3(0,.25f,-.01f);
            for(int row=0;row<3;row++)
            {
                int slot=loadoutPage*3+row;if(slot>=ownedLoadout.Capacity)break;var option=ownedLoadout.Slot(slot);float x=(row-1)*.37f;
                var card=AddButton(panel,"",x,.055f,.34f,.27f,null,.03f,option!=null&&(option.CanEquip||option.CanDrop),option==null?null:option.Id);
                Text(card.Surface,(slot+1).ToString(),new Vector3(-.14f,.10f,-.004f),.020f,.04f,Hex("aab8b7"));
                var caption=Text(card.Surface,"",new Vector3(0,.023f,-.004f),.025f,.31f,option==null?Hex("aab8b7"):Color.white);
                var detail=Text(card.Surface,"",new Vector3(0,-.065f,-.004f),.020f,.31f,Color.white);
                loadoutCards.Add(new LoadoutCard{Slot=slot,Title=caption,Detail=detail});
            }
            RefreshLoadoutCards();Text(panel,"GRIP EQUIPS  |  TRIGGER DROPS",new Vector3(0,-.12f,-.01f),.020f,1.12f,Hex("aab8b7"));
            loadoutStatusText=Text(panel,loadoutStatus,new Vector3(0,-.172f,-.01f),.018f,1.12f,Color.white);
            if(ownedLoadout.Capacity>3)
            {
                AddButton(panel,"<",-.52f,-.26f,.10f,.07f,delegate{loadoutPage=1-loadoutPage;Build();},.027f);
                AddButton(panel,">",.52f,-.26f,.10f,.07f,delegate{loadoutPage=1-loadoutPage;Build();},.027f);
            }
            AddButton(panel,"CATALOG",-.22f,-.26f,.34f,.07f,OpenWeapons,.023f);
            AddButton(panel,"BACK",.22f,-.26f,.34f,.07f,delegate{SetPage(returnPage);},.023f);
        }
        void RefreshLoadoutCards()
        {
            if(Page!=R23UiPage.Loadout)return;
            foreach(var card in loadoutCards)
            {
                var option=ownedLoadout.Slot(card.Slot);card.Title.text=option==null?"EMPTY":Clean(option.Label,24);
                card.Detail.text=option==null?"":Clean((option.Equipped?(option.EquippedHand==0?"L HAND  ":"R HAND  "):"")+(option.Count>1?"x"+option.Count+"  ":"")+option.Detail,34);
            }
            if(title!=null)title.text="BACKPACK";
        }
''')
replace('        void BuildAppearance()\n','        void BuildSettings()\n','''        void BuildAppearance()
        {
            title.text="";
            Quad(panel,"Skin selector backing",FrontPoint(325,397),new Vector2(480f/1280*2.2f,535f/800*1.375f),new Color(.025f,.055f,.085f,.92f));
            FrontText("CHARACTERS",325,185,30,410);
            if(appearanceOptions.Count>0)
            {
                appearancePage=Mathf.Clamp(appearancePage,0,appearanceOptions.Count-1);var choice=appearanceOptions[appearancePage];
                FrontText(Clean(choice.Label,40),325,285,25,410);
                FrontButton("<",120,360,180,64,delegate{appearancePage=(appearancePage+appearanceOptions.Count-1)%appearanceOptions.Count;Build();},Hex("101b31"),Color.white,30);
                FrontButton(">",330,360,180,64,delegate{appearancePage=(appearancePage+1)%appearanceOptions.Count;Build();},Hex("101b31"),Color.white,30);
                var choose=FrontButton("USE CHARACTER",120,460,390,72,delegate{if(choice.Available&&AppearanceSelected!=null)AppearanceSelected(choice.Id);},Hex("0298c3"),Color.white,24);choose.Enabled=choice.Available;
            }
            FrontText(appearanceStatus,325,630,16,410);
            FrontButton("BACK",120,565,180,64,delegate{SetPage(returnPage);},Hex("101b31"),Color.white,22);
            FrontButton("WEAPONS",330,565,180,64,OpenWeapons,Hex("101b31"),Color.white,22);
        }
''')
replace('        void BuildSettings()\n','        void ChangeMotion(','''        void BuildSettings()
        {
            title.text=settingsPage<0?"SETTINGS":settingsPage==0?"ASHFALL / MOVEMENT":"ASHFALL / GENERAL";
            if(settingsPage<0)
            {
                MenuCard("LOCOMOTION",.40f,"settings_0",delegate{settingsPage=0;Build();});
                MenuCard("SET HEIGHT",.155f,"settings_1",delegate{if(RecalibrationRequested!=null)RecalibrationRequested();});
                MenuCard("WEAPONS",-.09f,"settings_2",OpenWeapons);
                MenuCard("GENERAL",-.335f,"settings_3",delegate{settingsPage=1;Build();});
                Text(panel,calibrationStatus,new Vector3(0,-.50f,-.01f),.020f,1.12f,Hex("aab8b7"));
                AddButton(panel,"BACK",0,-.64f,.94f,.08f,delegate{SetPage(returnPage);},.026f);return;
            }
            if(settingsPage==0)
            {
                var forward=AddButton(panel,"",0,.42f,.98f,.075f,delegate{ChangeMotion(1,3);},.03f);SetButtonStyle(forward,new Color(0,0,0,0),Hex("d1e5ed"));
                numericRows.Add(new NumericRow{Caption=delegate{return "FORWARD MOTION    "+(Settings.Forward==R23Forward.Head?"HEAD HEADING":"LEFT CONTROLLER HEADING");},Text=forward.Caption});
                var turn=AddButton(panel,"",0,.305f,.98f,.075f,delegate{var v=Settings;Publish(new R23UiSettings(v.Turning==R23Turning.Smooth?R23Turning.Snap:R23Turning.Smooth,v.Forward,v.SnapDegrees,v.SmoothDegreesPerSecond,v.WalkMultiplier,v.MasterVolume,v.SfxVolume,v.MusicVolume,v.AmbienceVolume,v.VoiceVolume));},.03f);SetButtonStyle(turn,new Color(0,0,0,0),Hex("d1e5ed"));
                numericRows.Add(new NumericRow{Caption=delegate{return "TURNING    "+Settings.Turning.ToString().ToUpperInvariant();},Text=turn.Caption});
                AddNumericRow(delegate{return "SNAP ANGLE    "+Mathf.RoundToInt(Settings.SnapDegrees)+" deg";},delegate{return Settings.SnapDegrees;},15,180,5,.19f,0);
                AddNumericRow(delegate{return "SMOOTH TURN    "+Mathf.RoundToInt(Settings.SmoothDegreesPerSecond)+" deg/s";},delegate{return Settings.SmoothDegreesPerSecond;},30,720,5,.075f,1);
                AddNumericRow(delegate{return "WALK SPEED    "+Settings.WalkMultiplier.ToString("F2")+"x";},delegate{return Settings.WalkMultiplier;},.25f,5,.05f,-.04f,2);
            }
            else
            {
                var names=new[]{"MASTER VOLUME","SFX VOLUME","MUSIC VOLUME","PLAYER VOICE VOLUME","AMBIENCE VOLUME"};
                for(int i=0;i<5;i++){int slot=i;AddNumericRow(delegate{return names[slot]+"    "+Mathf.RoundToInt(AudioValue(slot)*100)+"%";},delegate{return AudioValue(slot);},0,1,.01f,.42f-i*.115f,slot+3);}
            }
            RefreshSettingsRows();AddButton(panel,"BACK",0,-.61f,.94f,.08f,delegate{settingsPage=-1;Build();},.026f);
        }
        float AudioValue(int field){return field==0?Settings.MasterVolume:field==1?Settings.SfxVolume:field==2?Settings.MusicVolume:field==3?Settings.VoiceVolume:Settings.AmbienceVolume;}
        void AddNumericRow(Func<string> caption,Func<float> value,float min,float max,float step,float y,int field)
        {
            var text=Text(panel,caption(),new Vector3(0,y+.012f,-.01f),.030f,1.11f,Hex("d1e5ed"));
            var button=AddButton(panel,"",0,y-.035f,.90f,.065f,null,.03f);SetButtonStyle(button,new Color(0,0,0,0),Color.white);
            Quad(button.Surface,"Slider track",Vector3.zero,new Vector2(.90f,.012f),new Color(.15f,.22f,.26f));
            var fill=Quad(button.Surface,"Slider remaining",new Vector3(0,0,-.002f),new Vector2(1,.012f),Hex("40e8ef"));
            var knob=Quad(button.Surface,"Slider knob",new Vector3(0,0,-.004f),new Vector2(.018f,.035f),Color.white);
            button.SliderChanged=delegate(float fraction){float next=Mathf.Clamp(Mathf.Round(Mathf.Lerp(min,max,fraction)/step)*step,min,max);ApplyNumeric(field,next);};
            numericRows.Add(new NumericRow{Caption=caption,Value=value,Min=min,Max=max,Text=text,Fill=fill,Knob=knob,Width=.90f});
        }
        void ApplyNumeric(int field,float value)
        {
            var v=Settings;Publish(new R23UiSettings(v.Turning,v.Forward,field==0?value:v.SnapDegrees,field==1?value:v.SmoothDegreesPerSecond,field==2?value:v.WalkMultiplier,
                field==3?value:v.MasterVolume,field==4?value:v.SfxVolume,field==5?value:v.MusicVolume,field==7?value:v.AmbienceVolume,field==6?value:v.VoiceVolume),true);
        }
        void RefreshSettingsRows()
        {
            foreach(var row in numericRows){row.Text.text=row.Caption();if(row.Fill==null)continue;float fraction=Mathf.InverseLerp(row.Min,row.Max,row.Value());SetBar(row.Fill,fraction,row.Width);var at=row.Knob.localPosition;at.x=-row.Width*.5f+row.Width*fraction;row.Knob.localPosition=at;}
        }
''')
s=s.replace('int weaponPage,appearancePage,settingsPage=-1,weaponChoice;','int appearancePage,settingsPage=-1,weaponChoice;').replace('weaponPage=0;weaponChoice=0;','weaponChoice=0;')
p.write_text(s,encoding='utf-8')
