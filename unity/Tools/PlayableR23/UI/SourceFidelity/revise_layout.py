"""Apply source-derived layout bodies to the staged UI only. Live Assets promotion is explicit."""
from pathlib import Path
p=Path(__file__).parent/'StagedRuntime/R23VrUI.cs'
s=p.read_text(encoding='utf-8-sig')
start=s.index('        void Build()\n')
end=s.index('        void BuildMaps()\n',start)
s=s[:start]+'''        void Build()
        {
            foreach(var b in buttons)if(b.Surface!=null)b.Surface.gameObject.SetActive(false);
            buttons.Clear();labels.Clear();numericRows.Clear();barOrigins.Clear();compass.Clear();loadoutCards.Clear();loadoutStatusText=null;loadoutGesture.Reset();pressed[0]=pressed[1]=hovered[0]=hovered[1]=null;armed[0]=armed[1]=false;sliderDrag[0]=sliderDrag[1]=null;
            for(int i=0;i<2;i++){dots[i].gameObject.SetActive(false);beams[i].enabled=false;}
            Clear(panel);Clear(hud);title=detail=healthText=ammoText=hintText=weaponText=shieldText=staminaText=resourceText=null;healthFill=progressFill=shieldFill=staminaFill=null;shieldGroup=staminaGroup=buildGroup=ammoGroup=null;
            shroud.gameObject.SetActive(Page==R23UiPage.Loading||Page==R23UiPage.Error);
            panel.gameObject.SetActive(Page!=R23UiPage.Playing);hud.gameObject.SetActive(Page==R23UiPage.Playing);
            if(Page==R23UiPage.Playing){BuildHud();if(snapshot!=null)UpdateHud(snapshot);return;}
            if(Page==R23UiPage.Loading)
            {
                Quad(panel,"Loading panel",Vector3.zero,new Vector2(.65f,.23f),Hex("06131d"));
                Text(panel,"LOADING MAP",new Vector3(0,0,-.01f),.0576f,.61f,Color.white);return;
            }
            bool popup=Page==R23UiPage.Settings||Page==R23UiPage.Pause||Page==R23UiPage.Dead||Page==R23UiPage.Error;
            if(popup)Quad(panel,"SettingsBoard",Vector3.zero,new Vector2(1.3f,1.48f),Hex("040e17"));
            if(Page==R23UiPage.Loadout)Quad(panel,"Backpack backdrop",Vector3.zero,new Vector2(1.2f,.64f),Hex("4a5259"));
            title=Text(panel,"",new Vector3(0,.61f,-.01f),.042f,1.16f,Hex("40e8ef"));
            if(Page==R23UiPage.Error){title.text="COULD NOT LOAD";Text(panel,failure,new Vector3(0,.16f,-.01f),.03f,1.12f,Color.white);return;}
            if(Page==R23UiPage.Settings){BuildSettings();return;}
            if(Page==R23UiPage.Weapons){BuildWeapons();return;}
            if(Page==R23UiPage.Loadout){BuildLoadout();return;}
            if(Page==R23UiPage.Appearance){BuildAppearance();return;}
            if(Page==R23UiPage.Maps){BuildMaps();return;}
            if(Page==R23UiPage.Dead){title.text="YOU DIED";MenuCard("RESTART",.40f,"pause0993_0",delegate{if(RestartRequested!=null)RestartRequested();});return;}
            if(Page==R23UiPage.Ready){BuildReady();return;}
            title.text="ASHFALL";
            MenuCard("RESUME",.40f,"pause0993_0",SetPlaying);
            MenuCard("SETTINGS",.155f,"pause0993_1",OpenSettings);
            MenuCard("INITIALIZE VERTICAL COMBAT SYSTEM",-.09f,"pause0993_2",delegate{if(RecalibrationRequested!=null)RecalibrationRequested();});
            MenuCard("LOADOUT",-.335f,"settings_2",OpenLoadout);
            AddButton(panel,"MAP SELECT",-.27f,-.61f,.45f,.08f,OpenMaps,.025f);
            AddButton(panel,"RESTART ROUND",.27f,-.61f,.45f,.08f,delegate{if(RestartRequested!=null)RestartRequested();},.025f);
        }
        void BuildReady()
        {
            title.text="";
            FrontText(map,640,485,17,350);
            FrontButton("MAP SELECT",465,506,350,58,OpenMaps,Hex("0499c4"),Color.white,24);
            FrontButton("READY",465,574,350,88,delegate{if(StartRequested!=null)StartRequested();},Hex("e5eb00"),Hex("171b13"),48);
            FrontButton("SETTINGS",40,726,170,48,OpenSettings,Hex("101b31"),Color.white,22);
            FrontButton("LOADOUT",450,712,330,64,OpenLoadout,Hex("101b31"),Color.white,22);
            FrontButton("CHARACTERS",970,712,270,64,OpenAppearance,Hex("101b31"),Color.white,22);
        }
        void BuildHud()
        {
            hud.localPosition=Vector3.zero;
            Quad(hud,"Health track",new Vector3(-.02f,-.456f,.804f),new Vector2(.29f,.019f),Hex("17451c"));
            healthFill=Quad(hud,"Health remaining",new Vector3(-.02f,-.456f,.802f),new Vector2(1,.019f),Hex("35d400"));
            healthText=Text(hud,"",new Vector3(-.159f,-.4545f,.798f),.016f,.21f,Color.white);healthText.anchor=TextAnchor.MiddleLeft;
            shieldGroup=Child(hud,"Shield meter");
            Quad(shieldGroup,"Shield track",new Vector3(-.02f,-.433f,.804f),new Vector2(.29f,.019f),Hex("134757"));
            shieldFill=Quad(shieldGroup,"Shield remaining",new Vector3(-.02f,-.433f,.802f),new Vector2(1,.019f),Hex("00a9db"));
            shieldText=Text(shieldGroup,"",new Vector3(-.159f,-.4315f,.798f),.016f,.21f,Color.white);shieldText.anchor=TextAnchor.MiddleLeft;
            staminaGroup=Child(hud,"Sprint meter");
            Quad(staminaGroup,"Stamina track",new Vector3(-.02f,-.477f,.803f),new Vector2(.29f,.007f),Hex("233138"));
            staminaFill=Quad(staminaGroup,"Stamina remaining",new Vector3(-.02f,-.477f,.797f),new Vector2(1,.007f),Hex("f5cf53"));
            Icon(staminaGroup,"sprint_r4",new Vector3(-.177f,-.477f,.794f),new Vector2(.015f,.015f));
            buildGroup=Child(hud,"Build resources");
            Quad(buildGroup,"Build resource face",new Vector3(.147f,-.4445f,.806f),new Vector2(.039f,.042f),Hex("374047"));
            resourceText=Text(buildGroup,"",new Vector3(.145f,-.429f,.798f),.016f,.038f,Color.white);
            for(int x=0;x<3;x++)for(int y=0;y<3;y++)Quad(buildGroup,"Build square",new Vector3(.137f+x*.007f,-.446f-y*.006f,.798f),new Vector2(.006f,.005f),Hex("e1e7df"));
            for(int i=0;i<24;i++){var mark=Text(hud,i%3==0?new[]{"N","NE","E","SE","S","SW","W","NW"}[i/3]:"·",new Vector3(0,-.499f,.799f),i%3==0?.013f:.009f,.05f,Color.white);compass.Add(mark);}
            ammoGroup=Child(hud,"Compact source two-tone ammo");
            Quad(ammoGroup,"Loaded blue face",new Vector3(.245f,-.433f,.804f),new Vector2(.075f,.022f),Hex("008bca"));
            Quad(ammoGroup,"Reserve face",new Vector3(.245f,-.455f,.804f),new Vector2(.075f,.022f),Hex("1b252b"));
            ammoText=Text(ammoGroup,"",new Vector3(.245f,-.433f,.798f),.015f,.07f,Color.white);
            weaponText=Text(ammoGroup,"",new Vector3(.245f,-.455f,.798f),.015f,.07f,Color.white);
            // Gameplay has meters/readouts only. Menu buttons are deliberately absent.
        }
''' + s[end:]
p.write_text(s,encoding='utf-8')
