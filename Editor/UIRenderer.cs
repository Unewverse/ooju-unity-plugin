using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using OOJUPlugin;

namespace OojiCustomPlugin
{
    public static class UIRenderer
    {
        public static void DrawHeader(string title, UIStyles styles)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 50);
            if (EditorGUIUtility.isProSkin)
            {
                EditorGUI.DrawRect(headerRect, new Color(0.2f, 0.2f, 0.2f));
            }
            else
            {
                EditorGUI.DrawRect(headerRect, new Color(0.7f, 0.7f, 0.7f));
            }
            
            GUI.color = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);
            GUI.Label(new Rect(headerRect.x + 15, headerRect.y + 10, headerRect.width - 30, 30), title, styles.headerStyle);
            GUI.color = Color.white;
        }

        public static void DrawGltfFastStatus(bool isInstalled, Action onInstallClick)
        {
            Rect statusBarRect = EditorGUILayout.GetControlRect(false, 30);
            if (EditorGUIUtility.isProSkin)
            {
                EditorGUI.DrawRect(statusBarRect, new Color(0.25f, 0.25f, 0.25f));
            }
            else
            {
                EditorGUI.DrawRect(statusBarRect, new Color(0.8f, 0.8f, 0.8f));
            }
            
            if (isInstalled)
            {
                // Green status indicator
                Rect indicatorRect = new Rect(statusBarRect.x + 10, statusBarRect.y + 10, 10, 10);
                EditorGUI.DrawRect(indicatorRect, new Color(0.4f, 0.8f, 0.4f));
                
                // Status text
                GUI.Label(new Rect(statusBarRect.x + 30, statusBarRect.y + 7, statusBarRect.width - 40, 20), 
                    "GLTFast is installed and ready to use", 
                    EditorStyles.boldLabel);
            }
            else
            {
                // Yellow status indicator
                Rect indicatorRect = new Rect(statusBarRect.x + 10, statusBarRect.y + 10, 10, 10);
                EditorGUI.DrawRect(indicatorRect, new Color(0.9f, 0.8f, 0.2f));
                
                // Status text
                GUI.Label(new Rect(statusBarRect.x + 30, statusBarRect.y + 5, statusBarRect.width - 140, 20), 
                    "GLTFast is not installed", 
                    EditorStyles.boldLabel);
                
                // Install button
                Rect buttonRect = new Rect(statusBarRect.width - 100, statusBarRect.y + 5, 80, 20);
                if (GUI.Button(buttonRect, "Install"))
                {
                    onInstallClick?.Invoke();
                }
            }
        }

        /// <summary>
        /// Draws the login UI
        /// </summary>
        public static void DrawLoginUI(
            ref string email, 
            ref string password, 
            bool isLoggingIn, 
            string statusMessage, 
            Action onLoginClick,
            UIStyles styles)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.Label("Authentication", styles.subHeaderStyle);
            GUILayout.Space(5);

            email = EditorGUILayout.TextField("Email:", email);
            password = EditorGUILayout.PasswordField("Password:", password);

            GUI.enabled = !isLoggingIn && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password);
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Login", GUILayout.Width(120), GUILayout.Height(30)))
            {
                onLoginClick?.Invoke();
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUI.enabled = true;

            if (isLoggingIn)
            {
                EditorGUILayout.HelpBox("Logging in... please wait.", MessageType.Info);
            }

            // Status Message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }

        public static void DrawAccountBar(string email, Action onLogoutClick)
        {
            // Account info section - slim top bar
            Rect accountRect = EditorGUILayout.GetControlRect(false, 36);
            EditorGUI.DrawRect(accountRect, EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.75f, 0.75f, 0.8f));
            
            // Email with icon
            Texture2D userIcon = EditorGUIUtility.FindTexture("d_UnityEditor.InspectorWindow");
            GUI.DrawTexture(new Rect(accountRect.x + 10, accountRect.y + 10, 16, 16), userIcon);
            GUI.Label(new Rect(accountRect.x + 32, accountRect.y + 10, accountRect.width - 120, 20), email, EditorStyles.boldLabel);
            
            // Logout button 
            Rect logoutRect = new Rect(accountRect.width - 90, accountRect.y + 8, 80, 20);
            if (GUI.Button(logoutRect, "Logout"))
            {
                onLogoutClick?.Invoke();
            }
        }

        public static void DrawUploadSection(
            GameObject selectedObject,
            bool isExporting,
            bool isGltfInstalled,
            string statusMessage,
            UIStyles styles,
            Action<GameObject> onExportClick)
        {
            // Upload section with nicer header
            Rect uploadHeaderRect = EditorGUILayout.GetControlRect(false, 28);
            EditorGUI.DrawRect(uploadHeaderRect, EditorGUIUtility.isProSkin ? new Color(0.35f, 0.5f, 0.35f) : new Color(0.65f, 0.8f, 0.65f));
            GUI.Label(uploadHeaderRect, "  Upload Assets", styles.sectionHeaderStyle);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.Space(5);
            
            if (selectedObject != null)
            {
                DrawSelectedObjectInfo(selectedObject, isExporting, isGltfInstalled, styles, onExportClick);
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("Select a GameObject to export", MessageType.Info, true);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

            // Status Message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(5);
                MessageType messageType = statusMessage.Contains("failed") || statusMessage.Contains("Failed") 
                    ? MessageType.Error 
                    : MessageType.Info;
                    
                Rect statusRect = EditorGUILayout.GetControlRect(false, 30);
                if (messageType == MessageType.Error)
                {
                    EditorGUI.DrawRect(statusRect, new Color(0.8f, 0.3f, 0.3f, 0.2f));
                }
                else
                {
                    EditorGUI.DrawRect(statusRect, new Color(0.3f, 0.8f, 0.3f, 0.2f));
                }
                
                GUI.Label(new Rect(statusRect.x + 10, statusRect.y + 8, statusRect.width - 20, 20), statusMessage);
            }
            
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private static void DrawSelectedObjectInfo(
            GameObject selectedObject, 
            bool isExporting, 
            bool isGltfInstalled, 
            UIStyles styles,
            Action<GameObject> onExportClick)
        {
            // Show selected object info in a nice box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Show object name with icon
            Texture2D objIcon = EditorGUIUtility.FindTexture("d_GameObject Icon");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(5);
            GUILayout.Label(objIcon, GUILayout.Width(16), GUILayout.Height(16));
            GUILayout.Label(selectedObject.name, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            // Show object info in smaller text
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(26);
            string objectInfo = "";
            if (selectedObject.GetComponent<MeshFilter>() != null && selectedObject.GetComponent<MeshRenderer>() != null)
            {
                MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
                objectInfo = $"Mesh: {meshFilter.sharedMesh.vertexCount} vertices";
            }
            else if (selectedObject.GetComponent<SkinnedMeshRenderer>() != null)
            {
                SkinnedMeshRenderer renderer = selectedObject.GetComponent<SkinnedMeshRenderer>();
                objectInfo = $"Skinned Mesh: {renderer.sharedMesh.vertexCount} vertices";
            }
            else
            {
                objectInfo = "GameObject";
            }
            GUILayout.Label(objectInfo, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            GUI.enabled = !isExporting && isGltfInstalled;
            
            // Button area
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Use a more noticeable color for the upload button
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = styles.uploadButtonColor;
            
            if (GUILayout.Button("Export & Upload", styles.buttonStyle, GUILayout.Width(150), GUILayout.Height(30)))
            {
                onExportClick?.Invoke(selectedObject);
            }
            
            // Restore original color
            GUI.backgroundColor = originalColor;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;
            
            if (isExporting)
            {
                GUILayout.Space(5);
                EditorGUILayout.HelpBox("Processing... please wait.", MessageType.Info);
            }
        }

        public static void DrawDownloadSection(
            bool assetsAvailable,
            int assetCount,
            List<NetworkUtility.ExportableAsset> availableAssets,
            ref List<string> selectedAssetIds,
            bool isCheckingAssets,
            bool isDownloading,
            bool autoSyncEnabled,
            int syncInterval,
            string statusMessage,
            UIStyles styles,
            Action onRefreshClick,
            Action<bool> onAutoSyncToggle,
            Action<string[]> onDownloadSelectedClick,
            Action onSyncChangedClick)
        {
            // Download section with nicer header
            Rect downloadHeaderRect = EditorGUILayout.GetControlRect(false, 28);
            EditorGUI.DrawRect(downloadHeaderRect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.4f, 0.6f) : new Color(0.6f, 0.7f, 0.9f));
            GUI.Label(downloadHeaderRect, "  Download Assets", styles.sectionHeaderStyle);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUILayout.Space(5);
            
            // Refresh row with counter
            EditorGUILayout.BeginHorizontal();
            
            // Asset count with icon
            if (assetsAvailable) {
                Texture2D assetIcon = EditorGUIUtility.FindTexture("d_Package Manager");
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(24));
                GUILayout.Label(new GUIContent($" {assetCount} assets available", assetIcon), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.FlexibleSpace();
            
            // Refresh button
            GUI.enabled = !isCheckingAssets && !isDownloading;
            
            Rect refreshButtonRect = GUILayoutUtility.GetRect(80, 24);
            if (GUI.Button(refreshButtonRect, new GUIContent(" Refresh", styles.refreshIcon)))
            {
                onRefreshClick?.Invoke();
            }
            
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            
            // Show assets info or no assets message
            if (isCheckingAssets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Space(10);
                Rect spinnerRect = EditorGUILayout.GetControlRect(false, 40);
                GUI.Label(new Rect(spinnerRect.width/2 - 100, spinnerRect.y, 200, 40), "Checking for available assets...", EditorStyles.boldLabel);
                GUILayout.Space(10);
                EditorGUILayout.EndVertical();
            }
            else if (assetsAvailable)
            {
                DrawDownloadButtons(isDownloading, availableAssets, ref selectedAssetIds, styles, onDownloadSelectedClick, onSyncChangedClick);
            }
            else
            {
                // Style for no assets message
                Rect noAssetsRect = EditorGUILayout.GetControlRect(false, 80);
                EditorGUI.DrawRect(noAssetsRect, new Color(0.9f, 0.9f, 0.9f, 0.1f));
                
                GUI.Label(new Rect(noAssetsRect.x + 20, noAssetsRect.y + 20, noAssetsRect.width - 40, 40), 
                    "No assets available for download.\nUpload some assets first or check your connection.", 
                    styles.noAssetsStyle);
            }
            
            GUILayout.Space(10);
            
            // Auto-sync section with toggle
            Rect syncRect = EditorGUILayout.GetControlRect(false, 24);
            EditorGUI.DrawRect(syncRect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
            
            bool newAutoSyncValue = EditorGUI.Toggle(
                new Rect(syncRect.x + 10, syncRect.y + 4, 16, 16), 
                autoSyncEnabled);
                    
            GUI.Label(new Rect(syncRect.x + 32, syncRect.y + 4, syncRect.width - 40, 16), 
                $"Auto-sync every {syncInterval} minutes");
                    
            if (newAutoSyncValue != autoSyncEnabled)
            {
                onAutoSyncToggle?.Invoke(newAutoSyncValue);
            }
            
            // Download status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(5);
                MessageType messageType = statusMessage.Contains("failed") || statusMessage.Contains("Failed") 
                    ? MessageType.Error 
                    : MessageType.Info;
                
                Rect statusRect = EditorGUILayout.GetControlRect(false, 30);
                if (messageType == MessageType.Error)
                {
                    EditorGUI.DrawRect(statusRect, new Color(0.8f, 0.3f, 0.3f, 0.2f));
                }
                else if (statusMessage.Contains("Complete") || statusMessage.Contains("complete"))
                {
                    EditorGUI.DrawRect(statusRect, new Color(0.3f, 0.8f, 0.3f, 0.2f));
                }
                else
                {
                    EditorGUI.DrawRect(statusRect, new Color(0.3f, 0.3f, 0.8f, 0.2f));
                }
                
                GUI.Label(new Rect(statusRect.x + 10, statusRect.y + 8, statusRect.width - 20, 20), statusMessage);
            }
            
            EditorGUILayout.EndVertical();
        }

        private static void DrawDownloadButtons(
            bool isDownloading,
            List<NetworkUtility.ExportableAsset> availableAssets,
            ref List<string> selectedAssetIds,
            UIStyles styles,
            Action<string[]> onDownloadSelectedClick,
            Action onSyncChangedClick)
        {
            GUI.enabled = !isDownloading;
            
            // Make sure selectedAssetIds is initialized
            if (selectedAssetIds == null)
                selectedAssetIds = new List<string>();
            
            // Asset selection area
            EditorGUILayout.LabelField("Available Assets", EditorStyles.boldLabel);
            
            // Show scrollable asset grid
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (availableAssets != null && availableAssets.Count > 0)
            {
                Vector2 iconSize = new Vector2(80, 80);
                float padding = 10f;
                float availableWidth = EditorGUIUtility.currentViewWidth - 40; // Account for padding
                int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (iconSize.x + padding)));
                
                int assetIndex = 0;
                while (assetIndex < availableAssets.Count)
                {
                    // Start a new row
                    EditorGUILayout.BeginHorizontal();
                    
                    // Add assets to this row
                    for (int col = 0; col < columns && assetIndex < availableAssets.Count; col++)
                    {
                        try {
                            var asset = availableAssets[assetIndex];
                            assetIndex++;
                            
                            if (asset == null) continue;
                            
                            // Begin vertical layout for each asset
                            EditorGUILayout.BeginVertical(GUILayout.Width(iconSize.x));
                            
                            // Choose icon based on asset type
                            Texture2D icon = EditorGUIUtility.FindTexture("d_DefaultAsset");
                            try {
                                if (asset.content_type != null && asset.content_type.Contains("image"))
                                    icon = EditorGUIUtility.FindTexture("d_Image");
                                else if (asset.content_type != null && asset.content_type.Contains("model"))
                                    icon = EditorGUIUtility.FindTexture("d_Mesh");
                            }
                            catch {
                                // Fallback to default icon if any error
                            }
                            
                            // Selection status (with null check)
                            bool isSelected = asset.id != null && selectedAssetIds.Contains(asset.id);
                            bool newIsSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(iconSize.x));
                            
                            // Handle selection change
                            if (newIsSelected != isSelected && asset.id != null)
                            {
                                if (newIsSelected)
                                    selectedAssetIds.Add(asset.id);
                                else
                                    selectedAssetIds.Remove(asset.id);
                            }
                            
                            // Draw icon
                            Rect iconRect = GUILayoutUtility.GetRect(iconSize.x, iconSize.y);
                            if (icon != null)
                                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                            
                            // Draw name below icon (with null check)
                            string displayName = "Untitled Asset";
                            if (!string.IsNullOrEmpty(asset.filename))
                                displayName = asset.filename.Length > 12 ? asset.filename.Substring(0, 12) + "..." : asset.filename;
                            
                            EditorGUILayout.LabelField(displayName, EditorStyles.miniLabel, GUILayout.Width(iconSize.x));
                            
                            EditorGUILayout.EndVertical();
                        }
                        catch (Exception ex) {
                            Debug.LogError($"Error rendering asset at index {assetIndex-1}: {ex.Message}");
                            // Make sure we still end the vertical group if there was an error
                            try { EditorGUILayout.EndVertical(); } catch { }
                        }
                        
                        GUILayout.Space(10);
                    }
                    
                    GUILayout.FlexibleSpace();
                    
                    // End horizontal row
                    EditorGUILayout.EndHorizontal();
                    
                    GUILayout.Space(10);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No assets available", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
            
            // Button area
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Set a distinct color for the download buttons
            Color originalColor = GUI.backgroundColor;
            
            // Download selected button
            GUI.enabled = !isDownloading && selectedAssetIds != null && selectedAssetIds.Count > 0;
            GUI.backgroundColor = styles.downloadButtonColor;
            
            if (GUILayout.Button($"Download Selected ({(selectedAssetIds != null ? selectedAssetIds.Count : 0)})", 
                styles.buttonStyle, GUILayout.Width(200), GUILayout.Height(30)))
            {
                onDownloadSelectedClick?.Invoke(selectedAssetIds.ToArray());
            }
            
            GUILayout.Space(10);

            GUI.backgroundColor = originalColor;
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            if (isDownloading)
            {
                GUILayout.Space(5);
                EditorGUILayout.HelpBox("Downloading... please wait.", MessageType.Info);
            }
        }
    }
}