#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

[InitializeOnLoad]
public static class GltfFastDefineSymbolManager
{
    static GltfFastDefineSymbolManager()
    {
#if UNITY_2021_2_OR_NEWER
        var buildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone);
        var defineSymbols = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
#else
        var defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
#endif

        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "GLTFast"))
        {
            if (!defineSymbols.Contains("GLTFast_EXPORT"))
            {
                defineSymbols += ";GLTFast_EXPORT";
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, defineSymbols);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defineSymbols);
#endif
                Debug.Log("GLTFast_EXPORT scripting define symbol added.");
            }
        }
        else
        {
            if (defineSymbols.Contains("GLTFast_EXPORT"))
            {
                defineSymbols = defineSymbols.Replace(";GLTFast_EXPORT", "");
#if UNITY_2021_2_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, defineSymbols);
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defineSymbols);
#endif
                Debug.Log("GLTFast_EXPORT scripting define symbol removed.");
            }
        }
    }
}
#endif
