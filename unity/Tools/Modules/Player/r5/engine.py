from pathlib import Path
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Assets\Ashfall\Modules\Player')
f=p/'PlayerR5Ports.cs';s=f.read_text().replace('public interface IXrPlayerLifecycle {','public interface IXrPlayerLifecycle { event System.Action Invalidated;');f.write_text(s)
f=p/'PlayerR5State.cs';s=f.read_text().replace('public InputFrame Current {','public bool Valid {get {return valid;} }\n        public InputFrame Current {');f.write_text(s)
f=p/'Engine/UnityPlayerDriver.cs';s=f.read_text().replace('public IXrPlayerLifecycle XrLifecycle {get;set;}','''private IXrPlayerLifecycle xrLifecycle;
        public IXrPlayerLifecycle XrLifecycle {get {return xrLifecycle;} set {if(xrLifecycle!=null)xrLifecycle.Invalidated-=OnXrInvalidated;xrLifecycle=value;if(value!=null)value.Invalidated+=OnXrInvalidated;DisarmPublished(true);} }
        private void OnXrInvalidated() {DisarmPublished(true);}''').replace('float dt = Time.unscaledDeltaTime;','if(!frames.Valid) {DisarmPublished();return;}\n            float dt = Time.unscaledDeltaTime;')
s=s.replace('        private bool ceiling;','        private bool ceiling;')
f.write_text(s)
