using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time setup utility, NOT part of the mod itself - delete or ignore this file once
/// you've run it. Only needed after (re-)copying the game's assemblies into
/// Assets/Plugins/CoreKeeperModSDK.
///
/// Unity auto-references every "Editor compatible" Plugin DLL into every assembly in
/// the project by default - including other packages' own loose (asmdef-less) Editor
/// scripts. Confirmed by testing: a 100% vanilla SDK checkout with none of these DLLs
/// present compiles with zero errors, but once ~150 real game assemblies (Pug.*.dll etc,
/// needed so CombatMeter.asmdef can resolve them) are added as ordinary Plugin DLLs,
/// unrelated built-in Unity package Editor tooling (com.unity.2d.sprite,
/// com.unity.render-pipelines.core) starts failing to compile - almost certainly because
/// those packages' loose Editor scripts pick up one of these DLLs as an implicit
/// reference and something about the resulting assembly resolution goes wrong for them.
///
/// The fix: mark each of these DLLs "explicitly referenced" so Unity only wires them
/// into assemblies that ask for them by name (like CombatMeter.asmdef already does via
/// precompiledReferences+overrideReferences), not implicitly into everything. The
/// Inspector calls this checkbox "Auto Reference" (unchecked = explicitly referenced);
/// the underlying PluginImporter.isExplicitlyReferenced property is internal, so this
/// has to go through reflection rather than a normal method call.
///
/// Run via: Unity.exe -batchmode -quit -projectPath &lt;path&gt; -executeMethod SetupFixAutoReference.Run
/// </summary>
public static class SetupFixAutoReference
{
    private const string TargetFolder = "Assets/Plugins/CoreKeeperModSDK";

    private static readonly PropertyInfo ExplicitlyReferencedProperty = typeof(PluginImporter).GetProperty(
        "isExplicitlyReferenced",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

    public static void Run()
    {
        if (ExplicitlyReferencedProperty == null)
        {
            Debug.LogError("[SetupFixAutoReference] Could not find PluginImporter.isExplicitlyReferenced via reflection - Unity's internal API may have changed. Fix must be done by hand via the Inspector's 'Auto Reference' checkbox instead.");
            return;
        }

        int changed = 0;
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith(TargetFolder) || !path.EndsWith(".dll"))
            {
                continue;
            }

            if (AssetImporter.GetAtPath(path) is not PluginImporter importer || importer.isNativePlugin)
            {
                continue;
            }

            ExplicitlyReferencedProperty.SetValue(importer, true);
            importer.SaveAndReimport();
            changed++;
        }

        Debug.Log($"[SetupFixAutoReference] Marked {changed} managed plugin DLL(s) under {TargetFolder} as explicitly-referenced only.");
        AssetDatabase.SaveAssets();
    }
}
