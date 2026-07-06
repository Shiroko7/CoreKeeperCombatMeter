using PugMod;
using UnityEngine;

public class CombatMeterMod : IMod
{
    public const string ModId = "CombatMeter";

    internal static CombatMeterMod Instance { get; private set; }

    public void EarlyInit()
    {
        Instance = this;

        // Needed so the combatmeter.toggle / combatmeter.reset chat commands are reachable
        // (mirrors the SDK's own ModCommandsExample/EnableConsole.cs).
        Manager.enableConsole = true;

        Debug.Log("[CombatMeter] Loaded.");
    }

    public void Init()
    {
        CombatMeterOverlay.EnsureInstance();
    }

    public void Shutdown()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }

    public void ModObjectLoaded(Object obj)
    {
    }

    public void Update()
    {
    }
}
