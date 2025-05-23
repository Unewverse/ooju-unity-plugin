using UnityEditor;
using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEditor.IMGUI.Controls;

namespace OojiCustomPlugin
{
    public class UserAssetManager : EditorWindow
    {
        private const string TokenPrefKey = "UserAssetManager_Token";
        private const string GltfFastDependency = "com.unity.cloud.gltfast";
        private const string AutoSyncPrefKey = "UserAssetManager_AutoSync";
        private const int AutoSyncIntervalMinutes = 15;

        // Tab system
        private enum Tab { Import, Assets, Settings }
        private Tab currentTab = Tab.Import;
        private string[] tabNames = { "Import", "My Assets", "Settings" };

        // Auth and status
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

        // UI States
        private bool isLoggingIn = false;
        private bool isExportingAndUploading = false;
        private bool isDownloading = false;
        private Vector2 scrollPosition;
        private Vector2 assetGridScrollPosition;
        private bool isGltfFastInstalled = false;

        // Drag and drop
        private bool isDraggingFile = false;
        private List<string> pendingUploadFiles = new List<string>();

        // Asset previews
        private Dictionary<string, Texture2D> assetPreviews = new Dictionary<string, Texture2D>();
        private int previewSize = 100;
        private bool isGeneratingPreviews = false;

        // UI and styles
        private UIStyles styles;
        private SearchField assetSearchField;
        private string assetSearchQuery = "";

        // Assets management
        private List<NetworkUtility.ExportableAsset> availableAssets = new List<NetworkUtility.ExportableAsset>();
        private List<NetworkUtility.ExportableAsset> filteredAssets = new List<NetworkUtility.ExportableAsset>();
        private List<string> selectedAssetIds = new List<string>();

        [System.Serializable] class SceneFile { public string sceneName; public Obj[] objects; }
        [System.Serializable] class Obj { public string id, name, type, mesh; public Xf transform; }
        [System.Serializable] class Xf  { public Vector3 position, rotation, scale; }

        [MenuItem("OOJU/Asset Manager")]
        public static void ShowWindow()
        {
            GetWindow<UserAssetManager>("OOJU Asset Manager");
        }

        private void OnEnable()
        {
            styles = new UIStyles();
            styles.Initialize();

            assetSearchField = new SearchField();

            // Use NetworkUtility to get the stored token
            authToken = NetworkUtility.GetStoredToken();

            if (NetworkUtility.HasValidStoredToken())
            {
                Debug.Log("Loaded saved token.");
                CheckAssets();
            }
            else
            {
                Debug.Log("No valid token found.");
                authToken = ""; // Make sure it's empty if no valid token
            }

            autoSyncEnabled = EditorPrefs.GetBool(AutoSyncPrefKey, false);
            AssetDownloader.LoadSyncTime();

            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {

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
                    CheckAssets();
                }
            }
        }

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

            if (filteredAssets == null)
                filteredAssets = new List<NetworkUtility.ExportableAsset>();

