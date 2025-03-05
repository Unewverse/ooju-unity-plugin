using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEngine.Networking;
using System;

namespace OojiCustomPlugin
{
    public class AssetModificationHandler : Editor
    {
        [MenuItem("GameObject/OOJU/Update Asset", false, 0)]
        public static void UpdateAsset()
        {

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog(
                    "Update Asset",
                    "Please select an object in the hierarchy to update.",
                    "OK"
                );
                return;
            }
            

            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath))
            {
    
                assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selectedObject);
                if (string.IsNullOrEmpty(assetPath))
                {
                    EditorUtility.DisplayDialog(
                        "Update Asset",
                        "Could not determine the asset path for the selected object.",
                        "OK"
                    );
                    return;
                }
            }


            string fileName = Path.GetFileName(assetPath);
            string assetId = AssetDownloader.GetAssetId(fileName);
            if (string.IsNullOrEmpty(assetId))
            {
                EditorUtility.DisplayDialog(
                    "Asset ID Not Found",
                    "Could not determine the asset ID. Make sure it was originally downloaded from the OOJU server.",
                    "OK"
                );
                return;
            }
            

            string token = EditorPrefs.GetString("UserAssetManager_Token", "");
            if (string.IsNullOrEmpty(token))
            {
                EditorUtility.DisplayDialog(
                    "Authentication Required",
                    "Please log in through the OOJU Asset Manager.",
                    "OK"
                );
                return;
            }


            string extension = Path.GetExtension(assetPath).ToLower();
            if (extension == ".fbx" || extension == ".obj" || extension == ".glb")
            {
    
                UpdateModelAsset(selectedObject, assetId, token);
            }
            else if (extension == ".prefab")
            {
    
                UpdatePrefabAsset(selectedObject, assetPath, assetId, token);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Unsupported Asset",
                    "Only FBX, OBJ, GLB, or Prefab assets are supported for updating.",
                    "OK"
                );
            }
        }

        private static void UpdateModelAsset(GameObject selectedObject, string assetId, string token)
        {
            EditorUtility.DisplayProgressBar("Updating Model", "Preparing to export...", 0.1f);
            
            string fileName = selectedObject.name + ".glb"; 
            string localAssetPath = Path.Combine("Assets/OOJU_Assets", fileName);
            string localFullPath = Path.GetFullPath(localAssetPath);

            ExportUtility.CustomExportGLBAsync(selectedObject, localFullPath, (success) =>
            {
                EditorUtility.ClearProgressBar();
                if (!success)
                {
                    EditorUtility.DisplayDialog(
                        "Update Asset",
                        "Failed to export model. See console for details.",
                        "OK"
                    );
                    return;
                }

    
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(localAssetPath, ImportAssetOptions.ForceUpdate);

    
                EditorCoroutineUtility.StartCoroutineOwnerless(
                    UpdateAssetCoroutine(localFullPath, assetId, token)
                );
            });
        }

        private static void UpdatePrefabAsset(GameObject selectedObject, 
                                              string originalAssetPath, 
                                              string assetId, 
                                              string token)
        {

            PrefabUtility.ApplyPrefabInstance(selectedObject, InteractionMode.UserAction);


            string fileName = Path.GetFileName(originalAssetPath);
            string updatedLocalAssetPath = Path.Combine("Assets/OOJU_Assets", fileName);
            string updatedLocalAssetFullPath = Path.GetFullPath(updatedLocalAssetPath);


            if (!originalAssetPath.StartsWith("Assets/OOJU_Assets"))
            {
    
                string moveError = AssetDatabase.MoveAsset(originalAssetPath, updatedLocalAssetPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    EditorUtility.DisplayDialog(
                        "Prefab Move Error",
                        $"Could not move prefab to OOJU_Assets folder:\n{moveError}",
                        "OK"
                    );
                    return;
                }
                AssetDatabase.Refresh();
            }


            AssetDatabase.ImportAsset(updatedLocalAssetPath, ImportAssetOptions.ForceUpdate);


            EditorCoroutineUtility.StartCoroutineOwnerless(
                UpdateAssetCoroutine(updatedLocalAssetFullPath, assetId, token)
            );
        }


        private static IEnumerator UpdateAssetCoroutine(string fullPath, string assetId, string token)
        {
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog(
                    "Update Failed",
                    $"File not found: {fullPath}",
                    "OK"
                );
                yield break;
            }


            EditorUtility.DisplayProgressBar("Updating Asset", "Uploading file...", 0.5f);


            byte[] fileData = null;
            string fileName = Path.GetFileName(fullPath);
            try
            {
                fileData = File.ReadAllBytes(fullPath);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "Update Failed",
                    $"Error reading file: {ex.Message}",
                    "OK"
                );
                yield break;
            }


            WWWForm form = new WWWForm();
            form.AddBinaryData("file", fileData, fileName, "model/gltf-binary");


            UnityWebRequest www = UnityWebRequest.Post(
                NetworkUtility.BackendUrl + $"/assets/{assetId}",
                form
            );
            www.method = "PUT";
            www.SetRequestHeader("Authorization", "Bearer " + token);


            var operation = www.SendWebRequest();
            while (!operation.isDone)
            {
                yield return null;
            }

            EditorUtility.ClearProgressBar();


            if (www.result == UnityWebRequest.Result.Success)
            {
                EditorUtility.DisplayDialog(
                    "Update Complete",
                    $"The asset has been updated successfully.",
                    "OK"
                );
            }
            else
            {
                string errorMsg = www.error;
                if (!string.IsNullOrEmpty(www.downloadHandler.text))
                {
                    errorMsg += " - " + www.downloadHandler.text;
                }
                
                EditorUtility.DisplayDialog(
                    "Update Failed",
                    $"Failed to update the asset: {errorMsg}",
                    "OK"
                );
            }
            
            www.Dispose();
        }
    }
}
