using System.Collections.Generic;
using Ashfall.Modules.ParityHarness;
namespace Ashfall.Integration.ParitySubjects
{
    // Owned extension seam. Implementations construct fresh actual candidate modules per scenario.
    // Never represents engine physics, authenticated transport identity or inventory mutation.
    public interface IParityExpansionProvider
    {
        IEnumerable<Scenario> CreateScenarios();
        IEnumerable<string> RequiredIds { get; }
    }
}
