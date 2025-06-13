using UnityEditor;
using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using OojiCustomPlugin;

namespace OOJUPlugin
{
    public class OOJUManager : EditorWindow
    {
        // Tab system
        private enum Tab { Asset, Animation, Settings }
        private Tab currentTab = Tab.Asset;
        private string[] tabNames = { "Asset Manager", "Animation Manager", "Settings" };

        // Asset Manager variables
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

        // Add this as a class field
        private int assetSubTab = 0;
        private readonly string[] assetTabLabels = { "Import", "My Assets", "Settings" };

        [MenuItem("OOJU/Manager")]
        public static void ShowWindow()
        {
            GetWindow<OOJUManager>("OOJU Manager");
        }

        private void OnEnable()
        {
            styles = new UIStyles();
            // styles.Initialize(); // Do not call GUI-related functions in OnEnable

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
                authToken = "";
            }

            autoSyncEnabled = EditorPrefs.GetBool("OOJUManager_AutoSync", false);
            AssetDownloader.LoadSyncTime();

            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool("OOJUManager_AutoSync", autoSyncEnabled);
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnGUI()
        {
            if (styles == null)
                styles = new UIStyles();
            if (!styles.IsInitialized)
                styles.Initialize();

            // Draw the tab bar
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, tabNames, styles.tabStyle);

            // Draw the selected tab content
            switch (currentTab)
            {
                case Tab.Asset:
                    DrawAssetTab();
                    break;
                case Tab.Animation:
                    DrawAnimationTab();
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
            }
        }

        private void DrawAssetTab()
        {
            string storedToken = NetworkUtility.GetStoredToken();
            isGltfFastInstalled = GltfFastUtility.IsInstalled();
            if (!string.IsNullOrEmpty(storedToken) && string.IsNullOrEmpty(authToken))
            {
                authToken = storedToken;
            }
            if (styles == null || !styles.IsInitialized)
            {
                styles = new UIStyles();
                styles.Initialize();
            }
            HandleDragAndDrop();
            UIRenderer.DrawHeader("OOJU Asset Manager", styles);
            GUILayout.Space(10);
            if (string.IsNullOrEmpty(authToken))
            {
                DrawLoginUI();
                return;
            }
            UIRenderer.DrawAccountBar(userEmail, () =>
            {
                NetworkUtility.ClearStoredToken();
                authToken = "";
                uploadStatus = "Logged out.";
                downloadStatus = "";
                assetsAvailable = false;
                assetCount = 0;
            });
            GUILayout.Space(5);
            UIRenderer.DrawGltfFastStatus(isGltfFastInstalled, InstallGltfFast);
            GUILayout.Space(10);
            // Draw the internal asset tab bar
            assetSubTab = GUILayout.Toolbar(assetSubTab, assetTabLabels, styles.tabStyle);
            GUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
            switch (assetSubTab)
            {
                case 0:
                    DrawImportTab();
                    break;
                case 1:
                    DrawAssetsTab();
                    break;
                case 2:
                    DrawSettingsTab();
                    break;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawAnimationTab()
        {
            // Animation Manager UI will be implemented here
            // This will contain the functionality from ObjectAutoAnimator
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
            zipPath = EditorGUILayout.TextField(zipPath); // text-field
            if (GUILayout.Button("...", GUILayout.Width(30))) // browse button
            {
                zipPath = EditorUtility.OpenFilePanel(
                    "Pick .ooju2unity.zip", "", "zip");
                EditorPrefs.SetString("OOJU_zipPath", zipPath);
            }
            EditorGUILayout.EndHorizontal();
            GUI.enabled = System.IO.File.Exists(zipPath);
            if (GUILayout.Button("Import OOJU Scene"))
            {
                _ = OOJUSceneImportUtility.ImportSceneZipAsync(zipPath);
            }
            GUI.enabled = true;
            GUILayout.Space(20);
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
                        "Are you sure you want to reset all settings? This will log you out and clear all preferences.",
                        "Reset", "Cancel"))
                {
                    ResetAllSettings();
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void OnEditorUpdate()
        {
            if (autoSyncEnabled && !string.IsNullOrEmpty(authToken) && !isDownloading)
            {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - lastSyncCheckTime > 15 * 60) // 15 minutes
                {
                    lastSyncCheckTime = currentTime;
                    CheckAssets();
                }
            }
        }

        private void CheckAssets()
        {
            // Asset checking logic will be implemented here
        }

        // All helper methods (DrawImportTab, DrawAssetsTab, DrawSettingsTab, DrawLoginUI, HandleDragAndDrop, etc.) from UserAssetManager should be copied here and adapted as needed.

        // --- Begin migrated methods from UserAssetManager ---
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
        private void DrawLoginUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Login", styles.headerStyle);
            GUILayout.Space(10);

            userEmail = EditorGUILayout.TextField("Email", userEmail);
            userPassword = EditorGUILayout.PasswordField("Password", userPassword);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = !isLoggingIn;
            if (GUILayout.Button("Login", styles.buttonStyle, GUILayout.Width(120), GUILayout.Height(30)))
            {
                isLoggingIn = true;
                uploadStatus = "Logging in...";
                EditorCoroutineUtility.StartCoroutineOwnerless(LoginCoroutine());
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(uploadStatus))
            {
                GUILayout.Space(10);
                MessageType messageType = uploadStatus.Contains("fail") || uploadStatus.Contains("error") ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(uploadStatus, messageType);
            }

            EditorGUILayout.EndVertical();
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
                try { EditorGUILayout.EndVertical(); } catch { }
            }
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
                EditorGUI.BeginChangeCheck();
                autoSyncEnabled = EditorGUILayout.ToggleLeft($"Auto-sync every 15m", autoSyncEnabled, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool("OOJUManager_AutoSync", autoSyncEnabled);
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                // Asset grid or info
                if (assetsAvailable && filteredAssets != null && filteredAssets.Count > 0)
                {
                    DrawAssetsGrid();
                }
                else if (isCheckingAssets)
                {
                    GUILayout.Label("Checking for assets...", styles.infoBoxStyle);
                }
                else
                {
                    GUILayout.Label("No assets found.", styles.noAssetsStyle);
                }
                EditorGUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in DrawAssetsTab: {ex.Message}\n{ex.StackTrace}");
                try { EditorGUILayout.EndVertical(); } catch { }
            }
        }
        private void DrawSelectedObjectInfo(GameObject selectedObject)
        {
            // ... (copy the full DrawSelectedObjectInfo implementation from UserAssetManager)
        }
        private void InstallGltfFast()
        {
            // ... (copy the full InstallGltfFast implementation from UserAssetManager)
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
        private void ResetAllSettings()
        {
            authToken = "";
            EditorPrefs.DeleteKey("UserAssetManager_Token");
            autoSyncEnabled = false;
            EditorPrefs.SetBool("UserAssetManager_AutoSync", false);
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
            scrollPosition = Vector2.zero;
            assetGridScrollPosition = Vector2.zero;
            currentTab = 0;
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
        private bool IsAssetDownloaded(NetworkUtility.ExportableAsset asset)
        {
            if (string.IsNullOrEmpty(asset.filename))
                return false;
            string localPath = System.IO.Path.Combine(Application.dataPath, "OOJU", "Assets", asset.filename);
            return System.IO.File.Exists(localPath);
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
        // --- End migrated methods ---
    }
} 