using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Ashfall.Modules.ParityHarness;
using Ashfall.Integration.ParitySubjects;
namespace Ashfall.Tools.ParityExpansion
{
    public static class MainProgram
    {
        public static int Main(string[] args)
        {
            var provider=new CandidateProvider();var cases=new List<Scenario>(RepresentativeScenarios.Create());cases.AddRange(provider.CreateScenarios());cases.Add(LifecycleChecks.Create());
            if(args.Length>1&&args[1]=="--omit-expansion")cases.RemoveAll(s=>s.Id=="EXP-PAD-CONTACT");
            var report=ScenarioRunner.Run(cases,new LinkedSubjectProvider().Create(),10);
            var ids=new HashSet<string>();foreach(var s in cases)ids.Add(s.Id);
            foreach(string id in provider.RequiredIds)if(!ids.Contains(id))report.MissingRequiredIds.Add(id);
            if(!ids.Contains("EXP-CAPSULE-RESET"))report.MissingRequiredIds.Add("EXP-CAPSULE-RESET");
            // Both registries remain required. Missing extension IDs cannot produce Complete.
            if(args.Length>1&&args[1]=="--self-check") {
                var incomplete=ScenarioRunner.Run(new[]{new Scenario("ONLY","self-check","required IDs retained",delegate(Subjects s){})},new Subjects(),1);
                Verify.That(incomplete.MissingRequiredIds.Count>0&&incomplete.ExitCode(true)==2,"accepted required IDs lost");
                bool rejected=false;try{ScenarioRunner.Run(new[]{cases[0],cases[0]},new Subjects(),1);}catch(ArgumentException){rejected=true;}
                Verify.That(rejected,"duplicate IDs accepted");
            }
            if(args.Length>1&&args[1]=="--omit-expansion")Verify.That(report.MissingRequiredIds.Contains("EXP-PAD-CONTACT"),"extension ID omission invisible");
            File.WriteAllText(args[0],new JavaScriptSerializer().Serialize(report));
            foreach(var row in report.Results)Console.WriteLine(row.Status+" "+row.Id+" "+row.Detail);
            Console.WriteLine("PASS="+report.Passed+" FAIL="+report.Failed+" UNKNOWN="+report.Unknown+" Complete="+report.Complete+" strict="+report.ExitCode(true));
            return report.ExitCode(args.Length>1&&(args[1]=="--strict"||args[1]=="--omit-expansion"));
        }
    }
}

