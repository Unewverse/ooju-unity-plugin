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
            Debug.LogError("No GameObject provided for export!");
            return null;
        }

        if (!IsGltfFastInstalled())
        {
            Debug.LogWarning("GLTFast is not installed. Please install it via the Unity Package Manager to enable export functionality.");
            return null;
        }

        string finalPath = Path.Combine(Application.temporaryCachePath, go.name + ".glb");

        try
        {
            Debug.Log($"Exporting GameObject: {go.name}");

            if (go.GetComponent<Renderer>() == null)
            {
                Debug.LogError($"GameObject {go.name} does not have a Renderer component and might not be exportable.");
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
                Debug.Log($"Export successful. File size: {fileSize} bytes.");
                return finalPath;
            }
            else
            {
                Debug.LogError("glTFast export failed or file was not created.");
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
                        // Now we're on the main thread, safe to call DestroyImmediate
                        if (tempObject != null)
                            GameObject.DestroyImmediate(tempObject);
                            
                        if (exportTask.IsFaulted)
                        {
                            Debug.LogError($"Export failed: {exportTask.Exception}");
                            onComplete(false);
                            return;
                        }
                        
                        string tempPath = exportTask.Result;
                        
                        if (string.IsNullOrEmpty(tempPath) || !File.Exists(tempPath))
                        {
                            Debug.LogError("Export failed: No output file was generated");
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
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error in export completion: {ex.Message}");
                        onComplete(false);
                    }
                };
                
                EditorApplication.update += checkTaskStatus;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error starting export: {ex.Message}");
                onComplete(false);
            }
        }

}
