using Ashfall.Modules.Audio;
namespace Ashfall.Integration.AudioRuntime
{
    // Coordinator provides immutable accepted/licensed bytes; implementations must not
    // manufacture approval from existence. Tests use explicitly labelled test receipts.
    public interface IApprovedAudioBytes { byte[] Read(AudioAsset asset); }
    public interface IAudioOwnerLease { bool IsCurrent(string session, string owner, long epoch); }
    public sealed class CueIdentity
    {
        public readonly string Session, Owner, Event;
        public readonly long Epoch;
        public CueIdentity(string session,string owner,long epoch,string eventId)
        {
            if(string.IsNullOrWhiteSpace(session)||string.IsNullOrWhiteSpace(owner)||string.IsNullOrWhiteSpace(eventId)||session.Contains("\n")||owner.Contains("\n")||eventId.Contains("\n")||epoch<0)throw new System.ArgumentException("Cue identity");
            Session=session;Owner=owner;Epoch=epoch;Event=eventId;
        }
        public string OwnerKey {get{return Part(Session)+Part(Owner)+Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture);}}
        public string EventKey {get{return Part(OwnerKey)+Part(Event);}}
        private static string Part(string s){return s.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+s;}
    }
}
