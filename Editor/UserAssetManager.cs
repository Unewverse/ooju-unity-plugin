using UnityEditor;
using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;


namespace OojiCustomPlugin
{
    public class UserAssetManager : EditorWindow
    {
        private const string TokenPrefKey = "UserAssetManager_Token";
        private const string GltfFastDependency = "com.unity.cloud.gltfast";
        private const string AutoSyncPrefKey = "UserAssetManager_AutoSync";
        private const int AutoSyncIntervalMinutes = 15;

        
        private string authToken = "";
        private string uploadStatus = "";
        private string userEmail = "";
        private string userPassword = "";
        private string downloadStatus = "";
        private bool autoSyncEnabled = false;
        private double lastSyncCheckTime = 0;
        private bool isCheckingAssets = false;
        private bool assetsAvailable = false;
        private int assetCount = 0;

        
        private bool isLoggingIn = false;
        private bool isExportingAndUploading = false;
        private bool isDownloading = false;
        private Vector2 scrollPosition;

        private bool isGltfFastInstalled = false;

        private UIStyles styles;

        [MenuItem("Tools/OOJU Asset Manager")]
        public static void ShowWindow()
        {
            GetWindow<UserAssetManager>("OOJU Asset Manager");
        }

        private void OnEnable()
        {
            styles = new UIStyles();
            
            isGltfFastInstalled = GltfFastUtility.IsInstalled();

            authToken = EditorPrefs.GetString(TokenPrefKey, "");
            if (!string.IsNullOrEmpty(authToken))
            {
                Debug.Log("Loaded saved token.");
                CheckAssets();
            }

            autoSyncEnabled = EditorPrefs.GetBool(AutoSyncPrefKey, false);

            AssetDownloader.LoadSyncTime();

            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(authToken))
            {
                EditorPrefs.SetString(TokenPrefKey, authToken);
                Debug.Log("Token saved.");
            }

