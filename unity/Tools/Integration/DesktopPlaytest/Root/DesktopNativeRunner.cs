#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
public sealed class DesktopNativeRunner:MonoBehaviour
{
    public static Action<string,Exception> Finished;
    IEnumerator Start()
    {
        foreach(string name in new[]{"arena","arenaReview","pawn","pawnReview","combat","combatOriginalExpected","combatReview","construction","hud","bootstrapReview","hardening"})
        {
            IEnumerator routine=Routine(name);bool failed=false;
            while(true)
            {
                bool more=false;object item=null;Exception error=null;
                try{more=routine.MoveNext();if(more)item=routine.Current;}catch(Exception e){error=e;}
                if(error!=null){var disposable=routine as IDisposable;if(disposable!=null)disposable.Dispose();if(Finished!=null)Finished(name,error);failed=true;break;}
                if(!more)break;yield return item;
            }
            if(!failed&&Finished!=null)Finished(name,null);
            yield return null;
        }
        if(Finished!=null)Finished("done",null);
    }
    IEnumerator Routine(string name)
    {
        if(name=="arena")return Ashfall.Tools.Integration.DesktopPlaytest.DesktopArenaNativeFixture.Run(Debug.Log);
        if(name=="arenaReview")return Ashfall.Reviews.DesktopArenaR1.ArenaHoldouts.DuplicateAliasCleanup(Debug.Log);
        if(name=="hud")return NativeHudFixture.Run();
        if(name=="bootstrapReview")return DesktopBootstrapStartupHold.Run(Debug.Log);
        if(name=="hardening")return DesktopHardening21Fixture.Run();
        return Synchronous(name);
    }
    IEnumerator Synchronous(string name)
    {
        if(name=="pawn")Debug.Log(Ashfall.Tools.Integration.DesktopPlaytest.DesktopPawnNativeFixture.Run());
        else if(name=="pawnReview")Debug.Log(Ashfall.Reviews.DesktopPawnReviewR1.NativeHoldouts.Run());
        else if(name=="combat")DesktopCombatR1.NativeGate.Run();
        else if(name=="combatOriginalExpected")DesktopCombatIndependentReview.NativeHoldouts.Run();
        else if(name=="combatReview")DesktopCombatCorrectedReview.NativeHoldouts.Run();
        else if(name=="construction")Ashfall.DesktopConstructionFixtures.NativeFixture.RunArena();
        yield break;
    }
}
#endif
