#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ashfall.Integration.Playable.UI
{
    // No InputManager, EventSystem or physics collider dependency. The rig supplies tracked rays.
    public sealed partial class R23VrUI : MonoBehaviour
    {
        sealed class Label { public TextMesh Text;public float Height,MaxWidth;public bool CenterGlyphY;public float TargetY; }
        sealed class Button { public Transform Surface;public Vector2 Size;public Action Action;public Renderer Renderer,Edge,Focus;public Transform Marker;public Renderer[] Frames;public bool Enabled,SelectionEdgeOnly;public string OwnedId,Help;public int SoundId;public Color Normal,Hover,Ink;public TextMesh Caption;public Action<float> SliderChanged; }
        sealed class NumericRow {public Func<string> Caption;public Func<float> Value;public float Min,Max;public TextMesh Text;public Transform Fill,Knob;public float Width;}
        sealed class LoadoutCard {public int Slot;public string Family;public TextMesh Title,Detail;public Button Button;}
        readonly List<Label> labels=new List<Label>();
        readonly List<Button> buttons=new List<Button>();
        readonly List<Material> materials=new List<Material>();
        readonly R23LoadoutGesture loadoutGesture=new R23LoadoutGesture();
        readonly R23MenuAudioEdges soundEdges=new R23MenuAudioEdges();
        R23MenuAudio menuAudio;
        readonly List<LoadoutCard> loadoutCards=new List<LoadoutCard>();
        R23OwnedLoadout ownedLoadout=new R23OwnedLoadout(null);
        TextMesh loadoutStatusText,paddlesStatusText;string loadoutStatus="";
        Func<Vector3> locomotionAnchorProvider;Vector3 lastLoadoutAnchor;bool haveLoadoutAnchor;
        readonly bool[] held=new bool[2],armed=new bool[2];
        readonly Button[] pressed=new Button[2],hovered=new Button[2];
        readonly Transform[] dots=new Transform[2];
        readonly LineRenderer[] beams=new LineRenderer[2];
        readonly Button[] sliderDrag=new Button[2];
        Button focusButton;
        readonly List<NumericRow> numericRows=new List<NumericRow>();
        readonly Dictionary<string,Material> iconMaterials=new Dictionary<string,Material>();
        readonly Dictionary<string,Texture2D> inventoryTextures=new Dictionary<string,Texture2D>();
        readonly Dictionary<string,R23BackpackResource> backpackResources=new Dictionary<string,R23BackpackResource>();
        readonly Dictionary<Transform,float> barOrigins=new Dictionary<Transform,float>();
        Transform shieldGroup,staminaGroup,buildGroup;
        Transform menuAnchor,staminaFill;readonly List<TextMesh> compass=new List<TextMesh>();
        bool preferenceDirty;float nextPreferenceSave;
        Camera view;Transform panel,hud,shroud;Font font;Material fontMaterial,flatMaterial;
        TextMesh title,detail,healthText,shieldText,staminaText,resourceText;
        Transform healthFill,progressFill,shieldFill;
        R23HudSnapshot snapshot;
        readonly List<R23WeaponOption> options=new List<R23WeaponOption>();
        readonly R23MapSelection mapSelection=new R23MapSelection();
        int mapPage;
        readonly List<R23AppearanceOption> appearanceOptions=new List<R23AppearanceOption>();
        int appearancePage,settingsPage=-1,weaponChoice;
        int appearanceRow;float appearanceScroll;
        string appearanceStatus="",appearanceSelected="";
        string calibrationStatus="HOLD STILL TO NORMALIZE HEIGHT";
        string map="MAP",stage="LOADING MAP",failure="";
        string viewedWeaponId,viewedAppearanceId;
        float progress;
        R23UiPage returnPage=R23UiPage.Ready;
        bool initialized;
        public R23UiPage Page { get;private set; }
        public R23UiSettings Settings { get;private set; }
        public R23MenuAudio MenuAudio {get{return menuAudio;}}
        public bool BlocksGameplay { get { return Page!=R23UiPage.Playing; } }
        public bool PointerOverUi { get { return hovered[0]!=null||hovered[1]!=null; } }
        public event Action StartRequested,RestartRequested,ReloadRequested,RecalibrationRequested,MainMenuRequested,MapLobbyRequested;
        public event Action<string> WeaponSelected,AppearanceSelected;
        public event Action<string> WeaponViewed;
        public string CurrentViewedWeaponId {get{return Page==R23UiPage.Weapons?viewedWeaponId:null;}}
        public event Action<string> AppearanceViewed;
        public string CurrentViewedAppearanceId {get{return Page==R23UiPage.Appearance?viewedAppearanceId:null;}}
        public event Action<string,bool> MapSelected;
        public event Action<string> MapChoiceChanged;
        public string SelectedMapId {get{return mapSelection.Selected==null?null:mapSelection.Selected.Id;}}
        public event Action<R23UiSettings> SettingsChanged;
        public event Action<string,int> EquipRequested;
        public event Action<string> DropRequested;
        public event Action<string> ResourceDropRequested;
        public event Action PaddlesRequested;
        string paddlesStatus="";
        public void SetPaddlesStatus(string value)
        {paddlesStatus=Clean(value,80);if(paddlesStatusText!=null){paddlesStatusText.text=paddlesStatus;paddlesStatusText.gameObject.SetActive(!String.IsNullOrEmpty(paddlesStatus));}}
        void RequestPaddles(){CloseBackpack();if(PaddlesRequested!=null)PaddlesRequested();}

        public void Initialize(Camera trackedCamera)
        {
            if(initialized)throw new InvalidOperationException("UI already initialized");
            if(trackedCamera==null)throw new ArgumentNullException("trackedCamera");
            initialized=true;view=trackedCamera;Settings=R23UiPreferences.Load();R25LegacyPack.Begin(gameObject,Settings);
            menuAudio=Child(transform,"R23 owned source menu audio").gameObject.AddComponent<R23MenuAudio>();menuAudio.Initialize(Settings.SfxVolume);
            var shader=Resources.Load<Shader>("R23VrUi");
            if(shader==null)throw new InvalidOperationException("R23VrUi shader resource missing");
            flatMaterial=new Material(shader);materials.Add(flatMaterial);flatMaterial.renderQueue=4101;
            font=Resources.Load<Font>("R23InventoryUI");
            if(font==null)throw new InvalidOperationException("Original Godot UI font missing");
            fontMaterial=new Material(shader);materials.Add(fontMaterial);fontMaterial.SetFloat("_Font",1);fontMaterial.renderQueue=4102;RefreshFont(font);Font.textureRebuilt+=RefreshFont;
            panel=Child(transform,"R23 eye-level menu");
            hud=Child(view.transform,"R23 readable combat HUD");hud.localPosition=new Vector3(0,-.29f,1.1f);
            shroud=Quad(view.transform,"Opaque loading backdrop",new Vector3(0,0,.25f),new Vector2(8,8),Hex(R25Theme.Void));
            var shroudMaterial=new Material(flatMaterial);materials.Add(shroudMaterial);shroudMaterial.renderQueue=4100;shroud.GetComponent<Renderer>().sharedMaterial=shroudMaterial;
            var beamMaterial=new Material(flatMaterial);materials.Add(beamMaterial);beamMaterial.renderQueue=4104;
            for(int i=0;i<2;i++)
            {
                dots[i]=Quad(transform,"UI pointer "+i,Vector3.zero,new Vector2(.014f,.014f),Color.white);dots[i].gameObject.SetActive(false);
                var beam=new GameObject("R23 interactive hand laser "+i);beam.transform.SetParent(transform,false);beams[i]=beam.AddComponent<LineRenderer>();beams[i].sharedMaterial=beamMaterial;
                beams[i].useWorldSpace=true;beams[i].positionCount=2;beams[i].startWidth=.002f;beams[i].endWidth=.001f;beams[i].startColor=Hex(R25Theme.Ember);beams[i].endColor=Hex(R25Theme.Ember);beams[i].sortingOrder=126;beams[i].enabled=false;
            }
            BeginLoading("MAP");
        }
        void RefreshFont(Font rebuilt) { if(rebuilt==font&&fontMaterial!=null)fontMaterial.mainTexture=font.material.mainTexture; }
        public void BeginLoading(string mapName) { Require();map=Clean(mapName,32);progress=0;stage="LOADING MAP";AnchorLoadingLogo();SetPage(R23UiPage.Loading); }
        public void SetLoadingProgress(string phase,float fraction)
        { if(Page!=R23UiPage.Loading)return;stage=Clean(phase,40);progress=Mathf.Clamp01(float.IsNaN(fraction)?0:fraction);if(detail!=null)detail.text="LOADING  "+Mathf.RoundToInt(progress*100)+"%";SetBar(progressFill,progress,.98f); }
        public void ShowReady() { Require();if(Page==R23UiPage.Loading)SetPage(R23UiPage.Ready); }
        public void ShowFailure(string message) { Require();failure=Clean(message,76);SetPage(R23UiPage.Error); }
        public void SetPlaying() { Require();if(snapshot!=null&&snapshot.Health<=0)snapshot=null;SetPage(R23UiPage.Playing); }
        public void ToggleMenu()
        {
            Require();if(Page==R23UiPage.Playing)SetPage(R23UiPage.Pause);
            else if(Page==R23UiPage.Pause||Page==R23UiPage.Settings||Page==R23UiPage.Weapons||Page==R23UiPage.Appearance||Page==R23UiPage.Maps||Page==R23UiPage.Loadout||Page==R23UiPage.SpawnChoice||Page==R23UiPage.MatchRules||Page==R23UiPage.Credits)SetPage(returnPage==R23UiPage.Ready?R23UiPage.Ready:R23UiPage.Playing);
        }
        public void OpenSettings() { settingsPage=-1;OpenSubpage(R23UiPage.Settings); }
        public void OpenWeaponSettings(){settingsPage=2;OpenSubpage(R23UiPage.Settings);}
        // Actual item_catalog_ui_088 follows the locomotion anchor translation,
        // preserving the board's opened yaw/height instead of following tracked head motion.
        public void SetLocomotionAnchorProvider(Func<Vector3> provider)
        {Require();locomotionAnchorProvider=provider;SnapshotLoadoutAnchor();}
        void SnapshotLoadoutAnchor()
        {haveLoadoutAnchor=false;if(Page!=R23UiPage.Loadout||locomotionAnchorProvider==null)return;var at=locomotionAnchorProvider();if(Finite(at)){lastLoadoutAnchor=at;haveLoadoutAnchor=true;}}
        void FollowLoadoutAnchor()
        {
            if(Page!=R23UiPage.Loadout||locomotionAnchorProvider==null){haveLoadoutAnchor=false;return;}
            var at=locomotionAnchorProvider();if(!Finite(at)){haveLoadoutAnchor=false;return;}
            if(haveLoadoutAnchor)panel.position+=at-lastLoadoutAnchor;
            lastLoadoutAnchor=at;haveLoadoutAnchor=true;
        }
        public void SetMenuAnchor(Transform worldAnchor){Require();menuAnchor=worldAnchor;Recenter();}
        public void ClearMenuAnchor(){Require();menuAnchor=null;Recenter();}
        public void SetCalibrationStatus(string value)
        {Require();string status=Clean(value,48);if(status==calibrationStatus)return;calibrationStatus=status;if(Page==R23UiPage.Settings)Build();}
        public void OpenWeapons() { weaponChoice=0;OpenSubpage(R23UiPage.Weapons); }
        // Browse only: the same inspected profile/caption as the arrows, with no equip request.
        public bool BrowseWeapon(string id)
        {
            Require();if(Page!=R23UiPage.Weapons)return false;
            for(int i=0;i<options.Count;i++)if(options[i].Id==id){weaponChoice=i;Build();return true;}
            return false;
        }
        // Legacy host request seam. Source gallery has no visible weapon grant button.
        public bool RequestWeaponSelection(string id)
        {
            Require();foreach(var option in options)if(option.Id==id&&option.Available){if(WeaponSelected!=null)WeaponSelected(id);return true;}return false;
        }
        // Retained input/test seam; gameplay HUD itself has no menu/reload buttons.
        public void RequestReload(){Require();if(Page==R23UiPage.Playing&&ReloadRequested!=null)ReloadRequested();}
        public void OpenLoadout(){loadoutStatus="";OpenSubpage(R23UiPage.Loadout);}
        public void SetBackpackResources(IEnumerable<R23BackpackResource> values)
        {Require();backpackResources.Clear();if(values!=null)foreach(var row in values)if(row!=null)backpackResources[row.Family]=row;if(Page==R23UiPage.Loadout)RefreshLoadoutCards();}
        public void SetOwnedLoadout(IEnumerable<R23LoadoutOption> values,int capacity=3)
        {
            Require();var next=new R23OwnedLoadout(values,capacity);bool changed=!ownedLoadout.SameIdentity(next);ownedLoadout=next;
            if(changed){loadoutGesture.Reset();if(Page==R23UiPage.Loadout)Build();}
            else if(Page==R23UiPage.Loadout)RefreshLoadoutCards();
        }
        public void SetLoadoutStatus(string value)
        {Require();loadoutStatus=Clean(value,55);if(loadoutStatusText!=null)loadoutStatusText.text=loadoutStatus;}
        public void OpenMaps(){ResetMapSearch();RefreshLegacyMaps();OpenSubpage(R23UiPage.Maps);}
        public void SetMapOptions(IEnumerable<R23MapOption> values)
        {
            Require();mapSelection.SetOptions(values);mapPage=Mathf.Min(mapPage,Mathf.Max(0,(mapSelection.Count-1)/4));
            if(Page==R23UiPage.SpawnChoice&&!mapSelection.ChoosingSpawn)SetPage(R23UiPage.Ready);
            else if(Page==R23UiPage.Maps||Page==R23UiPage.Ready||Page==R23UiPage.SpawnChoice)Build();
        }
        // Host synchronization only: no choice, loading, entry or Start event is emitted.
        public bool SetSelectedMap(string id)
        {
            Require();if(!mapSelection.Select(id))return false;
            if(Page==R23UiPage.SpawnChoice)SetPage(R23UiPage.Ready);else if(Page==R23UiPage.Ready||Page==R23UiPage.Maps)Build();return true;
        }
        public bool ChooseMap(string id)
        {
            Require();if(Page!=R23UiPage.Maps||LanMapHostOnly)return false;
            if(!mapSelection.Select(id))return false;
            if(lanState!=Networking.R24LanLobbyState.Offline){SetPage(returnPage==R23UiPage.Ready?R23UiPage.Ready:R23UiPage.Pause);if(MapSelected!=null)MapSelected(SelectedMapId,false);return true;}
            returnPage=R23UiPage.Ready;SetPage(R23UiPage.Ready);
            if(MapChoiceChanged!=null)MapChoiceChanged(SelectedMapId);
            // A map choice enters its lobby, never its game spawn or match rules.
            if(MapSelected!=null)MapSelected(SelectedMapId,false);return true;
        }
        // Compatibility entry seam; no spawn-choice modal is shown anymore.
        public void OpenSpawnChoice(){RequestMapLobby();}
        public bool ConfirmMapSpawn(bool gameSpawn){return RequestMapLobby();}
        public bool RequestMapLobby()
        {
            Require();var request=mapSelection.LobbyEntry();
            if(request==null||LanMapHostOnly||Page==R23UiPage.Loading||Page==R23UiPage.Error||Page==R23UiPage.Dead||MapSelected==null)return false;
            SetPage(R23UiPage.Ready);if(MapChoiceChanged!=null&&lanState==Networking.R24LanLobbyState.Offline)MapChoiceChanged(request.Id);MapSelected(request.Id,false);return true;
        }
        // Both visible START MATCH buttons use the same immutable-rules dispatch.
        public void RequestStart(){Require();RequestMatchStart();}
        // Retained explicit legacy host API; it is not drawn by the new lobby flow.
        public void RequestLegacyStart(){Require();if(StartRequested!=null)StartRequested();}
        readonly R24BackpackCloseGesture backpackCloseGesture=new R24BackpackCloseGesture();
        public bool SampleBackpackClose(bool aHeld,bool eligible)
        {Require();bool consume=backpackCloseGesture.Sample(aHeld,eligible,Page==R23UiPage.Loadout);if(backpackCloseGesture.CloseRequested)CloseBackpack();return consume;}
        public void OpenAppearance() { RefreshLegacySkins();OpenSubpage(R23UiPage.Appearance); }
        void OpenSubpage(R23UiPage page)
        {
            Require();if(Page==R23UiPage.Loading||Page==R23UiPage.Error||Page==R23UiPage.Dead)return;
            if(Page==R23UiPage.Ready)returnPage=R23UiPage.Ready;
            else if(Page==R23UiPage.Playing||Page==R23UiPage.Pause)returnPage=R23UiPage.Pause;
            SetPage(page);
        }
        public void SetSettings(R23UiSettings value)
        { Require();if(value==null)throw new ArgumentNullException("value");FlushPreferences();Settings=value;R25LegacyPack.Apply(value);menuAudio.SetSfxVolume(value.SfxVolume);if(Page==R23UiPage.Settings)Build(); }
        public void SetWeaponOptions(IEnumerable<R23WeaponOption> values)
        {
            Require();string previous=CurrentViewedWeaponId;options.Clear();if(values!=null)foreach(var option in values)if(option!=null)options.Add(option);
            weaponChoice=0;for(int i=0;i<options.Count;i++)if(options[i].Id==previous){weaponChoice=i;break;}
            if(Page==R23UiPage.Weapons)Build();
        }
        public void SetAppearanceOptions(IEnumerable<R23AppearanceOption> values)
        {
            Require();string previous=CurrentViewedAppearanceId??appearanceSelected;
            appearanceOptions.Clear();if(values!=null)foreach(var option in values)if(option!=null)appearanceOptions.Add(option);
            appearancePage=0;for(int i=0;i<appearanceOptions.Count;i++)if(appearanceOptions[i].Id==previous){appearancePage=i;break;}
            if(Page==R23UiPage.Appearance)Build();
        }
        public bool BrowseAppearance(string id)
        {
            Require();if(Page!=R23UiPage.Appearance)return false;for(int i=0;i<appearanceOptions.Count;i++)if(appearanceOptions[i].Id==id&&appearanceOptions[i].Available){appearancePage=i;Build();return true;}return false;
        }
        // Same inspected-card handler; changes the preview, not the worn style.
        public bool CycleAppearance(int direction)
        {
            Require();if(Page!=R23UiPage.Appearance||appearanceOptions.Count==0||direction==0)return false;
            return BrowseAppearance(appearanceOptions[(appearancePage+(direction>0?1:-1)+appearanceOptions.Count)%appearanceOptions.Count].Id);
        }
        public void ScrollOutfits(float axis,float seconds,bool eligible)
        {
            if(Page!=R23UiPage.Appearance||!eligible||float.IsNaN(axis)||float.IsInfinity(axis)||float.IsNaN(seconds)||float.IsInfinity(seconds)||Mathf.Abs(axis)<=.2f)return;
            int maximum=Mathf.Max(0,(appearanceOptions.Count+2)/3-2);
            appearanceScroll=Mathf.Clamp(appearanceScroll-axis*650*Mathf.Clamp(seconds,0,.1f),0,maximum*252);
            int row=Mathf.Clamp(Mathf.RoundToInt(appearanceScroll/252),0,maximum);if(row==appearanceRow)return;appearanceRow=row;Build();
        }
        void ScrollOutfitRow(int direction)
        {appearanceRow=Mathf.Clamp(appearanceRow+direction,0,Mathf.Max(0,(appearanceOptions.Count+2)/3-2));appearanceScroll=appearanceRow*252;Build();}
        // Coordinator-only callable check. Uses the real arrow path and host frame binding;
        // it never calls Preview.Show or changes the player's equipped appearance itself.
        public IEnumerator CheckAppearancePreviewChange(R23AppearancePreview preview,Action<string> report,float timeoutSeconds=30)
        {
            Require();if(preview==null)throw new ArgumentNullException("preview");
            if(appearanceOptions.Count<2)throw new InvalidOperationException("Preview change check requires two real styles");
            var savedPage=Page;int savedChoice=appearancePage;string firstMeshes=null,firstMaterials=null;
            try
            {
                OpenAppearance();if(Page!=R23UiPage.Appearance)throw new InvalidOperationException("Appearance menu unavailable");
                for(int step=0;step<2;step++)
                {
                    if(!CycleAppearance(step==0?1:-1))throw new InvalidOperationException("Outfit card selection rejected");
                    string expected=CurrentViewedAppearanceId;float deadline=Time.realtimeSinceStartup+Mathf.Clamp(timeoutSeconds,1,120);
                    yield return null;yield return null;
                    while(!preview.Ready||preview.Loading||preview.SelectedStyleId!=expected)
                    {
                        if(Page!=R23UiPage.Appearance)throw new InvalidOperationException("Appearance page closed during preview check");
                        if(preview.Failure!=null)throw new InvalidOperationException(preview.Failure);
                        if(Time.realtimeSinceStartup>deadline)throw new InvalidOperationException("Preview did not follow arrow "+expected+"; actual request="+preview.RequestedStyleId+" loaded="+preview.SelectedStyleId+". Check root CurrentViewedAppearanceId binding.");
                        yield return null;
                    }
                    yield return null;var receipt=preview.InspectPresentation();
                    if(receipt==null||receipt.ProfileId!=expected||receipt.RequestedId!=expected||receipt.VisibleRenderers<1||!receipt.HeadVisible)
                        throw new InvalidOperationException("Preview caption changed without a visible committed full-size source asset: "+expected);
                    if(step==0){firstMeshes=receipt.MeshNames;firstMaterials=receipt.MaterialNames;}
                    else if(firstMeshes==receipt.MeshNames&&firstMaterials==receipt.MaterialNames)throw new InvalidOperationException("Both arrow choices retained the same rendered mesh/material identities");
                    if(report!=null)report("PASS outfit selection "+expected+" actualProfile="+receipt.ProfileId+" visibleRenderers="+receipt.VisibleRenderers+" headVisible="+receipt.HeadVisible);
                }
            }
            finally
            {
                appearancePage=savedChoice;
                if(Page==R23UiPage.Appearance)SetPage(savedPage);
            }
        }
        public void SetAppearanceStatus(string selectedId,bool loading,string error=null)
        {
            Require();string status=!string.IsNullOrEmpty(error)?"LOAD FAILED / "+Clean(error,44):(loading?"LOADING APPEARANCE...":"");
            if(appearanceSelected==(selectedId??"")&&appearanceStatus==status)return;
            // Player load status must not rebuild the independent browsed preview or rearm its buttons.
            appearanceSelected=selectedId??"";appearanceStatus=status;
        }
        public void UpdateHud(R23HudSnapshot value)
        {
            Require();if(value==null)throw new ArgumentNullException("value");snapshot=value;
            if(R24MatchLifeText.UseLegacyDeathModal(value.Health<=0,localMatchLife,authoritativeHealthPending)&&Page!=R23UiPage.Loading&&Page!=R23UiPage.Error&&Page!=R23UiPage.Dead)SetPage(R23UiPage.Dead);
            if(healthText!=null)healthText.text=Mathf.CeilToInt(Mathf.Max(0,value.Health)).ToString();
            if(shieldText!=null)shieldText.text=value.Shield>=0?Mathf.CeilToInt(value.Shield).ToString():"";
            if(resourceText!=null)resourceText.text=value.BuildCharges>=999?"\u221e":value.BuildCharges.ToString();
            if(shieldGroup!=null)shieldGroup.gameObject.SetActive(value.Shield>=0);PositionCompactMatchHud();
            if(staminaGroup!=null)staminaGroup.gameObject.SetActive(value.Stamina>=0);
            if(buildGroup!=null)buildGroup.gameObject.SetActive(value.BuildCharges>=0);
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
            UpdateTitleMoment();
            FollowLoadoutAnchor();
            bool valid=tracked&&Finite(origin)&&Finite(direction)&&direction.sqrMagnitude>.0001f;
            UpdateSettingsHelpRay(hand,origin,direction,valid);
            Vector3 hitPoint;var hit=PointerHit(origin,direction,valid,out hitPoint);
            hovered[hand]=hit;
            if(soundEdges.Hover(hand,(int)Page,hit==null?-1:hit.SoundId,valid&&Application.isFocused&&MenuAudioActive()))menuAudio.Play(R23MenuCue.Hover);
            DrawPointer(hand,origin,direction,valid,hit,hitPoint);
            if(hit!=null)focusButton=hit;
            foreach(var b in buttons)SetHovered(b,hovered[0]==b||hovered[1]==b||focusButton==b);

            Vector2 backpackPoint;bool backpackPointValid=ReadBackpackPlane(origin,direction,out backpackPoint);float backpackDelta;
            var intent=loadoutGesture.Sample(hand,hit==null?null:hit.OwnedId,selectHeld,gripHeld,valid,Page==R23UiPage.Loadout,backpackPoint.x,backpackPoint.y,backpackPointValid,creativeCatalog&&backpackPoint.y>=CatalogBottom&&backpackPoint.y<=CatalogTop,out backpackDelta);
            if(backpackDelta!=0&&DragBackpack(backpackDelta)){RefreshSettingsHelp();return true;}
            Action action=null;bool click=intent!=null;
            if(!valid){sliderDrag[hand]=null;armed[hand]=false;pressed[hand]=null;held[hand]=selectHeld;RefreshSettingsHelp();return false;}
            if(selectHeld&&armed[hand]&&!held[hand]&&hit!=null&&hit.SliderChanged!=null)sliderDrag[hand]=hit;
            if(selectHeld&&sliderDrag[hand]!=null)
            {
                var target=sliderDrag[hand];var localOrigin=target.Surface.InverseTransformPoint(origin);var localDirection=target.Surface.InverseTransformDirection(direction.normalized);
                if(Mathf.Abs(localDirection.z)>.00001f){float travel=-localOrigin.z/localDirection.z;if(travel>=0){float x=(localOrigin+localDirection*travel).x;target.SliderChanged(Mathf.Clamp01(x/target.Size.x+.5f));}}
            }
            if(!selectHeld&&sliderDrag[hand]!=null){sliderDrag[hand]=null;FlushPreferences();click=true;}
            if(!selectHeld)
            {
                if(held[hand]&&armed[hand]&&hit!=null&&hit==pressed[hand]){action=Page==R23UiPage.Loadout&&hit.OwnedId!=null?null:hit.Action;click|=action!=null;}
                armed[hand]=true;pressed[hand]=null;
            }
            else if(!held[hand]&&armed[hand])pressed[hand]=hit;
            if(selectHeld||gripHeld)titleInput=true;held[hand]=selectHeld;if(selectHeld)SetPressed(pressed[hand]);
            bool consumed=hit!=null;if(click&&Application.isFocused&&MenuAudioActive())menuAudio.Play(R23MenuCue.Click);if(action!=null)action();
            if(intent!=null)PublishLoadout(intent);RefreshSettingsHelp();return consumed;
        }
        // Presentation only: no held-edge sampling, hover sound, selection,
        // settings callback or item dispatch runs from the late render phase.
        public void RefreshPointerPresentation(int hand,Vector3 origin,Vector3 direction,bool tracked)
        {
            if(!initialized||!isActiveAndEnabled)return;
            if(hand<0||hand>1)throw new ArgumentOutOfRangeException("hand");
            UpdateTitleMoment();
            FollowLoadoutAnchor();bool valid=tracked&&Finite(origin)&&Finite(direction)&&direction.sqrMagnitude>.0001f;
            Vector3 point;var hit=PointerHit(origin,direction,valid,out point);DrawPointer(hand,origin,direction,valid,hit,point);
        }
        Button PointerHit(Vector3 origin,Vector3 direction,bool valid,out Vector3 hitPoint)
        {
            Button hit=null;hitPoint=Vector3.zero;float best=float.PositiveInfinity;
            if(valid)foreach(var b in buttons)
            {
                if(!b.Enabled||!b.Surface.gameObject.activeInHierarchy)continue;
                Vector3 localOrigin=b.Surface.InverseTransformPoint(origin),localDirection=b.Surface.InverseTransformDirection(direction.normalized);
                if(Mathf.Abs(localDirection.z)<.00001f)continue;
                float t=-localOrigin.z/localDirection.z;if(t<0||t>8||t>=best)continue;
                var at=localOrigin+localDirection*t;if(Mathf.Abs(at.x)>b.Size.x*.5f||Mathf.Abs(at.y)>b.Size.y*.5f)continue;
                var worldPoint=b.Surface.TransformPoint(new Vector3(at.x,at.y,-.004f));if(!BackpackCatalogPointVisible(b,worldPoint))continue;best=t;hit=b;hitPoint=worldPoint;
            }
            return hit;
        }
        void DrawPointer(int hand,Vector3 origin,Vector3 direction,bool valid,Button hit,Vector3 hitPoint)
        {
            bool showBeam=valid&&Page!=R23UiPage.Playing&&Page!=R23UiPage.Loading&&Page!=R23UiPage.Error;
            dots[hand].gameObject.SetActive(showBeam&&hit!=null);
            if(showBeam&&hit!=null){dots[hand].position=hitPoint;dots[hand].rotation=hit.Surface.rotation;}
            beams[hand].enabled=showBeam;
            if(showBeam){beams[hand].SetPosition(0,origin);beams[hand].SetPosition(1,hit!=null?hitPoint:origin+direction.normalized*2.5f);}
        }
        void PublishLoadout(R23LoadoutIntent intent)
        {
            if(Page!=R23UiPage.Loadout)return;
            if(PublishCreative(intent))return;
            if(intent.Id=="backpack/drop-owned"){if(canDropOwnedBackpack&&BackpackDropRequested!=null)BackpackDropRequested();else SetLoadoutStatus(ownedBackpackCount==0?"NO EXTRA BACKPACK":"FREE ONE SLOT BEFORE DROPPING A BACKPACK");return;}
            if(intent.Id=="backpack/close"){CloseBackpack();return;}if(intent.Id=="utility/revive"){RequestPaddles();return;}
            if(intent.Id.StartsWith("reserve/",StringComparison.Ordinal))
            {R23BackpackResource row;if(backpackResources.TryGetValue(intent.Id.Substring(8),out row)&&row.CanDrop&&ResourceDropRequested!=null)ResourceDropRequested(row.Family);return;}
            R23LoadoutOption option=null;foreach(var entry in ownedLoadout.Options)if(entry.Id==intent.Id){option=entry;break;}
            if(option==null)return;
            if(intent.Action==R23LoadoutAction.Equip&&option.CanEquip){CloseBackpack();if(EquipRequested!=null)EquipRequested(option.Id,intent.Hand);}
            else if(intent.Action==R23LoadoutAction.Drop&&option.CanDrop){if(DropRequested!=null)DropRequested(option.Id);}
            else SetLoadoutStatus("UNAVAILABLE / COMPLETE CURRENT INTERACTION FIRST");
        }
        static bool Finite(Vector3 v) { return !(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z)); }
        void Require() { if(!initialized)throw new InvalidOperationException("Initialize UI with the tracked camera first"); }
        void SetPage(R23UiPage page) { FlushPreferences();if(page!=R23UiPage.Pause&&page!=R23UiPage.Dead)homeConfirm=false;if(page!=R23UiPage.SpawnChoice)mapSelection.CancelSpawn();Page=page;if(page==R23UiPage.Playing)returnPage=R23UiPage.Pause;Recenter();Build(); }
        public void Recenter()
        {
            Require();
            if(Page==R23UiPage.Loading){panel.SetPositionAndRotation(loadingPosition,loadingRotation);return;}
            if(menuAnchor!=null&&(Page==R23UiPage.Ready||Page==R23UiPage.Weapons||Page==R23UiPage.Appearance||Page==R23UiPage.Maps||Page==R23UiPage.SpawnChoice||Page==R23UiPage.MatchRules||Page==R23UiPage.Credits)){panel.SetPositionAndRotation(menuAnchor.position,menuAnchor.rotation);return;}
            Vector3 forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up);if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;forward.Normalize();
            panel.position=view.transform.position+forward*1.6f;panel.rotation=Quaternion.LookRotation(forward,Vector3.up);
            SnapshotLoadoutAnchor();
        }
        void BuildPage()
        {
            if(Page!=R23UiPage.Weapons)PublishWeaponViewed(null);
            if(Page!=R23UiPage.Appearance)PublishAppearanceViewed(null);
            foreach(var b in buttons)if(b.Surface!=null)b.Surface.gameObject.SetActive(false);
            ClearSettingsHelp();buttons.Clear();labels.Clear();numericRows.Clear();barOrigins.Clear();compass.Clear();loadoutCards.Clear();loadoutStatusText=paddlesStatusText=null;backpackCatalogSurfaces.Clear();if(!preserveBackpackGestureForScroll)loadoutGesture.Reset();focusButton=null;pressed[0]=pressed[1]=hovered[0]=hovered[1]=null;armed[0]=armed[1]=false;sliderDrag[0]=sliderDrag[1]=null;
            for(int i=0;i<2;i++){dots[i].gameObject.SetActive(false);beams[i].enabled=false;}
            Clear(panel);Clear(hud);ClearLocalMatchGhostHudReferences();ClearCompactMatchHudReferences();title=detail=healthText=shieldText=staminaText=resourceText=null;healthFill=progressFill=shieldFill=staminaFill=null;shieldGroup=staminaGroup=buildGroup=null;
            shroud.gameObject.SetActive(Page==R23UiPage.Loading||Page==R23UiPage.Error);
            panel.gameObject.SetActive(Page!=R23UiPage.Playing);hud.gameObject.SetActive(Page==R23UiPage.Playing);
            if(Page==R23UiPage.Playing){BuildHud();if(snapshot!=null)UpdateHud(snapshot);return;}
            if(Page==R23UiPage.Loading)
            {
                BuildLoadingLogo();return;
            }
            bool popup=Page==R23UiPage.Settings||Page==R23UiPage.Pause||Page==R23UiPage.Dead||Page==R23UiPage.Error;
            if(popup&&Page!=R23UiPage.Dead)Quad(panel,"SettingsBoard",Vector3.zero,new Vector2(1.3f,1.48f),Hex(R25Theme.Panel));
            if(Page==R23UiPage.Loadout)Quad(panel,"Backpack backdrop",Vector3.zero,new Vector2(1.2f,creativeCatalog?1.58f:.76f),Hex(R25Theme.Raised));
            BuildThemePanel(popup);
            title=Text(panel,"",new Vector3(0,.61f,-.01f),Page==R23UiPage.Loadout?.0288f:.042f,1.16f,Hex(R25Theme.Text));
            if(Page==R23UiPage.Error){title.text="COULD NOT LOAD";Text(panel,failure,new Vector3(0,.16f,-.01f),.03f,1.12f,Hex(R25Theme.Text));return;}
            if(Page==R23UiPage.Credits){BuildCredits();return;}
            if(Page==R23UiPage.Settings){BuildSettings();return;}
            if(Page==R23UiPage.Weapons){BuildWeapons();return;}
            if(Page==R23UiPage.Loadout){BuildLoadout();return;}
            if(Page==R23UiPage.Appearance){BuildAppearance();return;}
            if(Page==R23UiPage.Maps){BuildMaps();return;}
            if(Page==R23UiPage.SpawnChoice){BuildReady();return;}
            if(Page==R23UiPage.MatchRules){BuildMatchRules();return;}
            if(Page==R23UiPage.Dead&&homeConfirm){BuildHomeConfirm();return;}
            if(Page==R23UiPage.Dead&&localMatchLife!=null){BuildMatchLifePage();return;}
            if(Page==R23UiPage.Dead){title.text="YOU DIED";MenuCard("RESTART",.40f,"pause0993_0",delegate{if(RestartRequested!=null)RestartRequested();});MenuCard(HomeCaption(),-.09f,"pause0993_3",RequestHome);return;}
            if(Page==R23UiPage.Ready){BuildReady();return;}
            BuildPause(); // R26VrUI.Rooms.cs: shared match actions, HOME confirmation, bottom BACK
        }
        void BuildReady()
        {
            if(friendsOpen||InRoomUi){BuildFriends();return;} // a room always shows its panel in the menu city
            title.text="";
            FrontButton("MULTIPLAYER",340,374,600,88,OpenFriends,Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber),32);
            FrontText(mapSelection.Selected==null?map:Clean(mapSelection.Selected.Label,44),640,485,17,350);
            FrontButton("MAP SELECT",465,506,350,58,OpenMaps,Hex(R25Theme.Raised),Hex(R25Theme.Text),24);
            var ready=FrontButton(LanMapHostOnly?"WAIT FOR HOST":StartActionLabel,465,574,350,88,RequestStart,Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber),38);ready.Enabled=CanStartSelectedMatch;if(!ready.Enabled)SetButtonStyle(ready,Hex(R25Theme.Disabled),Hex(R25Theme.TextDisabled));
            FrontButton("CREDITS",240,726,170,48,OpenCredits,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            FrontButton("SETTINGS",40,726,170,48,OpenSettings,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            FrontButton("WEAPONS",450,712,180,64,OpenWeapons,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            FrontButton("OUTFITS",650,712,180,64,OpenAppearance,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            FrontButton("MATCH RULES",970,655,270,64,OpenMatchRules,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            BuildValleyCampaignActions();FrontText(NextMatchCaption(),640,686,17,650).color=Hex(R25Theme.TextDim);
        }
        void BuildHud()
        {
            // Keep angular meter/text size while moving the HUD farther and slightly down.
            hud.localPosition=new Vector3(0,-.04f,0);hud.localScale=Vector3.one*1.2f;
            Quad(hud,"Health track",new Vector3(-.02f,-.456f,.804f),new Vector2(.29f,.019f),Hex(R25Theme.HudHealthTrack));
            healthFill=Quad(hud,"Health remaining",new Vector3(-.02f,-.456f,.802f),new Vector2(1,.019f),Hex(R25Theme.HudHealth));
            healthText=Text(hud,"",new Vector3(-.159f,-.456f,.798f),.014f,.21f,Hex(R25Theme.Text));healthText.anchor=TextAnchor.MiddleLeft;CenterHudGlyphs(healthText,-.456f);
            shieldGroup=Child(hud,"Shield meter");
            Quad(shieldGroup,"Shield track",new Vector3(-.02f,-.433f,.804f),new Vector2(.29f,.019f),Hex(R25Theme.HudShieldTrack));
            shieldFill=Quad(shieldGroup,"Shield remaining",new Vector3(-.02f,-.433f,.802f),new Vector2(1,.019f),Hex(R25Theme.HudShield));
            shieldText=Text(shieldGroup,"",new Vector3(-.159f,-.433f,.798f),.014f,.21f,Hex(R25Theme.Text));shieldText.anchor=TextAnchor.MiddleLeft;CenterHudGlyphs(shieldText,-.433f);
            staminaGroup=Child(hud,"Sprint meter");
            Quad(staminaGroup,"Stamina track",new Vector3(-.02f,-.477f,.803f),new Vector2(.29f,.007f),Hex(R25Theme.HudStaminaTrack));
            staminaFill=Quad(staminaGroup,"Stamina remaining",new Vector3(-.02f,-.477f,.797f),new Vector2(1,.007f),Hex(R25Theme.HudStamina));
            Icon(staminaGroup,"sprint_r4",new Vector3(-.177f,-.477f,.794f),new Vector2(.015f,.015f));
            buildGroup=Child(hud,"Build resources");
            Quad(buildGroup,"Build resource face",new Vector3(.147f,-.4445f,.806f),new Vector2(.039f,.042f),Hex(R25Theme.HudBuildTrack));
            resourceText=Text(buildGroup,"",new Vector3(.145f,-.429f,.798f),.016f,.038f,Hex(R25Theme.Text));
            for(int x=0;x<3;x++)for(int y=0;y<3;y++)Quad(buildGroup,"Build square",new Vector3(.137f+x*.007f,-.446f-y*.006f,.798f),new Vector2(.006f,.005f),Hex(R25Theme.HudBuild));
            for(int i=0;i<24;i++){var mark=Text(hud,i%3==0?new[]{"N","NE","E","SE","S","SW","W","NW"}[i/3]:"\u00b7",new Vector3(0,-.499f,.799f),i%3==0?.013f:.009f,.05f,Hex(R25Theme.Text));compass.Add(mark);}
            // Constrain first-frame fills before the first live HUD snapshot arrives.
            SetBar(healthFill,0,.29f);SetBar(shieldFill,0,.29f);SetBar(staminaFill,0,.29f);
            BuildLocalMatchGhostHud();
            BuildCompactMatchHud();
            // Gameplay has meters/readouts only. Menu buttons are deliberately absent.
        }
        void BuildMaps(){BuildMapSearchPicker();}
        void MapThumbnail(R23MapOption option,float x,float y)
        {
            string resource=SourceMapThumbnailResource(option);if(string.IsNullOrEmpty(resource))return;
            var texture=R25LegacyPack.IsMap(option.Id)?R25LegacyPack.Thumbnail(option.Id.Substring(8)):Resources.Load<Texture2D>(resource);if(texture==null)return;
            Material material;string key="map:"+resource;
            if(!iconMaterials.TryGetValue(key,out material))
            {material=new Material(Resources.Load<Shader>("R23SourceIcon"));material.SetTexture("_IconTex",texture);material.renderQueue=4102;materials.Add(material);iconMaterials.Add(key,material);}
            var picture=Quad(panel,"Source map thumbnail "+option.Id,FrontPoint(x+220,y+85),new Vector2(440f/1280*2.2f,170f/800*1.375f),Color.white);
            var filter=picture.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
            float aspect=(float)texture.width/texture.height,target=440f/170;
            float u=aspect>target?target/aspect:1,v=aspect>target?1:aspect/target;
            mesh.uv=new[]{new Vector2((1-u)/2,(1-v)/2),new Vector2((1+u)/2,(1-v)/2),new Vector2((1+u)/2,(1+v)/2),new Vector2((1-u)/2,(1+v)/2)};
            var renderer=picture.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.sortingOrder=12;
        }
        static string SourceMapThumbnailResource(R23MapOption option)
        {return option.Id=="valley"||option.Id=="range"||option.Id=="cyber_bird"?"R23MapThumbnails/"+option.Id:option.ThumbnailResource;}
        void BuildWeapons()
        {
            // Verified R24 R5 front_menu1000._layout_skin_r9 weapon page.
            title.text="";
            FrontButton("WEAPONS",400,710,230,54,OpenWeapons,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            FrontButton("OUTFITS",650,710,230,54,OpenAppearance,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            if(options.Count>0)
            {
                weaponChoice=Mathf.Clamp(weaponChoice,0,options.Count-1);var option=options[weaponChoice];
                FrontText(Clean(option.Label,44),640,579,34,760);
                FrontButton("<",350,610,210,78,delegate{weaponChoice=(weaponChoice+options.Count-1)%options.Count;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),46);
                FrontButton(">",720,610,210,78,delegate{weaponChoice=(weaponChoice+1)%options.Count;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),46);
            }
            else FrontText("NO WEAPONS AVAILABLE",640,565,24,600);
            FrontButton("BACK",40,720,170,54,delegate{SetPage(returnPage);},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            PublishWeaponViewed(options.Count==0?null:options[weaponChoice].Id);
        }
        void PublishWeaponViewed(string id)
        {if(viewedWeaponId==id)return;viewedWeaponId=id;if(WeaponViewed!=null)WeaponViewed(id);}
        void BuildLoadout()
        {
            // Actual non-testing item_catalog_ui_088:960x512 /1.2x.64m,
            // four columns, three pinned rows; inventory cells0/1/4/5/8/9.
            title.text="BACKPACK";title.transform.localPosition=new Vector3(-.03f,creativeCatalog?.67f:.27f,-.01f);
            var close=AddButton(panel,"X",.55f,creativeCatalog?.67f:.27f,.06f,.06f,CloseBackpack,.027f,ownedId:"backpack/close");SetButtonStyle(close,Hex(R25Theme.Raised),Hex(R25Theme.DangerText));close.SelectionEdgeOnly=true;SetHovered(close,false);
            var bag=AddButton(panel,"DROP BAG x"+ownedBackpackCount,-.455f,creativeCatalog?.67f:.27f,.25f,.0525f,null,.0144f,canDropOwnedBackpack,"backpack/drop-owned");
            bag.Caption.transform.localPosition=new Vector3(.021f,0,-.004f);bag.SelectionEdgeOnly=true;SetHovered(bag,false);
            Icon(bag.Surface,"backpack_r23_backpack",new Vector3(-.098f,0,-.005f),new Vector2(.042f,.028f));
            int[] inventoryCells={0,1,4,5,8,9};string[] families={"pistol","rifle","shells","sniper","smg","builds"};int[] resourceCells={2,3,6,7,10,11};
            for(int cell=0;cell<12;cell++)
            {
                int slot=Array.IndexOf(inventoryCells,cell),resource=Array.IndexOf(resourceCells,cell);var option=slot>=0&&slot<ownedLoadout.Capacity?ownedLoadout.Slot(slot):null;
                string family=resource>=0?families[resource]:null,key=family!=null?"supply/"+family:option==null?null:option.ProfileId;
                // GridContainer expands the source220px minimum to226px:
                // (960 - outer16*2 - gaps8*3)/4. Coordinates below retain
                // the actual child Control origins, not centered ink guesses.
                var card=AddButton(panel,"",-.43875f+cell%4*.2925f,(creativeCatalog?.495f:.1575f)-cell/4*.155f,.2825f,.145f,null,.0144f,false,family!=null?"reserve/"+family:option==null?null:option.Id);
                card.SelectionEdgeOnly=true;
                if(slot>=0){var number=Text(card.Surface,(slot+1).ToString(),new Vector3(-.13125f,.065f,-.005f),.0144f,.035f,Hex(R25Theme.TextDim));number.anchor=TextAnchor.UpperLeft;number.alignment=TextAlignment.Left;}
                // Source VBox: margins(6,5,6,4), picture75px, separation4px;
                // caption starts at y84. Its centered16px text is not a badge.
                if(!string.IsNullOrEmpty(key))InventoryIcon(card.Surface,key,new Vector3(0,.019375f,-.006f),.2675f,.09375f,option==null?1:option.Stars);
                var caption=Text(card.Surface,"",new Vector3(0,-.0325f,-.007f),.0144f,.2675f,Hex(R25Theme.Text));caption.anchor=TextAnchor.UpperCenter;
                // Label.position is its TOP-left source origin: slot(176,46),
                // reserve(9,75), font26. Preserve it without a line-box-center offset.
                var count=Text(card.Surface,"",new Vector3(family==null?.07875f:-.13f,family==null?.015f:-.02125f,-.009f),.0234f,family==null?.075f:.125f,Hex(R25Theme.Text));count.anchor=TextAnchor.UpperLeft;count.alignment=TextAlignment.Left;
                loadoutCards.Add(new LoadoutCard{Slot=slot,Family=family,Title=caption,Detail=count,Button=card});
            }
            if(!creativeCatalog){
            var revive=AddButton(panel,"REVIVE PADDLES",.455f,-.33f,.25f,.045f,RequestPaddles,.0144f,ownedId:"utility/revive");revive.Caption.transform.localPosition=new Vector3(.024f,0,-.004f);revive.SelectionEdgeOnly=true;SetHovered(revive,false);
            Icon(revive.Surface,"backpack_r23_revive",new Vector3(-.098f,0,-.005f),new Vector2(.042f,.028f));
            }
            BuildCreativeCatalog();
            RefreshLoadoutCards();loadoutStatusText=Text(panel,string.IsNullOrEmpty(loadoutStatus)?"RELEASE GRIP TO EQUIP  |  RELEASE TRIGGER TO DROP  |  DRAG TO SCROLL":loadoutStatus,new Vector3(0,creativeCatalog?-.735f:-.274f,-.01f),.0144f,1.16f,Hex(R25Theme.TextDim));
            paddlesStatusText=Text(panel,paddlesStatus,new Vector3(-.14f,creativeCatalog?-.765f:-.33f,-.01f),.0144f,.90f,Hex(R25Theme.TextDim));paddlesStatusText.gameObject.SetActive(!String.IsNullOrEmpty(paddlesStatus));
        }
        void CloseBackpack(){SetPage(returnPage==R23UiPage.Ready?R23UiPage.Ready:R23UiPage.Playing);}
        void RefreshLoadoutCards()
        {
            if(Page!=R23UiPage.Loadout)return;
            foreach(var card in loadoutCards)
            {
                if(card.Family!=null)
                {
                    R23BackpackResource row;backpackResources.TryGetValue(card.Family,out row);card.Title.text=card.Family=="rifle"?"ASSAULT":card.Family=="shells"?"SHOTGUN":card.Family.ToUpperInvariant();
                    card.Detail.text=row==null?"\u2014":row.Count>=(card.Family=="builds"?999:100000)?"\u221e":"x"+row.Count;card.Button.Enabled=row!=null&&row.CanDrop;continue;
                }
                var option=card.Slot<ownedLoadout.Capacity?ownedLoadout.Slot(card.Slot):null;
                card.Title.text=option==null?(card.Slot>=ownedLoadout.Capacity?"LOCKED":"EMPTY"):Ashfall.Integration.Playable.Input.R23InventoryPresentation.Caption(option.ProfileId,option.Label,option.Stars);
                card.Title.color=option!=null&&Ashfall.Integration.Playable.Input.R23InventoryPresentation.Tiered(option.ProfileId)?Hex(Ashfall.Integration.Playable.Input.R23InventoryPresentation.RarityColor(option.Stars)):Hex(R25Theme.Text);
                card.Detail.text=option!=null&&option.Count>1?"x"+option.Count:"";card.Button.Enabled=option!=null&&(option.CanEquip||option.CanDrop);
            }
            foreach(var card in loadoutCards)SetHovered(card.Button,hovered[0]==card.Button||hovered[1]==card.Button||focusButton==card.Button);
            if(title!=null)title.text="BACKPACK";
        }
        void InventoryIcon(Transform parent,string key,Vector3 at,float width,float height,int stars)
        {
            if(key=="backpack")
            {Icon(parent,"backpack_r23_backpack",at,new Vector2(Mathf.Min(width,height*1.5f),height));return;}
            Texture2D texture;if(!inventoryTextures.TryGetValue(key,out texture))
            {var bytes=Ashfall.Integration.Playable.Input.R23WeaponWheelIcons.Read(key);texture=null;if(bytes!=null){texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false);if(!texture.LoadImage(bytes,true))throw new InvalidOperationException("Original inventory icon "+key);texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;}inventoryTextures[key]=texture;}
            if(texture==null)return;string materialKey="inventory/"+key+"/"+stars;Material material;
            if(!iconMaterials.TryGetValue(materialKey,out material)){material=new Material(Resources.Load<Shader>("R23SourceIcon"));material.SetTexture("_IconTex",texture);material.SetColor("_Tint",Ashfall.Integration.Playable.Input.R23InventoryPresentation.Tiered(key)&&!key.StartsWith("supply/",StringComparison.Ordinal)?Hex(Ashfall.Integration.Playable.Input.R23InventoryPresentation.RarityColor(stars)):Hex(R25Theme.Text));material.renderQueue=4102;materials.Add(material);iconMaterials[materialKey]=material;}
            float ratio=(float)texture.width/texture.height;var size=new Vector2(Mathf.Min(width,height*ratio),Mathf.Min(height,width/ratio));var icon=Quad(parent,"Original backpack picture "+key,at,size,Color.white);
            // These source pictures face down in the backpack. Rotate the
            // picture only; weapon wheels and held meshes keep their own frame.
            if(key=="knife"||key=="katana"||key=="excalibur"||key=="cyberblade")icon.transform.localRotation=Quaternion.Euler(0,0,180);
            var renderer=icon.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.sortingOrder=30;
        }
        void BuildAppearance()
        {
            // R24 R5 outfit_browser: three named screenshot columns, two visible rows.
            // Row scrolling retains full readable cards within the physical board viewport.
            title.text="";FrontText("OUTFITS",250,110,32,360);
            if(appearanceOptions.Count>0)
            {
                appearancePage=Mathf.Clamp(appearancePage,0,appearanceOptions.Count-1);appearanceRow=Mathf.Clamp(appearanceRow,0,Mathf.Max(0,(appearanceOptions.Count+2)/3-2));var choice=appearanceOptions[appearancePage];
                for(int slot=0;slot<6;slot++)
                {
                    int index=appearanceRow*3+slot;if(index>=appearanceOptions.Count)break;var card=appearanceOptions[index];float x=70+184*(slot%3),y=165+252*(slot/3);
                    var button=FrontButton("",x,y,174,240,delegate{BrowseAppearance(card.Id);},Hex(index==appearancePage?R25Theme.RaisedSelected:R25Theme.Raised),Hex(R25Theme.Text),15);button.Enabled=card.Available;button.Frames=ThemeFrame(button.Surface,button.Size,Hex(R25Theme.Stroke));SetHovered(button,false);
                    OutfitThumbnail(card,x+87,y+95.5f);
                    string caption=card.Label.Replace(" \u00b7 ","\n");if(caption.IndexOf('\n')<0&&caption.Length>21){int split=caption.LastIndexOf(' ',Mathf.Min(21,caption.Length-1));if(split>0)caption=caption.Substring(0,split)+"\n"+caption.Substring(split+1);}
                    var captionText=FrontText(caption,x+87,y+210,17,162);button.Caption=captionText;SetHovered(button,false);
                }
                FrontText(Clean(choice.Label,70),940,660,26,520);
                var choose=FrontButton("USE CHARACTER",760,710,380,58,delegate{if(choice.Available&&AppearanceSelected!=null)AppearanceSelected(choice.Id);},Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber),24);choose.Enabled=choice.Available;
                if(appearanceOptions.Count>6){FrontButton("\u25b2",616,165,24,58,delegate{ScrollOutfitRow(-1);},Hex(R25Theme.RaisedSelected),Hex(R25Theme.Text),18);FrontButton("\u25bc",616,617,24,58,delegate{ScrollOutfitRow(1);},Hex(R25Theme.RaisedSelected),Hex(R25Theme.Text),18);}
            }
            FrontButton("BACK",70,710,200,58,delegate{SetPage(returnPage);},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            FrontButton("WEAPONS",320,710,230,58,OpenWeapons,Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            PublishAppearanceViewed(appearanceOptions.Count==0?null:appearanceOptions[appearancePage].Id);
        }
        void OutfitThumbnail(R23AppearanceOption option,float x,float y)
        {
            if(option.SourceIndex<0)return;string key="R24OutfitThumbnails/"+option.SourceIndex.ToString("D3");var texture=R25LegacyPack.IsSkin(option.Id)?R25LegacyPack.Thumbnail(option.Id):Resources.Load<Texture2D>(key);if(texture==null)return;
            Material material;if(!iconMaterials.TryGetValue(key,out material)){material=new Material(Resources.Load<Shader>("R23SourceIcon"));material.SetTexture("_IconTex",texture);material.renderQueue=4102;materials.Add(material);iconMaterials.Add(key,material);}
            float aspect=(float)texture.width/texture.height,width=Mathf.Min(160,177*aspect),height=Mathf.Min(177,160/aspect);
            var picture=Quad(panel,"Verified R5 outfit screenshot "+option.SourceIndex,FrontPoint(x,y),new Vector2(width/1280*2.2f,height/800*1.375f),Color.white);var renderer=picture.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.sortingOrder=12;
        }
        void PublishAppearanceViewed(string id)
        {if(viewedAppearanceId==id)return;viewedAppearanceId=id;if(AppearanceViewed!=null)AppearanceViewed(id);}
        void RefreshLegacyMaps()
        {var rows=new List<R23MapOption>();foreach(var row in World.R23MapCatalog.AvailableMaps)rows.Add(new R23MapOption(row.Id,row.Label));SetMapOptions(rows);}
        void RefreshLegacySkins()
        {var body=UnityEngine.Object.FindAnyObjectByType<Appearance.R23PlayerAppearance>();if(body==null)return;var rows=new List<R23AppearanceOption>();foreach(var row in body.AvailableStyles)if(row.SourceIndex<27||R25LegacyPack.SkinsEnabled)rows.Add(new R23AppearanceOption(row.Id,row.Label,sourceIndex:row.SourceIndex));SetAppearanceOptions(rows);}
        void BuildLegacySettings()
        {
            title.text="ASHFALL / LEGACY";
            string[] names={"RETIRED MAPS","RETIRED SKINS","RETIRED SOUNDS"};
            bool[] available={R25LegacyPack.Policy.HasMaps,R25LegacyPack.Policy.HasSkins,R25LegacyPack.Policy.HasSounds};
            bool[] on={Settings.RetiredMaps,Settings.RetiredSkins,Settings.RetiredSounds};
            for(int i=0;i<3;i++){int part=i;float y=.32f-i*.24f;
                var button=AddButton(panel,names[i]+"    "+(available[i]?(on[i]?"ON":"OFF"):"UNAVAILABLE"),0,y,1.08f,.09f,delegate{Publish(Settings.With(retiredMaps:part==0?(bool?)!Settings.RetiredMaps:null,retiredSkins:part==1?(bool?)!Settings.RetiredSkins:null,retiredSounds:part==2?(bool?)!Settings.RetiredSounds:null));R25LegacyPack.Apply(Settings);Build();},.028f);
                button.Enabled=available[i];SetButtonStyle(button,Hex(R25Theme.Raised),Hex(available[i]?R25Theme.Text:R25Theme.TextDisabled));
                Text(panel,available[i]?"Private legacy content. On by default.":"This part is absent from the installed pack.",new Vector3(0,y-.075f,-.025f),.019f,1.12f,Hex(R25Theme.TextDisabled));}
            AddButton(panel,"BACK TO SETTINGS",0,-.56f,.90f,.08f,delegate{settingsPage=-1;Build();},.026f);
        }
        void BuildSettings()
        {
            if(settingsPage==NameKeyboardSettingsPage){BuildNameKeyboard();return;} // NAMES: on-screen keyboard (R26VrUI.PlayerName.cs)
            if(settingsPage==3){BuildLegacySettings();return;}
            title.text=settingsPage<0?"SETTINGS":settingsPage==0?"ASHFALL / MOVEMENT":settingsPage==2?"ASHFALL / WEAPONS":"ASHFALL / GENERAL";
            if(settingsPage<0)
            {
                MenuCard("LOCOMOTION",.40f,"settings_0",delegate{settingsPage=0;Build();});
                MenuCard("SET HEIGHT",.155f,"settings_1",delegate{if(RecalibrationRequested!=null)RecalibrationRequested();});
                MenuCard("WEAPONS",-.09f,"settings_2",OpenWeaponSettings);
                MenuCard("GENERAL",-.335f,"settings_3",delegate{settingsPage=1;Build();});
                if(R25LegacyPack.Present)AddButton(panel,"LEGACY",0,-.52f,.90f,.075f,delegate{settingsPage=3;Build();},.028f);
                BuildSettingsHelp(false,true,R25LegacyPack.Present?null:calibrationStatus);
                AddButton(panel,"BACK",0,-.64f,.90f,.08f,delegate{SetPage(returnPage);},.026f);return;
            }
            if(settingsPage==0)
            {
                var forward=AddButton(panel,"",0,.42f,.90f,.065f,delegate{ChangeMotion(1,3);},.03f);PreparePersonalChoice(forward,"forward");
                numericRows.Add(new NumericRow{Caption=delegate{return "FORWARD MOTION    "+(Settings.Forward==R23Forward.Head?"HEAD HEADING":"LEFT CONTROLLER HEADING");},Text=forward.Caption});
                var turn=AddButton(panel,"",0,.305f,.90f,.065f,delegate{Publish(Settings.With(turning:Settings.Turning==R23Turning.Smooth?R23Turning.Snap:R23Turning.Smooth));},.03f);PreparePersonalChoice(turn,"turning");
                numericRows.Add(new NumericRow{Caption=delegate{return "TURNING    "+Settings.Turning.ToString().ToUpperInvariant();},Text=turn.Caption});
                AddNumericRow(delegate{return "SNAP ANGLE    "+Mathf.RoundToInt(Settings.SnapDegrees)+" deg";},delegate{return Settings.SnapDegrees;},15,180,5,.19f,0);
                AddNumericRow(delegate{return "SMOOTH TURN    "+Mathf.RoundToInt(Settings.SmoothDegreesPerSecond)+" deg/s";},delegate{return Settings.SmoothDegreesPerSecond;},30,720,5,.075f,1);
                AddNumericRow(delegate{return "FLING POWER    "+Mathf.RoundToInt(Settings.FlingPower*100)+"%";},delegate{return Settings.FlingPower;},0,5,.05f,-.04f,8);
                AddNumericRow(delegate{return "WALK SPEED    "+Settings.WalkMultiplier.ToString("F2")+"x";},delegate{return Settings.WalkMultiplier;},.25f,5,.05f,-.155f,2);
            }
            else if(settingsPage==2)
            {
                AddNumericRow(delegate{return "MOTION DAMPING    "+Mathf.RoundToInt(Settings.MotionDamping*100)+"%";},delegate{return Settings.MotionDamping;},0,2,.01f,.42f,9);
                AddNumericRow(delegate{return "OFFHAND INFLUENCE    "+Mathf.RoundToInt(Settings.OffhandInfluence*100)+"%";},delegate{return Settings.OffhandInfluence;},0,1,.01f,.305f,10);
                var grip=AddButton(panel,"",0,.19f,.90f,.065f,delegate{Publish(Settings.With(gripToggle:!Settings.GripToggle));},.03f);PreparePersonalChoice(grip,"grip_toggle");
                numericRows.Add(new NumericRow{Caption=delegate{return "GRIP TOGGLE    "+(Settings.GripToggle?"ON":"OFF");},Text=grip.Caption});
                var dominant=AddButton(panel,"",0,.075f,.90f,.065f,delegate{Publish(Settings.With(dominantHand:Settings.DominantHand==0?1:0));},.03f);PreparePersonalChoice(dominant,"dominant_hand");
                numericRows.Add(new NumericRow{Caption=delegate{return "DOMINANT HAND    "+(Settings.DominantHand==0?"LEFT":"RIGHT");},Text=dominant.Caption});
            }
            else
            {
                var names=new[]{"MASTER VOLUME","SFX VOLUME","MUSIC VOLUME","PLAYER VOICE VOLUME","AMBIENCE VOLUME"};
                for(int i=0;i<5;i++){int slot=i;AddNumericRow(delegate{return names[slot]+"    "+Mathf.RoundToInt(AudioValue(slot)*100)+"%";},delegate{return AudioValue(slot);},0,1,.01f,.42f-i*.115f,slot+3);}
            }
            BuildSettingsHelp(false,false,null);RefreshSettingsRows();AddButton(panel,settingsPage==2?"BACK TO SETTINGS":"BACK",0,settingsPage==2?-.04f:-.61f,.90f,.08f,delegate{settingsPage=-1;Build();},.026f);
        }
        float AudioValue(int field){return field==0?Settings.MasterVolume:field==1?Settings.SfxVolume:field==2?Settings.MusicVolume:field==3?Settings.VoiceVolume:Settings.AmbienceVolume;}
        void AddNumericRow(Func<string> caption,Func<float> value,float min,float max,float step,float y,int field)
        {
            var text=Text(panel,caption(),new Vector3(0,y+.028f,-.01f),.022f,.90f,Hex(R25Theme.Text));
            bool available=R24SettingsHelpCopy.PersonalAvailable(field);
            var button=AddButton(panel,"",0,y-.035f,.90f,.065f,null,.03f);button.Enabled=available;button.Help=R24SettingsHelpCopy.Personal(field);
            RegisterSettingsHelp(button.Surface,Vector3.zero,button.Size,button.Help);
            if(!available)text.color=Hex(R25Theme.TextDisabled);
            RegisterSettingsHelp(panel,new Vector3(0,y,0),new Vector2(1.14f,.10f),button.Help);
            Transform fill,knob;BuildSettingsSlider(button,.90f,available,out fill,out knob);
            button.SliderChanged=delegate(float fraction){float next=Mathf.Clamp(Mathf.Round(Mathf.Lerp(min,max,fraction)/step)*step,min,max);ApplyNumeric(field,next);};
            numericRows.Add(new NumericRow{Caption=caption,Value=value,Min=min,Max=max,Text=text,Fill=fill,Knob=knob,Width=.90f});
        }
        void ApplyNumeric(int field,float value)
        {
            Publish(Settings.With(snap:field==0?(float?)value:null,smooth:field==1?(float?)value:null,walk:field==2?(float?)value:null,master:field==3?(float?)value:null,sfx:field==4?(float?)value:null,music:field==5?(float?)value:null,ambience:field==7?(float?)value:null,voice:field==6?(float?)value:null,fling:field==8?(float?)value:null,damping:field==9?(float?)value:null,offhand:field==10?(float?)value:null),true);
        }
        void RefreshSettingsRows()
        {
            foreach(var row in numericRows){row.Text.text=row.Caption();if(row.Fill==null)continue;float fraction=Mathf.InverseLerp(row.Min,row.Max,row.Value());SetBar(row.Fill,fraction,row.Width);var at=row.Knob.localPosition;at.x=-row.Width*.5f+row.Width*fraction;row.Knob.localPosition=at;}
        }
        void ChangeMotion(int direction,int field)
        {
            var s=Settings;Publish(s.With(forward:field==3?(s.Forward==R23Forward.Head?R23Forward.LeftController:R23Forward.Head):s.Forward,snap:s.SnapDegrees+(field==0?direction*5:0),smooth:s.SmoothDegreesPerSecond+(field==1?direction*5:0),walk:s.WalkMultiplier+(field==2?direction*.05f:0)));
        }
        void ChangeAudio(int field,int direction)
        {
            var s=Settings;float d=direction*.01f;Publish(s.With(master:s.MasterVolume+(field==0?d:0),sfx:s.SfxVolume+(field==1?d:0),music:s.MusicVolume+(field==2?d:0),ambience:s.AmbienceVolume+(field==4?d:0),voice:s.VoiceVolume+(field==3?d:0)));
        }
        void Publish(R23UiSettings value,bool dragging=false)
        {
            if(R23UiSettingsCodec.SameValue(Settings,value))return;Settings=value;R25LegacyPack.Apply(value);menuAudio.SetSfxVolume(value.SfxVolume);
            if(dragging){preferenceDirty=true;if(Time.unscaledTime>=nextPreferenceSave){FlushPreferences();nextPreferenceSave=Time.unscaledTime+.1f;}}
            else R23UiPreferences.Save(value);
            if(Page==R23UiPage.MatchRules||Page==R23UiPage.Settings&&settingsPage>=0)RefreshSettingsRows();else Build();
            if(SettingsChanged!=null)SettingsChanged(value);
        }
        void FlushPreferences(){if(preferenceDirty){R23UiPreferences.Save(Settings);preferenceDirty=false;}}
        void Back() { AddButton(panel,"BACK",0,-.51f,1.2f,.11f,delegate{SetPage(returnPage);},.033f); }
        static Color Hex(string value){Color color;return ColorUtility.TryParseHtmlString("#"+value,out color)?color:Hex(R25Theme.Text);}
        static Vector3 FrontPoint(float x,float y){return new Vector3((x/1280f-.5f)*2.2f,(.5f-y/800f)*1.375f,-.01f);}
        TextMesh FrontText(string value,float x,float y,float fontPixels,float widthPixels)
        {return Text(panel,value,FrontPoint(x,y),Mathf.Max(R25Theme.MinimumPixels,fontPixels)*1.375f/800*.72f,widthPixels*2.2f/1280,Hex(R25Theme.Text));}
        Button FrontButton(string caption,float x,float y,float width,float height,Action action,Color fill,Color ink,float fontPixels)
        {
            var at=FrontPoint(x+width*.5f,y+height*.5f);var button=AddButton(panel,caption,at.x,at.y,width*2.2f/1280,height*1.375f/800,action,Mathf.Max(R25Theme.MinimumPixels,fontPixels)*1.375f/800*.72f);
            SetButtonStyle(button,fill,ink);return button;
        }
        Button MenuCard(string caption,float y,string icon,Action action,float height=.21f)
        {
            var button=AddButton(panel,"",0,y,Page==R23UiPage.Settings?.90f:.94f,height,action,.027f);button.Caption.transform.localPosition=new Vector3(0,-.052f,-.004f);button.Caption.text=caption;
            button.Focus=Quad(button.Surface,"Menu focus bar",new Vector3(-button.Size.x*.5f+.003f,0,-.003f),new Vector2(.006f,button.Size.y),Hex(R25Theme.Ember)).GetComponent<Renderer>();button.Focus.sortingOrder=11;button.Focus.gameObject.SetActive(false);
            Icon(button.Surface,icon,new Vector3(0,.037f,-.005f),new Vector2(.074f,.074f));
            if(Page==R23UiPage.Settings&&settingsPage<0){button.Help=R24SettingsHelpCopy.PersonalMenu(icon);RegisterSettingsHelp(button.Surface,Vector3.zero,button.Size,button.Help);}
            if(focusButton==null){focusButton=button;SetHovered(button,true);}
            return button;
        }
        Transform Icon(Transform parent,string key,Vector3 at,Vector2 size)
        {
            Material material;
            if(!iconMaterials.TryGetValue(key,out material))
            {
                var texture=Resources.Load<Texture2D>("R23SourceUI/"+key);
                if(texture==null)throw new InvalidOperationException("Missing source UI icon "+key);
                var shader=Resources.Load<Shader>("R23SourceIcon");if(shader==null)throw new InvalidOperationException("Missing source RGBA icon shader");
                material=new Material(shader);material.name="R23 source RGBA icon / "+key;materials.Add(material);material.SetTexture("_IconTex",texture);material.renderQueue=4102;iconMaterials.Add(key,material);
            }
            var icon=Quad(parent,"Source icon "+key,at,size,Color.white);var renderer=icon.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.sortingOrder=30;return icon;
        }
        // Narrow native diagnostic: logs actual import format/binding without changing pixels.
        public string SourceIconBindingDiagnostics()
        {
            Require();var result=new System.Text.StringBuilder();
            foreach(var pair in iconMaterials)
            {
                var texture=pair.Value.GetTexture("_IconTex") as Texture2D;
                result.Append(pair.Key).Append(" shader=").Append(pair.Value.shader.name);
                if(texture!=null)
                {
                    result.Append(" texture=").Append(texture.name).Append(" format=").Append(texture.format).Append(" size=").Append(texture.width).Append('x').Append(texture.height).Append(" readable=").Append(texture.isReadable);
                    if(texture.isReadable)result.Append(" rgbaBody=").Append(texture.GetPixel(texture.width/2,texture.height/5)).Append(" rgbaVisor=").Append(texture.GetPixel(texture.width/2,texture.height*2/3));
                }
                else result.Append(" MISSING _IconTex");
                result.AppendLine();
            }
            return result.ToString();
        }
        void SetButtonStyle(Button button,Color normal,Color ink)
        {
            button.Normal=normal;button.Hover=normal==Hex(R25Theme.Primary)?Hex(R25Theme.PrimaryHover):Hex(R25Theme.RaisedHover);
            if(normal.a==0)button.Hover=normal;button.Ink=ink;SetHovered(button,false);
        }
        static void SetHovered(Button button,bool selected)
        {
            selected=selected&&button.Enabled;
            bool primary=button.Normal==Hex(R25Theme.Primary), chosen=button.Normal==Hex(R25Theme.RaisedSelected);
            Color fill=!button.Enabled?Hex(R25Theme.Disabled):selected?button.Hover:button.Normal;
            Tint(button,fill);
            if(button.Caption!=null)button.Caption.color=!button.Enabled?Hex(R25Theme.TextDisabled):primary?Hex(R25Theme.TextOnEmber):chosen?Hex(R25Theme.EmberHot):button.Ink;
            Color edge=selected?Hex(R25Theme.Ember):chosen||primary?Hex(R25Theme.EmberHot):Hex(R25Theme.Stroke);
            if(button.Edge!=null){button.Edge.gameObject.SetActive(button.Enabled&&(!button.SelectionEdgeOnly||selected));SetRendererColor(button.Edge,edge);}
            if(button.Focus!=null){button.Focus.gameObject.SetActive(selected&&button.Enabled);SetRendererColor(button.Focus,Hex(R25Theme.Ember));}
            if(button.Marker!=null)button.Marker.gameObject.SetActive(chosen&&button.Enabled);
            if(button.Frames!=null)foreach(var frame in button.Frames)SetRendererColor(frame,edge);
        }
        static void SetRendererColor(Renderer renderer,Color color)
        {var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);}
        static void SetPressed(Button button)
        {
            if(button==null||!button.Enabled)return;
            bool primary=button.Normal==Hex(R25Theme.Primary);
            Tint(button,Hex(primary?R25Theme.Ember:R25Theme.Pressed));
            if(button.Edge!=null)SetRendererColor(button.Edge,Hex(primary?R25Theme.Text:R25Theme.EmberHot));
            if(button.Frames!=null)foreach(var frame in button.Frames)SetRendererColor(frame,Hex(R25Theme.EmberHot));
        }
        Button AddButton(Transform parent,string label,float x,float y,float width,float height,Action action,float textHeight=.039f,bool available=true,string ownedId=null)
        {
            Transform surface=Child(parent,label);surface.localPosition=new Vector3(x,y,-.012f);
            var visual=Quad(surface,"Button background",Vector3.zero,new Vector2(width,height),Color.white);
            var caption=Text(surface,label,new Vector3(0,0,-.004f),textHeight,width*.94f,Hex(R25Theme.Text));
            var edge=Quad(surface,"Button underline",new Vector3(0,-height*.5f+.003f,-.002f),new Vector2(width,.006f),Color.white);
            var button=new Button{Surface=surface,Size=new Vector2(width,height),Action=action,Renderer=visual.GetComponent<Renderer>(),Edge=edge.GetComponent<Renderer>(),Caption=caption,Enabled=available,OwnedId=ownedId,SoundId=buttons.Count};buttons.Add(button);
            SetButtonStyle(button,label=="START ROUND"||label=="RESTART ROUND"?Hex(R25Theme.Primary):Hex(R25Theme.Raised),Hex(R25Theme.Text));
            button.Marker=Quad(surface,"Selected diamond",new Vector3(-width*.5f+.0275f,0,-.006f),new Vector2(.010f,.010f),Hex(R25Theme.EmberHot));button.Marker.localRotation=Quaternion.Euler(0,0,45);button.Marker.gameObject.SetActive(false);button.Marker.GetComponent<Renderer>().sortingOrder=11;return button;
        }
        TextMesh Text(Transform parent,string value,Vector3 at,float height,float width,Color color)
        {
            Transform t=Child(parent,"Label");t.localPosition=at;t.localScale=Vector3.one;
            var text=t.gameObject.AddComponent<TextMesh>();text.font=font;text.fontSize=96;text.characterSize=1;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.richText=false;text.color=color;text.text=value;
            var renderer=t.GetComponent<MeshRenderer>();renderer.sharedMaterial=fontMaterial;renderer.sortingOrder=20;var label=new Label{Text=text,Height=height,MaxWidth=width};labels.Add(label);FitText(label);return text;
        }
        static Transform Child(Transform parent,string name) { var t=new GameObject(name).transform;t.gameObject.layer=5;t.SetParent(parent,false);return t; }
        Transform Quad(Transform parent,string name,Vector3 at,Vector2 size,Color color)
        {
            Transform t=Child(parent,name);t.localPosition=at;
            var mesh=new Mesh();mesh.name="R23 UI quad";mesh.vertices=new[]{new Vector3(-size.x/2,-size.y/2,0),new Vector3(size.x/2,-size.y/2,0),new Vector3(size.x/2,size.y/2,0),new Vector3(-size.x/2,size.y/2,0)};mesh.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};mesh.colors=color==Hex(R25Theme.Panel)?new[]{Hex(R25Theme.PanelBottom),Hex(R25Theme.PanelBottom),Hex(R25Theme.PanelTop),Hex(R25Theme.PanelTop)}:new[]{color,color,color,color};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=flatMaterial;
            // Overlay bars must sort above their backdrop independent of the
            // renderer-centre distance at the left/right edges of the HUD.
            renderer.sortingOrder=name=="SettingsBoard"||name.IndexOf("backdrop",StringComparison.OrdinalIgnoreCase)>=0?0:name=="Button background"?10:name=="Button underline"?11:name.IndexOf("remaining",StringComparison.OrdinalIgnoreCase)>=0?3:name.IndexOf("track",StringComparison.OrdinalIgnoreCase)>=0?2:1;return t;
        }
        static void Tint(Button button,Color color) { var p=new MaterialPropertyBlock();button.Renderer.GetPropertyBlock(p);p.SetColor("_Color",color);button.Renderer.SetPropertyBlock(p); }
        void SetBar(Transform bar,float fraction,float width) { if(bar==null)return;float center;if(!barOrigins.TryGetValue(bar,out center)){center=bar.localPosition.x;barOrigins.Add(bar,center);}var scale=bar.localScale;scale.x=Mathf.Max(.0001f,fraction*width);bar.localScale=scale;var at=bar.localPosition;at.x=center-width*(1-fraction)/2;bar.localPosition=at; }
        static string Clean(string value,int max) { string s=(value??"").Replace('\n',' ').Replace('\r',' ').ToUpperInvariant();return s.Length>max?s.Substring(0,max-3)+"...":s; }
        void CenterHudGlyphs(TextMesh text,float centerY)
        {foreach(var label in labels)if(label.Text==text){label.CenterGlyphY=true;label.TargetY=centerY;FitText(label);return;}}
        void FitText(Label label)
        {
            if(string.IsNullOrEmpty(label.Text.text))return;
            font.RequestCharactersInTexture(label.Text.text,label.Text.fontSize,label.Text.fontStyle);
            var bounds=label.Text.GetComponent<MeshRenderer>().localBounds.size;
            // Source sizes are em pixels, not the generated TextMesh line box.
            // R23GodotUI at 96px has a 130.73px line box but ~70.55px capitals;
            // dividing by bounds.y shrank every caption to about half its source size.
            // TextMesh uses 0.1 local units per characterSize/em pixel. Callers
            // express their desired capital height as .72 of the nominal em.
            float em=label.Text.fontSize*label.Text.characterSize*.1f;
            if(em>.00001f&&bounds.x>.00001f)label.Text.transform.localScale=Vector3.one*Mathf.Min(label.Height/(.72f*em),label.MaxWidth/bounds.x);
            // TextMesh bounds include line spacing; lift bar digits 2.5 mm, tuned against the rendered 100/50/7 ink.
            if(label.CenterGlyphY){var p=label.Text.transform.localPosition;p.y=label.TargetY-label.Text.GetComponent<MeshRenderer>().localBounds.center.y*label.Text.transform.localScale.y+(label.Text==healthText||label.Text==shieldText?.0025f:0);label.Text.transform.localPosition=p;}
        }
        // Direct native Camera.Render may precede the first LateUpdate after changing pages.
        public void RefreshTextLayout(){Require();foreach(var label in labels)FitText(label);}
        bool MenuAudioActive(){return isActiveAndEnabled&&Page!=R23UiPage.Loading&&Page!=R23UiPage.Playing&&Page!=R23UiPage.Error;}
        static bool MenuHeadTracked()
        {
            if(!UnityEngine.XR.XRSettings.isDeviceActive)return true;
            bool tracked;return UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head).TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out tracked)&&tracked;
        }
        void LateUpdate()
        {
            if(!initialized)return;
            AnimateFriendsWaiting();
            foreach(var button in buttons)SetHovered(button,hovered[0]==button||hovered[1]==button||focusButton==button);for(int hand=0;hand<2;hand++)if(held[hand])SetPressed(pressed[hand]);
            UpdateTitleMoment();
            FollowLoadoutAnchor();
            if(soundEdges.Initialize(MenuAudioActive()&&returnPage==R23UiPage.Ready,MenuHeadTracked(),Application.isFocused,menuAudio.HasCue(R23MenuCue.Initialize)))menuAudio.Play(R23MenuCue.Initialize);
            // Retain nominal source em size, with actual generated width as an overflow guard.
            foreach(var label in labels)FitText(label);
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
            FlushPreferences();DisposeTitleMoment();if(loadingTexture!=null)Destroy(loadingTexture);
            if(menuAudio!=null)Destroy(menuAudio.gameObject);
            Font.textureRebuilt-=RefreshFont;
            if(panel!=null)Clear(panel);if(hud!=null){Clear(hud);Destroy(hud.gameObject);}if(shroud!=null){var mesh=shroud.GetComponent<MeshFilter>().sharedMesh;Destroy(mesh);Destroy(shroud.gameObject);}
            foreach(var dot in dots)if(dot!=null)Destroy(dot.GetComponent<MeshFilter>().sharedMesh);
            foreach(var material in materials)Destroy(material);
            foreach(var texture in inventoryTextures.Values)if(texture!=null)Destroy(texture);
        }
        void OnDisable(){if(titleRoot!=null)titleRoot.gameObject.SetActive(false);if(titleVoice!=null)titleVoice.Stop();ClearPointedSettingsHelp();FlushPreferences();soundEdges.ResetHover();if(menuAudio!=null)menuAudio.Stop();foreach(var beam in beams)if(beam!=null)beam.enabled=false;foreach(var dot in dots)if(dot!=null)dot.gameObject.SetActive(false);if(hud!=null)hud.gameObject.SetActive(false);}
        void OnApplicationFocus(bool focused){if(!focused){soundEdges.ResetHover();if(menuAudio!=null)menuAudio.Stop();}}
        void OnEnable(){if(initialized)Build();}
    }
}
#endif
