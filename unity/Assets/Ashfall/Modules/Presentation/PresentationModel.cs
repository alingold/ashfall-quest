using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Core;

namespace Ashfall.Modules.Presentation
{
    public sealed class UiRow
    {
        public readonly string Id, Label, Value;
        public readonly bool Enabled;
        public readonly SettingSpec Setting;
        public UiRow(string id,string label,string value,bool enabled,SettingSpec setting)
        { Id=id; Label=label; Value=value; Enabled=enabled; Setting=setting; }
    }
    public sealed class PresentationModel
    {
        private sealed class Place { public string Page, Focus; }
        private readonly IPresentationHost host;
        private readonly IList<SettingSpec> specs;
        private readonly Dictionary<string,string> values=new Dictionary<string,string>();
        private readonly Dictionary<string,string> dirty=new Dictionary<string,string>();
        private readonly Stack<Place> history=new Stack<Place>();
        private long operation;
        private SettingsSnapshot snapshot;
        public bool SettingsReady { get; private set; }
        public const string ApiVersion="Presentation/3";
        private string failedAction;
        public string Page { get; private set; }
        public string Focus { get; private set; }
        public string Error { get; private set; }
        public bool Loading { get; private set; }
        public bool IsOpen { get { return Page!="closed"; } }
        public bool HasChanges { get { return dirty.Count>0; } }
        public long Operation { get { return operation; } }
        public string Value(string key) { return values[key]; }
        public PresentationModel(IPresentationHost host)
        {
            if(host==null) throw new ArgumentNullException("host");
            specs=SettingsCatalog.Create(host is IPresentationCatalogHost && ((IPresentationCatalogHost)host).SkinRuntimeSettings);
            this.host=host; Page="closed"; Error="";
            RefreshSettings();
        }
        // Refresh personal baselines without replacing retained edits. Effective
        // host/map values are display-only and never become personal drafts.
        public void RefreshSettings()
        {
            try {
                var typed=host as IPresentationSettingsHost;
                var nextSnapshot=typed==null?null:typed.CaptureSettings();
                if(typed!=null && nextSnapshot==null)throw new InvalidOperationException();
                var next=new Dictionary<string,string>();
                foreach(var spec in specs) {
                    if(dirty.ContainsKey(spec.Key)){next[spec.Key]=dirty[spec.Key];continue;}
                    string value;
                    bool found=nextSnapshot!=null?nextSnapshot.TryReadPersonal(spec.Key,out value):host.TryReadSetting(spec.Key,out value);
                    next[spec.Key]=spec.Normalize(found?value:spec.Default);
                }
                snapshot=nextSnapshot;
                foreach(var pair in next)values[pair.Key]=pair.Value;
                if(!SettingsReady)Error="";
                SettingsReady=true;
                ReconcileSprint();
            } catch {
                SettingsReady=false;
                foreach(var spec in specs)if(!values.ContainsKey(spec.Key))values[spec.Key]=spec.Default;
                Error="Settings could not be loaded. Retry or resume.";
            }
        }
        public string EffectiveValue(string key)
        {
            string value;
            if(snapshot!=null && snapshot.TryReadOverride(key,out value))
                foreach(var spec in specs)if(spec.Key==key)return spec.Normalize(value);
            return Value(key);
        }
        private bool Editable(string key)
        {
            // The supplied numeric HUD has neither source HUD style nor size support.
            // Keep saved values intact, but don't offer nonfunctional choices.
            return SettingsReady && key!="hud_size" && key!="hud_style" &&
                (snapshot==null || !snapshot.IsOverridden(key)) && host.CanEdit(key);
        }
        private double Number(string key) { return double.Parse(values[key],CultureInfo.InvariantCulture); }
        private void Add(List<UiRow> rows,string id,string label,bool enabled) { rows.Add(new UiRow(id,label,"",enabled && (!Loading || id=="back" || id=="resume" || id=="discard"),null)); }
        public IList<UiRow> Rows()
        {
            var rows=new List<UiRow>();
            if(Page=="closed") return rows.AsReadOnly();
            if(Page=="pause") {
                Add(rows,"resume","Resume",true); Add(rows,"root","Settings",true);
                Add(rows,"action:calibrate","Calibrate height",host.CanRequest("calibrate"));
                Add(rows,"action:home","Home / leave session",host.CanRequest("home"));
            } else if(Page=="root") {
                Add(rows,"movement","Locomotion",true); Add(rows,"action:calibrate","Set height",host.CanRequest("calibrate"));
                Add(rows,"weapons","Weapons",true); Add(rows,"general","General",true);
            } else {
                foreach(var spec in specs) if(spec.Page==Page && spec.Page!="hidden")
                    rows.Add(new UiRow(spec.Key,spec.Label,EffectiveValue(spec.Key),!Loading && Editable(spec.Key),spec));
                if(Page=="movement") Add(rows,"turning","Smooth turn options",true);
                if(Page=="turning") Add(rows,"flight","Flight / sprint tuning",true);
                if(Page=="turning") Add(rows,"reset:movement","Reset movement defaults",CanReset(false));
                if(Page=="weapons") Add(rows,"reset:weapons","Reset weapon defaults",CanReset(true));
                if(Page=="general") { Add(rows,"game","Game options",true); Add(rows,"action:voice","Players / microphone",host.CanRequest("voice")); }
            }
            if(Page!="pause") { Add(rows,"back","Back / save changes",true); Add(rows,"resume","Resume",true); }
            if(HasChanges)Add(rows,"discard","Discard changes and resume",true);
            if(!String.IsNullOrEmpty(Error)) Add(rows,"retry","Retry",true);
            return rows.AsReadOnly();
        }
        public void Open()
        {
            if(IsOpen) return;
            RefreshSettings(); host.SetGameplayPaused(true); Page="pause"; history.Clear(); RepairFocus();
        }
        private void RepairFocus()
        {
            var rows=Rows(); foreach(var row in rows) if(row.Id==Focus && row.Enabled)return;
            Focus=null; foreach(var row in rows)if(row.Enabled){Focus=row.Id;break;}
        }
        public void RefreshAvailability() { RepairFocus(); }
        public void FocusRow(string id) { foreach(var row in Rows())if(row.Id==id && row.Enabled){Focus=id;return;} }
        public void Move(int direction)
        {
            if(direction==0)return;
            RepairFocus(); var rows=Rows(); int index=-1;
            for(int i=0;i<rows.Count;i++)if(rows[i].Id==Focus)index=i;
            for(int i=1;i<=rows.Count;i++){int at=(index+(direction>0?i:-i)+rows.Count*2)%rows.Count;if(rows[at].Enabled){Focus=rows[at].Id;return;}}
        }
        // Reconcile only an authorized dependent draft. Locked values remain visible
        // with explicit conflict feedback; leaving pause never depends on save success.
        private bool ReconcileSprint()
        {
            if(Number("sprint_fast_gain")>=Number("sprint_slow_gain"))return true;
            if(Editable("sprint_fast_gain")) { Put("sprint_fast_gain",Value("sprint_slow_gain"));return true; }
            Error="Sprint settings conflict. Your changes are kept; retry or discard them.";
            return false;
        }
        private bool Save()
        {
            if(!SettingsReady){Error="Settings could not be loaded. Retry or resume.";return false;}
            RefreshSettings();
            if(!SettingsReady || !ReconcileSprint())return false;
            if(dirty.Count==0)return true;
            RuleResult result;
            foreach(var key in dirty.Keys)if(!Editable(key)){Error="Settings are unavailable. Your changes are kept; retry or discard them.";return false;}
            try { result=host.CommitSettings(new Dictionary<string,string>(dirty)); }
            catch { Error="Settings could not be saved. Please retry.";return false; }
            if(!result.Accepted){Error="Settings could not be saved. Please retry.";return false;}
            dirty.Clear();Error="";return true;
        }
        public void Back()
        {
            if(!IsOpen)return;
            LeaveLoading();
            Save(); // Failure retains the draft but must never trap pause ownership.
            failedAction=null;
            if(history.Count>0){var place=history.Pop();Page=place.Page;Focus=place.Focus;RepairFocus();}
            else { host.SetGameplayPaused(false);Page="closed";Focus=null; }
        }
        private void LeaveLoading() { if(Loading){Loading=false;++operation;failedAction=null;} }
        public void Resume()
        {
            if(!IsOpen)return;
            LeaveLoading(); Save(); Suspend();
        }
        // Teardown releases only this UI's pause ownership, never writes settings.
        public void Suspend()
        {
            LeaveLoading();
            if(IsOpen)host.SetGameplayPaused(false);
            Page="closed";Focus=null;history.Clear();
        }
        public void DiscardAndResume()
        {
            dirty.Clear();Error="";failedAction=null;Suspend();RefreshSettings();
        }
        public void Activate(string id)
        {
            UiRow selected=null;foreach(var row in Rows())if(row.Id==id && row.Enabled)selected=row;
            if(selected==null)return;
            Focus=id;
            if(id.StartsWith("reset:",StringComparison.Ordinal)){
                bool weapons=id=="reset:weapons";
                if(CanReset(weapons))foreach(var spec in specs)if(InReset(spec,weapons))Put(spec.Key,spec.Normalize(spec.Default));
                return;
            }
            if(selected.Setting!=null) { if(!selected.Setting.Numeric)Adjust(id,1);return; }
            if(id=="back"){Back();return;}
            if(id=="resume"){Resume();return;}
            if(id=="discard"){DiscardAndResume();return;}
            if(id=="retry"){
                if(failedAction!=null){string action=failedAction;failedAction=null;Error="";Activate("action:"+action);}
                else { if(!SettingsReady)RefreshSettings(); if(Save())Error=""; }
                RepairFocus();return;
            }
            if(id.StartsWith("action:",StringComparison.Ordinal)){
                if(!Save())return; Loading=true; Error=""; failedAction=id.Substring(7); ++operation;
                try { host.Request(id.Substring(7),operation); }
                catch { Complete(operation,false,"",false); }
                RepairFocus();return;
            }
            history.Push(new Place {Page=Page,Focus=Focus});Page=id;Focus=null;failedAction=null;RepairFocus();
        }
        public void Complete(long token,bool success,string message,bool close)
        {
            if(!Loading || token!=operation)return;
            Loading=false;
            if(success)failedAction=null;
            // Host errors may contain diagnostic/secret details; use player text.
            Error=success?"":"Action could not be completed. Please try again.";
            if(success && close){history.Clear();Page="pause";Back();}else RepairFocus();
        }
        private void Put(string key,string value) { if(values[key]==value)return;values[key]=value;dirty[key]=value;failedAction=null; }
        private static bool InReset(SettingSpec spec,bool weapons)
        {
            return weapons?spec.Key.StartsWith("weapon_settings.",StringComparison.Ordinal):
                spec.Page=="movement" || spec.Page=="turning" || spec.Page=="flight" || spec.Key=="vignette_enabled" || spec.Key=="sprint_double_press_window";
        }
        private bool CanReset(bool weapons)
        {
            foreach(var spec in specs)if(InReset(spec,weapons) && !Editable(spec.Key))return false;
            return true;
        }
        public bool SetNumeric(string key,double number)
        {
            foreach(var row in Rows())if(row.Id==key && row.Enabled && row.Setting!=null && row.Setting.Numeric){
                if(double.IsNaN(number)||double.IsInfinity(number))return false;
                var spec=row.Setting;
                number=Math.Max(spec.Min,Math.Min(spec.Max,number));
                number=Math.Round(number/spec.Step,MidpointRounding.AwayFromZero)*spec.Step;
                string value=spec.Normalize(number.ToString("R",CultureInfo.InvariantCulture));
                if(key=="sprint_fast_gain")value=Math.Max(double.Parse(value,CultureInfo.InvariantCulture),Number("sprint_slow_gain")).ToString(CultureInfo.InvariantCulture);
                // Dependent setting must also be editable, otherwise reject atomically.
                if(key=="sprint_slow_gain" && double.Parse(value,CultureInfo.InvariantCulture)>Number("sprint_fast_gain") && !Editable("sprint_fast_gain"))return false;
                Put(key,value);
                if(key=="sprint_slow_gain" && Number(key)>Number("sprint_fast_gain"))Put("sprint_fast_gain",value);
                return true;
            }
            return false;
        }
        public void Adjust(string key,int direction)
        {
            if(direction==0)return;
            foreach(var row in Rows())if(row.Id==key && row.Enabled && row.Setting!=null){
                var s=row.Setting;
                if(s.Numeric)SetNumeric(key,Number(key)+(direction>0?s.Step:-s.Step));
                else {int index=Array.IndexOf(s.Choices,values[key]);Put(key,s.Choices[(index+(direction>0?1:s.Choices.Length-1))%s.Choices.Length]);}
                return;
            }
        }
    }
}
