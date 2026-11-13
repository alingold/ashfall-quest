#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ashfall.Integration.Playable.UI
{
    // No InputManager, EventSystem or physics collider dependency. The rig supplies tracked rays.
    public sealed class R23VrUI : MonoBehaviour
    {
        sealed class Label { public TextMesh Text;public float Height,MaxWidth; }
        sealed class Button { public Transform Surface;public Vector2 Size;public Action Action;public Renderer Renderer,Edge;public bool Enabled;public string OwnedId;public Color Normal,Hover,Ink;public TextMesh Caption;public Action<float> SliderChanged; }
        sealed class NumericRow {public Func<string> Caption;public Func<float> Value;public float Min,Max;public TextMesh Text;public Transform Fill,Knob;public float Width;}
        sealed class LoadoutCard {public int Slot;public TextMesh Title,Detail;}
        readonly List<Label> labels=new List<Label>();
        readonly List<Button> buttons=new List<Button>();
        readonly List<Material> materials=new List<Material>();
        readonly R23LoadoutGesture loadoutGesture=new R23LoadoutGesture();
        readonly List<LoadoutCard> loadoutCards=new List<LoadoutCard>();
        R23OwnedLoadout ownedLoadout=new R23OwnedLoadout(null);
        TextMesh loadoutStatusText;string loadoutStatus="";int loadoutPage;
        readonly bool[] held=new bool[2],armed=new bool[2];
        readonly Button[] pressed=new Button[2],hovered=new Button[2];
        readonly Transform[] dots=new Transform[2];
        readonly LineRenderer[] beams=new LineRenderer[2];
        readonly Button[] sliderDrag=new Button[2];
        Button focusButton;
        readonly List<NumericRow> numericRows=new List<NumericRow>();
        readonly Dictionary<string,Material> iconMaterials=new Dictionary<string,Material>();
        readonly Dictionary<Transform,float> barOrigins=new Dictionary<Transform,float>();
        Transform shieldGroup,staminaGroup,buildGroup,ammoGroup;
        Transform menuAnchor,staminaFill;readonly List<TextMesh> compass=new List<TextMesh>();
        bool preferenceDirty;float nextPreferenceSave;
        Camera view;Transform panel,hud,shroud;Font font;Material fontMaterial,flatMaterial;
        TextMesh title,detail,healthText,ammoText,hintText,weaponText,shieldText,staminaText,resourceText;
        Transform healthFill,progressFill,shieldFill;
        R23HudSnapshot snapshot;
        readonly List<R23WeaponOption> options=new List<R23WeaponOption>();
        readonly List<R23MapOption> mapOptions=new List<R23MapOption>();
        readonly List<R23AppearanceOption> appearanceOptions=new List<R23AppearanceOption>();
        int appearancePage,settingsPage=-1,weaponChoice;
        string appearanceStatus="",appearanceSelected="";
        string calibrationStatus="STAND UPRIGHT TO CALIBRATE";
        string map="MAP",stage="LOADING MAP",failure="";
        float progress;
        R23UiPage returnPage=R23UiPage.Ready;
        bool initialized;
        public R23UiPage Page { get;private set; }
        public R23UiSettings Settings { get;private set; }
        public bool BlocksGameplay { get { return Page!=R23UiPage.Playing; } }
        public bool PointerOverUi { get { return hovered[0]!=null||hovered[1]!=null; } }
        public event Action StartRequested,RestartRequested,ReloadRequested,RecalibrationRequested,MainMenuRequested;
        public event Action<string> WeaponSelected,AppearanceSelected;
        public event Action<string,bool> MapSelected;
        public event Action<R23UiSettings> SettingsChanged;
        public event Action<string,int> EquipRequested;
        public event Action<string> DropRequested;

        public void Initialize(Camera trackedCamera)
        {
            if(initialized)throw new InvalidOperationException("UI already initialized");
            if(trackedCamera==null)throw new ArgumentNullException("trackedCamera");
            initialized=true;view=trackedCamera;Settings=R23UiPreferences.Load();
            var shader=Resources.Load<Shader>("R23VrUi");
            if(shader==null)throw new InvalidOperationException("R23VrUi shader resource missing");
            flatMaterial=new Material(shader);materials.Add(flatMaterial);flatMaterial.renderQueue=4101;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(font==null)throw new InvalidOperationException("LegacyRuntime font missing");
            fontMaterial=new Material(shader);materials.Add(fontMaterial);fontMaterial.SetFloat("_Font",1);fontMaterial.renderQueue=4102;RefreshFont(font);Font.textureRebuilt+=RefreshFont;
            panel=Child(transform,"R23 eye-level menu");
            hud=Child(view.transform,"R23 readable combat HUD");hud.localPosition=new Vector3(0,-.29f,1.1f);
            shroud=Quad(view.transform,"Opaque loading backdrop",new Vector3(0,0,.25f),new Vector2(8,8),new Color(.008f,.018f,.027f));
            var shroudMaterial=new Material(flatMaterial);materials.Add(shroudMaterial);shroudMaterial.renderQueue=4100;shroud.GetComponent<Renderer>().sharedMaterial=shroudMaterial;
            var beamMaterial=new Material(flatMaterial);materials.Add(beamMaterial);beamMaterial.renderQueue=4104;
            for(int i=0;i<2;i++)
            {
                dots[i]=Quad(transform,"UI pointer "+i,Vector3.zero,new Vector2(.014f,.014f),Color.white);dots[i].gameObject.SetActive(false);
                var beam=new GameObject("R23 interactive hand laser "+i);beam.transform.SetParent(transform,false);beams[i]=beam.AddComponent<LineRenderer>();beams[i].sharedMaterial=beamMaterial;
                beams[i].useWorldSpace=true;beams[i].positionCount=2;beams[i].startWidth=.002f;beams[i].endWidth=.001f;beams[i].startColor=new Color(.2f,.85f,1);beams[i].endColor=new Color(.2f,.85f,1);beams[i].sortingOrder=126;beams[i].enabled=false;
            }
            BeginLoading("MAP");
        }
        void RefreshFont(Font rebuilt) { if(rebuilt==font&&fontMaterial!=null)fontMaterial.mainTexture=font.material.mainTexture; }
        public void BeginLoading(string mapName) { Require();map=Clean(mapName,32);progress=0;stage="LOADING MAP";SetPage(R23UiPage.Loading); }
        public void SetLoadingProgress(string phase,float fraction)
        { if(Page!=R23UiPage.Loading)return;stage=Clean(phase,40);progress=Mathf.Clamp01(float.IsNaN(fraction)?0:fraction);if(detail!=null)detail.text=map+"  /  "+stage;SetBar(progressFill,progress,.98f); }
        public void ShowReady() { Require();if(Page==R23UiPage.Loading)SetPage(R23UiPage.Ready); }
        public void ShowFailure(string message) { Require();failure=Clean(message,76);SetPage(R23UiPage.Error); }
        public void SetPlaying() { Require();if(snapshot!=null&&snapshot.Health<=0)snapshot=null;SetPage(R23UiPage.Playing); }
        public void ToggleMenu()
        {
            Require();if(Page==R23UiPage.Playing)SetPage(R23UiPage.Pause);
            else if(Page==R23UiPage.Pause||Page==R23UiPage.Settings||Page==R23UiPage.Weapons||Page==R23UiPage.Appearance||Page==R23UiPage.Maps||Page==R23UiPage.Loadout)SetPage(returnPage==R23UiPage.Ready?R23UiPage.Ready:R23UiPage.Playing);
        }
        public void OpenSettings() { settingsPage=-1;OpenSubpage(R23UiPage.Settings); }
        public void SetMenuAnchor(Transform worldAnchor){Require();menuAnchor=worldAnchor;Recenter();}
        public void ClearMenuAnchor(){Require();menuAnchor=null;Recenter();}
        public void SetCalibrationStatus(string value)
        {Require();string status=Clean(value,48);if(status==calibrationStatus)return;calibrationStatus=status;if(Page==R23UiPage.Settings)Build();}
        public void OpenWeapons() { OpenSubpage(R23UiPage.Weapons); }
        // Retained input/test seam; gameplay HUD itself has no menu/reload buttons.
        public void RequestReload(){Require();if(Page==R23UiPage.Playing&&ReloadRequested!=null)ReloadRequested();}
        public void OpenLoadout(){OpenSubpage(R23UiPage.Loadout);}
        public void SetOwnedLoadout(IEnumerable<R23LoadoutOption> values,int capacity=3)
        {
            Require();var next=new R23OwnedLoadout(values,capacity);bool changed=!ownedLoadout.SameIdentity(next);ownedLoadout=next;
            if(changed){loadoutGesture.Reset();loadoutPage=Mathf.Min(loadoutPage,(capacity-1)/3);if(Page==R23UiPage.Loadout)Build();}
            else if(Page==R23UiPage.Loadout)RefreshLoadoutCards();
        }
        public void SetLoadoutStatus(string value)
        {Require();loadoutStatus=Clean(value,55);if(loadoutStatusText!=null)loadoutStatusText.text=loadoutStatus;}
        public void OpenMaps(){OpenSubpage(R23UiPage.Maps);}
        public void SetMapOptions(IEnumerable<R23MapOption> values){mapOptions.Clear();if(values!=null)mapOptions.AddRange(values);if(Page==R23UiPage.Maps)Build();}
        public void OpenAppearance() { OpenSubpage(R23UiPage.Appearance); }
        void OpenSubpage(R23UiPage page)
        {
            Require();if(Page==R23UiPage.Loading||Page==R23UiPage.Error||Page==R23UiPage.Dead)return;
            if(Page==R23UiPage.Ready)returnPage=R23UiPage.Ready;
            else if(Page==R23UiPage.Playing||Page==R23UiPage.Pause)returnPage=R23UiPage.Pause;
            SetPage(page);
        }
        public void SetSettings(R23UiSettings value)
        { Require();if(value==null)throw new ArgumentNullException("value");FlushPreferences();Settings=value;if(Page==R23UiPage.Settings)Build(); }
        public void SetWeaponOptions(IEnumerable<R23WeaponOption> values)
        {
            Require();options.Clear();if(values!=null)foreach(var option in values)if(option!=null)options.Add(option);
            weaponChoice=0;if(Page==R23UiPage.Weapons)Build();
        }
        public void SetAppearanceOptions(IEnumerable<R23AppearanceOption> values)
        {
            Require();appearanceOptions.Clear();if(values!=null)foreach(var option in values)if(option!=null)appearanceOptions.Add(option);
            appearancePage=0;if(Page==R23UiPage.Appearance)Build();
        }
        public void SetAppearanceStatus(string selectedId,bool loading,string error=null)
        {
            Require();string status=!string.IsNullOrEmpty(error)?"LOAD FAILED / "+Clean(error,44):(loading?"LOADING APPEARANCE...":"SELECT YOUR APPEARANCE");
            if(appearanceSelected==(selectedId??"")&&appearanceStatus==status)return;
            appearanceSelected=selectedId??"";appearanceStatus=status;if(Page==R23UiPage.Appearance)Build();
        }
        public void UpdateHud(R23HudSnapshot value)
        {
            Require();if(value==null)throw new ArgumentNullException("value");snapshot=value;
            if(value.Health<=0&&Page!=R23UiPage.Loading&&Page!=R23UiPage.Error&&Page!=R23UiPage.Dead)SetPage(R23UiPage.Dead);
            if(healthText!=null)healthText.text=Mathf.CeilToInt(Mathf.Max(0,value.Health)).ToString();
            if(ammoText!=null)ammoText.text=value.Reloading?"...":value.Ammo.ToString();
            if(weaponText!=null)weaponText.text=value.Reserve<0||value.Reserve>=100000?"∞":value.Reserve.ToString();
            if(hintText!=null)hintText.text=value.Hint.Length>0?Clean(value.Hint,74):value.HasWeapon?"TRIGGER FIRE  /  A RELOAD":"POINT AT A WEAPON AND GRIP";
            if(shieldText!=null)shieldText.text=value.Shield>=0?Mathf.CeilToInt(value.Shield).ToString():"";
            if(resourceText!=null)resourceText.text=value.BuildCharges>=999?"∞":value.BuildCharges.ToString();
            if(shieldGroup!=null)shieldGroup.gameObject.SetActive(value.Shield>=0);
            if(staminaGroup!=null)staminaGroup.gameObject.SetActive(value.Stamina>=0);
            if(buildGroup!=null)buildGroup.gameObject.SetActive(value.BuildCharges>=0);
            if(ammoGroup!=null)ammoGroup.gameObject.SetActive(value.HasWeapon);
            SetBar(healthFill,value.MaxHealth>0?Mathf.Clamp01(value.Health/value.MaxHealth):0,.29f);
            SetBar(shieldFill,value.MaxShield>0?Mathf.Clamp01(Mathf.Max(0,value.Shield)/value.MaxShield):0,.29f);
            SetBar(staminaFill,value.MaxStamina>0?Mathf.Clamp01(Mathf.Max(0,value.Stamina)/value.MaxStamina):0,.29f);
        }
        // Call once per hand per frame, including tracked=false on tracking loss. Click activates on release.
        // Return true for a ray over a UI button, so the input owner can suppress firing/pickup.
        public bool UpdatePointer(Vector3 origin,Vector3 direction,bool selectHeld,bool tracked=true)
        { return UpdatePointer(0,origin,direction,selectHeld,tracked); }
        public bool UpdatePointer(int hand,Vector3 origin,Vector3 direction,bool selectHeld,bool tracked=true)
        {return UpdatePointerCore(hand,origin,direction,selectHeld,false,tracked);}
        // Use this once per hand per frame instead of UpdatePointer when grip input is available.
        public bool UpdateLoadoutPointer(int hand,Vector3 origin,Vector3 direction,bool triggerHeld,bool gripHeld,bool tracked=true)
        {return UpdatePointerCore(hand,origin,direction,triggerHeld,gripHeld,tracked);}
        bool UpdatePointerCore(int hand,Vector3 origin,Vector3 direction,bool selectHeld,bool gripHeld,bool tracked)
        {
            Require();if(hand<0||hand>1)throw new ArgumentOutOfRangeException("hand");
            Button hit=null;Vector3 hitPoint=Vector3.zero;float best=float.PositiveInfinity;
            bool valid=tracked&&Finite(origin)&&Finite(direction)&&direction.sqrMagnitude>.0001f;
            if(valid)foreach(var b in buttons)
            {
                if(!b.Enabled||!b.Surface.gameObject.activeInHierarchy)continue;
                Vector3 localOrigin=b.Surface.InverseTransformPoint(origin),localDirection=b.Surface.InverseTransformDirection(direction.normalized);
                if(Mathf.Abs(localDirection.z)<.00001f)continue;
                float t=-localOrigin.z/localDirection.z;if(t<0||t>8||t>=best)continue;
                var at=localOrigin+localDirection*t;
                if(Mathf.Abs(at.x)>b.Size.x*.5f||Mathf.Abs(at.y)>b.Size.y*.5f)continue;
                best=t;hit=b;hitPoint=b.Surface.TransformPoint(new Vector3(at.x,at.y,-.004f));
            }
            hovered[hand]=hit;
            dots[hand].gameObject.SetActive(hit!=null);
            if(hit!=null){dots[hand].position=hitPoint;dots[hand].rotation=hit.Surface.rotation;}
            if(hit!=null)focusButton=hit;
            foreach(var b in buttons)SetHovered(b,hovered[0]==b||hovered[1]==b||focusButton==b);
            bool showBeam=valid&&Page!=R23UiPage.Playing&&Page!=R23UiPage.Loading&&Page!=R23UiPage.Error;
            beams[hand].enabled=showBeam;
            if(showBeam){beams[hand].SetPosition(0,origin);beams[hand].SetPosition(1,hit!=null?hitPoint:origin+direction.normalized*2.5f);}
            var intent=loadoutGesture.Sample(hand,hit==null?null:hit.OwnedId,selectHeld,gripHeld,valid,Page==R23UiPage.Loadout);
            Action action=null;
            if(!valid){sliderDrag[hand]=null;armed[hand]=false;pressed[hand]=null;held[hand]=selectHeld;return false;}
            if(selectHeld&&armed[hand]&&!held[hand]&&hit!=null&&hit.SliderChanged!=null)sliderDrag[hand]=hit;
            if(selectHeld&&sliderDrag[hand]!=null)
            {
                var target=sliderDrag[hand];var localOrigin=target.Surface.InverseTransformPoint(origin);var localDirection=target.Surface.InverseTransformDirection(direction.normalized);
                if(Mathf.Abs(localDirection.z)>.00001f){float travel=-localOrigin.z/localDirection.z;if(travel>=0){float x=(localOrigin+localDirection*travel).x;target.SliderChanged(Mathf.Clamp01(x/target.Size.x+.5f));}}
            }
            if(!selectHeld&&sliderDrag[hand]!=null){sliderDrag[hand]=null;FlushPreferences();}
            if(!selectHeld)
            {
                if(held[hand]&&armed[hand]&&hit!=null&&hit==pressed[hand])action=hit.Action;
                armed[hand]=true;pressed[hand]=null;
            }
            else if(!held[hand]&&armed[hand])pressed[hand]=hit;
            held[hand]=selectHeld;
            bool consumed=hit!=null;if(action!=null)action();
            if(intent!=null)PublishLoadout(intent);return consumed;
        }
        void PublishLoadout(R23LoadoutIntent intent)
        {
            if(Page!=R23UiPage.Loadout)return;
            R23LoadoutOption option=null;foreach(var entry in ownedLoadout.Options)if(entry.Id==intent.Id){option=entry;break;}
            if(option==null)return;
            if(intent.Action==R23LoadoutAction.Equip&&option.CanEquip){if(EquipRequested!=null)EquipRequested(option.Id,intent.Hand);}
            else if(intent.Action==R23LoadoutAction.Drop&&option.CanDrop){if(DropRequested!=null)DropRequested(option.Id);}
            else SetLoadoutStatus("UNAVAILABLE / COMPLETE CURRENT INTERACTION FIRST");
        }
        static bool Finite(Vector3 v) { return !(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z)); }
        void Require() { if(!initialized)throw new InvalidOperationException("Initialize UI with the tracked camera first"); }
        void SetPage(R23UiPage page) { FlushPreferences();Page=page;if(page==R23UiPage.Playing)returnPage=R23UiPage.Pause;Recenter();Build(); }
        public void Recenter()
        {
            Require();
            if(menuAnchor!=null&&(Page==R23UiPage.Ready||Page==R23UiPage.Weapons||Page==R23UiPage.Appearance||Page==R23UiPage.Maps)){panel.SetPositionAndRotation(menuAnchor.position,menuAnchor.rotation);return;}
            Vector3 forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up);if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;forward.Normalize();
            panel.position=view.transform.position+forward*1.6f;panel.rotation=Quaternion.LookRotation(forward,Vector3.up);
        }
        void Build()
        {
            foreach(var b in buttons)if(b.Surface!=null)b.Surface.gameObject.SetActive(false);
            buttons.Clear();labels.Clear();numericRows.Clear();barOrigins.Clear();compass.Clear();loadoutCards.Clear();loadoutStatusText=null;loadoutGesture.Reset();focusButton=null;pressed[0]=pressed[1]=hovered[0]=hovered[1]=null;armed[0]=armed[1]=false;sliderDrag[0]=sliderDrag[1]=null;
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
            MenuCard("HOME",-.335f,"pause0993_3",delegate{if(MainMenuRequested!=null)MainMenuRequested();});
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
        void BuildMaps()
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
        void BuildWeapons()
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
        void BuildLoadout()
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
        void BuildAppearance()
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
        void BuildSettings()
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
        void ChangeMotion(int direction,int field)
        {
            var s=Settings;Publish(new R23UiSettings(s.Turning,field==3?(s.Forward==R23Forward.Head?R23Forward.LeftController:R23Forward.Head):s.Forward,
                s.SnapDegrees+(field==0?direction*5:0),s.SmoothDegreesPerSecond+(field==1?direction*5:0),s.WalkMultiplier+(field==2?direction*.05f:0),s.MasterVolume,s.SfxVolume,s.MusicVolume,s.AmbienceVolume,s.VoiceVolume));
        }
        void ChangeAudio(int field,int direction)
        {
            var s=Settings;float d=direction*.01f;Publish(new R23UiSettings(s.Turning,s.Forward,s.SnapDegrees,s.SmoothDegreesPerSecond,s.WalkMultiplier,
                s.MasterVolume+(field==0?d:0),s.SfxVolume+(field==1?d:0),s.MusicVolume+(field==2?d:0),s.AmbienceVolume+(field==4?d:0),s.VoiceVolume+(field==3?d:0)));
        }
        void Publish(R23UiSettings value,bool dragging=false)
        {
            if(R23UiSettingsCodec.SameValue(Settings,value))return;Settings=value;
            if(dragging){preferenceDirty=true;if(Time.unscaledTime>=nextPreferenceSave){FlushPreferences();nextPreferenceSave=Time.unscaledTime+.1f;}}
            else R23UiPreferences.Save(value);
            if(Page==R23UiPage.Settings&&settingsPage>=0)RefreshSettingsRows();else Build();
            if(SettingsChanged!=null)SettingsChanged(value);
        }
        void FlushPreferences(){if(preferenceDirty){R23UiPreferences.Save(Settings);preferenceDirty=false;}}
        void Back() { AddButton(panel,"BACK",0,-.51f,1.2f,.11f,delegate{SetPage(returnPage);},.033f); }
        static Color Hex(string value){Color color;return ColorUtility.TryParseHtmlString("#"+value,out color)?color:Color.white;}
        static Vector3 FrontPoint(float x,float y){return new Vector3((x/1280f-.5f)*2.2f,(.5f-y/800f)*1.375f,-.01f);}
        TextMesh FrontText(string value,float x,float y,float fontPixels,float widthPixels)
        {return Text(panel,value,FrontPoint(x,y),fontPixels*1.375f/800*.72f,widthPixels*2.2f/1280,Color.white);}
        Button FrontButton(string caption,float x,float y,float width,float height,Action action,Color fill,Color ink,float fontPixels)
        {
            var at=FrontPoint(x+width*.5f,y+height*.5f);var button=AddButton(panel,caption,at.x,at.y,width*2.2f/1280,height*1.375f/800,action,fontPixels*1.375f/800*.72f);
            SetButtonStyle(button,fill,ink);return button;
        }
        void MenuCard(string caption,float y,string icon,Action action)
        {
            var button=AddButton(panel,"",0,y,.94f,.21f,action,.027f);button.Caption.transform.localPosition=new Vector3(0,-.052f,-.004f);button.Caption.text=caption;
            Icon(button.Surface,icon,new Vector3(0,.037f,-.005f),new Vector2(.074f,.074f));
            if(focusButton==null){focusButton=button;SetHovered(button,true);}
        }
        Transform Icon(Transform parent,string key,Vector3 at,Vector2 size)
        {
            Material material;
            if(!iconMaterials.TryGetValue(key,out material))
            {
                var texture=Resources.Load<Texture2D>("R23SourceUI/"+key);
                if(texture==null)throw new InvalidOperationException("Missing source UI icon "+key);
                material=new Material(flatMaterial);materials.Add(material);material.mainTexture=texture;material.renderQueue=4102;iconMaterials.Add(key,material);
            }
            var icon=Quad(parent,"Source icon "+key,at,size,Color.white);var renderer=icon.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.sortingOrder=30;return icon;
        }
        void SetButtonStyle(Button button,Color normal,Color ink)
        {
            button.Normal=normal;button.Hover=Page==R23UiPage.Settings||Page==R23UiPage.Pause||Page==R23UiPage.Loadout?Hex("225d76"):Color.Lerp(normal,Color.white,.15f);
            if(normal.a==0)button.Hover=normal;button.Ink=ink;if(button.Edge!=null)button.Edge.gameObject.SetActive(normal.a>0);SetHovered(button,false);
        }
        static void SetHovered(Button button,bool selected)
        {
            Tint(button,selected?button.Hover:button.Normal);
            if(button.Caption!=null)button.Caption.color=button.Enabled?(selected&&(button.Normal==Hex("252a30")||button.Normal.a==0)?Hex("40e8ef"):button.Ink):Hex("89939a");
            if(button.Edge!=null){var block=new MaterialPropertyBlock();block.SetColor("_Color",selected?Hex("40e8ef"):Hex("89939a"));button.Edge.SetPropertyBlock(block);}
        }
        Button AddButton(Transform parent,string label,float x,float y,float width,float height,Action action,float textHeight=.039f,bool available=true,string ownedId=null)
        {
            Transform surface=Child(parent,label);surface.localPosition=new Vector3(x,y,-.012f);
            var visual=Quad(surface,"Button background",Vector3.zero,new Vector2(width,height),Color.white);
            var caption=Text(surface,label,new Vector3(0,0,-.004f),textHeight,width*.94f,Color.white);
            var edge=Quad(surface,"Button underline",new Vector3(0,-height*.5f+.003f,-.002f),new Vector2(width,.006f),Color.white);
            var button=new Button{Surface=surface,Size=new Vector2(width,height),Action=action,Renderer=visual.GetComponent<Renderer>(),Edge=edge.GetComponent<Renderer>(),Caption=caption,Enabled=available,OwnedId=ownedId};buttons.Add(button);
            SetButtonStyle(button,Page==R23UiPage.Settings||Page==R23UiPage.Pause?Hex("252a30"):Page==R23UiPage.Loadout?Hex("101e26"):Hex("101b31"),Hex("dfe6ea"));return button;
        }
        TextMesh Text(Transform parent,string value,Vector3 at,float height,float width,Color color)
        {
            Transform t=Child(parent,"Label");t.localPosition=at;t.localScale=Vector3.one*.005f;
            var text=t.gameObject.AddComponent<TextMesh>();text.font=font;text.fontSize=96;text.characterSize=1;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.richText=false;text.color=color;text.text=value;
            t.GetComponent<MeshRenderer>().sharedMaterial=fontMaterial;labels.Add(new Label{Text=text,Height=height,MaxWidth=width});return text;
        }
        static Transform Child(Transform parent,string name) { var t=new GameObject(name).transform;t.SetParent(parent,false);return t; }
        Transform Quad(Transform parent,string name,Vector3 at,Vector2 size,Color color)
        {
            Transform t=Child(parent,name);t.localPosition=at;
            var mesh=new Mesh();mesh.name="R23 UI quad";mesh.vertices=new[]{new Vector3(-size.x/2,-size.y/2,0),new Vector3(size.x/2,-size.y/2,0),new Vector3(size.x/2,size.y/2,0),new Vector3(-size.x/2,size.y/2,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.colors=new[]{color,color,color,color};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=flatMaterial;
            // Overlay bars must sort above their backdrop independent of the
            // renderer-centre distance at the left/right edges of the HUD.
            renderer.sortingOrder=name=="SettingsBoard"||name.IndexOf("backdrop",StringComparison.OrdinalIgnoreCase)>=0?0:name=="Button background"?10:name=="Button underline"?11:name.IndexOf("remaining",StringComparison.OrdinalIgnoreCase)>=0?3:name.IndexOf("track",StringComparison.OrdinalIgnoreCase)>=0?2:1;return t;
        }
        static void Tint(Button button,Color color) { var p=new MaterialPropertyBlock();p.SetColor("_Color",color);button.Renderer.SetPropertyBlock(p); }
        void SetBar(Transform bar,float fraction,float width) { if(bar==null)return;float center;if(!barOrigins.TryGetValue(bar,out center)){center=bar.localPosition.x;barOrigins.Add(bar,center);}var scale=bar.localScale;scale.x=Mathf.Max(.0001f,fraction*width);bar.localScale=scale;var at=bar.localPosition;at.x=center-width*(1-fraction)/2;bar.localPosition=at; }
        static string Clean(string value,int max) { string s=(value??"").Replace('\n',' ').Replace('\r',' ').ToUpperInvariant();return s.Length>max?s.Substring(0,max-3)+"...":s; }
        void LateUpdate()
        {
            if(!initialized)return;
            // Measure actual generated glyph bounds: characterSize is not a physical height in Unity.
            foreach(var label in labels)
            {
                var renderer=label.Text.GetComponent<MeshRenderer>();var bounds=renderer.localBounds.size;
                if(bounds.y>.00001f&&bounds.x>.00001f)label.Text.transform.localScale=Vector3.one*Mathf.Min(label.Height/bounds.y,label.MaxWidth/bounds.x);
            }
            if(Page==R23UiPage.Loading){panel.position=view.transform.TransformPoint(new Vector3(0,-.08f,1));panel.rotation=view.transform.rotation;}
            if(Page==R23UiPage.Playing&&compass.Count==24)
            {
                float yaw=view.transform.eulerAngles.y;
                for(int i=0;i<24;i++){float difference=Mathf.DeltaAngle(yaw,i*15);compass[i].gameObject.SetActive(Mathf.Abs(difference)<=65);var at=compass[i].transform.localPosition;at.x=-.02f+difference*.0021f;compass[i].transform.localPosition=at;}
            }
        }
        static void Clear(Transform parent)
        {
            for(int i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i);child.gameObject.SetActive(false);var filters=child.GetComponentsInChildren<MeshFilter>(true);foreach(var f in filters)if(f.sharedMesh!=null&&f.sharedMesh.name=="R23 UI quad")Destroy(f.sharedMesh);Destroy(child.gameObject);}
        }
        void OnDestroy()
        {
            FlushPreferences();
            Font.textureRebuilt-=RefreshFont;
            if(panel!=null)Clear(panel);if(hud!=null){Clear(hud);Destroy(hud.gameObject);}if(shroud!=null){var mesh=shroud.GetComponent<MeshFilter>().sharedMesh;Destroy(mesh);Destroy(shroud.gameObject);}
            foreach(var dot in dots)if(dot!=null)Destroy(dot.GetComponent<MeshFilter>().sharedMesh);
            foreach(var material in materials)Destroy(material);
        }
        void OnDisable(){FlushPreferences();foreach(var beam in beams)if(beam!=null)beam.enabled=false;foreach(var dot in dots)if(dot!=null)dot.gameObject.SetActive(false);if(hud!=null)hud.gameObject.SetActive(false);}
        void OnEnable(){if(initialized)Build();}
    }
}
#endif
