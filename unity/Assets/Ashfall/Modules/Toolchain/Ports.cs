#if UNITY_EDITOR || !UNITY_6000_0_OR_NEWER
namespace Ashfall.Modules.Toolchain
{
    // Owned ports only. No shared Core contract or runtime service is replaced.
    public interface ICompilerRunner
    {
        CompileResult Compile(string[] sources, string outputDirectory, bool executable);
    }
    public interface IModuleDiscovery
    {
        string[] Discover(string moduleDirectory);
    }
}

#endif
