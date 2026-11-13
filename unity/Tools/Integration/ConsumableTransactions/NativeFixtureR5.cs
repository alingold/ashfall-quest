#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
// Root-only callable fixture. Include frozen TestsR5Base.cs and TestsR5.cs.
// No bootstrap or engine launch; root invokes under engine_lane.py.
public static class ConsumableCustodyNativeFixtureR5
{
    public static string Run(string ownedOutputDirectory)
    {
        if(String.IsNullOrEmpty(ownedOutputDirectory))throw new ArgumentException("output");
        Directory.CreateDirectory(ownedOutputDirectory);
        string path=Path.Combine(ownedOutputDirectory,"pickup-native-fixture.bin");
        if(File.Exists(path))throw new InvalidOperationException("Preserve previous fixture output");
        TestsR5.Main(new[]{"capture",path});TestsR5.Main(new[]{"restore",path});
        return "PASS local synthetic owner custody fixture; not engine physics, adoption, authentication or whole-effect recovery";
    }
}
#endif
