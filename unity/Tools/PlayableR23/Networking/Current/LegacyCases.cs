using Ashfall.Integration.Playable.UI;
partial class LobbyCases
{
    static void LegacyCases()
    {
        var on=new R23UiSettings();var off=on.With(retiredMaps:false,retiredSkins:false,retiredSounds:false);
        Need(on.RetiredMaps&&on.RetiredSkins&&on.RetiredSounds,"Legacy defaults on");
        var absent=new R25LegacyPolicy(false,44,112,97);
        Need(!absent.Maps(on)&&!absent.Skins(on)&&!absent.Sounds(on),"Absent pack overrides saved settings");
        var partial=new R25LegacyPolicy(true,44,0,0);
        Need(partial.Maps(on)&&!partial.Skins(on)&&!partial.Sounds(on),"Partial pack disables unavailable parts");
        var all=new R25LegacyPolicy(true,44,112,97);
        Need(all.Maps(on)&&all.Skins(on)&&all.Sounds(on)&&!all.Maps(off)&&!all.Skins(off)&&!all.Sounds(off),"Independent opt-out gates");
        Need(!all.Skins(off.With(retiredMaps:true))&&!all.Sounds(off.With(retiredMaps:true)),"Maps does not enable skins or sounds");
        Need(R23UiSettingsCodec.SameValue(on,R23UiSettingsCodec.Decode(R23UiSettingsCodec.Encode(on))),"Legacy preference persistence");
        var old=R23UiSettingsCodec.Decode("r23.ui.v1;master_volume=0.5");
        Need(old.MasterVolume==.5f&&old.RetiredMaps&&old.RetiredSkins&&old.RetiredSounds,"Old preferences migrate with defaults on");
        var savedFalse=R23UiSettingsCodec.Decode("r23.ui.v1;master_volume=0.5;legacy_maps=false;legacy_skins=false;legacy_sounds=false");
        Need(savedFalse.MasterVolume==.5f&&savedFalse.RetiredMaps&&savedFalse.RetiredSkins&&savedFalse.RetiredSounds,"Unmarked saved opt-outs migrate once");
        Need(R23UiSettingsCodec.Encode(on).Contains("legacy_defaults=2"),"Save includes migration marker");
        Need(R23UiSettingsCodec.SameValue(off,R23UiSettingsCodec.Decode(R23UiSettingsCodec.Encode(off))),"Marked opt-outs survive restart");
        var missing=R23UiSettingsCodec.Decode("r23.ui.v1;legacy_defaults=2;legacy_maps=false");
        Need(!missing.RetiredMaps&&missing.RetiredSkins&&missing.RetiredSounds,"Marked missing keys default on independently");
        for(int i=0;i<27;i++)Need(R25LegacyPolicy.PeerStyle(i,false)==i,"Shipped indices stable");
        for(int i=27;i<139;i++)Need(R25LegacyPolicy.PeerStyle(i,false)==0&&R25LegacyPolicy.PeerStyle(i,true)==i,"Peer without pack defaults");
        Need(R25LegacyPolicy.PeerStyle(139,true)==0,"Invalid index defaults");
        var zero=new Ashfall.Core.Float3(0,0,0);
        var t=new Ashfall.Integration.Playable.Networking.R24LanTransform(zero,new Ashfall.Integration.Playable.Networking.R24LanRotation(0,0,0,1));
        for(int i=27;i<139;i++){
            var pose=new Ashfall.Integration.Playable.Networking.R24LanPose("menu",i,0,t,t,t,t,zero,0,0);
            Ashfall.Integration.Playable.Networking.R24LanPose decoded;
            Need(pose.Valid&&Ashfall.Integration.Playable.Networking.R24LanPose.TryDecode(pose.Encode(),out decoded)&&decoded.Style==i,"Legacy byte style roundtrip");}

    }
}
