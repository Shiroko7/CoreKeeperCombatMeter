using PugMod;
using UnityEngine.Scripting;

public static class CombatMeterCommands
{
    [Preserve]
    [CommandWithModSupport("combatmeter.toggle", "Shows or hides the combat meter window.")]
    public static string ToggleMeter()
    {
        return CombatMeterOverlay.Toggle();
    }

    [Preserve]
    [CommandWithModSupport("combatmeter.reset", "Ends the current encounter and starts a fresh one.")]
    public static string ResetMeter()
    {
        CombatLogAggregator.Instance.ResetCurrent();
        return "Combat meter reset.";
    }
}
