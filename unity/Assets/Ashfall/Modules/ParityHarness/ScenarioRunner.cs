using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ashfall.Modules.ParityHarness
{
    public sealed class UnknownEvidenceException : Exception
    { public UnknownEvidenceException(string reason) : base(reason) { } }

    public sealed class Scenario
    {
        public readonly string Id, Source, Expected;
        public readonly Action<Subjects> Execute;
        public Scenario(string id, string source, string expected, Action<Subjects> execute)
        {
            if (String.IsNullOrEmpty(id) || String.IsNullOrEmpty(source) || String.IsNullOrEmpty(expected) || execute == null)
                throw new ArgumentException("Scenario requires ID, source, frozen expectation and executable");
            Id = id; Source = source; Expected = expected; Execute = execute;
        }
    }
    public sealed class ScenarioResult
    {
        public string Id, Source, Expected, Status, Detail;
        public int Executions;
        public long MinTicks, MedianTicks, MaxTicks;
    }
    public sealed class RunReport
    {
        public int Schema = 2;
        public string ApiVersion = "ParityHarness/r2";
        public List<string> MissingRequiredIds = new List<string>();
        public string Evidence = "neutral .NET candidate self-check; no engine/device acceptance";
        public long StopwatchFrequency = Stopwatch.Frequency;
        public int Passed, Failed, Unknown;
        public bool Complete { get { return Results.Count > 0 && Failed == 0 && Unknown == 0 && MissingRequiredIds.Count == 0; } }
        public List<ScenarioResult> Results = new List<ScenarioResult>();
        public int ExitCode(bool requireComplete) { return Failed > 0 ? 1 : requireComplete && !Complete ? 2 : 0; }
    }
    public static class ScenarioRunner
    {
        // Validate the complete registry before execution so duplicate IDs cannot hide coverage.
        public static RunReport Run(IEnumerable<Scenario> scenarios, Subjects subjects, int repetitions)
        {
            if (subjects == null || scenarios == null) throw new ArgumentNullException();
            if (repetitions < 1 || repetitions > 10000) throw new ArgumentOutOfRangeException("repetitions");
            List<Scenario> registry = new List<Scenario>(scenarios);
            if (registry.Count == 0) throw new ArgumentException("Empty scenario registry has no coverage");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Scenario s in registry)
                if (s == null || !ids.Add(s.Id)) throw new ArgumentException("Null or duplicate scenario ID");
            RunReport report = new RunReport();
            foreach (string required in RepresentativeScenarios.RequiredIds)
                if (!ids.Contains(required)) report.MissingRequiredIds.Add(required);
            foreach (Scenario s in registry)
            {
                ScenarioResult result = new ScenarioResult { Id = s.Id, Source = s.Source, Expected = s.Expected, Status = "PASS", Detail = "" };
                List<long> ticks = new List<long>();
                for (int i = 0; i < repetitions; i++)
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    try { result.Executions++; s.Execute(subjects); }
                    catch (UnknownEvidenceException ex) { result.Status = "UNKNOWN"; result.Detail = ex.Message; }
                    catch (Exception ex) { result.Status = "FAIL"; result.Detail = ex.GetType().Name + ": " + ex.Message; }
                    watch.Stop(); ticks.Add(watch.ElapsedTicks);
                    if (result.Status != "PASS") break;
                }
                ticks.Sort(); result.MinTicks = ticks[0]; result.MedianTicks = ticks[ticks.Count / 2]; result.MaxTicks = ticks[ticks.Count - 1];
                if (result.Status == "PASS") report.Passed++;
                else if (result.Status == "FAIL") report.Failed++;
                else report.Unknown++;
                report.Results.Add(result);
            }
            return report;
        }
    }
    public static class Verify
    {
        public static void That(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        public static void Near(double actual, double expected, string message)
        { That(!Double.IsNaN(actual) && !Double.IsInfinity(actual) && Math.Abs(actual - expected) <= 0.00001, message + ": expected " + expected + ", observed " + actual); }
        public static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new InvalidOperationException("Malformed geometry was accepted");
        }
    }
}
