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

        // Asset Manager variables
        private string authToken = "";
        private string uploadStatus = "";
        private string downloadStatus = "";
        private string userEmail = "";
        private string userPassword = "";
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

        // Interaction-related fields (migrated from OOJUInteractionWindow)
        private Vector2 mainScrollPosition = Vector2.zero;
        private Vector2 analyzerScrollPosition = Vector2.zero;
        private Vector2 descriptionScrollPosition = Vector2.zero;
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
        private bool showInteractionGeneration = true;
        private bool showAddPlayer = true;
        private bool showAnimation = true;
        private GUIStyle bigFoldoutStyle;
        private UIStyles interactionStyles;
        private AnimationUI animationUI;
        private enum InteractionTab { Tools, Settings }
        private InteractionTab currentInteractionTab = InteractionTab.Tools;
        // 색상 정의 (OOJUInteractionWindow에서 이식)
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
            if (animationUI == null)
                animationUI = new AnimationUI();
            if (bigFoldoutStyle == null)
            {
                bigFoldoutStyle = new GUIStyle(EditorStyles.foldout);
                bigFoldoutStyle.fontSize = 16;
                bigFoldoutStyle.fontStyle = FontStyle.Bold;
                bigFoldoutStyle.normal.textColor = Color.white;
                bigFoldoutStyle.onNormal.textColor = Color.white;
                bigFoldoutStyle.active.textColor = Color.white;
                bigFoldoutStyle.onActive.textColor = Color.white;
                bigFoldoutStyle.focused.textColor = Color.white;
                bigFoldoutStyle.onFocused.textColor = Color.white;
            }

            float contentWidth = position.width - 40f;
            float buttonWidth = Mathf.Min(250f, contentWidth * 0.7f);

            // Internal tab UI (Tools/Settings)
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentInteractionTab == InteractionTab.Tools, "Tools", EditorStyles.toolbarButton))
                currentInteractionTab = InteractionTab.Tools;
            if (GUILayout.Toggle(currentInteractionTab == InteractionTab.Settings, "Settings", EditorStyles.toolbarButton))
                currentInteractionTab = InteractionTab.Settings;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            switch (currentInteractionTab)
            {
                case InteractionTab.Tools:
                    DrawInteractionToolsTab(contentWidth, buttonWidth);
                    break;
                case InteractionTab.Settings:
                    DrawInteractionSettingsTab();
                    break;
            }
        }

        // Draws the main interaction tools tab UI
        private void DrawInteractionToolsTab(float contentWidth, float buttonWidth)
        {
            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(20);

            // Interaction Generation foldout
            EditorGUILayout.BeginVertical();
            showInteractionGeneration = EditorGUILayout.Foldout(showInteractionGeneration, "Interaction Generation", true, bigFoldoutStyle);
            if (showInteractionGeneration)
            {
                DrawDescriptionSection(buttonWidth);
                GUILayout.Space(16);
                DrawSentenceToInteractionSection(buttonWidth);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            // Player foldout
            EditorGUILayout.BeginVertical();
            showAddPlayer = EditorGUILayout.Foldout(showAddPlayer, "Player", true, bigFoldoutStyle);
            if (showAddPlayer)
            {
                DrawAddPlayerSection(buttonWidth);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            // Animation foldout
            EditorGUILayout.BeginVertical();
            showAnimation = EditorGUILayout.Foldout(showAnimation, "Animation", true, bigFoldoutStyle);
            if (showAnimation)
            {
                DrawAnimationSection();
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
            EditorGUILayout.LabelField("Suggestion", sectionTitleStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            EditorGUILayout.LabelField("Suggest appropriate interactions for selected objects based on the scene context.", descLabelStyle);
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            // Suggest Interactions button
            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;
            GUI.backgroundColor = ButtonBgColor;
            GUI.contentColor = ButtonTextColor;
            EditorGUI.BeginDisabledGroup(isGeneratingDescription);
            if (GUILayout.Button(new GUIContent("Suggest Interactions", "Suggest appropriate interactions for selected objects based on the scene context."), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
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
                EditorGUILayout.LabelField("Interaction Suggestions:", EditorStyles.boldLabel);
                bool hasAnyValidSuggestion = false;
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
                            cleanSuggestion = System.Text.RegularExpressions.Regex.Replace(cleanSuggestion, @"\*\*(.*?)\*\*", "$1");
                            // Show suggestion as a word-wrapped label with max width
                            EditorGUILayout.LabelField(cleanSuggestion, EditorStyles.wordWrappedLabel, GUILayout.MaxWidth(400));
                            // Generate button centered
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            prevBg = GUI.backgroundColor;
                            prevContent = GUI.contentColor;
                            GUI.backgroundColor = ButtonBgColor;
                            GUI.contentColor = ButtonTextColor;
                            if (GUILayout.Button(new GUIContent("Generate", "Generate this suggestion and create the script."), GUILayout.Width(80)))
                            {
                                lastGeneratedSuggestionPerObject[objName] = cleanSuggestion;
                                userInteractionInput = cleanSuggestion;
                                GenerateSentenceToInteraction();
                            }
                            GUI.backgroundColor = prevBg;
                            GUI.contentColor = prevContent;
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(5);
                            validFound = true;
                            hasAnyValidSuggestion = true;
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
                if (GUILayout.Button(new GUIContent("Regenerate Interaction Suggestions", "Generate interaction suggestions for the currently selected objects based on the existing scene description and your input."), GUILayout.Width(buttonWidth), GUILayout.Height(22)))
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
            EditorGUILayout.LabelField("Sentence-to-Interaction", sectionTitleStyle2);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            EditorGUILayout.LabelField("Describe the interaction you want to create as a single sentence", EditorStyles.miniLabel);
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            var wordWrapStyle = new GUIStyle(EditorStyles.textField) { wordWrap = true };
            wordWrapStyle.normal.textColor = InputTextColor;
            if (string.IsNullOrEmpty(userInteractionInput))
            {
                EditorGUILayout.LabelField("e.g. Make the object spin when clicked.", EditorStyles.wordWrappedMiniLabel);
            }
            userInteractionInput = EditorGUILayout.TextArea(userInteractionInput, wordWrapStyle, GUILayout.Height(60), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(800));
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;
            GUI.backgroundColor = ButtonBgColor;
            GUI.contentColor = ButtonTextColor;
            if (GUILayout.Button(new GUIContent("Generate Interaction", "Generate a Unity C# script for the described interaction."), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
            {
                try { GenerateSentenceToInteraction(); } catch (Exception ex) { Debug.LogError($"Error in GenerateSentenceToInteraction: {ex.Message}"); EditorUtility.DisplayDialog("Error", $"Error in GenerateSentenceToInteraction: {ex.Message}", "OK"); }
            }
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            if (!string.IsNullOrEmpty(lastGeneratedClassName) && lastGeneratedClassName != "No code block found.")
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                prevBg = GUI.backgroundColor;
                prevContent = GUI.contentColor;
                GUI.backgroundColor = ButtonBgColor;
                GUI.contentColor = ButtonTextColor;
                if (GUILayout.Button(new GUIContent("Assign Script to Selected Object(s)", "Assign the generated script to the selected objects."), GUILayout.Width(buttonWidth), GUILayout.Height(28)))
                {
                    AssignScriptToSelectedObjects();
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
                    EditorGUILayout.HelpBox($"Generated script saved to: {lastGeneratedScriptPath}", MessageType.Info);
                }
                if (!string.IsNullOrEmpty(lastSuggestedObjectNames))
                {
                    EditorGUILayout.LabelField("Suggested Object Name(s):", EditorStyles.boldLabel);
                    EditorGUILayout.TextField(lastSuggestedObjectNames);
                }
                if (foundSuggestedObjects != null && foundSuggestedObjects.Count > 0)
                {
                    EditorGUILayout.LabelField("Found in Scene:", EditorStyles.boldLabel);
                    foreach (var obj in foundSuggestedObjects)
                    {
                        EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                    }
                }
            }
        }

        // Draws the animation section
        private void DrawAnimationSection()
        {
            try
            {
                if (animationUI == null)
                {
                    EditorGUILayout.HelpBox("AnimationUI is not initialized.", MessageType.Error);
                    return;
                }
                if (AnimationSettings.Instance == null)
                {
                    EditorGUILayout.HelpBox("AnimationSettings is not initialized.", MessageType.Error);
                    return;
                }
                animationUI.DrawAnimationUI();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in DrawAnimationSection: {ex.Message}");
                EditorGUILayout.HelpBox($"Error in DrawAnimationSection: {ex.Message}", MessageType.Error);
            }
        }

        // Draws the Add Player section (UI + logic)
        private void DrawAddPlayerSection(float buttonWidth)
        {
            // Section icon and header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(EditorGUIUtility.IconContent("Avatar Icon"), GUILayout.Width(22), GUILayout.Height(22));
            GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel);
            sectionTitleStyle.fontSize = 14;
            sectionTitleStyle.normal.textColor = SectionTitleColor;
            EditorGUILayout.LabelField("Add Player", sectionTitleStyle);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
            EditorGUILayout.LabelField("Add a player controller to your scene.", EditorStyles.miniLabel);
            GUILayout.Space(8);
            // Add First-person Player button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Color prevBg = GUI.backgroundColor;
            Color prevContent = GUI.contentColor;
            GUI.backgroundColor = ButtonBgColor;
            GUI.contentColor = ButtonTextColor;
            if (GUILayout.Button(new GUIContent("Add First-person Player", "Add a first-person player controller to the scene."), GUILayout.Width(buttonWidth), GUILayout.Height(30)))
            {
                AddFirstPersonPlayerToScene();
            }
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(16);
            // Add Ground button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            prevBg = GUI.backgroundColor;
            prevContent = GUI.contentColor;
            GUI.backgroundColor = ButtonBgColor;
            GUI.contentColor = ButtonTextColor;
            if (GUILayout.Button(new GUIContent("Add Ground", "Add a large ground plane (cube) at y=0."), GUILayout.Width(buttonWidth), GUILayout.Height(28)))
            {
                AddGroundToScene();
            }
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            // Set Selected as Ground button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            prevBg = GUI.backgroundColor;
            prevContent = GUI.contentColor;
            GUI.backgroundColor = ButtonBgColor;
            GUI.contentColor = ButtonTextColor;
            if (GUILayout.Button(new GUIContent("Set Selected as Ground", "Add a MeshCollider to the selected object(s) and set their layer to Default."), GUILayout.Width(buttonWidth), GUILayout.Height(28)))
            {
                SetSelectedAsGround();
            }
            GUI.backgroundColor = prevBg;
            GUI.contentColor = prevContent;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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
            EditorUtility.DisplayDialog("Ground Added", "A large ground cube has been added at y=0.", "OK");
        }

        // Helper: Add a MeshCollider to selected objects and set their layer to Default
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
            EditorUtility.DisplayDialog("First-person Player", "First-person player has been added to the scene!\nUse WASD or arrow keys and Space to jump in Play mode.", "OK");
        }

        // Returns the code for a simple FirstPersonPlayer script (WASD/arrow keys + Space to jump)
        private string GetFirstPersonPlayerScriptCode()
        {
            return "using UnityEngine;\n" +
                   "// Simple first-person player controller (WASD/arrow keys + Space to jump)\n" +
                   "public class FirstPersonPlayer : MonoBehaviour\n" +
                   "{\n" +
                   "    public float speed = 5f;\n" +
                   "    public float mouseSensitivity = 2f;\n" +
                   "    public float jumpHeight = 2f;\n" +
                   "    public float gravity = -9.81f;\n" +
                   "    private float rotationY = 0f;\n" +
                   "    private CharacterController controller;\n" +
                   "    private Vector3 velocity;\n" +
                   "    private bool isGrounded;\n" +
                   "    void Start()\n" +
                   "    {\n" +
                   "        controller = GetComponent<CharacterController>();\n" +
                   "        // Add a camera if not present\n" +
                   "        if (GetComponentInChildren<Camera>() == null)\n" +
                   "        {\n" +
                   "            GameObject camObj = new GameObject(\"PlayerCamera\");\n" +
                   "            camObj.transform.SetParent(transform);\n" +
                   "            camObj.transform.localPosition = new Vector3(0, 1.6f, 0);\n" +
                   "            camObj.AddComponent<Camera>();\n" +
                   "        }\n" +
                   "    }\n" +
                   "    void Update()\n" +
                   "    {\n" +
                   "        // Move\n" +
                   "        float h = Input.GetAxis(\"Horizontal\");\n" +
                   "        float v = Input.GetAxis(\"Vertical\");\n" +
                   "        Vector3 move = transform.right * h + transform.forward * v;\n" +
                   "        if (controller != null)\n" +
                   "            controller.Move(move * speed * Time.deltaTime);\n" +
                   "        else\n" +
                   "            transform.position += move * speed * Time.deltaTime;\n" +
                   "        // Mouse look (only in play mode)\n" +
                   "        if (Application.isPlaying)\n" +
                   "        {\n" +
                   "            float mouseX = Input.GetAxis(\"Mouse X\") * mouseSensitivity;\n" +
                   "            float mouseY = Input.GetAxis(\"Mouse Y\") * mouseSensitivity;\n" +
                   "            transform.Rotate(0, mouseX, 0);\n" +
                   "            rotationY -= mouseY;\n" +
                   "            rotationY = Mathf.Clamp(rotationY, -90f, 90f);\n" +
                   "            Camera cam = GetComponentInChildren<Camera>();\n" +
                   "            if (cam)\n" +
                   "                cam.transform.localEulerAngles = new Vector3(rotationY, 0, 0);\n" +
                   "        }\n" +
                   "        // Jump & Gravity\n" +
                   "        if (controller != null)\n" +
                   "        {\n" +
                   "            isGrounded = controller.isGrounded;\n" +
                   "            if (isGrounded && velocity.y < 0)\n" +
                   "                velocity.y = -2f;\n" +
                   "            if (isGrounded && Input.GetKeyDown(KeyCode.Space))\n" +
                   "                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);\n" +
                   "            velocity.y += gravity * Time.deltaTime;\n" +
                   "            controller.Move(velocity * Time.deltaTime);\n" +
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
            var scriptType = GetTypeByName(lastGeneratedClassName);
            if (scriptType == null)
            {
                EditorUtility.DisplayDialog("Assign Script", $"Could not find compiled script type: {lastGeneratedClassName}. Please recompile scripts and try again.", "OK");
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
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(className);
                if (type != null)
                    return type;
                type = assembly.GetTypes().FirstOrDefault(t => t.Name == className);
                if (type != null)
                    return type;
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

        // Helper: Save the generated script code to a new C# file in the project, returns the file path
        private string SaveGeneratedScript(string scriptCode, string className = null)
        {
            string directory = "Assets/OOJU/Interaction/Generated";
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);
            if (string.IsNullOrEmpty(className))
            {
                className = GenerateClassNameFromSentence(userInteractionInput);
            }
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            className = $"{className}_{timestamp}";
            scriptCode = ReplaceClassNameInScript(scriptCode, className);
            scriptCode = RemoveDuplicateClassAndMethods(scriptCode, className);
            string filePath = System.IO.Path.Combine(directory, $"{className}.cs");
            System.IO.File.WriteAllText(filePath, scriptCode);
            AssetDatabase.Refresh();
            return filePath;
        }

        // Helper: Replace the first class name in the script code with the given class name
        private string ReplaceClassNameInScript(string scriptCode, string newClassName)
        {
            if (string.IsNullOrEmpty(scriptCode) || string.IsNullOrEmpty(newClassName)) return scriptCode;
            var regex = new System.Text.RegularExpressions.Regex(@"class\\s+([A-Za-z_][A-Za-z0-9_]*)");
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
                if (string.IsNullOrEmpty(sceneDescription))
                {
                    EditorUtility.DisplayDialog("Error", "Please generate a scene description first.", "OK");
                    return;
                }
                if (string.IsNullOrEmpty(userInteractionInput))
                {
                    EditorUtility.DisplayDialog("Error", "Please enter an interaction description.", "OK");
                    return;
                }
                isGeneratingDescription = true;
                EditorUtility.DisplayProgressBar("Generating Interaction", "Please wait while the interaction is being generated...", 0.5f);
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
                    lastGeneratedScriptPath = SaveGeneratedScript(code);
                    lastGeneratedClassName = ExtractClassNameFromCode(code);
                    UnityEditor.AssetDatabase.Refresh();
                }
                else
                {
                    lastGeneratedScriptPath = "No code block found.";
                    lastGeneratedClassName = "";
                }
                lastSuggestedObjectNames = ExtractSuggestedObjectNames(sentenceToInteractionResult);
                foundSuggestedObjects = FindObjectsInSceneByNames(lastSuggestedObjectNames);
                EditorUtility.DisplayDialog("Sentence-to-Interaction", "Interaction generated successfully. You can now assign the script to the selected object(s).", "OK");
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

        private void DrawInteractionSettingsTab()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AI Model Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            string[] llmTypes = new[] { "OpenAI", "Claude", "Gemini" };
            int selectedIdx = Array.IndexOf(llmTypes, OISettings.Instance.SelectedLLMType);
            if (selectedIdx < 0) selectedIdx = 0;
            selectedIdx = EditorGUILayout.Popup("Models", selectedIdx, llmTypes);
            OISettings.Instance.SelectedLLMType = llmTypes[selectedIdx];
            EditorGUILayout.Space();
            switch (OISettings.Instance.SelectedLLMType)
            {
                case "OpenAI":
                    OISettings.Instance.ApiKey = EditorGUILayout.PasswordField("OpenAI API Key", OISettings.Instance.ApiKey);
                    break;
                case "Claude":
                    OISettings.Instance.ClaudeApiKey = EditorGUILayout.PasswordField("Claude API Key", OISettings.Instance.ClaudeApiKey);
                    break;
                case "Gemini":
                    OISettings.Instance.GeminiApiKey = EditorGUILayout.PasswordField("Gemini API Key", OISettings.Instance.GeminiApiKey);
                    break;
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Save Settings"))
            {
                OISettings.Instance.SaveSettings();
                EditorUtility.SetDirty(OISettings.Instance);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Saved", "Settings have been saved.", "OK");
            }
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
            yield return NetworkUtility.GetAssets(
                authToken,
                (assets) =>
                {
                    availableAssets = assets;
                    assetCount = assets.Count;
                    assetsAvailable = assetCount > 0;
                    FilterAssets();
                    isCheckingAssets = false;
                    downloadStatus = $"Found {assetCount} assets.";
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
        private void DrawImportTab()
        {
            try
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Import Assets", styles.sectionHeaderStyle);
                GUILayout.Space(10);

                // ZIP Scene Import section
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Import OOJU Scene", styles.subSectionHeaderStyle);
                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Import ZIP Scene", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    string zipPath = EditorUtility.OpenFilePanel("Select OOJU Scene ZIP", "", "zip");
                    if (!string.IsNullOrEmpty(zipPath))
                    {
                        uploadStatus = "Processing ZIP file...";
                        EditorCoroutineUtility.StartCoroutineOwnerless(UploadZipFileCoroutine(zipPath));
                    }
                }
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

                GUILayout.Space(20);

                // Original drag & drop area
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

        // DrawSettingsTab 함수 추가 (클래스 내부)
        private void DrawSettingsTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Asset Manager Settings", styles.sectionHeaderStyle);
            GUILayout.Space(10);

            // Auto-sync settings
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            autoSyncEnabled = EditorGUILayout.ToggleLeft("Enable Auto-sync (every 15 minutes)", autoSyncEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("OOJUManager_AutoSync", autoSyncEnabled);
            }
            EditorGUILayout.EndHorizontal();
            
            if (autoSyncEnabled)
            {
                EditorGUILayout.HelpBox("Assets will be automatically synchronized with the server every 15 minutes.", MessageType.Info);
            }

            GUILayout.Space(20);

            // ZIP File Upload
            EditorGUILayout.LabelField("ZIP File Upload", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Upload ZIP File", GUILayout.Width(150), GUILayout.Height(30)))
            {
                string zipPath = EditorUtility.OpenFilePanel("Select ZIP File", "", "zip");
                if (!string.IsNullOrEmpty(zipPath))
                {
                    if (string.IsNullOrEmpty(authToken))
                    {
                        EditorUtility.DisplayDialog("Error", "Please log in first.", "OK");
                    }
                    else
                    {
                        uploadStatus = "Uploading ZIP file...";
                        EditorCoroutineUtility.StartCoroutineOwnerless(UploadZipFileCoroutine(zipPath));
                    }
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(uploadStatus) && uploadStatus.Contains("ZIP"))
            {
                EditorGUILayout.HelpBox(uploadStatus, MessageType.Info);
            }

            GUILayout.Space(20);

            // GLTFast installation status
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("GLTFast Status:", EditorStyles.boldLabel);
            if (isGltfFastInstalled)
            {
                EditorGUILayout.LabelField("Installed", EditorStyles.boldLabel);
                GUI.color = Color.green;
                GUILayout.Label(EditorGUIUtility.IconContent("d_Valid@2x"));
                GUI.color = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField("Not Installed", EditorStyles.boldLabel);
                if (GUILayout.Button("Install GLTFast"))
                {
                    InstallGltfFast();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!isGltfFastInstalled)
            {
                EditorGUILayout.HelpBox("GLTFast is required for importing glTF/GLB files. Click 'Install GLTFast' to add it to your project.", MessageType.Warning);
            }

            GUILayout.Space(20);

            // Reset settings button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.6f);
            if (GUILayout.Button("Reset All Settings", GUILayout.Width(150), GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Are you sure you want to reset all settings? This will clear your login information and preferences.",
                    "Reset", "Cancel"))
                {
                    ResetAllSettings();
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Cache information
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Cache Information", EditorStyles.boldLabel);
            string cachePath = Path.Combine(Application.dataPath, "OOJU", "Assets");
            if (Directory.Exists(cachePath))
            {
                long size = GetDirectorySize(new DirectoryInfo(cachePath));
                EditorGUILayout.LabelField($"Cache Size: {FormatSize(size)}");

                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear Cache", GUILayout.Width(120)))
                {
                    if (EditorUtility.DisplayDialog("Clear Cache",
                        "Are you sure you want to clear the asset cache? This will delete all downloaded assets.",
                        "Clear", "Cancel"))
                    {
                        ClearCache();
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Cache is empty");
            }

            EditorGUILayout.EndVertical();
        }

        private void ClearCache()
        {
            try
            {
                string cachePath = Path.Combine(Application.dataPath, "OOJU", "Assets");
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    AssetDatabase.Refresh();
                    Debug.Log("Asset cache cleared successfully.");
                    EditorUtility.DisplayDialog("Cache Cleared", "Asset cache has been cleared successfully.", "OK");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error clearing cache: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to clear cache: {e.Message}", "OK");
            }
        }

        private long GetDirectorySize(DirectoryInfo directoryInfo)
        {
            long size = 0;
            try
            {
                // Add file sizes
                FileInfo[] files = directoryInfo.GetFiles();
                foreach (FileInfo file in files)
                {
                    size += file.Length;
                }

                // Add subdirectory sizes
                DirectoryInfo[] dirs = directoryInfo.GetDirectories();
                foreach (DirectoryInfo dir in dirs)
                {
                    size += GetDirectorySize(dir);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error calculating directory size: {e.Message}");
            }
            return size;
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            return $"{size:0.##} {sizes[order]}";
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
    }
} 