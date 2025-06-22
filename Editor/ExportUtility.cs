using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Reflection; 

using GLTFast;
using GLTFast.Export;

using UnityEditor;


public static class ExportUtility
{
    public static async Task<string> CustomExportGLB(GameObject go)
    {
        if (go == null)
        {
            return null;
        }

        if (!IsGltfFastInstalled())
        {
            return null;
        }

        string finalPath = Path.Combine(Application.temporaryCachePath, go.name + ".glb");

        try
        {
            Debug.Log($"Exporting GameObject: {go.name}");

            if (go.GetComponent<Renderer>() == null)
            {
                return null;
            }

            var exportSettings = new ExportSettings
            {
                Format = GltfFormat.Binary,
                FileConflictResolution = FileConflictResolution.Overwrite
            };

            var gameObjectExportSettings = new GameObjectExportSettings
            {
                OnlyActiveInHierarchy = false,
                DisabledComponents = true,
                LayerMask = LayerMask.GetMask("Default", "MyCustomLayer"),
            };

            var export = new GameObjectExport(exportSettings, gameObjectExportSettings);

            export.AddScene(
                new[] { go },
                go.transform.worldToLocalMatrix,
                "Exported Scene"
            );

            var success = await export.SaveToFileAndDispose(finalPath);

            if (success && File.Exists(finalPath))
            {
                long fileSize = new FileInfo(finalPath).Length;
                return finalPath;
            }
            else
            {
                return null;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Export error: {ex.Message}");
            return null;
        }
    }

    private static bool IsGltfFastInstalled()
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetName().Name == "glTFast");
    }

    public static void CustomExportGLBAsync(GameObject gameObject, string outputPath, Action<bool> onComplete)
    {
        try
        {
            GameObject tempObject = GameObject.Instantiate(gameObject);
            
            Task<string> exportTask = CustomExportGLB(tempObject);
            
            EditorApplication.CallbackFunction checkTaskStatus = null;
            checkTaskStatus = () => {
                if (!exportTask.IsCompleted)
                    return;
                
                EditorApplication.update -= checkTaskStatus;
                
                try
                {
                    if (tempObject != null)
                        GameObject.DestroyImmediate(tempObject);
                        
                    if (exportTask.IsFaulted)
                    {
                        onComplete(false);
                        return;
                    }
                    
                    string tempPath = exportTask.Result;
                    
                    if (string.IsNullOrEmpty(tempPath) || !File.Exists(tempPath))
                    {
                        onComplete(false);
                        return;
                    }
                    
                    string directory = Path.GetDirectoryName(outputPath);
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                    
                    File.Copy(tempPath, outputPath, true);
                    
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                        
                    AssetDatabase.Refresh();
                    
                    onComplete(true);
                }
                catch
                {
                    onComplete(false);
                }
            };
            
            EditorApplication.update += checkTaskStatus;
        }
        catch
        {
            onComplete(false);
        }
    }

}
