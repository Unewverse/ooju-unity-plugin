using UnityEditor;
using UnityEngine;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using System.IO;
using System.Linq;
using System.Reflection;
using System;

namespace OojiCustomPlugin
{
    public class UserAssetManager : EditorWindow
    {
        private const string TokenPrefKey = "UserAssetManager_Token";
        private const string GltfFastDependency = "com.unity.cloud.gltfast";

        private string authToken = "";
        private string uploadStatus = "";
        private string userEmail = "";
        private string userPassword = "";

        // Flags to track asynchronous operations
        private bool isLoggingIn = false;
        private bool isExportingAndUploading = false;

        private bool isGltfFastInstalled = false;

        [MenuItem("Tools/OOJU Asset Manager")]
        public static void ShowWindow()
        {
            GetWindow<UserAssetManager>("OOJU Asset Manager");
        }

        private void OnEnable()
        {
            // Check if GLTFast is installed
            isGltfFastInstalled = CheckGltfFastInstalled();

            // Load the saved token
            authToken = EditorPrefs.GetString(TokenPrefKey, "");
            if (!string.IsNullOrEmpty(authToken))
            {
                Debug.Log("Loaded saved token.");
            }
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(authToken))
            {
                EditorPrefs.SetString(TokenPrefKey, authToken);
                Debug.Log("Token saved.");
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("User Asset Manager", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Display GLTFast installation status and install button
            DrawGltfFastStatus();

            // Authentication Section
            if (string.IsNullOrEmpty(authToken))
            {
                DrawLoginUI();
            }
            else
            {
                DrawLoggedInUI();
            }

            // Status Message
            if (!string.IsNullOrEmpty(uploadStatus))
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(uploadStatus, MessageType.Info);
            }
        }

        private void DrawGltfFastStatus()
        {
            if (isGltfFastInstalled)
            {
                GUILayout.Label("GLTFast is installed and ready to use.", EditorStyles.helpBox);
            }
            else
            {
                GUILayout.Label("GLTFast is not installed. Export functionality will be disabled.", EditorStyles.helpBox);
                if (GUILayout.Button("Install GLTFast"))
                {
                    if (EditorUtility.DisplayDialog(
                        "Install GLTFast",
                        "GLTFast is required for exporting assets. Would you like to install it now?",
                        "Install",
                        "Cancel"))
                    {
                        InstallGltfFast();
                    }
                }
            }
        }

        private void DrawLoginUI()
        {
            GUILayout.Label("Login to export and upload assets", EditorStyles.boldLabel);

            userEmail = EditorGUILayout.TextField("Email:", userEmail);
            userPassword = EditorGUILayout.PasswordField("Password:", userPassword);

            GUI.enabled = !isLoggingIn;
            if (GUILayout.Button("Login"))
            {
                uploadStatus = "";
                isLoggingIn = true;
                EditorCoroutineUtility.StartCoroutineOwnerless(LoginCoroutine());
            }
            GUI.enabled = true;

            if (isLoggingIn)
            {
                GUILayout.Label("Logging in... please wait.", EditorStyles.miniLabel);
            }
        }

        private void DrawLoggedInUI()
        {
            GUILayout.Label($"Logged in as: {userEmail}", EditorStyles.miniLabel);
            if (GUILayout.Button("Logout"))
            {
                authToken = "";
                EditorPrefs.DeleteKey(TokenPrefKey);
                uploadStatus = "Logged out.";
                Debug.Log("Logged out.");
            }

            GUILayout.Space(10);

            if (Selection.activeGameObject != null)
            {
                DrawExportUI();
            }
            else
            {
                EditorGUILayout.HelpBox("No GameObject selected. Please select an object to export.", MessageType.Warning);
            }
        }

        private void DrawExportUI()
        {
            GUILayout.Label($"Selected GameObject: {Selection.activeGameObject.name}", EditorStyles.miniLabel);

            GUI.enabled = !isExportingAndUploading && isGltfFastInstalled;
            if (GUILayout.Button("Export to GLB and Upload"))
            {
                uploadStatus = "Preparing to export...";
                isExportingAndUploading = true;
                ExportAndUploadGLB(Selection.activeGameObject);
            }
            GUI.enabled = true;

            if (isExportingAndUploading)
            {
                GUILayout.Label("Processing... please wait.", EditorStyles.miniLabel);
            }
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
                },
                (error) =>
                {
                    uploadStatus = $"Upload failed: {error}";
                    Debug.LogError(uploadStatus);
                    isExportingAndUploading = false;
                }
            ));
        }

        private bool CheckGltfFastInstalled()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "glTFast");
        }

        private void InstallGltfFast()
        {
            string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            string manifest = File.ReadAllText(manifestPath);

            if (!manifest.Contains(GltfFastDependency))
            {
                Debug.Log("Adding GLTFast to manifest.json...");
                int dependenciesIndex = manifest.IndexOf("\"dependencies\": {") + 16;
                string gltfFastEntry = $"\n    \"{GltfFastDependency}\": \"6.10.1\",";
                manifest = manifest.Insert(dependenciesIndex, gltfFastEntry);
                File.WriteAllText(manifestPath, manifest);
                AssetDatabase.Refresh();
                Debug.Log("GLTFast added to manifest.json. Unity will now import the package.");
            }
            else
            {
                Debug.Log("GLTFast is already installed.");
            }
        }
    }
}
