using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.EditorCoroutines.Editor;

namespace OojiCustomPlugin
{
    public class AssetSyncManager
    {
        private const string SyncManifestFile = "ooju_sync_manifest.json";
        private static string AssetsFolderPath => Path.Combine(Application.dataPath, "OOJU_Assets");
        
        [Serializable]
        private class SyncManifest
        {
            public List<AssetRecord> assets = new List<AssetRecord>();
        }
        
        [Serializable]
        private class AssetRecord
        {
            public string id;
            public string localPath;
            public string lastUpdated;
        }

        [InitializeOnLoadMethod]
        private static void RegisterAssetModificationHook()
        {
            AssetModificationProcessor.RegisterAssetModificationCallback(OnAssetModified);
        }
        
        
        private class AssetModificationProcessor : UnityEditor.AssetModificationProcessor
        {
            private static Action<string[]> assetModificationCallback;
            
            public static void RegisterAssetModificationCallback(Action<string[]> callback)
            {
                assetModificationCallback = callback;
            }
            
            private static string[] OnWillSaveAssets(string[] paths)
            {
                assetModificationCallback?.Invoke(paths);
                return paths;
            }
        }
        
        private static List<string> _uploadQueue = new List<string>();

        private static void OnAssetModified(string[] paths)
        {
            
            string assetsFolder = Path.GetFullPath(AssetsFolderPath).Replace('\\', '/');
            
            
            foreach (string path in paths)
            {
                string fullPath = Path.GetFullPath(path).Replace('\\', '/');
                
                
                if (fullPath.StartsWith(assetsFolder))
                {
                    if (!_uploadQueue.Contains(fullPath))
                    {
                        _uploadQueue.Add(fullPath);
                        Debug.Log($"Queued modified OOJU asset for upload: {Path.GetFileName(fullPath)}");
                    }
                }
            }
            
            
            
            
            
            
            
            
            
            
        }
        
        
        private static IEnumerator ProcessUploadQueue(string token)
        {
            if (_uploadQueue.Count == 0 || string.IsNullOrEmpty(token))
            {
                yield break;
            }
            
            while (_uploadQueue.Count > 0)
            {
                string assetPath = _uploadQueue[0];
                _uploadQueue.RemoveAt(0);
                
                
                if (!File.Exists(assetPath))
                    continue;
                
                string fileName = Path.GetFileName(assetPath);
                
                Debug.Log($"Uploading modified asset: {fileName}");
                
                bool uploadSuccess = false;
                yield return NetworkUtility.UploadFile(
                    assetPath,
                    token,
                    (success) => { uploadSuccess = true; },
                    (error) => { Debug.LogError($"Failed to upload {fileName}: {error}"); }
                );
                
                if (uploadSuccess)
                {
                    Debug.Log($"Successfully uploaded asset: {fileName}");
                }
                
                
                yield return new WaitForSeconds(0.2f);
            }
            

        }
        
        
        public static IEnumerator UploadAllAssets(
            string token,
            Action<string> progressCallback = null,
            Action<bool> completeCallback = null)
        {
            if (string.IsNullOrEmpty(token))
            {
                progressCallback?.Invoke("Authentication token is required");
                completeCallback?.Invoke(false);
                yield break;
            }
            
            string assetsFolderPath = AssetsFolderPath;
            if (!Directory.Exists(assetsFolderPath))
            {
                progressCallback?.Invoke("No assets folder found");
                completeCallback?.Invoke(false);
                yield break;
            }
            
            
            string[] allFiles = Directory.GetFiles(assetsFolderPath, "*.*", SearchOption.AllDirectories);
            
            
            List<string> assetFiles = new List<string>();
            foreach (string file in allFiles)
            {
                
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                
                if (Path.GetFileName(file).Equals(SyncManifestFile, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                assetFiles.Add(file);
            }
            
            int total = assetFiles.Count;
            int processed = 0;
            int succeeded = 0;
            
            if (total == 0)
            {
                progressCallback?.Invoke("No valid assets found to upload");
                completeCallback?.Invoke(true);
                yield break;
            }
            
            progressCallback?.Invoke($"Found {total} assets to upload");
            
            foreach (string filePath in assetFiles)
            {
                processed++;
                string fileName = Path.GetFileName(filePath);
                progressCallback?.Invoke($"Uploading asset {processed}/{total}: {fileName}");
                
                bool uploadSuccess = false;
                yield return NetworkUtility.UploadFile(
                    filePath,
                    token,
                    (success) => { uploadSuccess = true; },
                    (error) => { Debug.LogError($"Failed to upload asset: {error}"); }
                );
                
                if (uploadSuccess)
                    succeeded++;
                
                yield return new WaitForSeconds(0.2f);
            }
            
            progressCallback?.Invoke($"Upload complete: {succeeded}/{total} assets uploaded successfully");
            completeCallback?.Invoke(true);
        }


        private static SyncManifest LoadManifest()
        {
            string manifestPath = Path.Combine(AssetsFolderPath, SyncManifestFile);
            
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    return JsonUtility.FromJson<SyncManifest>(json) ?? new SyncManifest();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading sync manifest: {ex.Message}");
                }
            }
            
            return new SyncManifest();
        }
        
