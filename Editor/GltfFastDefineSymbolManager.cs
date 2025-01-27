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
        var defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);

        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "GLTFast"))
        {
            if (!defineSymbols.Contains("GLTFast_EXPORT"))
            {
                defineSymbols += ";GLTFast_EXPORT";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defineSymbols);
                Debug.Log("GLTFast_EXPORT scripting define symbol added.");
            }
        }
        else
        {
            if (defineSymbols.Contains("GLTFast_EXPORT"))
            {
                defineSymbols = defineSymbols.Replace(";GLTFast_EXPORT", "");
                PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defineSymbols);
                Debug.Log("GLTFast_EXPORT scripting define symbol removed.");
            }
        }
    }
}
#endif
