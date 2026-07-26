using System;
using System.IO;
using PugMod;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time setup utility, NOT part of the mod itself - builds CombatMeter and installs
/// it directly into the game's StreamingAssets/Mods folder for local testing, without
/// needing to click through the Mod SDK window's Create Mod tab (which is meant for
/// scaffolding a brand new mod from a template and would overwrite this mod's existing,
/// already-hand-written asmdef).
///
/// Run via: Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod SetupBuildAndInstallMod.Run
/// Requires the project to already compile with 0 errors (Unity refuses to run
/// -executeMethod at all otherwise).
/// </summary>
public static class SetupBuildAndInstallMod
{
    private const string ModName = "CombatMeter";
    private const string ModPath = "Assets/CombatMeter";
    private const string SettingsAssetPath = "Assets/CombatMeter.asset";
    private const string GameInstallPath = @"C:\Program Files (x86)\Steam\steamapps\common\Core Keeper";

    public static void Run()
    {
        var settings = AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(SettingsAssetPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ModBuilderSettings>();
            settings.metadata.guid = Guid.NewGuid().ToString("N");
            settings.metadata.name = ModName;
            settings.metadata.accessesExtraAssemblies = true;
            settings.metadata.requiredOn = ModMetadata.ModExistsOn.ClientAndServer;
            settings.metadata.files = new System.Collections.Generic.List<ModFile>();
            settings.metadata.dependencies = new System.Collections.Generic.List<ModMetadata.Dependency>();
            settings.modPath = ModPath;
            settings.buildBundles = false; // no prefabs/scriptable data to bundle - just scripts
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupBuildAndInstallMod] Created new ModBuilderSettings at {SettingsAssetPath}");
        }

        // Defensive: Unity's serializer only coerces null List<T> fields to empty lists
        // after a full save/reload round-trip, which doesn't happen within a single
        // in-memory script run.
        settings.metadata.files ??= new System.Collections.Generic.List<ModFile>();
        settings.metadata.dependencies ??= new System.Collections.Generic.List<ModMetadata.Dependency>();

        string installRoot = Path.Combine(GameInstallPath, "CoreKeeper_Data", "StreamingAssets", "Mods");

        ModBuilder.BuildMod(settings, installRoot, success =>
        {
            if (success)
            {
                Debug.Log($"[SetupBuildAndInstallMod] SUCCESS - mod built and installed to {Path.Combine(installRoot, ModName)}");
            }
            else
            {
                Debug.LogError("[SetupBuildAndInstallMod] FAILED to build/install mod - see errors above.");
            }
        });
    }
}