            EditorCoroutineUtility.StartCoroutineOwnerless(SafeGetExportableAssetsCoroutine());
        }

        private IEnumerator SafeGetExportableAssetsCoroutine()
        {
            bool requestCompleted = false;
            bool requestSucceeded = false;
            List<NetworkUtility.ExportableAsset> loadedAssets = null;
            string errorMessage = "";

            EditorCoroutineUtility.StartCoroutineOwnerless(
                NetworkUtility.GetExportableAssets(
                    authToken,
                    (assets) =>
                    {
                        requestCompleted = true;
                        requestSucceeded = true;
                        loadedAssets = assets;
                    },
                    (error) =>
                    {
                        requestCompleted = true;
                        requestSucceeded = false;
                        errorMessage = error;
                    }
                )
            );

            float timeout = 20f; // 30 seconds timeout
            float elapsed = 0f;

            while (!requestCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (!requestCompleted)
            {
                isCheckingAssets = false;
                Repaint();
                yield break;
            }

            if (!requestSucceeded)
            {
                availableAssets = new List<NetworkUtility.ExportableAsset>();
                filteredAssets = new List<NetworkUtility.ExportableAsset>();
                assetsAvailable = false;
                assetCount = 0;
                isCheckingAssets = false;
                Repaint();
                yield break;
            }

            if (loadedAssets != null)
            {
                loadedAssets.RemoveAll(a => a == null);

                availableAssets = loadedAssets;
                assetsAvailable = loadedAssets.Count > 0;
                assetCount = loadedAssets.Count;

                FilterAssets();

                if (assetsAvailable && loadedAssets.Count > 0)
                {
                    yield return null;
                    EditorCoroutineUtility.StartCoroutineOwnerless(SafeGeneratePreviewsCoroutine());
                }
            }
            else
            {
                availableAssets = new List<NetworkUtility.ExportableAsset>();
                filteredAssets = new List<NetworkUtility.ExportableAsset>();
                assetsAvailable = false;
                assetCount = 0;
            }

            isCheckingAssets = false;
            Repaint();
        }

        private IEnumerator SafeGeneratePreviewsCoroutine()
        {
            if (assetPreviews == null)
                assetPreviews = new Dictionary<string, Texture2D>();

            if (availableAssets == null || availableAssets.Count == 0)
                yield break;

            for (int i = 0; i < availableAssets.Count; i++)
            {
                var asset = availableAssets[i];
                if (asset == null || string.IsNullOrEmpty(asset.id))
                    continue;

                if (assetPreviews.ContainsKey(asset.id) && assetPreviews[asset.id] != null)
                    continue;

                Texture2D defaultIcon = null;

                if ((asset.content_type != null && asset.content_type.Contains("model")) ||
                    (asset.filename != null && (asset.filename.EndsWith(".glb") || asset.filename.EndsWith(".gltf"))))
                {
                    defaultIcon = EditorGUIUtility.FindTexture("d_Mesh Icon");
                }
                else if ((asset.content_type != null && asset.content_type.Contains("image")) ||
                        (asset.filename != null && (asset.filename.EndsWith(".png") || asset.filename.EndsWith(".jpg"))))
                {
                    defaultIcon = EditorGUIUtility.FindTexture("d_Image Icon");
                }
                else
                {
                    defaultIcon = EditorGUIUtility.FindTexture("d_DefaultAsset Icon");
                }

                if (defaultIcon != null)
                {
                    assetPreviews[asset.id] = defaultIcon;
                }

                if (i % 5 == 0)
                    yield return null;
            }

            Repaint();
        }


        private void FilterAssets()
        {
            try
            {
                if (availableAssets == null)
                    availableAssets = new List<NetworkUtility.ExportableAsset>();

                if (filteredAssets == null)
                    filteredAssets = new List<NetworkUtility.ExportableAsset>();

                filteredAssets.Clear();

                if (string.IsNullOrWhiteSpace(assetSearchQuery))
                {
                    foreach (var asset in availableAssets)
                    {
                        if (asset != null)
                            filteredAssets.Add(asset);
                    }
                    return;
                }

                string query = assetSearchQuery.ToLowerInvariant();

                foreach (var asset in availableAssets)
                {
                    if (asset == null)
                        continue;

                    bool matchesSearch = false;

                    if (asset.filename != null && asset.filename.ToLowerInvariant().Contains(query))
                        matchesSearch = true;

                    if (asset.content_type != null && asset.content_type.ToLowerInvariant().Contains(query))
                        matchesSearch = true;

                    if (matchesSearch)
                        filteredAssets.Add(asset);
                }
            }
            catch (Exception ex)
            {
                if (filteredAssets == null)
                    filteredAssets = new List<NetworkUtility.ExportableAsset>();

                filteredAssets.Clear();
                if (availableAssets != null)
                {
                    foreach (var asset in availableAssets)
                    {
                        if (asset != null)
                            filteredAssets.Add(asset);
                    }
                }
            }
        }

        private void GenerateAssetPreviews()
        {
            if (isGeneratingPreviews || availableAssets == null || availableAssets.Count == 0)
                return;

            if (assetPreviews == null)
                assetPreviews = new Dictionary<string, Texture2D>();

            isGeneratingPreviews = true;
            EditorCoroutineUtility.StartCoroutineOwnerless(GeneratePreviewsCoroutine());
        }

        private IEnumerator GeneratePreviewsCoroutine()
        {
            isGeneratingPreviews = true;

            foreach (var asset in availableAssets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.id))
                    continue;

                if (assetPreviews.ContainsKey(asset.id + "_real") && assetPreviews[asset.id + "_real"] != null)
                    continue;

                string fileName = asset.filename;
                if (string.IsNullOrEmpty(fileName))
                    continue;

                Texture2D defaultIcon = GetDefaultIconForAsset(asset);
                if (defaultIcon != null && !assetPreviews.ContainsKey(asset.id))
                {
                    assetPreviews[asset.id] = defaultIcon;
                }

                string localPath = Path.Combine(Application.dataPath, "OOJU_Assets", fileName);
                bool isDownloaded = File.Exists(localPath);
                string unityAssetPath = "Assets/OOJU_Assets/" + fileName;

                if (isDownloaded && AssetDatabase.AssetPathExists(unityAssetPath))
                {
                    if (IsImageAsset(asset))
                    {
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(unityAssetPath);
                        if (texture != null)
                        {
                            try
                            {
                                Texture2D preview = new Texture2D(128, 128, TextureFormat.RGBA32, false);

                                RenderTexture rt = RenderTexture.GetTemporary(128, 128);
                                Graphics.Blit(texture, rt);

                                RenderTexture.active = rt;
                                preview.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                                preview.Apply();
                                RenderTexture.active = null;
                                RenderTexture.ReleaseTemporary(rt);

                                assetPreviews[asset.id] = preview;
                                assetPreviews[asset.id + "_real"] = preview;
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Error generating image preview: {ex.Message}");
                            }
                        }
                    }
                    else if (IsModelAsset(asset))
                    {
                        UnityEngine.Object modelObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(unityAssetPath);
                        if (modelObject != null)
                        {
                            EditorCoroutineUtility.StartCoroutineOwnerless(
                                MonitorModelPreviewGeneration(asset.id, modelObject));
                        }
                    }
                }

                if (availableAssets.IndexOf(asset) % 5 == 0)
                {
                    yield return null;
                }
            }

            isGeneratingPreviews = false;
            EditorApplication.delayCall += () =>
            {
                Repaint();
            };
        }

        private void UpdateModelPreviews()
        {
            if (assetsAvailable && availableAssets != null && availableAssets.Count > 0)
            {
                bool anyPreviewUpdated = false;

                foreach (var asset in availableAssets)
                {
                    if (asset == null || string.IsNullOrEmpty(asset.id) || !IsModelAsset(asset))
                        continue;

                    if (assetPreviews.ContainsKey(asset.id + "_real"))
                        continue;

                    string assetPath = "Assets/OOJU_Assets/" + asset.filename;
                    if (!AssetDatabase.AssetPathExists(assetPath))
                        continue;

                    UnityEngine.Object modelObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (modelObject == null)
                        continue;

                    Texture2D preview = AssetPreview.GetAssetPreview(modelObject);
                    if (preview != null)
                    {
                        assetPreviews[asset.id] = preview;
                        assetPreviews[asset.id + "_real"] = preview;
                        anyPreviewUpdated = true;
                    }
                }

                if (anyPreviewUpdated)
                {
                    Repaint();
                }
            }
        }


        private Texture2D GetDefaultIconForAsset(NetworkUtility.ExportableAsset asset)
        {
            if (IsModelAsset(asset))
            {
                return EditorGUIUtility.FindTexture("d_Mesh Icon");
            }
            else if (IsImageAsset(asset))
            {
                return EditorGUIUtility.FindTexture("d_Image Icon");
            }
            else
            {
                return EditorGUIUtility.FindTexture("d_DefaultAsset Icon");
            }
        }

        private IEnumerator MonitorModelPreviewGeneration(string assetId, UnityEngine.Object modelObject)
        {
            if (modelObject == null || string.IsNullOrEmpty(assetId))
                yield break;

            float timeout = 5.0f; // 5 second timeout
            float elapsed = 0f;
            float checkInterval = 0.5f; // Check every 0.5 seconds

            Texture2D preview = null;
            bool previewFound = false;

            while (elapsed < timeout)
            {
                preview = AssetPreview.GetAssetPreview(modelObject);

                if (preview != null)
                {
                    previewFound = true;
                    break;
                }

                yield return new EditorWaitForSeconds(checkInterval);
                elapsed += checkInterval;
            }

            if (previewFound && preview != null)
            {
                lock (assetPreviews)
                {
                    assetPreviews[assetId] = preview;
                    assetPreviews[assetId + "_real"] = preview;
                }

                EditorApplication.delayCall += () =>
                {
                    Repaint();
                };

                yield break;
            }

            Texture2D thumbnail = AssetPreview.GetMiniThumbnail(modelObject);
            if (thumbnail != null)
            {
                lock (assetPreviews)
                {
                    assetPreviews[assetId] = thumbnail;
                    assetPreviews[assetId + "_real"] = thumbnail;
                }

                EditorApplication.delayCall += () =>
                {
                    Repaint();
                };
            }
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

                downloadStatus = $"Downloading {downloaded + 1}/{total}: {fileName}...";
                Repaint();

                bool success = false;
                yield return NetworkUtility.DownloadAsset(
                    assetId,
                    authToken,
                    localPath,
                    (result) =>
                    {
                        success = result;
                        if (success)
                        {
                            AssetDownloader.StoreAssetId(fileName, assetId);
                        }
                    },
                    (progress) =>
                    {
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

            if (downloaded > 0)
            {
                yield return new EditorWaitForSeconds(0.5f);

                foreach (string assetId in assetIds)
                {
                    if (assetPreviews.ContainsKey(assetId))
                        assetPreviews.Remove(assetId);

                    if (assetPreviews.ContainsKey(assetId + "_real"))
                        assetPreviews.Remove(assetId + "_real");
                }

                yield return ForcePreviewGeneration(assetIds);
            }

            downloadStatus = $"Download complete: {downloaded} succeeded, {failed} failed.";
            isDownloading = false;
            Repaint();
        }

        private IEnumerator ForcePreviewGeneration(string[] assetIds)
        {
            if (availableAssets == null || assetIds == null || assetIds.Length == 0)
                yield break;

            downloadStatus = "Generating previews...";

            foreach (string assetId in assetIds)
            {
                var asset = availableAssets.Find(a => a.id == assetId);
                if (asset == null || string.IsNullOrEmpty(asset.filename))
                    continue;

                string assetPath = "Assets/OOJU_Assets/" + asset.filename;

                if (!AssetDatabase.AssetPathExists(assetPath))
                    continue;

                if (IsImageAsset(asset))
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture != null)
                    {
                        int previewSize = 256;

                        try
                        {
                            Texture2D preview = new Texture2D(previewSize, previewSize, TextureFormat.RGBA32, false);

                            RenderTexture rt = RenderTexture.GetTemporary(previewSize, previewSize);
                            Graphics.Blit(texture, rt);

                            RenderTexture.active = rt;
                            preview.ReadPixels(new Rect(0, 0, previewSize, previewSize), 0, 0);
                            preview.Apply();
                            RenderTexture.active = null;
                            RenderTexture.ReleaseTemporary(rt);

                            assetPreviews[assetId] = preview;
                            assetPreviews[assetId + "_real"] = preview;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error forcing image preview: {ex.Message}");
                        }
                    }
                }
                else if (IsModelAsset(asset))
                {
                    UnityEngine.Object modelObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (modelObject != null)
                    {
                        EditorCoroutineUtility.StartCoroutineOwnerless(
                            MonitorModelPreviewGeneration(assetId, modelObject));
                    }
                }

                yield return null;
            }

            EditorApplication.delayCall += () =>
            {
                Repaint();
            };
        }

        private void OnGUI()
        {
            string storedToken = NetworkUtility.GetStoredToken();

            isGltfFastInstalled = GltfFastUtility.IsInstalled();

            if (!string.IsNullOrEmpty(storedToken) && string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning($"authToken is empty but stored token exists: '{storedToken}' - fixing...");
                authToken = storedToken;
            }
            try
            {
                if (styles == null || !styles.IsInitialized)
                {
                    styles = new UIStyles();
                    styles.Initialize();
                }

                if (currentTab == Tab.Assets && assetsAvailable && !isGeneratingPreviews)
                {
                    bool needsUpdate = false;
                    foreach (var asset in availableAssets)
                    {
                        if (asset == null || string.IsNullOrEmpty(asset.id) || !IsModelAsset(asset))
                            continue;

                        if (!assetPreviews.ContainsKey(asset.id + "_real") && IsAssetDownloaded(asset))
                        {
                            needsUpdate = true;
                            break;
                        }
                    }

                    if (needsUpdate && !isGeneratingPreviews)
                    {
                        EditorCoroutineUtility.StartCoroutineOwnerless(DelayedPreviewUpdate());
                    }
                }

                HandleDragAndDrop();

                UIRenderer.DrawHeader("OOJU Asset Manager", styles);
                GUILayout.Space(10);
                Debug.Log($"Token: {authToken}");
                if (string.IsNullOrEmpty(authToken))
                {

                    DrawLoginUI();
                    return;
                }

                UIRenderer.DrawAccountBar(userEmail, () =>
                {
                    NetworkUtility.ClearStoredToken();
                    authToken = "";
                    EditorPrefs.DeleteKey(TokenPrefKey);
                    uploadStatus = "Logged out.";
                    downloadStatus = "";
                    assetsAvailable = false;
                    assetCount = 0;
                    Debug.Log("Logged out.");
                });

                GUILayout.Space(5);

                UIRenderer.DrawGltfFastStatus(isGltfFastInstalled, InstallGltfFast);

                GUILayout.Space(10);

                Rect toolbarRect = EditorGUILayout.GetControlRect(false, 30);
                string[] tabLabels = new string[tabNames.Length];

                for (int i = 0; i < tabNames.Length; i++)
                {
                    tabLabels[i] = "  " + tabNames[i] + "  ";
                }

                if (EditorGUIUtility.isProSkin)
                    EditorGUI.DrawRect(toolbarRect, new Color(0.22f, 0.22f, 0.22f));
                else
                    EditorGUI.DrawRect(toolbarRect, new Color(0.8f, 0.8f, 0.8f));

                EditorGUI.BeginChangeCheck();
                int newTab = GUI.Toolbar(toolbarRect, (int)currentTab, tabLabels, styles.tabStyle);

                if (EditorGUI.EndChangeCheck())
                {
                    currentTab = (Tab)newTab;
                    scrollPosition = Vector2.zero;
                    assetGridScrollPosition = Vector2.zero;
                }

                GUILayout.Space(10);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);

                switch (currentTab)
                {
                    case Tab.Import:
                        DrawImportTab();
                        break;
                    case Tab.Assets:
                        DrawAssetsTab();
                        break;
                    case Tab.Settings:
                        DrawSettingsTab();
                        break;
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                try
                {
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
                catch { }
            }
        }

        private void DrawImportTab()
        {
            try
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                GUILayout.Label("Import Assets", styles.sectionHeaderStyle);
                GUILayout.Space(10);

                Rect dropArea = GUILayoutUtility.GetRect(0, 100, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "");

                if (isDraggingFile)
                {
                    EditorGUI.DrawRect(dropArea, new Color(0.5f, 0.8f, 0.5f, 0.3f));
                    GUI.Label(dropArea, "Drop files here to upload", styles.dropAreaActiveStyle);
                }
                else
                {
                    EditorGUI.DrawRect(dropArea, new Color(0.5f, 0.5f, 0.5f, 0.1f));
                    GUI.Label(dropArea, "Drag & drop files here to upload\nor", styles.dropAreaStyle);

                    Rect buttonRect = new Rect(
                        dropArea.x + dropArea.width / 2 - 60,
                        dropArea.y + dropArea.height - 30,
                        120, 24);

                    if (GUI.Button(buttonRect, "Browse Files..."))
                    {
                        BrowseFilesToUpload();
                    }
                }

                GUILayout.Space(10);

                GUILayout.Label("Upload from Scene", styles.subSectionHeaderStyle);

                GameObject selectedObject = Selection.activeGameObject;
                if (selectedObject != null)
                {
                    DrawSelectedObjectInfo(selectedObject);
                }
                // else
                // {
                //     EditorGUILayout.HelpBox("Select a GameObject in your scene to upload as a GLB file.", MessageType.Info);

                //     EditorGUILayout.BeginHorizontal();
                //     GUILayout.FlexibleSpace();

                //     GUI.enabled = !isExportingAndUploading && isGltfFastInstalled;

                //     GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
                //     if (GUILayout.Button("Select a GameObject", styles.buttonStyle, GUILayout.Width(180), GUILayout.Height(30)))
                //     {
                //         // Do nothing
                //     }
                //     GUI.backgroundColor = Color.white;

                //     GUI.enabled = true;

                //     GUILayout.FlexibleSpace();
                //     EditorGUILayout.EndHorizontal();
                // }

                if (pendingUploadFiles != null && pendingUploadFiles.Count > 0)
                {
                    GUILayout.Space(15);
                    GUILayout.Label("Pending Uploads", styles.subSectionHeaderStyle);

                    for (int i = 0; i < pendingUploadFiles.Count; i++)
                    {
                        string file = pendingUploadFiles[i];
                        if (string.IsNullOrEmpty(file) || !File.Exists(file))
                        {
                            pendingUploadFiles.RemoveAt(i);
                            i--;
                            continue;
                        }

                        string filename = Path.GetFileName(file);

                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                        Texture2D icon = GetFileIcon(file);
                        if (icon != null)
                        {
                            GUILayout.Label(new GUIContent(icon), GUILayout.Width(24), GUILayout.Height(24));
                        }

                        EditorGUILayout.BeginVertical();
                        GUILayout.Label(filename, EditorStyles.boldLabel);
                        GUILayout.Label($"Path: {file}", EditorStyles.miniLabel);
                        EditorGUILayout.EndVertical();

                        if (GUILayout.Button("×", styles.removeButtonStyle, GUILayout.Width(24), GUILayout.Height(24)))
                        {
                            pendingUploadFiles.RemoveAt(i);
                            i--;
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    GUILayout.Space(10);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    GUI.enabled = !isExportingAndUploading && pendingUploadFiles.Count > 0;
                    GUI.backgroundColor = styles.uploadButtonColor;

                    if (GUILayout.Button($"Upload {pendingUploadFiles.Count} Files", styles.buttonStyle, GUILayout.Width(200), GUILayout.Height(30)))
                    {
                        UploadPendingFiles();
                    }

                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }

                if (!string.IsNullOrEmpty(uploadStatus))
                {
                    GUILayout.Space(10);

                    MessageType messageType = uploadStatus.Contains("fail") || uploadStatus.Contains("error")
                        ? MessageType.Error
                        : MessageType.Info;

                    EditorGUILayout.HelpBox(uploadStatus, messageType);
                }

                EditorGUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in DrawImportTab: {ex.Message}\n{ex.StackTrace}");

                try
                {
                    EditorGUILayout.EndVertical();
                }
                catch { }
            }
        }

        private IEnumerator DelayedPreviewUpdate()
        {
            yield return null;

            foreach (var asset in availableAssets)
            {
                if (asset == null || string.IsNullOrEmpty(asset.id) || !IsModelAsset(asset))
                    continue;

                if (assetPreviews.ContainsKey(asset.id + "_real"))
                    continue;

                if (!IsAssetDownloaded(asset))
                    continue;

                string assetPath = "Assets/OOJU_Assets/" + asset.filename;
                if (!AssetDatabase.AssetPathExists(assetPath))
                    continue;

                UnityEngine.Object modelObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (modelObject == null)
                    continue;

                Texture2D preview = AssetPreview.GetAssetPreview(modelObject);
                if (preview != null)
                {

                    assetPreviews[asset.id] = preview;
                    assetPreviews[asset.id + "_real"] = preview;
                }

                yield return null;
            }

            Repaint();
        }

        private void DrawAssetsTab()
        {
            try
            {

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Header with search
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("My Assets", styles.sectionHeaderStyle, GUILayout.ExpandWidth(false));

                GUILayout.FlexibleSpace();

                // Search field - check if initialized
                if (assetSearchField == null)
                    assetSearchField = new SearchField();

                string newSearch = assetSearchField.OnGUI(
                    GUILayoutUtility.GetRect(100, 200, 18, 18, GUILayout.ExpandWidth(true), GUILayout.MaxWidth(300)),
                    assetSearchQuery);

                if (newSearch != assetSearchQuery)
                {
                    assetSearchQuery = newSearch;
                    FilterAssets();
                }

                // Refresh button
                if (GUILayout.Button(new GUIContent(styles.refreshIcon), styles.iconButtonStyle, GUILayout.Width(24), GUILayout.Height(24)))
                {
                    CheckAssets();
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                // Stats and controls
                EditorGUILayout.BeginHorizontal();

                if (assetsAvailable && filteredAssets != null)
                {
                    GUILayout.Label($"{filteredAssets.Count} of {assetCount} assets", EditorStyles.boldLabel);
                }
                else if (isCheckingAssets)
                {
                    GUILayout.Label("Loading assets...", EditorStyles.boldLabel);
                }
                else
                {
                    GUILayout.Label("No assets available", EditorStyles.boldLabel);
                }

                GUILayout.FlexibleSpace();

                // Auto-sync toggle
                EditorGUI.BeginChangeCheck();
                autoSyncEnabled = EditorGUILayout.ToggleLeft($"Auto-sync every {AutoSyncIntervalMinutes}m", autoSyncEnabled, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(AutoSyncPrefKey, autoSyncEnabled);
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                // Assets grid - safely handle different states
                if (isCheckingAssets)
                {
                    GUILayout.Label("Loading assets...", styles.centeredLabelStyle);
                }
                else if (!assetsAvailable || filteredAssets == null || filteredAssets.Count == 0)
                {
                    GUILayout.Label("No assets available. Upload some files first.", styles.centeredLabelStyle);
                }
                else
                {
                    // Make sure filteredAssets is initialized
                    if (filteredAssets == null)
                        filteredAssets = new List<NetworkUtility.ExportableAsset>();

                    // Safe call to DrawAssetsGrid with null checks
                    if (filteredAssets.Count > 0)
                        DrawAssetsGrid();
                    else
                        GUILayout.Label("No assets match your search criteria.", styles.centeredLabelStyle);
                }

                // Download controls
                if (assetsAvailable && !isCheckingAssets && filteredAssets != null && filteredAssets.Count > 0)
                {
                    GUILayout.Space(10);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    // Make sure selectedAssetIds is initialized
                    if (selectedAssetIds == null)
                        selectedAssetIds = new List<string>();

                    GUI.enabled = !isDownloading && selectedAssetIds.Count > 0;
                    GUI.backgroundColor = styles.downloadButtonColor;

                    if (GUILayout.Button($"Download Selected ({selectedAssetIds.Count})", styles.buttonStyle, GUILayout.Width(200), GUILayout.Height(30)))
                    {
                        DownloadSelectedAssets(selectedAssetIds.ToArray());
                    }

                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();

                    // Status message
                    if (!string.IsNullOrEmpty(downloadStatus))
                    {
                        GUILayout.Space(10);

                        MessageType messageType = downloadStatus.Contains("fail") || downloadStatus.Contains("error")
                            ? MessageType.Error
                            : MessageType.Info;

                        EditorGUILayout.HelpBox(downloadStatus, messageType);
                    }
                }

                EditorGUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                // Log exception but don't crash the editor
                Debug.LogError($"Error in DrawAssetsTab: {ex.Message}\n{ex.StackTrace}");

                // Try to end any potentially unclosed layout groups
                try
                {
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }
                catch { }
            }
        }
        private void DrawAssetsGrid()
        {
            try
            {
                if (filteredAssets == null || filteredAssets.Count == 0)
                {
                    GUILayout.Label("No assets to display.", styles.centeredLabelStyle);
                    return;
                }

                float windowWidth = position.width - 40;
                int gridItemWidth = 110;
                int gridItemHeight = 130;
                int itemPadding = 15;

                int itemsPerRow = Mathf.Max(1, Mathf.FloorToInt(windowWidth / (gridItemWidth + itemPadding)));

                assetGridScrollPosition = EditorGUILayout.BeginScrollView(
                    assetGridScrollPosition,
                    false, true,
                    GUILayout.ExpandHeight(true));

                int totalRows = Mathf.CeilToInt((float)filteredAssets.Count / itemsPerRow);

                for (int row = 0; row < totalRows; row++)
                {
                    EditorGUILayout.BeginHorizontal(GUILayout.Height(gridItemHeight));

                    int startIndex = row * itemsPerRow;
                    int endIndex = Mathf.Min(startIndex + itemsPerRow, filteredAssets.Count);

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        var asset = filteredAssets[i];
                        if (asset == null) continue;

                        DrawGridItem(asset, gridItemWidth, gridItemHeight);

                        GUILayout.Space(itemPadding);
                    }

                    if (endIndex - startIndex < itemsPerRow)
                    {
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(itemPadding);
                }

                EditorGUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in DrawAssetsGrid: {ex.Message}\n{ex.StackTrace}");

                try
                {
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndHorizontal();
                }
                catch { }
            }
        }

        private void DrawGridItem(NetworkUtility.ExportableAsset asset, int itemWidth, int itemHeight)
        {
            Rect outerRect = EditorGUILayout.GetControlRect(false, itemHeight);
            outerRect.width = itemWidth;

            GUI.Box(outerRect, "");

            Rect checkboxRect = new Rect(outerRect.x + 5, outerRect.y + 5, 20, 20);

            if (selectedAssetIds == null)
                selectedAssetIds = new List<string>();

            bool isSelected = asset.id != null && selectedAssetIds.Contains(asset.id);
            bool newSelection = GUI.Toggle(checkboxRect, isSelected, "");

            if (newSelection != isSelected && asset.id != null)
            {
                if (newSelection)
                    selectedAssetIds.Add(asset.id);
                else
                    selectedAssetIds.Remove(asset.id);
            }

            bool isDownloaded = IsAssetDownloaded(asset);
            if (isDownloaded)
            {
                Rect downloadedRect = new Rect(outerRect.x + itemWidth - 25, outerRect.y + 5, 20, 20);
                GUI.color = Color.green;
                GUI.DrawTexture(downloadedRect, EditorGUIUtility.FindTexture("d_Installed"));
                GUI.color = Color.white;
            }

            int previewSize = Mathf.Min(itemWidth - 20, itemHeight - 50);
            Rect previewRect = new Rect(
                outerRect.x + (itemWidth - previewSize) / 2,
                outerRect.y + 30,
                previewSize,
                previewSize
            );

            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));

            if (assetPreviews != null && asset.id != null)
            {
                Texture2D preview = null;
                bool hasRealPreview = assetPreviews.TryGetValue(asset.id + "_real", out preview) && preview != null;

                if (!hasRealPreview)
                {
                    assetPreviews.TryGetValue(asset.id, out preview);
                }

                if (preview != null)
                {
                    float aspectRatio = (float)preview.width / preview.height;
                    Rect adjustedRect;

                    if (aspectRatio >= 1.0f)
                    {
                        float adjustedHeight = previewSize / aspectRatio;
                        adjustedRect = new Rect(
                            previewRect.x,
                            previewRect.y + (previewRect.height - adjustedHeight) / 2,
                            previewSize,
                            adjustedHeight
                        );
                    }
                    else
                    {
                        float adjustedWidth = previewSize * aspectRatio;
                        adjustedRect = new Rect(
                            previewRect.x + (previewRect.width - adjustedWidth) / 2,
                            previewRect.y,
                            adjustedWidth,
                            previewSize
                        );
                    }

                    GUI.DrawTexture(adjustedRect, preview, ScaleMode.ScaleToFit);

                    if (hasRealPreview)
                    {
                        Rect frameRect = new Rect(
                            adjustedRect.x - 1,
                            adjustedRect.y - 1,
                            adjustedRect.width + 2,
                            adjustedRect.height + 2
                        );

                        Color outlineColor = EditorGUIUtility.isProSkin
                            ? new Color(0.6f, 0.6f, 0.6f, 0.5f)
                            : new Color(0.3f, 0.3f, 0.3f, 0.5f);

                        EditorGUI.DrawRect(
                            new Rect(frameRect.x, frameRect.y, frameRect.width, 1), outlineColor);
                        EditorGUI.DrawRect(
                            new Rect(frameRect.x, frameRect.y + frameRect.height, frameRect.width, 1), outlineColor);
                        EditorGUI.DrawRect(
                            new Rect(frameRect.x, frameRect.y, 1, frameRect.height), outlineColor);
                        EditorGUI.DrawRect(
                            new Rect(frameRect.x + frameRect.width, frameRect.y, 1, frameRect.height), outlineColor);
                    }
                }
                else
                {
                    Texture2D icon = GetDefaultIconForAsset(asset);
                    if (icon != null)
                    {
                        float iconSize = previewSize * 0.6f;
                        Rect iconRect = new Rect(
                            previewRect.x + (previewRect.width - iconSize) / 2,
                            previewRect.y + (previewRect.height - iconSize) / 2,
                            iconSize,
                            iconSize
                        );
                        GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                    }
                }
            }

            string displayName = asset.filename ?? "Unnamed Asset";
            if (displayName.Length > 20)
            {
                displayName = displayName.Substring(0, 17) + "...";
            }

            Rect nameRect = new Rect(
                outerRect.x + 5,
                outerRect.y + itemHeight - 35,
                itemWidth - 10,
                20
            );

            GUI.Label(nameRect, displayName, EditorStyles.boldLabel);

            string fileType = GetFileTypeDisplay(asset);

            Rect typeRect = new Rect(
                outerRect.x + 5,
                outerRect.y + itemHeight - 18,
                itemWidth - 10,
                15
            );

            GUI.Label(typeRect, fileType, EditorStyles.miniLabel);
        }

        private void DrawAssetTypeHeader(string title, Color color)
        {
            GUILayout.Space(10);
            Rect headerRect = EditorGUILayout.GetControlRect(false, 24);
            EditorGUI.DrawRect(headerRect, EditorGUIUtility.isProSkin ? color * 0.3f : color * 0.6f);

            GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            GUI.Label(new Rect(headerRect.x + 10, headerRect.y + 4, headerRect.width - 20, 16), title, EditorStyles.boldLabel);
            GUI.color = Color.white;

            GUILayout.Space(5);
        }

        private string GetFileTypeDisplay(NetworkUtility.ExportableAsset asset)
        {
            string fileType = "Unknown";

            if (IsModelAsset(asset))
            {
                fileType = "3D Model";

                if (asset.filename != null)
                {
                    string ext = Path.GetExtension(asset.filename).ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ext))
                        fileType += " " + ext;
                }
            }
            else if (IsImageAsset(asset))
            {
                fileType = "Image";

                if (asset.filename != null)
                {
                    string ext = Path.GetExtension(asset.filename).ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ext))
                        fileType += " " + ext;
                }
            }
            else if (asset.content_type != null)
            {
                string[] parts = asset.content_type.Split('/');
                if (parts.Length > 1)
                    fileType = parts[1].ToUpperInvariant();
            }
            else if (asset.filename != null)
            {
                string ext = Path.GetExtension(asset.filename);
                if (!string.IsNullOrEmpty(ext))
                    fileType = ext.ToUpperInvariant().TrimStart('.');
            }

            return fileType;
        }


        private void DrawAssetTypeGrid(List<NetworkUtility.ExportableAsset> assets, int maxItemsPerRow, GUILayoutOption[] itemOptions)
        {
            int itemsInCurrentRow = 0;
            bool rowStarted = false;

            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                if (asset == null) continue;

                if (itemsInCurrentRow == 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    rowStarted = true;
                }

                EditorGUILayout.BeginVertical(styles.assetGridItemStyle, itemOptions);

                bool isSelected = selectedAssetIds.Contains(asset.id);
                bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(previewSize - 10));

                if (newSelection != isSelected)
                {
                    if (newSelection)
                        selectedAssetIds.Add(asset.id);
                    else
                        selectedAssetIds.Remove(asset.id);
                }

                Rect previewRect = GUILayoutUtility.GetRect(previewSize - 10, previewSize - 10);
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

                Texture2D previewTexture = null;

                if (assetPreviews.TryGetValue(asset.id, out previewTexture))
                {
                    GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit);
                }
                else
                {
                    if (IsImageAsset(asset))
                    {
                        previewTexture = EditorGUIUtility.FindTexture("d_Image Icon");
                    }
                    else if (IsModelAsset(asset))
                    {
                        previewTexture = EditorGUIUtility.FindTexture("d_Mesh Icon");
                    }
                    else
                    {
                        previewTexture = EditorGUIUtility.FindTexture("d_DefaultAsset Icon");
                    }

                    if (previewTexture != null)
                    {
                        assetPreviews[asset.id] = previewTexture;
                    }

                    float iconSize = Mathf.Min(previewRect.width, previewRect.height) * 0.6f;
                    float iconX = previewRect.x + (previewRect.width - iconSize) / 2;
                    float iconY = previewRect.y + (previewRect.height - iconSize) / 2;
                    GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), previewTexture, ScaleMode.ScaleToFit);
                }

                if (!assetPreviews.ContainsKey(asset.id + "_loaded") && !string.IsNullOrEmpty(asset.filename))
                {
                    TryLoadAssetPreview(asset);
                }

                string displayName = asset.filename;
                if (displayName != null && displayName.Length > 15)
                {
                    displayName = displayName.Substring(0, 12) + "...";
                }

                GUILayout.Label(displayName ?? "Unnamed Asset", styles.assetNameStyle);

                string fileType = GetAssetTypeDisplay(asset);
                GUILayout.Label(fileType, styles.assetTypeStyle);

                if (IsAssetDownloaded(asset))
                {
                    Rect downloadedRect = GUILayoutUtility.GetRect(previewSize - 10, 16);
                    GUI.color = Color.green;
                    GUI.Label(downloadedRect, "✓ Downloaded", styles.assetStatusStyle);
                    GUI.color = Color.white;
                }

                EditorGUILayout.EndVertical();

                GUILayout.Space(5);

                itemsInCurrentRow++;

                if (itemsInCurrentRow >= maxItemsPerRow || i == assets.Count - 1)
                {
                    if (itemsInCurrentRow < maxItemsPerRow)
                    {
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.EndHorizontal();
                    rowStarted = false;
                    itemsInCurrentRow = 0;
                }
            }

            if (rowStarted)
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool IsImageAsset(NetworkUtility.ExportableAsset asset)
        {
            if (asset.content_type != null && asset.content_type.Contains("image"))
                return true;

            if (asset.filename != null)
            {
                string ext = Path.GetExtension(asset.filename).ToLowerInvariant();
                return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".tga";
            }

            return false;
        }

        private bool IsModelAsset(NetworkUtility.ExportableAsset asset)
        {
            if (asset.content_type != null && (asset.content_type.Contains("model") || asset.content_type.Contains("gltf")))
                return true;

            if (asset.filename != null)
            {
                string ext = System.IO.Path.GetExtension(asset.filename).ToLowerInvariant();
                return ext == ".glb" || ext == ".gltf" || ext == ".fbx" || ext == ".obj";
            }

            return false;
        }

        private string GetAssetTypeDisplay(NetworkUtility.ExportableAsset asset)
        {
            if (IsModelAsset(asset))
            {
                if (asset.filename != null)
                {
                    string ext = System.IO.Path.GetExtension(asset.filename).ToUpperInvariant();
                    return "3D Model " + ext;
                }
                return "3D Model";
            }

            if (IsImageAsset(asset))
            {
                if (asset.filename != null)
                {
                    string ext = System.IO.Path.GetExtension(asset.filename).ToUpperInvariant();
                    return "Image " + ext;
                }
                return "Image";
            }

            if (asset.content_type != null)
            {
                string[] parts = asset.content_type.Split('/');
                if (parts.Length >= 2)
                {
                    return char.ToUpper(parts[1][0]) + parts[1].Substring(1);
                }
            }

            if (asset.filename != null)
            {
                string ext = System.IO.Path.GetExtension(asset.filename).ToUpperInvariant();
                if (!string.IsNullOrEmpty(ext))
                    return ext.Substring(1); // Remove the dot
            }

            return "Unknown";
        }

        private bool IsAssetDownloaded(NetworkUtility.ExportableAsset asset)
        {
            if (string.IsNullOrEmpty(asset.filename))
                return false;

            string localPath = System.IO.Path.Combine(Application.dataPath, "OOJU_Assets", asset.filename);
            return System.IO.File.Exists(localPath);
        }

        private void TryLoadAssetPreview(NetworkUtility.ExportableAsset asset)
        {
            if (string.IsNullOrEmpty(asset.filename))
                return;

            EditorCoroutineUtility.StartCoroutineOwnerless(TryLoadAssetPreviewCoroutine(asset));
        }

        private IEnumerator TryLoadAssetPreviewCoroutine(NetworkUtility.ExportableAsset asset)
        {
            if (string.IsNullOrEmpty(asset.filename) || string.IsNullOrEmpty(asset.id))
                yield break;

            string assetPath = "Assets/OOJU_Assets/" + asset.filename;

            if (AssetDatabase.AssetPathExists(assetPath))
            {
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (obj != null)
                {
                    Texture2D preview = AssetPreview.GetAssetPreview(obj);

                    if (preview == null)
                    {
                        preview = AssetPreview.GetMiniThumbnail(obj);
                    }

                    if (preview != null)
                    {
                        assetPreviews[asset.id] = preview;
                        assetPreviews[asset.id + "_loaded"] = preview;

                        EditorApplication.delayCall += () =>
                        {
                            Repaint();
                        };
                    }
                }
            }

            yield break;
        }


        private void DrawAssetGridItem(NetworkUtility.ExportableAsset asset)
        {
            EditorGUILayout.BeginVertical(styles.assetGridItemStyle, GUILayout.Width(previewSize), GUILayout.Height(previewSize + 40));

            bool isSelected = selectedAssetIds.Contains(asset.id);
            bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(previewSize - 10));

            if (newSelection != isSelected)
            {
                if (newSelection)
                    selectedAssetIds.Add(asset.id);
                else
                    selectedAssetIds.Remove(asset.id);
            }

            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

            EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

            if (assetPreviews.TryGetValue(asset.id, out Texture2D preview))
            {
                GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            }
            else
            {
                Texture2D defaultIcon = EditorGUIUtility.FindTexture("d_DefaultAsset Icon");
                if (defaultIcon != null)
                {
                    float iconSize = Mathf.Min(previewRect.width, previewRect.height) * 0.6f;
                    float iconX = previewRect.x + (previewRect.width - iconSize) / 2;
                    float iconY = previewRect.y + (previewRect.height - iconSize) / 2;
                    GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), defaultIcon, ScaleMode.ScaleToFit);
                }
            }

            string displayName = asset.filename;
            if (displayName != null && displayName.Length > 15)
            {
                displayName = displayName.Substring(0, 12) + "...";
            }

            GUILayout.Label(displayName ?? "Unnamed Asset", styles.assetNameStyle);

            string fileType = "Unknown";
            if (!string.IsNullOrEmpty(asset.content_type))
            {
                fileType = asset.content_type.Split('/').LastOrDefault() ?? "file";
                fileType = char.ToUpper(fileType[0]) + fileType.Substring(1);
            }
            else if (!string.IsNullOrEmpty(asset.filename))
            {
                fileType = Path.GetExtension(asset.filename).ToUpper();
            }

            GUILayout.Label(fileType, styles.assetTypeStyle);

            EditorGUILayout.EndVertical();

            GUILayout.Space(5);
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("OOJU Scene (.zip)", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (!EditorPrefs.HasKey("OOJU_zipPath"))
                EditorPrefs.SetString("OOJU_zipPath", "");

            string zipPath = EditorPrefs.GetString("OOJU_zipPath");

            EditorGUILayout.BeginHorizontal();
            zipPath = EditorGUILayout.TextField(zipPath);       // text-field
            if (GUILayout.Button("...", GUILayout.Width(30)))   // browse button
            {
                zipPath = EditorUtility.OpenFilePanel(
                    "Pick .ooju2unity.zip", "", "zip");
                EditorPrefs.SetString("OOJU_zipPath", zipPath);
            }
            EditorGUILayout.EndHorizontal();

            GUI.enabled = System.IO.File.Exists(zipPath);
            if (GUILayout.Button("Import OOJU Scene"))
            {
                _ = OOJUSceneImportUtility.ImportSceneZipAsync(zipPath);                        // <-- the helper we added earlier
            }
            GUI.enabled = true;

            GUILayout.Space(20);   // little separator before the rest
            /* ───────────────────────────────────────────────────────────── */
            /*    the original Settings UI                                  */
            /* ───────────────────────────────────────────────────────────── */

            GUILayout.Label("Settings", styles.sectionHeaderStyle);
            GUILayout.Space(15);

            // preview size slider
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Preview Size:", GUILayout.Width(100));
            previewSize = EditorGUILayout.IntSlider(previewSize, 60, 200);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);

            // auto-sync toggle
            EditorGUILayout.LabelField("Auto-Sync", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            autoSyncEnabled = EditorGUILayout.Toggle("Enable Auto-Sync", autoSyncEnabled);
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;

            GUILayout.Space(15);

            // backend info & reset
            EditorGUILayout.LabelField("Backend Connection", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            string currentUrl = NetworkUtility.BackendUrl;
            EditorGUILayout.LabelField("Current Backend URL:", currentUrl);

            GUILayout.Space(10);
            if (GUILayout.Button("Reset All Settings", GUILayout.Width(150)))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset Settings",
                        "Are you sure you want to reset all settings? " +
                        "This will log you out and clear all preferences.",
                        "Reset", "Cancel"))
                {
                    ResetAllSettings();
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }
        private void DrawLoginUI()
        {
            UIRenderer.DrawLoginUI(
                ref userEmail,
                ref userPassword,
                isLoggingIn,
                uploadStatus,
                () =>
                {
                    uploadStatus = "";
                    isLoggingIn = true;
                    EditorCoroutineUtility.StartCoroutineOwnerless(LoginCoroutine());
                },
                styles
            );
        }

        private void DrawSelectedObjectInfo(GameObject selectedObject)
        {
            if (selectedObject == null)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);
            Texture2D objIcon = EditorGUIUtility.FindTexture("d_GameObject Icon");
            GUILayout.Label(new GUIContent(objIcon), GUILayout.Width(20), GUILayout.Height(20));
            GUILayout.Label(selectedObject.name, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30);
            string objectInfo = "GameObject";

            if (selectedObject.GetComponent<MeshFilter>() != null &&
                selectedObject.GetComponent<MeshRenderer>() != null &&
                selectedObject.GetComponent<MeshFilter>().sharedMesh != null)
            {
                MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
                objectInfo = $"Mesh: {meshFilter.sharedMesh.vertexCount} vertices";
            }
            else if (selectedObject.GetComponent<SkinnedMeshRenderer>() != null &&
                    selectedObject.GetComponent<SkinnedMeshRenderer>().sharedMesh != null)
            {
                SkinnedMeshRenderer renderer = selectedObject.GetComponent<SkinnedMeshRenderer>();
                objectInfo = $"Skinned Mesh: {renderer.sharedMesh.vertexCount} vertices";
            }

            GUILayout.Label(objectInfo, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUI.enabled = !isExportingAndUploading && isGltfFastInstalled;

            GUI.backgroundColor = styles.uploadButtonColor;
            if (GUILayout.Button("Export & Upload", styles.buttonStyle, GUILayout.Width(150), GUILayout.Height(30)))
            {
                ExportAndUploadGLB(selectedObject);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            Rect dropArea = new Rect(0, 0, position.width, position.height);

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragUpdated)
                    {
                        isDraggingFile = true;
                        Repaint();
                    }

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        isDraggingFile = false;

                        foreach (string path in DragAndDrop.paths)
                        {
                            if (File.Exists(path))
                            {
                                AddFileToUploadQueue(path);
                            }
                            else if (Directory.Exists(path))
                            {
                                ProcessDirectory(path);
                            }
                        }

                        Repaint();
                    }
                    break;

                case EventType.DragExited:
                    isDraggingFile = false;
                    Repaint();
                    break;
            }
        }

        private void ProcessDirectory(string directoryPath)
        {
            string[] supportedExtensions = new string[] { ".glb", ".gltf", ".fbx", ".obj", ".png", ".jpg", ".jpeg" };

            foreach (string file in Directory.GetFiles(directoryPath))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (Array.Exists(supportedExtensions, e => e == ext))
                {
                    AddFileToUploadQueue(file);
                }
            }
        }

        private void AddFileToUploadQueue(string filePath)
        {
            if (!pendingUploadFiles.Contains(filePath))
            {
                pendingUploadFiles.Add(filePath);
            }
        }

        private void BrowseFilesToUpload()
        {
            string[] supportedExtensions = new string[] {
                "glb", "GLB",
                "gltf", "GLTF",
                "fbx", "FBX",
                "obj", "OBJ",
                "png", "PNG",
                "jpg", "JPG",
                "jpeg", "JPEG"
            };

            string filters = "3D Models and Images (*.glb,*.gltf,*.fbx,*.obj,*.png,*.jpg,*.jpeg)";
            string path = EditorUtility.OpenFilePanelWithFilters("Select Files to Upload", "",
                new string[] { "3D Models and Images", "glb,gltf,fbx,obj,png,jpg,jpeg" });

            if (!string.IsNullOrEmpty(path))
            {
                AddFileToUploadQueue(path);
            }
        }

        private Texture2D GetFileIcon(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".glb" || ext == ".gltf" || ext == ".fbx" || ext == ".obj")
            {
                return EditorGUIUtility.FindTexture("d_Mesh Icon");
            }
            else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
            {
                return EditorGUIUtility.FindTexture("d_Image Icon");
            }
            else
            {
                return EditorGUIUtility.FindTexture("d_DefaultAsset Icon");
            }
        }

        private void UploadPendingFiles()
        {
            if (pendingUploadFiles.Count == 0 || isExportingAndUploading || string.IsNullOrEmpty(authToken))
                return;

            isExportingAndUploading = true;
            uploadStatus = $"Uploading {pendingUploadFiles.Count} files...";

            EditorCoroutineUtility.StartCoroutineOwnerless(UploadFilesCoroutine());
        }

        private IEnumerator UploadFilesCoroutine()
        {
            int total = pendingUploadFiles.Count;
            int uploaded = 0;
            int failed = 0;

            for (int i = 0; i < pendingUploadFiles.Count; i++)
            {
                string filePath = pendingUploadFiles[i];
                string fileName = Path.GetFileName(filePath);

                uploadStatus = $"Uploading {uploaded + 1}/{total}: {fileName}...";
                Repaint();

                bool success = false;
                yield return NetworkUtility.UploadFile(
                    filePath,
                    authToken,
                    (result) =>
                    {
                        success = true;
                        uploaded++;
                    },
                    (error) =>
                    {
                        failed++;
                    }
                );
            }

            uploadStatus = $"Upload complete: {uploaded} succeeded, {failed} failed.";
            isExportingAndUploading = false;

            if (uploaded > 0)
            {
                pendingUploadFiles.Clear();
                CheckAssets();
            }

            Repaint();
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

            if (selectedObject == null)
            {
                uploadStatus = "No GameObject selected for export.";
                return;
            }

            if (!isGltfFastInstalled)
            {
                uploadStatus = "GLTFast is not installed. Export functionality is unavailable.";
                return;
            }

            try
            {
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

                        EditorApplication.delayCall += () =>
                        {
                            CheckAssets();
                        };
                    },
                    (error) =>
                    {
                        uploadStatus = $"Upload failed: {error}";
                        Debug.LogError(uploadStatus);
                        isExportingAndUploading = false;
                    }
                ));
            }
            catch (Exception ex)
            {
                uploadStatus = $"Export error: {ex.Message}";
                Debug.LogError($"Error in ExportAndUploadGLB: {ex.Message}\n{ex.StackTrace}");
                isExportingAndUploading = false;
            }
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

        async void ImportOojuZip(string zipPath)
        {
            if (!System.IO.File.Exists(zipPath))
            {
                Debug.LogError("[OOJU] Zip not found: " + zipPath);
                return;
            }

            /* 1 unzip to a temp folder */
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, temp);

            /* 2 read scene.json */
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(temp, "scene.json"));
            var scene = JsonUtility.FromJson<SceneFile>(json);

            /* 3 root object */
            var rootGO = new GameObject(scene.sceneName ?? "OOJU Scene");

            /* 4 create objects (only meshes for now) */
            foreach (var ob in scene.objects)
            {
                if (ob.type != "mesh") continue;          // skip lights/cameras for now

                var go = new GameObject(ob.name);
                go.transform.SetParent(rootGO.transform, false);
                go.transform.localPosition   = ob.transform.position;
                go.transform.localEulerAngles= ob.transform.rotation;
                go.transform.localScale      = ob.transform.scale;

                var glb = System.IO.Path.Combine(temp, ob.mesh);

        #if UNITY_GLTF    // if you have GLTFast (package name com.unity.cloud.gltfast)
                var gltf = go.AddComponent<glTFast.GltfAsset>();
                await gltf.Load(glb);
        #else             // fallback: let Unity import it as a model asset
                var dstDir  = System.IO.Path.Combine("Assets/OOJU_Imported");
                var dstPath = System.IO.Path.Combine(dstDir, System.IO.Path.GetFileName(glb));
                System.IO.Directory.CreateDirectory(dstDir);
                System.IO.File.Copy(glb, dstPath, true);
                UnityEditor.AssetDatabase.Refresh();
                var prefab  = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(dstPath);
                if (prefab) UnityEngine.Object.Instantiate(prefab, go.transform);
        #endif
            }

            UnityEditor.Selection.activeGameObject = rootGO;
            Debug.Log("[OOJU] Scene import finished.");
        }

        private void ResetAllSettings()
        {
            authToken = "";
            EditorPrefs.DeleteKey(TokenPrefKey);

            autoSyncEnabled = false;
            EditorPrefs.SetBool(AutoSyncPrefKey, false);

            uploadStatus = "Settings have been reset.";
            downloadStatus = "";
            userEmail = "";
            userPassword = "";

            availableAssets.Clear();
            filteredAssets.Clear();
            selectedAssetIds.Clear();
            assetPreviews.Clear();
            pendingUploadFiles.Clear();

            assetsAvailable = false;
            assetCount = 0;

            currentTab = Tab.Import;

            Repaint();
        }
    }
}