        private static void SaveManifest(SyncManifest manifest)
        {
            string manifestPath = Path.Combine(AssetsFolderPath, SyncManifestFile);
            
            
            if (!Directory.Exists(AssetsFolderPath))
            {
                Directory.CreateDirectory(AssetsFolderPath);
            }
            
            try
            {
                string json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(manifestPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving sync manifest: {ex.Message}");
            }
        }
        
        public static void SyncAssets(string token, Action<string> onProgress, Action<bool> onComplete)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(SyncAssetsCoroutine(token, onProgress, onComplete));
        }
        
        private static IEnumerator SyncAssetsCoroutine(string token, Action<string> onProgress, Action<bool> onComplete)
        {
            onProgress?.Invoke("Fetching available assets...");
            
            
            SyncManifest manifest = LoadManifest();
            
            
            Dictionary<string, AssetRecord> existingAssets = new Dictionary<string, AssetRecord>();
            foreach (var asset in manifest.assets)
            {
                existingAssets[asset.id] = asset;
            }
            
            
            List<NetworkUtility.ExportableAsset> serverAssets = null;
            bool requestComplete = false;
            string requestError = null;
            
            yield return NetworkUtility.GetExportableAssets(
                token,
                (assets) => {
                    serverAssets = assets;
                    requestComplete = true;
                },
                (error) => {
                    requestError = error;
                    requestComplete = true;
                }
            );
            
            if (!requestComplete || requestError != null)
            {
                onProgress?.Invoke($"Error fetching assets: {requestError}");
                onComplete?.Invoke(false);
                yield break;
            }
            
            if (serverAssets == null || serverAssets.Count == 0)
            {
                onProgress?.Invoke("No assets found to sync.");
                onComplete?.Invoke(true);
                yield break;
            }
            
            onProgress?.Invoke($"Found {serverAssets.Count} assets. Checking for updates...");
            
            
            if (!Directory.Exists(AssetsFolderPath))
            {
                Directory.CreateDirectory(AssetsFolderPath);
            }
            
            int downloadCount = 0;
            int upToDateCount = 0;
            
            
            foreach (var serverAsset in serverAssets)
            {
                
                if (existingAssets.TryGetValue(serverAsset.id, out AssetRecord record))
                {
                    
                    DateTime serverDate = DateTime.Parse(serverAsset.created_at);
                    DateTime localDate = DateTime.Parse(record.lastUpdated);
                    
                    if (serverDate <= localDate && File.Exists(record.localPath))
                    {
                        
                        upToDateCount++;
                        continue;
                    }
                }
                
                
                string fileName = serverAsset.filename;
                string localPath = Path.Combine(AssetsFolderPath, fileName);
                
                onProgress?.Invoke($"Downloading {fileName}...");
                
                bool downloadSuccess = false;
                yield return NetworkUtility.DownloadAsset(
                    serverAsset.id,
                    token,
                    localPath,
                    (success) => downloadSuccess = success,
                    (progress) => onProgress?.Invoke($"Downloading {fileName}: {progress:P0}")
                );
                
                if (downloadSuccess)
                {
                    
                    AssetRecord newRecord = new AssetRecord
                    {
                        id = serverAsset.id,
                        localPath = localPath,
                        lastUpdated = serverAsset.created_at
                    };
                    
                    if (existingAssets.ContainsKey(serverAsset.id))
                    {
                        
                        int index = manifest.assets.FindIndex(a => a.id == serverAsset.id);
                        if (index >= 0)
                        {
                            manifest.assets[index] = newRecord;
                        }
                    }
                    else
                    {
                        
                        manifest.assets.Add(newRecord);
                        existingAssets[serverAsset.id] = newRecord;
                    }
                    
                    downloadCount++;
                }
            }
            
            
            SaveManifest(manifest);
            
            
            AssetDatabase.Refresh();
            
            onProgress?.Invoke($"Sync complete. Downloaded {downloadCount} assets, {upToDateCount} already up to date.");
            onComplete?.Invoke(true);
        }
    }
}
