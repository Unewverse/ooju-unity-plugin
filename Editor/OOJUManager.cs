
using UnityEditor;
using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor.IMGUI.Controls;
using OojiCustomPlugin;
using System.Text.RegularExpressions;
using OOJUPlugin;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OOJUPlugin
{
    public class OOJUManager : EditorWindow
    {
        // Tab system
        private enum Tab { Asset, Interaction }
        private Tab currentTab = Tab.Asset;
        private string[] tabNames = { "Asset", "Interaction" };

        // Interaction sub-tab system
        private enum InteractionSubTab { Tools, Settings }
        private InteractionSubTab currentInteractionSubTab = InteractionSubTab.Tools;
        private string[] interactionSubTabNames = { "Tools", "Settings" };

        // Asset Manager variables
        private string authToken = "";
        private string uploadStatus = "";
        private string downloadStatus = "";
        private string userEmail = "";
        private string userPassword = "";
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
        private List<string> pendingUploadFiles = new List<string>();

        // Asset previews
        private Dictionary<string, Texture2D> assetPreviews = new Dictionary<string, Texture2D>();
        private Texture2D defaultPreviewTexture;

        // UI and styles
        private UIStyles styles;
        private SearchField assetSearchField;
        private string assetSearchQuery = "";

        // Assets management
        private List<NetworkUtility.ExportableAsset> availableAssets = new List<NetworkUtility.ExportableAsset>();
        private List<NetworkUtility.ExportableAsset> filteredAssets = new List<NetworkUtility.ExportableAsset>();
        private List<string> selectedAssetIds = new List<string>();

        // Removed assetSubTab and assetTabLabels as they are no longer needed

        // Interaction-related fields (migrated from OOJUInteractionWindow)
        private Vector2 mainScrollPosition = Vector2.zero;
        private Vector2 analyzerScrollPosition = Vector2.zero;
        private Vector2 descriptionScrollPosition = Vector2.zero;
        private Vector2 settingsScrollPosition = Vector2.zero;
        private string sceneDescription = "";
        private bool isGeneratingDescription = false;
        private Dictionary<string, string[]> interactionSuggestions = null;
        private string userInteractionInput = "";
        private string sentenceToInteractionResult = "";
        private List<GameObject> foundSuggestedObjects = new List<GameObject>();
        private string lastGeneratedScriptPath = "";
        private string lastSuggestedObjectNames = "";
        private Vector2 lastScriptSummaryScroll = Vector2.zero;
        private Dictionary<string, string> userObjectInput = new Dictionary<string, string>();
        private string lastGeneratedClassName = "";
        private Dictionary<string, string> lastGeneratedSuggestionPerObject = new Dictionary<string, string>();

        // Player section state management
        private bool showingGroundOptions = false;
        private bool isSelectingIndividualObjects = false;
        private List<GameObject> availableGroundObjects = new List<GameObject>();
        private int currentObjectIndex = 0;

        // Hand Gesture variables
        private enum HandGesture
        {
            PointToSelect = 0,
            Pinch = 1,
            OpenPalm = 2,
            Tap = 3,
            Wave = 4
        }
        private HandGesture selectedGesture = HandGesture.PointToSelect;
        private string gestureReaction = "";

        private UIStyles interactionStyles;

        // Color definitions (ported from OOJUInteractionWindow)
        private readonly Color32 SectionTitleColor = new Color32(0xFC, 0xFC, 0xFC, 0xFF);
        private readonly Color32 DescriptionTextColor = new Color32(0xFC, 0xFC, 0xFC, 0xFF);
        private readonly Color32 ButtonBgColor = new Color32(0x67, 0x67, 0x67, 0xFF);
        private readonly Color32 ButtonTextColor = new Color32(0xDA, 0xDA, 0xDA, 0xFF);
        private readonly Color32 DisabledButtonBgColor = new Color32(0xB8, 0xB8, 0xB8, 0xFF);
        private readonly Color32 InputTextColor = new Color32(0xDA, 0xDA, 0xDA, 0xFF);

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

            // Load default preview texture
            LoadDefaultPreviewTexture();

            // Use NetworkUtility to get the stored token
            authToken = NetworkUtility.GetStoredToken();
            
            // Restore user email if token exists
            if (!string.IsNullOrEmpty(authToken))
            {
                userEmail = NetworkUtility.GetStoredUserEmail();
            }

            if (NetworkUtility.HasValidStoredToken())
            {
                CheckAssets();
            }
            else
            {
                authToken = "";
                userEmail = "";
            }

            EditorApplication.update += OnEditorUpdate;
            
            // Enable mouse move events for better GUI stability
            wantsMouseMove = true;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            
            // Clear preview cache when window is closed
            ClearPreviewCache();
            
            // Clean up default preview texture if we created it
            if (defaultPreviewTexture != null && !AssetDatabase.Contains(defaultPreviewTexture))
            {
                DestroyImmediate(defaultPreviewTexture);
                defaultPreviewTexture = null;
            }
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
                case Tab.Interaction:
                    DrawInteractionTab();
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
                // Also restore user email if not already set
                if (string.IsNullOrEmpty(userEmail))
                {
                    userEmail = NetworkUtility.GetStoredUserEmail();
                }
            }
            if (styles == null || !styles.IsInitialized)
            {
                styles = new UIStyles();
                styles.Initialize();
            }
            HandleDragAndDrop();
            
            // Draw the header bar (logo, title, web app button) - matching Interaction tab
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Texture2D logo = Resources.Load<Texture2D>("ooju_logo");
            if (logo != null)
                GUILayout.Label(logo, GUILayout.Width(40), GUILayout.Height(40));
            GUILayout.Label("OOJU", new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter }, GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Web App", GUILayout.Width(80), GUILayout.Height(24)))
            {
                Application.OpenURL("https://demo.ooju.world/");
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);
            if (string.IsNullOrEmpty(authToken))
            {
                DrawLoginUI();
                return;
            }
            UIRenderer.DrawAccountBar(userEmail, () =>
            {
                NetworkUtility.ClearStoredToken();
                authToken = "";
                userEmail = "";
                uploadStatus = "Logged out.";
                downloadStatus = "";
                assetsAvailable = false;
                assetCount = 0;
                
                // Clear preview cache
                ClearPreviewCache();
            });
            GUILayout.Space(10);
            
            // Draw unified content (Import + Assets combined)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
            DrawUnifiedAssetsContent();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawUnifiedAssetsContent()
        {
            try
            {
                // Import ZIP Scene section at the top with improved styling
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Section icon and header (matching Interaction tab style)
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("d_FolderOpened Icon"), GUILayout.Width(22), GUILayout.Height(22));
                GUIStyle importSectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                importSectionTitleStyle.fontSize = 14;
                importSectionTitleStyle.normal.textColor = SectionTitleColor;
                EditorGUILayout.LabelField("Import OOJU Scene", importSectionTitleStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
                
                // Description with styling matching Interaction tab
                GUIStyle importDescLabelStyle = new GUIStyle(EditorStyles.label);
                importDescLabelStyle.normal.textColor = DescriptionTextColor;
                EditorGUILayout.LabelField("Import scenes created in the OOJU web application.", importDescLabelStyle);
                GUILayout.Space(8);
                
                // Button with consistent styling
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Color importPrevBg = GUI.backgroundColor;
                Color importPrevContent = GUI.contentColor;
                GUI.backgroundColor = ButtonBgColor;
                GUI.contentColor = ButtonTextColor;
                if (GUILayout.Button(new GUIContent("Import ZIP Scene", "Select and import a scene ZIP file exported from OOJU web app."), GUILayout.Width(150), GUILayout.Height(30)))
                {
                    string zipPath = EditorUtility.OpenFilePanel("Select OOJU Scene ZIP", "", "zip");
                    if (!string.IsNullOrEmpty(zipPath))
                    {
                        uploadStatus = "Processing ZIP file...";
                        EditorCoroutineUtility.StartCoroutineOwnerless(UploadZipFileCoroutine(zipPath));
                    }
                }
                GUI.backgroundColor = importPrevBg;
                GUI.contentColor = importPrevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                if (!string.IsNullOrEmpty(uploadStatus) && uploadStatus.Contains("ZIP"))
                {
                    GUILayout.Space(5);
                    MessageType messageType = uploadStatus.Contains("Error") || uploadStatus.Contains("failed")
                        ? MessageType.Error
                        : MessageType.Info;
                    EditorGUILayout.HelpBox(uploadStatus, messageType);
                }
            EditorGUILayout.EndVertical();
                
                GUILayout.Space(15);
                
                // My Assets section below with improved styling
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Section icon and header (matching Interaction tab style)
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("d_CloudConnect"), GUILayout.Width(22), GUILayout.Height(22));
                GUIStyle assetsSectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                assetsSectionTitleStyle.fontSize = 14;
                assetsSectionTitleStyle.normal.textColor = SectionTitleColor;
                EditorGUILayout.LabelField("My Assets", assetsSectionTitleStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
                
                // Description with styling matching Interaction tab
                GUIStyle assetsDescLabelStyle = new GUIStyle(EditorStyles.label);
                assetsDescLabelStyle.normal.textColor = DescriptionTextColor;
                EditorGUILayout.LabelField("Assets managed through your OOJU account.", assetsDescLabelStyle);
                GUILayout.Space(8);
                
                // Search field (right aligned)
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (assetSearchField == null)
                    assetSearchField = new SearchField();
                string newSearch = assetSearchField.OnGUI(
                    GUILayoutUtility.GetRect(100, 200, 18, 18, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(250)),
                    assetSearchQuery);
                if (newSearch != assetSearchQuery)
                {
                    assetSearchQuery = newSearch;
                    FilterAssets();
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);
                
                // Stats and refresh button row
                EditorGUILayout.BeginHorizontal();
                if (assetsAvailable && filteredAssets != null)
                {
                    if (string.IsNullOrWhiteSpace(assetSearchQuery))
                    {
                        GUILayout.Label($"{assetCount} assets", EditorStyles.boldLabel);
                    }
                    else
                    {
                        GUILayout.Label($"{filteredAssets.Count} assets", EditorStyles.boldLabel);
                    }
                }
                else if (isCheckingAssets)
                {
                    GUILayout.Label("Loading assets...", EditorStyles.boldLabel);
                }
                else
                {
                    GUILayout.Label("No assets available", EditorStyles.boldLabel);
                }
                
                GUILayout.Space(10);
                
                // Refresh button with consistent styling
                Color assetsPrevBg = GUI.backgroundColor;
                Color assetsPrevContent = GUI.contentColor;
                GUI.backgroundColor = ButtonBgColor;
                GUI.contentColor = ButtonTextColor;
                if (GUILayout.Button(new GUIContent("Refresh", "Refresh asset list from server"), GUILayout.Width(80), GUILayout.Height(20)))
                {
                    CheckAssets();
                }
                GUI.backgroundColor = assetsPrevBg;
                GUI.contentColor = assetsPrevContent;
                EditorGUILayout.EndHorizontal();
                
                // Clean up selection list - remove already downloaded assets
                if (selectedAssetIds != null && selectedAssetIds.Count > 0)
                {
                    var assetsToRemove = new List<string>();
                    foreach (string assetId in selectedAssetIds)
                    {
                        var asset = availableAssets?.FirstOrDefault(a => a.id == assetId);
                        if (asset != null && IsAssetDownloaded(asset))
                        {
                            assetsToRemove.Add(assetId);
                        }
                    }
                    foreach (string assetId in assetsToRemove)
                    {
                        selectedAssetIds.Remove(assetId);
                    }
                }

                // Download selected assets button (only show if there are non-downloaded assets selected)
                if (selectedAssetIds != null && selectedAssetIds.Count > 0)
                {
                    GUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUI.enabled = !isDownloading;
                    Color downloadPrevBg = GUI.backgroundColor;
                    Color downloadPrevContent = GUI.contentColor;
                    GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f); // Keep green for download action
                    GUI.contentColor = Color.white;
                    if (GUILayout.Button(new GUIContent($"Download {selectedAssetIds.Count} Selected Asset(s)", "Download the selected assets to your project"), GUILayout.Width(250), GUILayout.Height(30)))
                    {
                        DownloadSelectedAssets();
                    }
                    GUI.backgroundColor = downloadPrevBg;
                    GUI.contentColor = downloadPrevContent;
                    GUI.enabled = true;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                }
                
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
                Debug.LogError($"Error in DrawUnifiedAssetsContent: {ex.Message}\n{ex.StackTrace}");
                try { EditorGUILayout.EndVertical(); } catch { }
            }
        }

        private void DrawInteractionTab()
        {
            // Draw the header bar (logo, title, website button)
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Texture2D logo = Resources.Load<Texture2D>("ooju_logo");
            if (logo != null)
                GUILayout.Label(logo, GUILayout.Width(40), GUILayout.Height(40));
            GUILayout.Label("OOJU", new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleCenter }, GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Website", GUILayout.Width(80), GUILayout.Height(24)))
            {
                Application.OpenURL("https://www.ooju.world/");
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);

            // Initialize styles if needed
            if (interactionStyles == null)
                interactionStyles = new UIStyles();
            if (!interactionStyles.IsInitialized)
                interactionStyles.Initialize();



            float contentWidth = position.width - 40f;
            float buttonWidth = Mathf.Min(250f, contentWidth * 0.7f);

            // Draw the sub-tab bar for interaction
            currentInteractionSubTab = (InteractionSubTab)GUILayout.Toolbar((int)currentInteractionSubTab, interactionSubTabNames, styles.tabStyle);
            GUILayout.Space(5);

            // Draw the selected sub-tab content
            switch (currentInteractionSubTab)
            {
                case InteractionSubTab.Tools:
                    DrawInteractionToolsTab(contentWidth, buttonWidth);
                    break;
                case InteractionSubTab.Settings:
                    DrawInteractionSettingsTab();
                    break;
            }
        }

        // Draws the interaction settings tab UI
        private void DrawInteractionSettingsTab()
        {
            settingsScrollPosition = EditorGUILayout.BeginScrollView(settingsScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(20);

            DrawSettingsSection();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        // Draws the main interaction tools tab UI
        private void DrawInteractionToolsTab(float contentWidth, float buttonWidth)
        {
            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(20);

            // Make Things Interactive section
            EditorGUILayout.BeginVertical();
            try
            {
                // Section title
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("d_UnityEditor.SceneHierarchyWindow"), GUILayout.Width(22), GUILayout.Height(22));
                GUIStyle mainTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                mainTitleStyle.fontSize = 18;
                mainTitleStyle.normal.textColor = SectionTitleColor;
                EditorGUILayout.LabelField("Make Things Interactive", mainTitleStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
                
                DrawSentenceToInteractionSection(buttonWidth);
                GUILayout.Space(16);
                DrawDescriptionSection(buttonWidth);
                GUILayout.Space(16);
                DrawHandGestureSection(buttonWidth);
                GUILayout.Space(16);
                DrawPlayerSection(buttonWidth);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in Make Things Interactive section: {e.Message}");
                EditorGUILayout.HelpBox("Error displaying interactive features", MessageType.Error);
            }
            EditorGUILayout.EndVertical();



            if (isGeneratingDescription)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Generating... Please wait.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        // Draws the scene description and analysis section
        private void DrawDescriptionSection(float buttonWidth)
        {
            // Section icon and header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow"), GUILayout.Width(22), GUILayout.Height(22));
            GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionTitleStyle.fontSize = 14;
            sectionTitleStyle.normal.textColor = SectionTitleColor;
            GUIStyle descLabelStyle = new GUIStyle(EditorStyles.label);
            descLabelStyle.normal.textColor = DescriptionTextColor;
            EditorGUILayout.LabelField("Ideas for You", sectionTitleStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            EditorGUILayout.LabelField("Here are some ideas for what your selected objects could do.", descLabelStyle);
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            // Suggest Interactions button
            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;
            GUI.backgroundColor = ButtonBgColor;
            GUI.contentColor = ButtonTextColor;
            EditorGUI.BeginDisabledGroup(isGeneratingDescription);
            if (GUILayout.Button(new GUIContent("Give Me Ideas", "Get personalized ideas for your selected objects."), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
            {
                try 
                { 
                    AnalyzeSceneAndSuggestInteractions(); 
                } 
                catch (System.Exception e) 
                { 
                    Debug.LogError($"Error in AnalyzeSceneAndSuggestInteractions: {e.Message}"); 
                    EditorUtility.DisplayDialog("Error", $"Error in AnalyzeSceneAndSuggestInteractions: {e.Message}", "OK"); 
                }
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(sceneDescription))
            {
                GUILayout.Space(2);
                // Thin divider
                Rect rect = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
                GUILayout.Space(2);
            }
            if (interactionSuggestions != null && interactionSuggestions.Count > 0)
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("Ideas for your objects:", EditorStyles.boldLabel);
                foreach (var kvp in interactionSuggestions)
                {
                    string objName = kvp.Key;
                    EditorGUILayout.LabelField($"- {objName}", EditorStyles.miniBoldLabel);
                    bool validFound = false;
                    foreach (var suggestion in kvp.Value)
                    {
                        string cleanSuggestion = suggestion;
                        if (!string.IsNullOrWhiteSpace(cleanSuggestion) && cleanSuggestion != "NONE" && cleanSuggestion != "ERROR" && !cleanSuggestion.Contains("No valid suggestions found"))
                        {
                            // Remove bold markdown (**) from suggestion
                            string fullSuggestion = System.Text.RegularExpressions.Regex.Replace(cleanSuggestion, @"\*\*(.*?)\*\*", "$1");
                            
                            // Show only short version in UI for better readability
                            string shortSuggestion = GetShortSuggestion(fullSuggestion);
                            EditorGUILayout.LabelField(shortSuggestion, EditorStyles.wordWrappedLabel, GUILayout.MaxWidth(400));
                            
                            // Generate button centered
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            prevBg = GUI.backgroundColor;
                            prevContent = GUI.contentColor;
                            GUI.backgroundColor = ButtonBgColor;
                            GUI.contentColor = ButtonTextColor;
                            if (GUILayout.Button(new GUIContent("Create", "Make this happen for your object."), GUILayout.Width(80)))
                            {
                                // Use the detailed generation function
                                GenerateFromSuggestion(objName, fullSuggestion);
                            }
                            GUI.backgroundColor = prevBg;
                            GUI.contentColor = prevContent;
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(5);
                            validFound = true;
                        }
                    }
                    if (!validFound)
                    {
                        // Only show HelpBox (icon is included automatically)
                        EditorGUILayout.HelpBox("No valid suggestions found for this object. Please provide a description and desired interaction for this object below.", MessageType.Warning);
                        if (!userObjectInput.ContainsKey(objName)) userObjectInput[objName] = "";
                        userObjectInput[objName] = EditorGUILayout.TextArea(userObjectInput[objName], GUILayout.Height(40), GUILayout.ExpandWidth(true));
                        EditorGUILayout.LabelField("This may help if the object is not mentioned in the scene description or is not relevant to the current scene context.", EditorStyles.wordWrappedMiniLabel);
                    }
                }
                GUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                prevBg = GUI.backgroundColor;
                prevContent = GUI.contentColor;
                GUI.backgroundColor = ButtonBgColor;
                GUI.contentColor = ButtonTextColor;
                EditorGUI.BeginDisabledGroup(isGeneratingDescription);
                if (GUILayout.Button(new GUIContent("Get More Ideas", "Get different ideas for your objects."), GUILayout.Width(buttonWidth), GUILayout.Height(22)))
                {
                    try 
                    { 
                        RegenerateInteractionSuggestionsOnly(); 
                    } 
                    catch (System.Exception e) 
                    { 
                        Debug.LogError($"Error in RegenerateInteractionSuggestionsOnly: {e.Message}"); 
                        EditorUtility.DisplayDialog("Error", $"Error in RegenerateInteractionSuggestionsOnly: {e.Message}", "OK"); 
                    }
                }
                EditorGUI.EndDisabledGroup();
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
        }

        // Draws the sentence-to-interaction section
        private void DrawSentenceToInteractionSection(float buttonWidth)
        {
            // Section icon and header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow"), GUILayout.Width(22), GUILayout.Height(22));
            GUIStyle sectionTitleStyle2 = new GUIStyle(EditorStyles.boldLabel);
            sectionTitleStyle2.fontSize = 14;
            sectionTitleStyle2.normal.textColor = SectionTitleColor;
            EditorGUILayout.LabelField("Describe What You Want", sectionTitleStyle2);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            EditorGUILayout.LabelField("Tell me what you want this object to do", EditorStyles.miniLabel);
            GUILayout.Space(8);
            
            // Show placeholder text when empty
            if (string.IsNullOrEmpty(userInteractionInput))
            {
                EditorGUILayout.LabelField("e.g. Spin around when I click it", EditorStyles.wordWrappedMiniLabel);
            }
            
            // Use DelayedTextField to avoid focus loss - press Enter to confirm input
            userInteractionInput = EditorGUILayout.DelayedTextField(userInteractionInput, GUILayout.Height(20), GUILayout.ExpandWidth(true));
            
            // Show current text in a read-only area if it's longer
            if (!string.IsNullOrEmpty(userInteractionInput) && userInteractionInput.Length > 50)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Full Text:", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(userInteractionInput, EditorStyles.textArea, GUILayout.Height(40));
            }
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;
            GUI.backgroundColor = ButtonBgColor;
            GUI.contentColor = ButtonTextColor;
            if (GUILayout.Button(new GUIContent("Make It Happen", "Create the behavior you described."), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
            {
                try { GenerateSentenceToInteraction(); } catch (Exception ex) { Debug.LogError($"Error in GenerateSentenceToInteraction: {ex.Message}"); EditorUtility.DisplayDialog("Error", $"Error in GenerateSentenceToInteraction: {ex.Message}", "OK"); }
            }
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            // Only show Assign Script button if a script was actually generated and saved
            if (!string.IsNullOrEmpty(lastGeneratedClassName) && 
                lastGeneratedClassName != "No code block found." &&
                !string.IsNullOrEmpty(lastGeneratedScriptPath) && 
                System.IO.File.Exists(lastGeneratedScriptPath))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                prevBg = GUI.backgroundColor;
                prevContent = GUI.contentColor;
                
                // Show compilation status
                if (EditorApplication.isCompiling)
                {
                    GUI.backgroundColor = DisabledButtonBgColor;
                    GUI.contentColor = ButtonTextColor;
                    GUILayout.Button(new GUIContent("Getting Ready...", "Please wait while Unity prepares your creation"), GUILayout.Width(buttonWidth), GUILayout.Height(28));
                }
                else
                {
                    GUI.backgroundColor = ButtonBgColor;
                    GUI.contentColor = ButtonTextColor;
                    if (GUILayout.Button(new GUIContent("Apply to Selected Objects", "Make your selected objects work this way."), GUILayout.Width(buttonWidth), GUILayout.Height(28)))
                    {
                        AssignScriptToSelectedObjects();
                    }
                }
                
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            if (!string.IsNullOrEmpty(sentenceToInteractionResult))
            {
                if (!string.IsNullOrEmpty(lastGeneratedScriptPath))
                {
                    EditorGUILayout.HelpBox($"Your creation is ready! Saved to: Assets/OOJU/Interaction/Generated", MessageType.Info);
                }
                if (!string.IsNullOrEmpty(lastSuggestedObjectNames))
                {
                    EditorGUILayout.LabelField("This works best with objects named:", EditorStyles.boldLabel);
                    EditorGUILayout.TextField(lastSuggestedObjectNames);
                }
                if (foundSuggestedObjects != null && foundSuggestedObjects.Count > 0)
                {
                    EditorGUILayout.LabelField("Found these objects in your world:", EditorStyles.boldLabel);
                    foreach (var obj in foundSuggestedObjects)
                    {
                        EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                    }
                }
            }
        }



        // Draws the Settings section
        private void DrawSettingsSection()
        {
            var settings = OISettings.Instance;
            
            // Section header with better spacing for dedicated tab
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_Settings"), GUILayout.Width(24), GUILayout.Height(24));
            GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionTitleStyle.fontSize = 16;
            sectionTitleStyle.normal.textColor = SectionTitleColor;
            EditorGUILayout.LabelField("LLM API Settings", sectionTitleStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
            
            GUIStyle descLabelStyle = new GUIStyle(EditorStyles.label);
            descLabelStyle.normal.textColor = DescriptionTextColor;
            EditorGUILayout.LabelField("Configure your LLM API settings for interaction generation.", descLabelStyle);
            EditorGUILayout.EndVertical();
            GUILayout.Space(15);
            
            // LLM Provider Selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("LLM Provider:", GUILayout.Width(100));
            string[] llmOptions = { "OpenAI", "Claude", "Gemini" };
            int selectedIndex = System.Array.IndexOf(llmOptions, settings.SelectedLLMType);
            if (selectedIndex == -1) selectedIndex = 0;
            
            int newIndex = EditorGUILayout.Popup(selectedIndex, llmOptions);
            if (newIndex != selectedIndex)
            {
                settings.SelectedLLMType = llmOptions[newIndex];
                settings.SaveSettings();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            
            // API Key input based on selected provider
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            switch (settings.SelectedLLMType)
            {
                case "OpenAI":
                    EditorGUILayout.LabelField("OpenAI API Key:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Get your API key from: https://platform.openai.com/api-keys", EditorStyles.miniLabel);
                    string newOpenAIKey = EditorGUILayout.PasswordField("API Key:", settings.ApiKey);
                    if (newOpenAIKey != settings.ApiKey)
                    {
                        settings.ApiKey = newOpenAIKey;
                        settings.SaveSettings();
                    }
                    break;
                    
                case "Claude":
                    EditorGUILayout.LabelField("Claude API Key:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Get your API key from: https://console.anthropic.com/", EditorStyles.miniLabel);
                    string newClaudeKey = EditorGUILayout.PasswordField("API Key:", settings.ClaudeApiKey);
                    if (newClaudeKey != settings.ClaudeApiKey)
                    {
                        settings.ClaudeApiKey = newClaudeKey;
                        settings.SaveSettings();
                    }
                    break;
                    
                case "Gemini":
                    EditorGUILayout.LabelField("Gemini API Key:", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Get your API key from: https://aistudio.google.com/app/apikey", EditorStyles.miniLabel);
                    string newGeminiKey = EditorGUILayout.PasswordField("API Key:", settings.GeminiApiKey);
                    if (newGeminiKey != settings.GeminiApiKey)
                    {
                        settings.GeminiApiKey = newGeminiKey;
                        settings.SaveSettings();
                    }
                    break;
            }
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(15);
            
            // Status display in a separate box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Configuration Status", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            bool hasValidKey = false;
            switch (settings.SelectedLLMType)
            {
                case "OpenAI":
                    hasValidKey = !string.IsNullOrEmpty(settings.ApiKey);
                    break;
                case "Claude":
                    hasValidKey = !string.IsNullOrEmpty(settings.ClaudeApiKey);
                    break;
                case "Gemini":
                    hasValidKey = !string.IsNullOrEmpty(settings.GeminiApiKey);
                    break;
            }
            
            if (hasValidKey)
            {
                EditorGUILayout.HelpBox($"{settings.SelectedLLMType} API key configured successfully.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Please enter your {settings.SelectedLLMType} API key to use interaction generation features.", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(20);
        }

        // Draws the Hand Gesture section
        private void DrawHandGestureSection(float buttonWidth)
        {
            try
            {
                // Section icon and header
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("d_Toolbar Plus"), GUILayout.Width(22), GUILayout.Height(22));
                GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                sectionTitleStyle.fontSize = 14;
                sectionTitleStyle.normal.textColor = SectionTitleColor;
                EditorGUILayout.LabelField("Control with Hands", sectionTitleStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
                EditorGUILayout.LabelField("Make objects respond to your hand gestures in XR.", EditorStyles.miniLabel);
                GUILayout.Space(8);
                
                // Selected object info
                GameObject selectedObj = Selection.activeGameObject;
                if (selectedObj == null)
                {
                    EditorGUILayout.HelpBox("Please select an object in the scene to add hand gesture control.", MessageType.Info);
                    return;
                }
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Selected Object:", EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.ObjectField(selectedObj, typeof(GameObject), true);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);
                
                // Gesture selection
                EditorGUILayout.LabelField("Choose a Hand Gesture:", EditorStyles.boldLabel);
                GUILayout.Space(4);
                
                string[] gestureLabels = {
                    "👆 Point to select objects",
                    "🤏 Pinch", 
                    "✋ Open palm",
                    "👉 Tap",
                    "🤚 Wave"
                };
                
                selectedGesture = (HandGesture)EditorGUILayout.Popup((int)selectedGesture, gestureLabels);
                GUILayout.Space(8);
                
                // Show effect description for selected gesture
                EditorGUILayout.LabelField("Effect:", EditorStyles.boldLabel);
                GUILayout.Space(4);
                
                string effectDescription = GetDetailedEffectDescription(selectedGesture);
                GUIStyle effectStyle = new GUIStyle(EditorStyles.helpBox);
                effectStyle.padding = new RectOffset(12, 12, 10, 10);
                effectStyle.fontSize = 12;
                effectStyle.normal.textColor = DescriptionTextColor;
                effectStyle.wordWrap = true;
                effectStyle.alignment = TextAnchor.UpperLeft;
                
                // Calculate height based on content
                float textHeight = effectStyle.CalcHeight(new GUIContent(effectDescription), EditorGUIUtility.currentViewWidth - 40);
                float minHeight = Mathf.Max(80f, textHeight + 20f); // Ensure minimum height and add some padding
                
                EditorGUILayout.LabelField(effectDescription, effectStyle, GUILayout.Height(minHeight), GUILayout.ExpandWidth(true));
                
                // Auto-set gestureReaction based on selected gesture
                gestureReaction = GetEffectName(selectedGesture);
                
                GUILayout.Space(10);
                
                // Create gesture button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Color prevBg = GUI.backgroundColor;
                Color prevContent = GUI.contentColor;
                GUI.backgroundColor = ButtonBgColor;
                GUI.contentColor = ButtonTextColor;
                
                if (GUILayout.Button(new GUIContent("Create Hand Control", "Create gesture-based interaction for this object."), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
                {
                    CreateHandControl();
                }
                
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in Hand Gesture section: {e.Message}");
                EditorGUILayout.HelpBox("Error displaying Hand Gesture section", MessageType.Error);
            }
        }

        // Draws the Player section
        private void DrawPlayerSection(float buttonWidth)
        {
            try
            {
                // Section icon and header
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("Avatar Icon"), GUILayout.Width(22), GUILayout.Height(22));
                GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                sectionTitleStyle.fontSize = 14;
                sectionTitleStyle.normal.textColor = SectionTitleColor;
                EditorGUILayout.LabelField("Player", sectionTitleStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
                if (!showingGroundOptions)
                {
                    EditorGUILayout.LabelField("Add a player who can walk around and explore your world.", EditorStyles.miniLabel);
                }
                GUILayout.Space(8);
                
                DrawAddPlayerSection(buttonWidth);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in Player section: {e.Message}");
                EditorGUILayout.HelpBox("Error displaying Player section", MessageType.Error);
            }
        }

        // Draws the Add Player section (UI + logic)
        private void DrawAddPlayerSection(float buttonWidth)
        {
            if (!showingGroundOptions)
            {
                // Main Add Player button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Color prevBg = GUI.backgroundColor;
                Color prevContent = GUI.contentColor;
                GUI.backgroundColor = ButtonBgColor;
                GUI.contentColor = ButtonTextColor;
                if (GUILayout.Button(new GUIContent("Add Player", "Add someone to walk around and explore your world."), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
                {
                    AddFirstPersonPlayerToScene();
                }
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawGroundOptionsSection(buttonWidth);
            }
        }

        // Draws the ground options section after player creation
        private void DrawGroundOptionsSection(float buttonWidth)
        {
            if (!isSelectingIndividualObjects)
            {
                // Ground options header
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(EditorGUIUtility.IconContent("d_Terrain Icon"), GUILayout.Width(22), GUILayout.Height(22));
                GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
                sectionTitleStyle.fontSize = 14;
                sectionTitleStyle.normal.textColor = SectionTitleColor;
                EditorGUILayout.LabelField("What should your player walk on?", sectionTitleStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
                EditorGUILayout.LabelField("Choose one of these options to prevent your player from falling endlessly:", EditorStyles.miniLabel);
                GUILayout.Space(12);

                // Option 1: Create New Ground
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Color prevBg = GUI.backgroundColor;
                Color prevContent = GUI.contentColor;
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f); // Green for recommended option
                GUI.contentColor = ButtonTextColor;
                if (GUILayout.Button(new GUIContent("Create New Ground", "Add a large floor for your player to walk on (Recommended)"), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
                {
                    AddGroundToScene();
                    showingGroundOptions = false;
                }
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // Option 2: Choose Existing Object
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = ButtonBgColor;
                GUI.contentColor = ButtonTextColor;
                if (GUILayout.Button(new GUIContent("Choose Existing Object", "Turn something already in your scene into walkable ground"), GUILayout.Width(buttonWidth), GUILayout.Height(28)))
                {
                    StartIndividualObjectSelection();
                }
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);

                // Option 3: Skip
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f); // Gray for skip option
                GUI.contentColor = ButtonTextColor;
                if (GUILayout.Button(new GUIContent("Skip for Now", "I'll add ground later (Player will fall unless you have ground)"), GUILayout.Width(buttonWidth), GUILayout.Height(28)))
                {
                    showingGroundOptions = false;
                    EditorUtility.DisplayDialog("No Ground Added", 
                        "No problem! You can always add ground later.\n\n" +
                        "Remember: Without ground, your player will fall endlessly when you press Play.\n\n" +
                        "Tip: You can always come back to this Player section to add ground later.", "Got it");
                }
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawIndividualObjectSelection();
            }
        }

        // Helper: Add a large ground cube at y=0
        private void AddGroundToScene()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(30, 1, 30);
            ground.layer = LayerMask.NameToLayer("Default");
            BoxCollider col = ground.GetComponent<BoxCollider>();
            if (col == null) ground.AddComponent<BoxCollider>();
            Selection.activeGameObject = ground;
            EditorGUIUtility.PingObject(ground);
            EditorUtility.DisplayDialog("Ground Added", 
                "Perfect! A large ground has been created for your player to walk on.\n\n" +
                "Your player is now ready to explore! Press Play to try it out.\n\n" +
                "Controls: WASD to move, Space to jump, Mouse to look around.", "Awesome!");
        }

        // Start the individual object selection process
        private void StartIndividualObjectSelection()
        {
            // Find all GameObjects in the scene that could be used as ground
            availableGroundObjects.Clear();
            
            // Get all root GameObjects in the active scene
            GameObject[] allObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            
            foreach (GameObject rootObj in allObjects)
            {
                // Add root object and all its children
                AddObjectAndChildren(rootObj, availableGroundObjects);
            }
            
            // Filter out objects that are clearly not suitable for ground
            availableGroundObjects.RemoveAll(obj => 
                obj == null || 
                obj.name.Contains("Player") || 
                obj.name.Contains("Camera") ||
                obj.name.Contains("Light") ||
                obj.GetComponent<Camera>() != null ||
                obj.GetComponent<Light>() != null
            );

            if (availableGroundObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("No Objects Found", 
                    "No suitable objects found in your scene to use as ground.\n\n" +
                    "Try adding some objects to your scene first, or choose 'Create New Ground' instead.", "OK");
                return;
            }

            isSelectingIndividualObjects = true;
            currentObjectIndex = 0;
        }

        // Recursively add object and its children to the list
        private void AddObjectAndChildren(GameObject obj, List<GameObject> list)
        {
            if (obj != null)
            {
                list.Add(obj);
                
                for (int i = 0; i < obj.transform.childCount; i++)
                {
                    Transform child = obj.transform.GetChild(i);
                    if (child != null && child.gameObject != null)
                    {
                        AddObjectAndChildren(child.gameObject, list);
                    }
                }
            }
        }

        // Draw the individual object selection UI
        private void DrawIndividualObjectSelection()
        {
            if (availableGroundObjects.Count == 0 || currentObjectIndex >= availableGroundObjects.Count)
            {
                // No more objects or invalid index
                isSelectingIndividualObjects = false;
                showingGroundOptions = false;
                EditorUtility.DisplayDialog("Selection Complete", "No more objects to review.", "OK");
                return;
            }

            GameObject currentObj = availableGroundObjects[currentObjectIndex];
            if (currentObj == null)
            {
                // Skip null objects
                currentObjectIndex++;
                return;
            }

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("d_GameObject Icon"), GUILayout.Width(22), GUILayout.Height(22));
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 14;
            titleStyle.normal.textColor = SectionTitleColor;
            EditorGUILayout.LabelField("Should this be walkable ground?", titleStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            
            EditorGUILayout.LabelField($"Reviewing object {currentObjectIndex + 1} of {availableGroundObjects.Count}", EditorStyles.miniLabel);
            GUILayout.Space(8);

            // Show current object
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Object:", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.ObjectField(currentObj, typeof(GameObject), true);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);

            // Highlight the object in scene
            if (GUILayout.Button(new GUIContent("Highlight in Scene", "Show this object in the Scene view"), GUILayout.Height(24)))
            {
                Selection.activeGameObject = currentObj;
                EditorGUIUtility.PingObject(currentObj);
                SceneView.FrameLastActiveSceneView();
            }
            GUILayout.Space(12);

            // Decision buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;
            
            // Yes button (Green)
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            GUI.contentColor = ButtonTextColor;
            if (GUILayout.Button(new GUIContent("✓ Yes, Use This", "Make this object walkable ground"), GUILayout.Width(120), GUILayout.Height(32)))
            {
                SetObjectAsGround(currentObj);
                currentObjectIndex++;
                if (currentObjectIndex >= availableGroundObjects.Count)
                {
                    FinishObjectSelection();
                }
            }
            
            GUILayout.Space(8);
            
            // No button (Yellow)
            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.4f);
            if (GUILayout.Button(new GUIContent("→ No, Next Object", "Skip this object and see the next one"), GUILayout.Width(120), GUILayout.Height(32)))
            {
                currentObjectIndex++;
                if (currentObjectIndex >= availableGroundObjects.Count)
                {
                    FinishObjectSelection();
                }
            }
            
            GUILayout.Space(8);
            
            // Cancel button (Gray)
            GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
            if (GUILayout.Button(new GUIContent("✕ Cancel", "Go back to ground options"), GUILayout.Width(80), GUILayout.Height(32)))
            {
                isSelectingIndividualObjects = false;
                currentObjectIndex = 0;
            }
            
            GUILayout.Space(8);
            
            // Done button (Blue) - allows user to finish early
            GUI.backgroundColor = new Color(0.4f, 0.6f, 0.8f);
            if (GUILayout.Button(new GUIContent("✓ Done", "I'm satisfied with the ground I've set up"), GUILayout.Width(80), GUILayout.Height(32)))
            {
                FinishObjectSelection();
            }
            
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // Set a specific object as ground
        private void SetObjectAsGround(GameObject obj)
        {
            if (obj == null) return;
            
            // Add collider if not present
            if (obj.GetComponent<Collider>() == null)
            {
                // Try to determine the best collider type
                var meshRenderer = obj.GetComponent<MeshRenderer>();
                var meshFilter = obj.GetComponent<MeshFilter>();
                
                if (meshRenderer != null && meshFilter != null && meshFilter.sharedMesh != null)
                {
                    obj.AddComponent<MeshCollider>();
                }
                else
                {
                    obj.AddComponent<BoxCollider>();
                }
            }
            
            obj.layer = LayerMask.NameToLayer("Default");
            
            EditorUtility.DisplayDialog("Ground Set!", 
                $"Great! '{obj.name}' is now walkable ground.\n\n" +
                "Would you like to continue reviewing other objects, or are you done?", "Continue");
        }

        // Finish the object selection process
        private void FinishObjectSelection()
        {
            isSelectingIndividualObjects = false;
            showingGroundOptions = false;
            
            EditorUtility.DisplayDialog("Selection Complete", 
                "You've reviewed all available objects.\n\n" +
                "Your player is ready! Press Play to test it out.\n\n" +
                "Controls: WASD to move, Space to jump, Mouse to look around.", "Great!");
        }

        // Helper: Add a MeshCollider to selected objects and set their layer to Default (Legacy function kept for compatibility)
        private void SetSelectedAsGround()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select at least one object in the scene.", "OK");
                return;
            }
            int count = 0;
            foreach (var obj in selected)
            {
                if (obj == null) continue;
                if (obj.GetComponent<Collider>() == null)
                    obj.AddComponent<MeshCollider>();
                obj.layer = LayerMask.NameToLayer("Default");
                count++;
            }
            EditorUtility.DisplayDialog("Set as Ground", $"MeshCollider added and layer set to Default for {count} object(s).", "OK");
        }

        // Helper: Extract first sentence from suggestion for UI display
        private string GetShortSuggestion(string fullSuggestion)
        {
            if (string.IsNullOrEmpty(fullSuggestion)) return fullSuggestion;
            
            // Find first sentence ending (period, exclamation, or question mark)
            int firstSentenceEnd = -1;
            for (int i = 0; i < fullSuggestion.Length; i++)
            {
                if (fullSuggestion[i] == '.' || fullSuggestion[i] == '!' || fullSuggestion[i] == '?')
                {
                    firstSentenceEnd = i;
                    break;
                }
            }
            
            if (firstSentenceEnd > 0)
            {
                return fullSuggestion.Substring(0, firstSentenceEnd + 1).Trim();
            }
            
            // If no sentence ending found, return up to 80 characters
            if (fullSuggestion.Length > 80)
            {
                return fullSuggestion.Substring(0, 80).Trim() + "...";
            }
            
            return fullSuggestion;
        }

        // Helper: Generate detailed prompt for script generation based on suggestion
        private void GenerateFromSuggestion(string objName, string fullSuggestion)
        {
            // Create detailed prompt for better script generation
            string detailedPrompt = $"Create a Unity C# script for {objName} with the following interaction: {fullSuggestion}. " +
                                   $"Make sure the script is compatible with Unity 6, uses modern C# practices, and includes proper error handling. " +
                                   $"Scene context: {sceneDescription}";
            
            // Store the detailed version for generation
            lastGeneratedSuggestionPerObject[objName] = fullSuggestion;
            userInteractionInput = detailedPrompt;
            GenerateSentenceToInteraction();
        }

        // Helper: Add a simple first-person player controller to the scene
        private void AddFirstPersonPlayerToScene()
        {
            var scriptAsset = AssetDatabase.FindAssets("FirstPersonPlayer t:Script");
            string scriptPath = null;
            string directory = "Assets/OOJU/Interaction/Player";
            if (scriptAsset.Length > 0)
            {
                scriptPath = AssetDatabase.GUIDToAssetPath(scriptAsset[0]);
            }
            else
            {
                if (!System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);
                scriptPath = System.IO.Path.Combine(directory, "FirstPersonPlayer.cs");
                System.IO.File.WriteAllText(scriptPath, GetFirstPersonPlayerScriptCode());
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Script Created", "FirstPersonPlayer.cs script was created. Please wait for Unity to compile, then press the button again to add the player.", "OK");
                return;
            }
            var scriptType = GetTypeByName("FirstPersonPlayer");
            if (scriptType == null)
            {
                EditorUtility.DisplayDialog("Script Compile Needed", "FirstPersonPlayer.cs script was created or updated. Please wait for Unity to compile, then try again.", "OK");
                return;
            }
            if (GameObject.FindFirstObjectByType(scriptType) != null)
            {
                EditorUtility.DisplayDialog("Already Exists", "A FirstPersonPlayer object already exists in the scene.", "OK");
                return;
            }
            GameObject player = new GameObject("FirstPersonPlayer");
            var charCtrlType = typeof(CharacterController);
            if (charCtrlType != null)
                player.AddComponent<CharacterController>();
            player.AddComponent(scriptType);
            Selection.activeGameObject = player;
            EditorGUIUtility.PingObject(player);
            
            // Show ground options after player creation
            showingGroundOptions = true;
            isSelectingIndividualObjects = false;
            currentObjectIndex = 0;
            
            EditorUtility.DisplayDialog("Player Added!", 
                "Great! Your player has been added to the scene.\n\n" +
                "Now let's set up something for them to walk on so they don't fall forever.", "Continue");
        }

        // Returns the code for a simple FirstPersonPlayer script (WASD/arrow keys + Space to jump)
        private string GetFirstPersonPlayerScriptCode()
        {
            return "using UnityEngine;\n" +
                   "using UnityEngine.InputSystem;\n" +
                   "// Simple first-person player controller (WASD/arrow keys + Space to jump). Uses the new Input System.\n" +
                   "[RequireComponent(typeof(CharacterController))]\n" +
                   "public class FirstPersonPlayer : MonoBehaviour\n" +
                   "{\n" +
                   "    public float speed = 5f;\n" +
                   "    public float mouseSensitivity = 2f;\n" +
                   "    public float jumpHeight = 2f;\n" +
                   "    public float gravity = -9.81f;\n" +
                   "    private float rotationY = 0f;\n" +
                   "    private CharacterController controller;\n" +
                   "    private Vector3 velocity;\n" +
                   "    private Camera playerCamera;\n" +
                   "\n" +
                   "    void Start()\n" +
                   "    {\n" +
                   "        controller = GetComponent<CharacterController>();\n" +
                   "        playerCamera = GetComponentInChildren<Camera>();\n" +
                   "        // Add a camera if not present\n" +
                   "        if (playerCamera == null)\n" +
                   "        {\n" +
                   "            GameObject camObj = new GameObject(\"PlayerCamera\");\n" +
                   "            camObj.transform.SetParent(transform);\n" +
                   "            camObj.transform.localPosition = new Vector3(0, 0.6f, 0); // Adjusted height for camera pivot\n" +
                   "            playerCamera = camObj.AddComponent<Camera>();\n" +
                   "        }\n" +
                   "\n" +
                   "        if (Application.isPlaying)\n" +
                   "        {\n" +
                   "            Cursor.lockState = CursorLockMode.Locked;\n" +
                   "            Cursor.visible = false;\n" +
                   "        }\n" +
                   "    }\n" +
                   "\n" +
                   "    void Update()\n" +
                   "    {\n" +
                   "        // Do nothing if input devices are not present\n" +
                   "        if (Keyboard.current == null || Mouse.current == null) return;\n" +
                   "\n" +
                   "        bool isGrounded = controller.isGrounded;\n" +
                   "        if (isGrounded && velocity.y < 0)\n" +
                   "        {\n" +
                   "            velocity.y = -2f;\n" +
                   "        }\n" +
                   "\n" +
                   "        // Move\n" +
                   "        Vector2 moveInput = Vector2.zero;\n" +
                   "        if (Keyboard.current.wKey.isPressed) moveInput.y = 1;\n" +
                   "        if (Keyboard.current.sKey.isPressed) moveInput.y = -1;\n" +
                   "        if (Keyboard.current.aKey.isPressed) moveInput.x = -1;\n" +
                   "        if (Keyboard.current.dKey.isPressed) moveInput.x = 1;\n" +
                   "\n" +
                   "        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;\n" +
                   "        controller.Move(move.normalized * speed * Time.deltaTime);\n" +
                   "\n" +
                   "        // Jump\n" +
                   "        if (isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)\n" +
                   "        {\n" +
                   "            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);\n" +
                   "        }\n" +
                   "\n" +
                   "        // Gravity\n" +
                   "        velocity.y += gravity * Time.deltaTime;\n" +
                   "        controller.Move(velocity * Time.deltaTime);\n" +
                   "\n" +
                   "        // Mouse look\n" +
                   "        if (Application.isPlaying)\n" +
                   "        {\n" +
                   "            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity * 0.1f;\n" +
                   "            transform.Rotate(0, mouseDelta.x, 0);\n" +
                   "            rotationY -= mouseDelta.y;\n" +
                   "            rotationY = Mathf.Clamp(rotationY, -90f, 90f);\n" +
                   "            if (playerCamera != null)\n" +
                   "                playerCamera.transform.localEulerAngles = new Vector3(rotationY, 0, 0);\n" +
                   "        }\n" +
                   "    }\n" +
                   "}";
        }

        // Helper: Assign the generated script to selected objects
        private void AssignScriptToSelectedObjects()
        {
            if (string.IsNullOrEmpty(lastGeneratedClassName))
            {
                EditorUtility.DisplayDialog("Assign Script", "No generated script to assign.", "OK");
                return;
            }
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Assign Script", "No objects selected.", "OK");
                return;
            }
            
            // Force compilation and wait
            AssetDatabase.Refresh();
            EditorUtility.DisplayProgressBar("Assigning Script", "Compiling scripts...", 0.5f);
            
            // Wait for compilation to complete
            int maxWaitTime = 150; // 15 seconds max
            int waitCount = 0;
            while (EditorApplication.isCompiling && waitCount < maxWaitTime)
            {
                System.Threading.Thread.Sleep(100);
                waitCount++;
                
                // Update progress bar
                float progress = (float)waitCount / maxWaitTime;
                EditorUtility.DisplayProgressBar("Assigning Script", $"Waiting for compilation... ({waitCount}/15s)", progress);
            }
            
            EditorUtility.ClearProgressBar();
            
            var scriptType = GetTypeByName(lastGeneratedClassName);
            if (scriptType == null)
            {
                // Try to find the script file and suggest manual assignment
                string scriptPath = lastGeneratedScriptPath;
                if (!string.IsNullOrEmpty(scriptPath) && System.IO.File.Exists(scriptPath))
                {
                    // Get the MonoScript asset
                    string relativePath = "Assets" + scriptPath.Substring(Application.dataPath.Length);
                    relativePath = relativePath.Replace('\\', '/');
                    MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePath);
                    
                    if (monoScript != null)
                    {
                        EditorUtility.DisplayDialog("Manual Assignment Required", 
                            $"Script file found but not yet compiled: {lastGeneratedClassName}\n\n" +
                            $"Please wait for Unity to finish compiling, then:\n" +
                            $"1. Select your object(s) in the scene\n" +
                            $"2. Drag the script from Project window to the Inspector\n" +
                            $"Script location: {relativePath}", "OK");
                        
                        // Ping the script in project window
                        EditorGUIUtility.PingObject(monoScript);
                        return;
                    }
                }
                
                EditorUtility.DisplayDialog("Script Not Found", 
                    $"Could not find compiled script type: {lastGeneratedClassName}\n\n" +
                    $"Possible solutions:\n" +
                    $"1. Wait for Unity to finish compiling scripts\n" +
                    $"2. Check Console for compilation errors\n" +
                    $"3. Try generating the script again\n" +
                    $"4. Manually assign the script from Project window", "OK");
                return;
            }
            
            int addedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (obj != null && obj.GetComponent(scriptType) == null)
                {
                    Undo.AddComponent(obj, scriptType);
                    addedCount++;
                }
            }
            EditorUtility.DisplayDialog("Assign Script", $"Script assigned to {addedCount} object(s).", "OK");
        }

        // Helper: Find a Type by class name in loaded assemblies
        private Type GetTypeByName(string className)
        {
            if (string.IsNullOrEmpty(className))
                return null;
                
            try
            {
                // Force refresh and wait a bit more for compilation
                AssetDatabase.Refresh();
                System.Threading.Thread.Sleep(200);
                
                // First try exact match in user assemblies (Assembly-CSharp, etc.)
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Focus on user code assemblies
                    if (assembly.FullName.Contains("Assembly-CSharp") || 
                        assembly.FullName.Contains("Assembly-CSharp-Editor"))
                    {
                        try
                        {
                            var type = assembly.GetType(className);
                            if (type != null && type.IsSubclassOf(typeof(MonoBehaviour)))
                            {
                                return type;
                            }
                        }
                        catch { continue; }
                    }
                }
                
                // Second try: search by name only in user assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.Contains("Assembly-CSharp"))
                    {
                        try
                        {
                            var types = assembly.GetTypes();
                            var type = types.FirstOrDefault(t => t.Name == className && t.IsSubclassOf(typeof(MonoBehaviour)));
                            if (type != null)
                            {
                                return type;
                            }
                        }
                        catch { continue; }
                    }
                }
                
                // Third try: search all non-system assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Skip system assemblies for performance
                    if (assembly.FullName.StartsWith("System") || 
                        assembly.FullName.StartsWith("Microsoft") ||
                        (assembly.FullName.StartsWith("Unity.") && !assembly.FullName.Contains("Assembly")))
                        continue;
                        
                    try
                    {
                        var types = assembly.GetTypes();
                        var type = types.FirstOrDefault(t => t.Name == className && t.IsSubclassOf(typeof(MonoBehaviour)));
                        if (type != null)
                        {
                            return type;
                        }
                    }
                    catch { continue; }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error searching for script type: {ex.Message}");
            }
            
            return null;
        }

        // Helper: Extract the first C# code block from the LLM result
        private string ExtractCodeBlock(string result)
        {
            int start = result.IndexOf("```csharp");
            if (start == -1) start = result.IndexOf("```cs");
            if (start == -1) start = result.IndexOf("```");
            if (start != -1)
            {
                int codeStart = result.IndexOf('\n', start);
                int end = result.IndexOf("```", codeStart + 1);
                if (codeStart != -1 && end != -1)
                {
                    return result.Substring(codeStart + 1, end - codeStart - 1).Trim();
                }
            }
            var match = System.Text.RegularExpressions.Regex.Match(
                result,
                @"(public|private|internal)?\s*class\s+[A-Za-z_][A-Za-z0-9_\s:<>]*\{[\s\S]*?\n\}",
                System.Text.RegularExpressions.RegexOptions.Multiline
            );
            if (match.Success)
            {
                return match.Value.Trim();
            }
            return null;
        }

        // Helper: Extract suggested object names from the LLM result
        private string ExtractSuggestedObjectNames(string result)
        {
            using (System.IO.StringReader reader = new System.IO.StringReader(result))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("Object(s):", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Object:", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = line.IndexOf(":");
                        if (idx != -1 && idx + 1 < line.Length)
                        {
                            return line.Substring(idx + 1).Trim();
                        }
                    }
                }
            }
            return string.Empty;
        }

        // Helper: Find GameObjects in the scene by comma-separated names
        private List<GameObject> FindObjectsInSceneByNames(string names)
        {
            List<GameObject> found = new List<GameObject>();
            if (string.IsNullOrEmpty(names)) return found;
            string[] split = names.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawName in split)
            {
                string name = rawName.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                GameObject obj = GameObject.Find(name);
                if (obj != null && !found.Contains(obj))
                    found.Add(obj);
            }
            return found;
        }

        // Helper: Save the generated script code and return file info
        private (string filePath, string className) SaveGeneratedScriptWithInfo(string scriptCode, string className = null)
        {
            string directory = "Assets/OOJU/Interaction/Generated";
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);
            if (string.IsNullOrEmpty(className))
            {
                className = GenerateClassNameFromSentence(userInteractionInput);
            }
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string finalClassName = $"{className}_{timestamp}";
            scriptCode = ReplaceClassNameInScript(scriptCode, finalClassName);
            scriptCode = RemoveDuplicateClassAndMethods(scriptCode, finalClassName);
            string filePath = System.IO.Path.Combine(directory, $"{finalClassName}.cs");
            System.IO.File.WriteAllText(filePath, scriptCode);
            AssetDatabase.Refresh();
            return (filePath, finalClassName);
        }

        // Helper: Save the generated script code to a new C# file in the project, returns the file path
        private string SaveGeneratedScript(string scriptCode, string className = null)
        {
            var result = SaveGeneratedScriptWithInfo(scriptCode, className);
            return result.filePath;
        }

        // Helper: Replace the first class name in the script code with the given class name
        private string ReplaceClassNameInScript(string scriptCode, string newClassName)
        {
            if (string.IsNullOrEmpty(scriptCode) || string.IsNullOrEmpty(newClassName)) return scriptCode;
            var regex = new System.Text.RegularExpressions.Regex(@"class\s+([A-Za-z_][A-Za-z0-9_]*)");
            return regex.Replace(scriptCode, $"class {newClassName}", 1);
        }

        // Helper: Remove duplicate class definitions and duplicate Unity methods (Update, Start, etc.)
        private string RemoveDuplicateClassAndMethods(string scriptCode, string className)
        {
            var lines = scriptCode.Split(new[] { '\n' });
            var newLines = new List<string>();
            var methodSet = new HashSet<string>();
            bool insideClass = false;
            bool insideMethod = false;
            string currentMethod = null;
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith($"class {className}"))
                {
                    if (insideClass)
                        continue;
                    insideClass = true;
                }
                if (insideClass && (trimmed.StartsWith("void ") || trimmed.StartsWith("public void ") || trimmed.StartsWith("private void ")))
                {
                    int paren = trimmed.IndexOf('(');
                    if (paren > 0)
                    {
                        string methodSig = trimmed.Substring(0, paren).Trim();
                        if (methodSet.Contains(methodSig))
                        {
                            insideMethod = true;
                            currentMethod = methodSig;
                            continue;
                        }
                        else
                        {
                            methodSet.Add(methodSig);
                        }
                    }
                }
                if (insideMethod)
                {
                    if (trimmed == "}")
                    {
                        insideMethod = false;
                        currentMethod = null;
                    }
                    continue;
                }
                newLines.Add(line);
            }
            return string.Join("\n", newLines);
        }

        // Helper: Generate a valid C# class/file name from the interaction sentence
        private string GenerateClassNameFromSentence(string sentence)
        {
            if (string.IsNullOrEmpty(sentence)) return "GeneratedInteractionScript";
            string name = new string(sentence.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
            name = name.Trim().Replace(' ', '_');
            if (name.Length > 32) name = name.Substring(0, 32);
            if (string.IsNullOrEmpty(name)) name = "GeneratedInteractionScript";
            if (!char.IsLetter(name[0])) name = "Script_" + name;
            return name;
        }

        // Helper: Extract the class name from the generated code using regex
        private string ExtractClassNameFromCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(code, @"class\s+([A-Za-z_][A-Za-z0-9_]*)");
            if (match.Success)
                return match.Groups[1].Value.Trim().TrimEnd('.');
            return null;
        }

        // Test Claude API key function
        private async void TestClaudeAPIKey()
        {
            var settings = OISettings.Instance;
            if (string.IsNullOrEmpty(settings.ClaudeApiKey))
            {
                EditorUtility.DisplayDialog("Test Error", "Claude API key is not set.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Testing Claude API", "Testing API key...", 0.5f);
                string result = await OIDescriptor.TestClaudeAPIKey(settings.ClaudeApiKey);
                EditorUtility.ClearProgressBar();
                
                if (result.Contains("Error:") || result.Contains("error"))
                {
                    EditorUtility.DisplayDialog("Claude API Test Failed", 
                        $"API key test failed:\n\n{result}\n\nPlease check:\n" +
                        "1. API key is correct\n" + 
                        "2. You have sufficient credits\n" +
                        "3. Your account is active", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Claude API Test Success", 
                        $"API key is working correctly!\n\nResponse: {result}", "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Test Error", $"Test failed with exception: {ex.Message}", "OK");
            }
        }

        // Helper: Check LLM API key and show error if not set
        private bool CheckLLMApiKeyAndShowError()
        {
            var settings = OISettings.Instance;
            string errorMsg = null;
            switch (settings.SelectedLLMType)
            {
                case "OpenAI":
                    if (string.IsNullOrEmpty(settings.ApiKey))
                        errorMsg = "OpenAI API Key is not set. Please set it in the Settings tab.";
                    break;
                case "Claude":
                    if (string.IsNullOrEmpty(settings.ClaudeApiKey))
                        errorMsg = "Claude API Key is not set. Please set it in the Settings tab.";
                    break;
                case "Gemini":
                    if (string.IsNullOrEmpty(settings.GeminiApiKey))
                        errorMsg = "Gemini API Key is not set. Please set it in the Settings tab.";
                    break;
            }
            if (errorMsg != null)
            {
                EditorUtility.DisplayDialog("Error", errorMsg, "OK");
                return false;
            }
            return true;
        }

        // Async: Analyze scene and suggest interactions
        private async void AnalyzeSceneAndSuggestInteractions()
        {
            if (!CheckLLMApiKeyAndShowError()) return;
            try
            {
                isGeneratingDescription = true;
                EditorUtility.DisplayProgressBar("Analyzing Scene & Generating Suggestions", "Please wait while the scene is being analyzed and suggestions are generated...", 0.5f);
                sceneDescription = await OIDescriptor.GenerateSceneDescription();
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects.Length == 0)
                {
                    EditorUtility.ClearProgressBar();
                    isGeneratingDescription = false;
                    EditorUtility.DisplayDialog("Error", "Please select at least one object.", "OK");
                    interactionSuggestions = null;
                    return;
                }
                string extraPrompt = "Prioritize interactions that can be implemented with Unity scripts only, and avoid suggestions that require the user to prepare extra resources such as sound or animation files.";
                var suggestions = new Dictionary<string, string[]>();
                foreach (var obj in selectedObjects)
                {
                    string objName = obj.name;
                    string prompt = $"Scene Description:\n{sceneDescription}\n\nObject Name: {objName}\n\nSuggest 3 realistic, Unity-implementable interactions for this object, considering the scene context. Only suggest interactions that make sense for this object in Unity. {extraPrompt} If the object is not interactive, respond ONLY with the word: NONE.";
                    string result = await OIDescriptor.RequestLLMInteraction(prompt);
                    string[] arr = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < arr.Length; i++)
                    {
                        arr[i] = System.Text.RegularExpressions.Regex.Replace(arr[i], @"^\s*(\d+\.|\*|-)\s*", "").Trim();
                    }
                    suggestions[objName] = arr.Length > 0 ? arr : new[] { "NONE" };
                }
                interactionSuggestions = suggestions;
                EditorUtility.DisplayDialog("Analyze & Suggest", "Scene analyzed and interaction suggestions generated successfully.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in AnalyzeSceneAndSuggestInteractions: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Error in AnalyzeSceneAndSuggestInteractions: {ex.Message}", "OK");
                sceneDescription = $"Error: {ex.Message}";
                interactionSuggestions = new Dictionary<string, string[]> { { "Error", new string[] { ex.Message } } };
            }
            finally
            {
                isGeneratingDescription = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        // Async: Generate Unity script from sentence description
        private async void GenerateSentenceToInteraction()
        {
            if (!CheckLLMApiKeyAndShowError()) return;
            try
            {
                if (string.IsNullOrEmpty(userInteractionInput))
                {
                    EditorUtility.DisplayDialog("Error", "Please enter an interaction description.", "OK");
                    return;
                }
                
                isGeneratingDescription = true;
                
                // Auto-generate scene description if not available
                if (string.IsNullOrEmpty(sceneDescription))
                {
                    EditorUtility.DisplayProgressBar("Generating Scene Description", "Analyzing scene first...", 0.3f);
                    sceneDescription = await OIDescriptor.GenerateSceneDescription();
                    
                    if (string.IsNullOrEmpty(sceneDescription) || sceneDescription.Contains("Error"))
                    {
                        EditorUtility.ClearProgressBar();
                        isGeneratingDescription = false;
                        EditorUtility.DisplayDialog("Error", "Failed to generate scene description. Please check your API settings and try again.", "OK");
                        return;
                    }
                }
                
                EditorUtility.DisplayProgressBar("Generating Interaction", "Please wait while the interaction is being generated...", 0.7f);
                string prompt = $"Scene Description:\n{sceneDescription}\n\nUser Request (Sentence):\n{userInteractionInput}\n\n1. Generate a Unity C# script for this interaction.\n2. The script must define only one class, and the class name must be unique (for example, append a timestamp or a random string).\n3. The generated class must inherit from UnityEngine.MonoBehaviour.\n4. Do not define the same class or method more than once.\n5. If you need to implement Update, Start, or other Unity methods, each should appear only once in the class.\n6. All comments in the script must be written in English.\n7. Output only the code block.\n8. Prioritize interactions that can be implemented with Unity scripts only, and avoid suggestions that require the user to prepare extra resources such as sound or animation files.";
                sentenceToInteractionResult = await OIDescriptor.RequestLLMInteraction(prompt);
                string code = ExtractCodeBlock(sentenceToInteractionResult);
                if (!string.IsNullOrEmpty(code) && !code.Contains(": MonoBehaviour"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(code, @"class\s+([A-Za-z_][A-Za-z0-9_]*)");
                    if (match.Success)
                    {
                        string original = match.Value;
                        string replacement = original + " : MonoBehaviour";
                        code = code.Replace(original, replacement);
                    }
                }
                if (!string.IsNullOrEmpty(code))
                {
                    // Save script and get the actual class name with timestamp
                    var scriptInfo = SaveGeneratedScriptWithInfo(code);
                    lastGeneratedScriptPath = scriptInfo.filePath;
                    lastGeneratedClassName = scriptInfo.className;
                    UnityEditor.AssetDatabase.Refresh();
                }
                else
                {
                    lastGeneratedScriptPath = "No code block found.";
                    lastGeneratedClassName = "";
                }
                lastSuggestedObjectNames = ExtractSuggestedObjectNames(sentenceToInteractionResult);
                foundSuggestedObjects = FindObjectsInSceneByNames(lastSuggestedObjectNames);
                
                // Show success message with script info
                string successMessage = "Interaction generated successfully!\n\n";
                if (!string.IsNullOrEmpty(lastGeneratedClassName))
                {
                    successMessage += $"Generated script: {lastGeneratedClassName}\n";
                }
                if (!string.IsNullOrEmpty(lastGeneratedScriptPath))
                {
                    successMessage += $"Location: Assets/OOJU/Interaction/Generated\n\n";
                }
                successMessage += "Next steps:\n";
                successMessage += "1. Wait for Unity to compile the script\n";
                successMessage += "2. Select object(s) in the scene\n";
                successMessage += "3. Click 'Assign Script to Selected Object(s)'";
                
                EditorUtility.DisplayDialog("Script Generated", successMessage, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating Sentence-to-Interaction: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Error generating interaction: {ex.Message}", "OK");
                sentenceToInteractionResult = $"Error: {ex.Message}";
                lastGeneratedScriptPath = "";
                lastGeneratedClassName = "";
                lastSuggestedObjectNames = "";
                foundSuggestedObjects.Clear();
            }
            finally
            {
                isGeneratingDescription = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        // Async: Regenerate interaction suggestions only for selected objects
        private async void RegenerateInteractionSuggestionsOnly()
        {
            if (!CheckLLMApiKeyAndShowError()) return;
            try
            {
                isGeneratingDescription = true;
                EditorUtility.DisplayProgressBar("Generating Suggestions", "Please wait while suggestions are being generated...", 0.5f);
                if (string.IsNullOrEmpty(sceneDescription))
                {
                    EditorUtility.ClearProgressBar();
                    isGeneratingDescription = false;
                    EditorUtility.DisplayDialog("Error", "Scene description is not available.", "OK");
                    return;
                }
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects.Length == 0)
                {
                    EditorUtility.ClearProgressBar();
                    isGeneratingDescription = false;
                    EditorUtility.DisplayDialog("Error", "Please select at least one object.", "OK");
                    interactionSuggestions = null;
                    return;
                }
                string extraPrompt = "Prioritize interactions that can be implemented with Unity scripts only, and avoid suggestions that require the user to prepare extra resources such as sound or animation files.";
                Dictionary<string, string> customObjectDescriptions = new Dictionary<string, string>();
                foreach (var obj in selectedObjects)
                {
                    string objName = obj.name;
                    if (userObjectInput.ContainsKey(objName) && !string.IsNullOrWhiteSpace(userObjectInput[objName]))
                    {
                        customObjectDescriptions[objName] = userObjectInput[objName];
                    }
                }
                Dictionary<string, string[]> suggestions;
                if (customObjectDescriptions.Count > 0)
                {
                    suggestions = new Dictionary<string, string[]>();
                    foreach (var obj in selectedObjects)
                    {
                        string objName = obj.name;
                        string prompt;
                        if (customObjectDescriptions.ContainsKey(objName))
                        {
                            prompt = $"Scene Description:\n{sceneDescription}\n\nObject Name: {objName}\nUser Description: {customObjectDescriptions[objName]}\n\nSuggest 3 realistic, Unity-implementable interactions for this object, considering the user description and scene context. Only suggest interactions that make sense for this object in Unity. {extraPrompt} If the object is not interactive, respond ONLY with the word: NONE.";
                        }
                        else
                        {
                            prompt = $"Scene Description:\n{sceneDescription}\n\nObject Name: {objName}\n\nSuggest 3 realistic, Unity-implementable interactions for this object, considering the scene context. Only suggest interactions that make sense for this object in Unity. {extraPrompt} If the object is not interactive, respond ONLY with the word: NONE.";
                        }
                        string result = await OIDescriptor.RequestLLMInteraction(prompt);
                        string[] arr = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < arr.Length; i++)
                        {
                            arr[i] = System.Text.RegularExpressions.Regex.Replace(arr[i], @"^\s*(\d+\.|\*|-)\s*", "").Trim();
                        }
                        suggestions[objName] = arr.Length > 0 ? arr : new[] { "NONE" };
                    }
                }
                else
                {
                    suggestions = new Dictionary<string, string[]>();
                    foreach (var obj in selectedObjects)
                    {
                        string objName = obj.name;
                        string prompt = $"Scene Description:\n{sceneDescription}\n\nObject Name: {objName}\n\nSuggest 3 realistic, Unity-implementable interactions for this object, considering the scene context. Only suggest interactions that make sense for this object in Unity. {extraPrompt} If the object is not interactive, respond ONLY with the word: NONE.";
                        string result = await OIDescriptor.RequestLLMInteraction(prompt);
                        string[] arr = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < arr.Length; i++)
                        {
                            arr[i] = System.Text.RegularExpressions.Regex.Replace(arr[i], @"^\s*(\d+\.|\*|-)\s*", "").Trim();
                        }
                        suggestions[objName] = arr.Length > 0 ? arr : new[] { "NONE" };
                    }
                }
                interactionSuggestions = suggestions;
                EditorUtility.DisplayDialog("Interaction Suggestions", "Suggestions generated successfully.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in RegenerateInteractionSuggestionsOnly: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Error in RegenerateInteractionSuggestionsOnly: {ex.Message}", "OK");
                interactionSuggestions = new Dictionary<string, string[]> { { "Error", new string[] { ex.Message } } };
            }
            finally
            {
                isGeneratingDescription = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void OnEditorUpdate()
        {
            // OnEditorUpdate method kept for potential future use
        }

        private void CheckAssets()
        {
            if (string.IsNullOrEmpty(authToken))
            {
                Debug.LogWarning("Cannot check assets: No auth token available");
                return;
            }

            isCheckingAssets = true;
            EditorCoroutineUtility.StartCoroutineOwnerless(CheckAssetsCoroutine());
        }

        private IEnumerator CheckAssetsCoroutine()
        {
            yield return NetworkUtility.GetExportableAssets(
                authToken,
                (assets) =>
                {
                    availableAssets = assets;
                    assetCount = assets.Count;
                    assetsAvailable = assetCount > 0;
                    FilterAssets();
                    isCheckingAssets = false;
                    downloadStatus = $"Found {assetCount} assets.";
                    
                    // Start loading preview images
                    StartLoadingPreviews();
                    
                    Repaint();
                },
                (error) =>
                {
                    Debug.LogError($"Error checking assets: {error}");
                    downloadStatus = $"Error checking assets: {error}";
                    isCheckingAssets = false;
                    assetsAvailable = false;
                    assetCount = 0;
                    Repaint();
                }
            );
        }

        private void StartLoadingPreviews()
        {
            if (availableAssets == null || availableAssets.Count == 0)
                return;

            // Load previews for downloaded assets only
            foreach (var asset in availableAssets)
            {
                if (asset?.id != null && !assetPreviews.ContainsKey(asset.id) && !assetPreviews.ContainsKey(asset.id + "_real"))
                {
                    EditorCoroutineUtility.StartCoroutineOwnerless(LoadAssetPreview(asset));
                }
            }
        }



        private IEnumerator LoadAssetPreview(NetworkUtility.ExportableAsset asset)
        {
            if (asset?.id == null) yield break;

            // Only try local loading for downloaded assets
            if (IsImageAsset(asset))
            {
                TryLoadLocalImagePreview(asset);
            }
            else if (IsModelAsset(asset))
            {
                TryLoadLocalModelPreview(asset);
            }
            else
            {
                // Mark as attempted for other file types
                assetPreviews[asset.id] = null;
            }
            
            yield break;
        }

        private void TryLoadLocalImagePreview(NetworkUtility.ExportableAsset asset)
        {
            if (asset?.filename == null || asset?.id == null) return;

            // Try multiple possible paths
            string[] possiblePaths = {
                System.IO.Path.Combine(Application.dataPath, "OOJU", "Asset", "My Assets", asset.filename),
                System.IO.Path.Combine(Application.dataPath, "OOJU", "Asset", "ZIP", asset.filename),
                System.IO.Path.Combine(Application.dataPath, "OOJU_Assets", asset.filename)
            };

            foreach (string localPath in possiblePaths)
            {
                if (System.IO.File.Exists(localPath))
                {
                    try
                    {
                        // Use Unity's AssetDatabase for better compatibility
                        string relativePath = "Assets" + localPath.Substring(Application.dataPath.Length);
                        relativePath = relativePath.Replace('\\', '/');
                        
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                        if (texture != null)
                        {
                            assetPreviews[asset.id + "_real"] = texture;
                            Repaint();
                            return;
                        }
                        else
                        {
                            // Fallback to direct file loading
                            byte[] fileData = System.IO.File.ReadAllBytes(localPath);
                            Texture2D directTexture = new Texture2D(2, 2);
                            if (directTexture.LoadImage(fileData))
                            {
                                assetPreviews[asset.id + "_real"] = directTexture;
                                Repaint();
                                return;
                            }
                            else
                            {
                                DestroyImmediate(directTexture);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Silently handle image loading errors
                    }
                }
            }
            
            // Mark as attempted
            assetPreviews[asset.id] = null;
        }



        private void TryLoadLocalModelPreview(NetworkUtility.ExportableAsset asset)
        {
            if (asset?.filename == null || asset?.id == null) return;

            // Try multiple possible paths for model files
            string[] possiblePaths = {
                System.IO.Path.Combine(Application.dataPath, "OOJU", "Asset", "My Assets", asset.filename),
                System.IO.Path.Combine(Application.dataPath, "OOJU", "Asset", "ZIP", asset.filename),
                System.IO.Path.Combine(Application.dataPath, "OOJU_Assets", asset.filename)
            };

            foreach (string localPath in possiblePaths)
            {
                if (System.IO.File.Exists(localPath))
                {
                    try
                    {
                        // Use Unity's AssetDatabase to get preview
                        string relativePath = "Assets" + localPath.Substring(Application.dataPath.Length);
                        relativePath = relativePath.Replace('\\', '/');
                        
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                        if (prefab != null)
                        {
                            // Generate preview using Unity's AssetPreview
                            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
                            if (preview != null)
                            {
                                assetPreviews[asset.id + "_real"] = preview;
                                Repaint();
                                return;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Silently handle model preview generation errors
                    }
                }
            }
            
            // Mark as attempted
            assetPreviews[asset.id] = null;
        }

        private void LoadDefaultPreviewTexture()
        {
            // Try to load ooju_logo.png from Resources folder
            defaultPreviewTexture = Resources.Load<Texture2D>("ooju_logo");
            
            if (defaultPreviewTexture == null)
            {
                // Try multiple fallback icons
                string[] fallbackIcons = {
                    "d_DefaultAsset Icon",
                    "DefaultAsset Icon",
                    "d_Folder Icon",
                    "Folder Icon"
                };
                
                foreach (string iconName in fallbackIcons)
                {
                    defaultPreviewTexture = EditorGUIUtility.FindTexture(iconName);
                    if (defaultPreviewTexture != null)
                        break;
                }
                
                // If still null, create a simple colored texture
                if (defaultPreviewTexture == null)
                {
                    defaultPreviewTexture = CreateSimpleTexture();
                }
            }
        }

        private Texture2D CreateSimpleTexture()
        {
            Texture2D texture = new Texture2D(64, 64);
            Color[] colors = new Color[64 * 64];
            Color fillColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Gray color
            
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = fillColor;
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }

        private void ClearPreviewCache()
        {
            if (assetPreviews != null)
            {
                // Destroy textures to free memory
                foreach (var kvp in assetPreviews)
                {
                    if (kvp.Value != null && kvp.Key.EndsWith("_real"))
                    {
                        // Only destroy textures we created, not Unity's built-in assets
                        if (!AssetDatabase.Contains(kvp.Value))
                        {
                            DestroyImmediate(kvp.Value);
                        }
                    }
                }
                assetPreviews.Clear();
            }
        }

        // All helper methods (DrawImportTab, DrawAssetsTab, DrawLoginUI, HandleDragAndDrop, etc.) from UserAssetManager should be copied here and adapted as needed.

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
                        Repaint();
                    }
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
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
                yield return NetworkUtility.UploadFile(
                    filePath,
                    authToken,
                    (result) =>
                    {
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
        // DrawImportTab and DrawAssetsTab methods removed - functionality merged into DrawUnifiedAssetsContent
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
                    userEmail = username; // Keep the email for display
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
            catch (Exception)
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
            catch (Exception e)
            {
                Debug.LogError($"Error in DrawAssetsGrid: {e.Message}\n{e.StackTrace}");
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
            
            bool isDownloaded = IsAssetDownloaded(asset);
            
            if (isDownloaded)
            {
                // Draw dark green box over checkbox for downloaded assets (matching download button color)
                EditorGUI.DrawRect(checkboxRect, new Color(0.4f, 0.8f, 0.4f));
                // Optionally draw a checkmark or "✓" symbol
                GUI.Label(checkboxRect, "✓", new GUIStyle(EditorStyles.boldLabel) 
                { 
                    alignment = TextAnchor.MiddleCenter, 
                    normal = { textColor = Color.white },
                    fontSize = 14
                });
            }
            else
            {
                // Only show checkbox for non-downloaded assets
            bool isSelected = asset.id != null && selectedAssetIds.Contains(asset.id);
            bool newSelection = GUI.Toggle(checkboxRect, isSelected, "");
            if (newSelection != isSelected && asset.id != null)
            {
                if (newSelection)
                    selectedAssetIds.Add(asset.id);
                else
                    selectedAssetIds.Remove(asset.id);
            }
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
                
                // Additional null check to prevent GUI errors
                if (preview != null && preview.width > 0 && preview.height > 0)
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
                    // Check if we're currently loading a preview for this asset
                    bool hasAttempted = assetPreviews.ContainsKey(asset.id) || assetPreviews.ContainsKey(asset.id + "_real");
                    bool isLoadingPreview = !hasAttempted;
                    
                    if (isLoadingPreview && IsImageAsset(asset))
                    {
                        // Show loading indicator for image assets
                        GUI.Label(previewRect, "Loading...", EditorStyles.centeredGreyMiniLabel);
                    }
                    else
                    {
                        // Show default preview (ooju_logo) or fallback icon
                        Texture2D displayTexture = defaultPreviewTexture;
                        
                        // If default preview is not available, use type-specific icon
                        if (displayTexture == null)
                        {
                            displayTexture = GetDefaultIconForAsset(asset);
                        }
                        
                        // Final safety check - ensure we never pass null to GUI.DrawTexture
                        if (displayTexture != null && displayTexture.width > 0 && displayTexture.height > 0)
                        {
                            float iconSize = previewSize * 0.8f; // Slightly larger for logo
                        Rect iconRect = new Rect(
                            previewRect.x + (previewRect.width - iconSize) / 2,
                            previewRect.y + (previewRect.height - iconSize) / 2,
                            iconSize,
                            iconSize
                        );
                            GUI.DrawTexture(iconRect, displayTexture, ScaleMode.ScaleToFit);
                        }
                        else
                        {
                            // Last resort: draw a simple placeholder rect
                            Rect placeholderRect = new Rect(
                                previewRect.x + previewSize * 0.25f,
                                previewRect.y + previewSize * 0.25f,
                                previewSize * 0.5f,
                                previewSize * 0.5f
                            );
                            EditorGUI.DrawRect(placeholderRect, new Color(0.4f, 0.4f, 0.4f, 0.8f));
                        }
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
            string localPath = System.IO.Path.Combine(Application.dataPath, "OOJU", "Asset", "My Assets", asset.filename);
            return System.IO.File.Exists(localPath);
        }
        private Texture2D GetDefaultIconForAsset(NetworkUtility.ExportableAsset asset)
        {
            Texture2D icon = null;
            
            if (IsModelAsset(asset))
            {
                icon = EditorGUIUtility.FindTexture("d_Mesh Icon");
            }
            else if (IsImageAsset(asset))
            {
                icon = EditorGUIUtility.FindTexture("d_Image Icon");
            }
            else
            {
                icon = EditorGUIUtility.FindTexture("d_DefaultAsset Icon");
            }
            
            // If all else fails, try to return the default preview texture
            if (icon == null)
            {
                icon = defaultPreviewTexture;
            }
            
            return icon;
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

        private IEnumerator UploadZipFileCoroutine(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                uploadStatus = "ZIP file not found.";
                EditorUtility.DisplayDialog("Error", "ZIP file not found.", "OK");
                yield break;
            }

            uploadStatus = "Processing ZIP file...";
            yield return new WaitForEndOfFrame();

            Task<GameObject> importTask = null;
            GameObject resultObject = null;

            try
            {
                importTask = OOJUSceneImportUtility.ImportSceneZipAsync(zipPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error starting import: {ex.Message}");
                uploadStatus = $"Error starting import: {ex.Message}";
                EditorUtility.DisplayDialog("Import Error", $"Error starting import: {ex.Message}", "OK");
                yield break;
            }

            while (!importTask.IsCompleted)
            {
                uploadStatus = "Importing scene...";
                yield return new WaitForEndOfFrame();
            }

            try
            {
                if (importTask.IsFaulted && importTask.Exception != null)
                {
                    Debug.LogError($"Error importing scene: {importTask.Exception.GetBaseException().Message}");
                    uploadStatus = "Error importing scene from ZIP file.";
                    EditorUtility.DisplayDialog("Import Error", "Error importing scene from ZIP file.", "OK");
                    yield break;
                }

                resultObject = importTask.Result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting import result: {ex.Message}");
                uploadStatus = "Error getting import result.";
                EditorUtility.DisplayDialog("Import Error", "Error getting import result.", "OK");
                yield break;
            }

            if (resultObject != null)
            {
                uploadStatus = "Scene imported successfully.";
                Selection.activeGameObject = resultObject;
                SceneView.FrameLastActiveSceneView();
                EditorUtility.DisplayDialog("Import Successful", "Scene has been imported successfully.", "OK");
            }
            else
            {
                uploadStatus = "No valid scene data found in ZIP file.";
                EditorUtility.DisplayDialog("Import Failed", "No valid scene data found in ZIP file.", "OK");
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            // Create target directory if it doesn't exist
            Directory.CreateDirectory(destinationDir);

            // Copy all files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy all subdirectories recursively
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        private void LoadSceneInEditor(string scenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
            }
        }

        private void LoadModelInScene(string modelPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    Undo.RegisterCreatedObjectUndo(instance, "Import 3D Model");
                    Selection.activeObject = instance;
                    SceneView.FrameLastActiveSceneView();
                }
            }
        }

        private void DownloadSelectedAssets()
        {
            if (selectedAssetIds == null || selectedAssetIds.Count == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select at least one asset to download.", "OK");
                return;
            }
            
            isDownloading = true;
            downloadStatus = $"Starting download of {selectedAssetIds.Count} asset(s)...";
            EditorCoroutineUtility.StartCoroutineOwnerless(DownloadSelectedAssetsCoroutine());
        }
        
        private IEnumerator DownloadSelectedAssetsCoroutine()
        {
            string downloadDir = System.IO.Path.Combine(Application.dataPath, "OOJU", "Asset", "My Assets");
            System.IO.Directory.CreateDirectory(downloadDir);
            
            int totalAssets = selectedAssetIds.Count;
            int downloadedCount = 0;
            int failedCount = 0;
            
            foreach (string assetId in selectedAssetIds)
            {
                // Find the asset by ID
                var asset = availableAssets.FirstOrDefault(a => a.id == assetId);
                if (asset == null)
                {
                    failedCount++;
                    continue;
                }
                
                string fileName = asset.filename ?? $"asset_{assetId}";
                string localPath = System.IO.Path.Combine(downloadDir, fileName);
                
                downloadStatus = $"Downloading {downloadedCount + 1}/{totalAssets}: {fileName}...";
                Repaint();
                
                bool downloadSuccess = false;
                yield return NetworkUtility.DownloadAsset(
                    assetId,
                    authToken,
                    localPath,
                    (success) => { downloadSuccess = success; },
                    (progress) => { /* Progress callback - could be used for progress bar */ }
                );
                
                if (downloadSuccess)
                {
                    downloadedCount++;
                }
                else
                {
                    failedCount++;
                }
                
                yield return new WaitForSeconds(0.1f); // Small delay between downloads
            }
            
            // Refresh Unity's asset database
            AssetDatabase.Refresh();
            
            downloadStatus = $"Download complete: {downloadedCount} succeeded, {failedCount} failed.";
            isDownloading = false;
            
            // Clear selection after successful download
            if (downloadedCount > 0)
            {
                selectedAssetIds.Clear();
                
                // Refresh preview loading for newly downloaded assets
                StartLoadingPreviews();
            }
            
            // Show completion dialog
            string message = $"Download completed!\n\nSuccessful: {downloadedCount}\nFailed: {failedCount}\n\nAssets saved to: Assets/OOJU/Asset/My Assets/";
            EditorUtility.DisplayDialog("Download Complete", message, "OK");
            
            Repaint();
        }

        private void CreateHandControl()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", 
                    "Please select at least one object in the scene to add hand control to.", "OK");
                return;
            }

            // Ensure XR Gesture Manager exists in scene
            EnsureXRGestureManager();

            // Create gesture interactions for selected objects
            int successCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (obj != null)
                {
                    if (SetupGestureInteraction(obj))
                    {
                        successCount++;
                    }
                }
            }

            if (successCount > 0)
            {
                EditorUtility.DisplayDialog("Hand Control Created", 
                    $"Successfully added {GetGestureName(selectedGesture)} gesture control to {successCount} object(s).\n\n" +
                    $"Gesture: {GetGestureName(selectedGesture)}\n" +
                    $"Effect: {GetEffectName(selectedGesture)}\n" +
                    $"Description: {gestureReaction}\n\n" +
                    "The objects will now respond to hand gestures in XR!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Setup Failed", 
                    "Could not set up hand control for the selected objects. Check the Console for errors.", "OK");
            }
        }

        private void EnsureXRGestureManager()
        {
            // Check if XRGestureInteractionManager already exists
            var existingManager = FindFirstObjectByType<OOJUPlugin.XRGestureInteractionManager>();
            if (existingManager != null)
            {
                return;
            }

            // Create XR Gesture Manager
            GameObject managerGO = new GameObject("XRGestureInteractionManager");
            managerGO.AddComponent<OOJUPlugin.XRGestureInteractionManager>();
        }

        private bool SetupGestureInteraction(GameObject target)
        {
            try
            {
                // Comprehensive null and validity checks
                if (target == null)
                {
                    Debug.LogError("SetupGestureInteraction: target GameObject is null");
                    return false;
                }

                if (!target)
                {
                    Debug.LogError("SetupGestureInteraction: target GameObject has been destroyed");
                    return false;
                }

                // Check if object is part of a prefab asset (not editable at runtime)
                if (PrefabUtility.IsPartOfPrefabAsset(target))
                {
                    Debug.LogError($"Cannot modify prefab asset directly: {target.name}. Please use a prefab instance in the scene instead.");
                    return false;
                }

                // Ensure the object has a Collider component (required by XRGestureResponder)
                var existingCollider = target.GetComponent<Collider>();
                if (existingCollider == null)
                {
                    // Try to add an appropriate collider based on the object
                    var meshRenderer = target.GetComponent<MeshRenderer>();
                    var meshFilter = target.GetComponent<MeshFilter>();
                    
                    if (meshRenderer != null && meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        // Add MeshCollider for objects with mesh
                        var meshCollider = target.AddComponent<MeshCollider>();
                        meshCollider.convex = true; // Required for trigger detection
                    }
                    else
                    {
                        // Add BoxCollider as fallback
                        var boxCollider = target.AddComponent<BoxCollider>();
                    }
                }

                // Add XRGestureResponder component if not present
                var responder = target.GetComponent<OOJUPlugin.XRGestureResponder>();
                
                if (responder == null)
                {
                    try
                    {
                        responder = target.AddComponent<OOJUPlugin.XRGestureResponder>();
                    }
                    catch (System.Exception addEx)
                    {
                        Debug.LogError($"Failed to add XRGestureResponder component to {target.name}: {addEx.Message}");
                        return false;
                    }
                }

                if (responder == null)
                {
                    Debug.LogError($"Responder is still null after adding component to {target.name}");
                    return false;
                }

                // Map selected gesture to interaction type  
                var gestureType = (OOJUPlugin.GestureType)((int)selectedGesture);
                var effectType = GetInteractionTypeForGesture(selectedGesture);

                // Add the gesture interaction
                try
                {
                    responder.AddGestureInteraction(gestureType, effectType);
                }
                catch (System.Exception interactionEx)
                {
                    Debug.LogError($"Failed in AddGestureInteraction for {target.name}: {interactionEx.Message}");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to setup gesture interaction for {(target != null ? target.name : "null target")}: {ex.Message}");
                return false;
            }
        }

        private OOJUPlugin.InteractionType GetInteractionTypeForGesture(HandGesture gesture)
        {
            switch (gesture)
            {
                case HandGesture.Pinch:
                    return OOJUPlugin.InteractionType.FollowHand;
                case HandGesture.Tap:
                    return OOJUPlugin.InteractionType.InfiniteRotation;
                case HandGesture.PointToSelect:
                    return OOJUPlugin.InteractionType.Highlight;
                case HandGesture.OpenPalm:
                    return OOJUPlugin.InteractionType.PushAway;
                case HandGesture.Wave:
                    return OOJUPlugin.InteractionType.Heartbeat;
                default:
                    return OOJUPlugin.InteractionType.Highlight;
            }
        }

        private string GetGestureName(HandGesture gesture)
        {
            switch (gesture)
            {
                case HandGesture.PointToSelect: return "Point to Select";
                case HandGesture.Pinch: return "Pinch";
                case HandGesture.OpenPalm: return "Open Palm";
                case HandGesture.Tap: return "Tap";
                case HandGesture.Wave: return "Wave";
                default: return gesture.ToString();
            }
        }

        private string GetEffectName(HandGesture gesture)
        {
            switch (gesture)
            {
                case HandGesture.PointToSelect: return "Highlight";
                case HandGesture.Pinch: return "Follow Hand";
                case HandGesture.OpenPalm: return "Push Away";
                case HandGesture.Tap: return "Infinite Rotation";
                case HandGesture.Wave: return "Heartbeat";
                default: return "Unknown";
            }
        }

        private string GetDetailedEffectDescription(HandGesture gesture)
        {
            switch (gesture)
            {
                case HandGesture.PointToSelect:
                    return "✨ Highlight Effect\n" +
                           "The object will glow or change color when you point at it in XR. " +
                           "Perfect for drawing attention to important objects or creating interactive UI elements.";
                
                case HandGesture.Pinch:
                    return "🤏 Follow Hand Effect\n" +
                           "The object will follow your hand movements when you pinch. " +
                           "Great for grabbing and moving objects in virtual space, like picking up items or manipulating tools.";
                
                case HandGesture.OpenPalm:
                    return "🖐️ Push Away Effect\n" +
                           "The object will be pushed away from your hand when you show an open palm. " +
                           "Useful for creating force-field effects or pushing objects away without touching them.";
                
                case HandGesture.Tap:
                    return "👉 Infinite Rotation Effect\n" +
                           "The object will start spinning continuously when you tap it. " +
                           "Perfect for creating spinning decorations, rotating platforms, or animated elements.";
                
                case HandGesture.Wave:
                    return "👋 Heartbeat Effect\n" +
                           "The object will pulse with a heartbeat-like rhythm when you wave at it. " +
                           "Great for creating lively, breathing effects or indicating that an object is alive or active.";
                
                default:
                    return "Unknown effect";
            }
        }
    }
} 