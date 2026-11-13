namespace Ashfall.Modules.Pickups
{
    // Called only after pickup eligibility checks and before local hand publication.
    // False preserves both modules; true means prepared inventory publication finished.
    // The coordinator serializes the entire call on the common host lane.
    public interface IPreparedPickupSelection
    {
        bool PublishSelection(string actor,string beforeHeld,string selected);
    }
}
