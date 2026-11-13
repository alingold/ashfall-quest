namespace Ashfall.Integration.Playable.UI
{
    public sealed class R25LegacyPolicy
    {
        public readonly bool Present, HasMaps, HasSkins, HasSounds;
        public R25LegacyPolicy(bool present, int maps, int skins, int sounds)
        { Present=present; HasMaps=present&&maps>0; HasSkins=present&&skins>0; HasSounds=present&&sounds>0; }
        public bool Maps(R23UiSettings s) { return HasMaps&&s.RetiredMaps; }
        public bool Skins(R23UiSettings s) { return HasSkins&&s.RetiredSkins; }
        public bool Sounds(R23UiSettings s) { return HasSounds&&s.RetiredSounds; }
        public static int PeerStyle(int index, bool pack) { return index>=0&&index<139&&(index<27||pack)?index:0; }
    }
}