            EditorPrefs.SetBool(AutoSyncPrefKey, autoSyncEnabled);

            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (autoSyncEnabled && !string.IsNullOrEmpty(authToken) && !isDownloading)
            {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - lastSyncCheckTime > AutoSyncIntervalMinutes * 60)
                {
                    lastSyncCheckTime = currentTime;
                    TriggerAutoSync();
                }
            }
        }

        private List<NetworkUtility.ExportableAsset> availableAssets = new List<NetworkUtility.ExportableAsset>();
        private List<string> selectedAssetIds = new List<string>();

        private void CheckAssets()
        {
            if (string.IsNullOrEmpty(authToken) || isCheckingAssets)
                return;
                
            isCheckingAssets = true;
            
            if (availableAssets == null)
                availableAssets = new List<NetworkUtility.ExportableAsset>();
            else
                availableAssets.Clear();
            
            if (selectedAssetIds == null)
                selectedAssetIds = new List<string>();
            
            EditorCoroutineUtility.StartCoroutineOwnerless(NetworkUtility.GetExportableAssets(
                authToken,
                (assets) => {
                    if (assets != null) {
                        availableAssets = assets;
                        assetsAvailable = assets.Count > 0;
                        assetCount = assets.Count;
                    } else {
                        availableAssets = new List<NetworkUtility.ExportableAsset>();
                        assetsAvailable = false;
                        assetCount = 0;
                    }
                    isCheckingAssets = false;
                    Repaint();
                },
                (error) => {
                    Debug.LogError($"Error checking assets: {error}");
                    availableAssets = new List<NetworkUtility.ExportableAsset>();
                    assetsAvailable = false;
                    assetCount = 0;
                    isCheckingAssets = false;
                    Repaint();
                }
            ));
        }


        private void DownloadSelectedAssets(string[] assetIds)
        {
            if (assetIds == null || assetIds.Length == 0)
            {
                downloadStatus = "No assets selected for download.";
                return;
            }
            
            downloadStatus = $"Preparing to download {assetIds.Length} selected assets...";
            isDownloading = true;
            
            EditorCoroutineUtility.StartCoroutineOwnerless(DownloadAssetsCoroutine(assetIds));
        }

        private IEnumerator DownloadAssetsCoroutine(string[] assetIds)
        {
            int total = assetIds.Length;
            int downloaded = 0;
            int failed = 0;
            
            string assetsDirectory = Path.Combine(Application.dataPath, "OOJU_Assets");
            if (!Directory.Exists(assetsDirectory))
            {
                Directory.CreateDirectory(assetsDirectory);
            }
            
            foreach (string assetId in assetIds)
            {
                var asset = availableAssets.Find(a => a.id == assetId);
                if (asset == null)
                {
                    failed++;
                    continue;
                }
                
                string fileName = asset.filename;
                string localPath = Path.Combine(assetsDirectory, fileName);
                
                downloadStatus = $"Downloading {downloaded+1}/{total}: {fileName}...";
                Repaint();
                
                bool success = false;
                yield return NetworkUtility.DownloadAsset(
                    assetId,
                    authToken,
                    localPath,
                    (result) => {
                        success = result;
                        if (success) {
                            AssetDownloader.StoreAssetId(fileName, assetId);
                        }
                    },
                    (progress) => {
                        downloadStatus = $"Downloading {fileName}: {progress:P0}";
                        Repaint();
                    }
                );
                
                if (success)
                    downloaded++;
                else
                    failed++;
            }
            
            AssetDatabase.Refresh();
            
            downloadStatus = $"Download complete: {downloaded} succeeded, {failed} failed.";
            isDownloading = false;
            Repaint();
        }
        
        private void OnGUI()
        {

            if (styles == null || !styles.IsInitialized)
            {
                styles = new UIStyles();
                styles.Initialize();
            }
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            UIRenderer.DrawHeader("OOJU Asset Manager", styles);
            
            GUILayout.Space(10);

            UIRenderer.DrawGltfFastStatus(isGltfFastInstalled, InstallGltfFast);

            if (string.IsNullOrEmpty(authToken))
            {
                DrawLoginUI();
            }
            else
            {
                DrawLoggedInUI();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLoginUI()
        {
            UIRenderer.DrawLoginUI(
                ref userEmail,
                ref userPassword,
                isLoggingIn,
                uploadStatus,
                () => {
                    uploadStatus = "";
                    isLoggingIn = true;
                    EditorCoroutineUtility.StartCoroutineOwnerless(LoginCoroutine());
                },
                styles
            );
        }

        private void DrawLoggedInUI()
        {
            
            UIRenderer.DrawAccountBar(userEmail, () => {
                authToken = "";
                EditorPrefs.DeleteKey(TokenPrefKey);
                uploadStatus = "Logged out.";
                downloadStatus = "";
                assetsAvailable = false;
                assetCount = 0;
                Debug.Log("Logged out.");
            });
            
            GUILayout.Space(10);
            

            bool useWideLayout = position.width > 500;
            
            if (useWideLayout)
            {
                EditorGUILayout.BeginHorizontal();
                

                EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
                DrawUploadSection();
                EditorGUILayout.EndVertical();
                
                GUILayout.Space(10);
                

                EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
                DrawDownloadSection();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawUploadSection();
                GUILayout.Space(15);
                DrawDownloadSection();
            }
        }
        
        private void DrawUploadSection()
        {
            UIRenderer.DrawUploadSection(
                Selection.activeGameObject,
                isExportingAndUploading,
                isGltfFastInstalled,
                uploadStatus,
                styles,
                ExportAndUploadGLB
            );
        }
        
        private void DrawDownloadSection()
        {
            UIRenderer.DrawDownloadSection(
                assetsAvailable,
                assetCount,
                availableAssets,
                ref selectedAssetIds,
                isCheckingAssets,
                isDownloading,
                autoSyncEnabled,
                AutoSyncIntervalMinutes,
                downloadStatus,
                styles,
                CheckAssets,
                (newValue) => {
                    autoSyncEnabled = newValue;
                    EditorPrefs.SetBool(AutoSyncPrefKey, autoSyncEnabled);
                },
                DownloadSelectedAssets,
                SyncChangedAssets
            );
        }

        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        

        private IEnumerator LoginCoroutine()
        {
            string username = userEmail.Trim();
            string password = userPassword;

            yield return NetworkUtility.Login(
                username,
                password,
                (token) =>
                {
                    authToken = token;
                    EditorPrefs.SetString(TokenPrefKey, authToken);
                    uploadStatus = "Login successful!";
                    isLoggingIn = false;
                    
                    
                    
                    
                    CheckAssets();
                },
                (error) =>
                {
                    uploadStatus = $"Login failed: {error}";
                    isLoggingIn = false;
                }
            );
        }

        private async void ExportAndUploadGLB(GameObject selectedObject)
        {
            if (!isGltfFastInstalled)
            {
                uploadStatus = "GLTFast is not installed. Export functionality is unavailable.";
                return;
            }

            uploadStatus = "Preparing to export...";
            isExportingAndUploading = true;

            string glbPath = await ExportUtility.CustomExportGLB(selectedObject);

            if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
            {
                uploadStatus = $"Export failed: GLB file not found at {glbPath}";
                isExportingAndUploading = false;
                return;
            }

            uploadStatus = "Export successful. Uploading...";

            EditorCoroutineUtility.StartCoroutineOwnerless(NetworkUtility.UploadFile(
                glbPath,
                authToken,
                (success) =>
                {
                    uploadStatus = $"Upload successful: {success}";
                    isExportingAndUploading = false;
                    
                    CheckAssets();
                },
                (error) =>
                {
                    uploadStatus = $"Upload failed: {error}";
                    Debug.LogError(uploadStatus);
                    isExportingAndUploading = false;
                }
            ));
        }

        private void InstallGltfFast()
        {
            if (EditorUtility.DisplayDialog(
                "Install GLTFast",
                "GLTFast is required for exporting assets. Would you like to install it now?",
                "Install",
                "Cancel"))
            {
                GltfFastUtility.Install(GltfFastDependency);
            }
        }

        private void TriggerAutoSync()
        {
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            Repaint();
        }

        private void SyncChangedAssets()
        {
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
            
        }

        
        
        
        
            
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        

        
        
        
        
        
        
        
        
        
        
            
        
            
        
        
            
        
        
        
        
            
        
        
    }
}
