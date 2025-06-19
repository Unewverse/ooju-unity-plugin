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
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    string name = assembly.GetName().Name;
                    if (name == "glTFast")
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void Install(string packageName)
        {
            string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("manifest.json not found!");
                return;
            }
            
            string manifest = File.ReadAllText(manifestPath);

            if (!manifest.Contains(packageName))
            {
                try
                {
                    int dependenciesIndex = manifest.IndexOf("\"dependencies\": {");
                    if (dependenciesIndex == -1)
                    {
                        return;
                    }
                    
                    dependenciesIndex += "\"dependencies\": {".Length;
                    string gltfFastEntry = $"\n    \"{packageName}\": \"6.10.1\",";
                    manifest = manifest.Insert(dependenciesIndex, gltfFastEntry);
                    
                    File.WriteAllText(manifestPath, manifest);
                    AssetDatabase.Refresh();
                    
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to modify manifest.json: {ex.Message}");
                }
            }
            else
            {
                Debug.Log($"{packageName} is already in manifest.json.");
            }
        }
    }
}