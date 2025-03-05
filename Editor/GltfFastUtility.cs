using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Reflection;
using System;

namespace OojiCustomPlugin
{

    public static class GltfFastUtility
    {
        public static bool IsInstalled()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "glTFast");
        }

        public static void Install(string packageName)
        {
            string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            string manifest = File.ReadAllText(manifestPath);

            if (!manifest.Contains(packageName))
            {
                Debug.Log("Adding GLTFast to manifest.json...");
                int dependenciesIndex = manifest.IndexOf("\"dependencies\": {") + 16;
                string gltfFastEntry = $"\n    \"{packageName}\": \"6.10.1\",";
                manifest = manifest.Insert(dependenciesIndex, gltfFastEntry);
                File.WriteAllText(manifestPath, manifest);
                AssetDatabase.Refresh();
                Debug.Log("GLTFast added to manifest.json. Unity will now import the package.");
            }
            else
            {
                Debug.Log("GLTFast is already installed.");
            }
        }
    }
}