using System.Collections.Generic;
namespace Ashfall.Integration.PersonalSettings
{
    // Owned coordination boundary; implementations serialize context changes with the UI.
    // No arbitrary callbacks run during a durable commit. Runtime consumes Effective after success.
    public interface ISettingsUiActions
    {
        void SetPaused(bool paused);
        bool CanRequest(string action);
        void Request(string action, long operation);
    }
    public enum SettingsSource { Quest, SkinRuntime }
}
