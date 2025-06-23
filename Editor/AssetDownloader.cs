using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

namespace OojiCustomPlugin
{
    public static class AssetDownloader
    {
        private const string AssetsFolderName = "OOJU_Assets";
        private const string SyncPrefsKey = "UserAssetManager_LastSync";
        private const string AssetMetadataKey = "AssetID_";
        private static DateTime lastSyncTime;

        [Serializable]
        public class AssetMetadata
        {
            public string id;
            public string filename;
            public string presigned_url;
            public string asset_type;
            public string last_modified;
        }

        [Serializable]
        public class AssetListResponse
        {
            public List<AssetMetadata> assets;
        }
        
        public static bool AssetsAvailable { get; private set; } = false;
        
        public static int AssetCount { get; private set; } = 0;
        
        public static void StoreAssetId(string filename, string assetId)
        {
            if (!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(assetId))
            {
                EditorPrefs.SetString(AssetMetadataKey + filename, assetId);
            }
        }

        public static string GetAssetId(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return null;
            return EditorPrefs.GetString(AssetMetadataKey + filename, null);
        }

        public static string GetAssetIdFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;
            string filename = Path.GetFileName(filePath);
            return GetAssetId(filename);
        }
        
        public static IEnumerator DownloadAssets(string token, Action<string> onProgress, Action<string> onComplete, Action<string> onError)
        {
            onProgress?.Invoke("Fetching available assets...");
            AssetsAvailable = false;
            AssetCount = 0;

            UnityWebRequest request = UnityWebRequest.Get(NetworkUtility.BackendUrl + "/assets");
            request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Failed to fetch assets: {request.error}");
                yield break;
            }

            AssetListResponse assetList;
            try
            {
                string jsonResponse = "{ \"assets\": " + request.downloadHandler.text + "}";
                assetList = JsonUtility.FromJson<AssetListResponse>(jsonResponse);

                if (assetList.assets == null || assetList.assets.Count == 0)
                {
                    AssetsAvailable = false;
                    AssetCount = 0;
                    onComplete?.Invoke("No assets found to download.");
                    yield break;
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Error processing assets: {ex.Message}");
                Debug.LogError($"Asset download error: {ex}");
                yield break;  
            }

            AssetsAvailable = true;
            AssetCount = assetList.assets.Count;

            string assetsDirectory = Path.Combine(Application.dataPath, AssetsFolderName);
            if (!Directory.Exists(assetsDirectory))
            {
                Directory.CreateDirectory(assetsDirectory);
            }

            int total = assetList.assets.Count;
            int downloaded = 0;

            foreach (var asset in assetList.assets)
            {
                onProgress?.Invoke($"Downloading asset {downloaded + 1}/{total}: {asset.filename}");

                yield return DownloadAsset(asset, token, assetsDirectory,
                    (success) =>
                    {
                        downloaded++;
                        onProgress?.Invoke($"Progress: {downloaded}/{total} assets downloaded");
                    },
                    (error) =>
                    {
                        onError?.Invoke(error);
                    });
            }

            AssetDatabase.Refresh();
            SaveSyncTime();

            onComplete?.Invoke($"Successfully downloaded {downloaded} assets.");
        }
        
        private static IEnumerator DownloadAsset(AssetMetadata asset, string token, string assetsDirectory, Action<bool> onComplete, Action<string> onError)
        {
            if (asset == null)
            {
                onError?.Invoke("Asset object is null.");
                onComplete?.Invoke(false);
                yield break;
            }
            if (string.IsNullOrEmpty(asset.filename))
            {
                asset.filename = "unnamed_asset";
            }

            string assetPath = Path.Combine(assetsDirectory, asset.filename);
            bool needsDownload = true;
            
            if (File.Exists(assetPath) && lastSyncTime != default)
            {
                if (DateTime.TryParse(asset.last_modified, out DateTime assetModifiedTime))
                {
                    needsDownload = assetModifiedTime > lastSyncTime;
                }
            }
            
            if (!needsDownload)
            {
                onComplete?.Invoke(true);
                yield break;
            }

            string downloadUrl = asset.presigned_url;
            if (!downloadUrl.StartsWith("http"))
            {
                downloadUrl = $"{NetworkUtility.BackendUrl}{downloadUrl}";
            }
            
            UnityWebRequest downloadRequest = UnityWebRequest.Get(downloadUrl);
            downloadRequest.SetRequestHeader("Authorization", $"Bearer {token}");
            
            yield return downloadRequest.SendWebRequest();
            
            if (downloadRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Failed to download {asset.filename}: {downloadRequest.error}");
                onComplete?.Invoke(false);
                yield break;
            }
            
            try
            {
                string directory = Path.GetDirectoryName(assetPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllBytes(assetPath, downloadRequest.downloadHandler.data);
                
                if (!string.IsNullOrEmpty(asset.id))
                {
                    StoreAssetId(asset.filename, asset.id);
                }
                
                onComplete?.Invoke(true);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Error saving asset {asset.filename}: {ex.Message}");
                Debug.LogError($"Error saving asset {asset.filename}: {ex}");
                onComplete?.Invoke(false);
            }
        }
        
        public static void LoadSyncTime()
        {
            string lastSyncString = EditorPrefs.GetString(SyncPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(lastSyncString))
            {
                if (DateTime.TryParse(lastSyncString, out DateTime result))
                {
                    lastSyncTime = result;
                }
            }
        }
        
        public static void SaveSyncTime()
        {
            lastSyncTime = DateTime.UtcNow;
            EditorPrefs.SetString(SyncPrefsKey, lastSyncTime.ToString("o"));
        }
        
        public static IEnumerator SyncAssets(string token, Action<string> onProgress, Action<string> onComplete, Action<string> onError)
        {
            LoadSyncTime();
            yield return DownloadAssets(token, onProgress, onComplete, onError);
        }
        
        public static IEnumerator CheckAvailableAssets(string token, Action<bool, int> onComplete, Action<string> onError)
        {
            UnityWebRequest request = UnityWebRequest.Get(NetworkUtility.BackendUrl + "/assets/exportable");
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Failed to check assets: {request.error}");
                AssetsAvailable = false;
                AssetCount = 0;
                onComplete?.Invoke(false, 0);
                yield break;
            }
            
            try
            {
                string jsonResponse = "{ \"assets\": " + request.downloadHandler.text + "}";
                AssetListResponse assetList = JsonUtility.FromJson<AssetListResponse>(jsonResponse);
                
                if (assetList.assets == null || assetList.assets.Count == 0)
                {
                    AssetsAvailable = false;
                    AssetCount = 0;
                    onComplete?.Invoke(false, 0);
                }
                else
                {
                    AssetsAvailable = true;
                    AssetCount = assetList.assets.Count;
                    onComplete?.Invoke(true, assetList.assets.Count);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error checking assets: {ex.Message}");
                AssetsAvailable = false;
                AssetCount = 0;
                onError?.Invoke($"Error checking assets: {ex.Message}");
                onComplete?.Invoke(false, 0);
            }
        }
    }
}