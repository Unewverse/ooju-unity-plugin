using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
namespace OojiCustomPlugin
{
    public static class NetworkUtility
    {
        public static string BackendUrl { get; private set; } = "https://api.ooju.world/api/v1";

        [Serializable]
        public class LoginPayload
        {
            public string username;
            public string password;
            public string device_type = "unity";
        }

        public static IEnumerator Login(string username, string password, Action<string> onSuccess, Action<string> onFailure)
        {
            var payload = new LoginPayload
            {
                username = username, 
                password = password,
                device_type = "unity"
            };
            string jsonPayload = JsonUtility.ToJson(payload);
            Debug.Log($"Sending login payload: {jsonPayload}");

            UnityWebRequest request = new UnityWebRequest(BackendUrl + "/token", "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var responseText = request.downloadHandler.text;
                    Debug.Log($"Login response: {responseText}");
                    
                    var responseData = JsonUtility.FromJson<LoginResponse>(responseText);

                    string token = responseData.access_token;
                    
                    // Store token persistently
                    StoreToken(token);
                    
                    // Store token expiration if available
                    if (!string.IsNullOrEmpty(responseData.expires_at))
                    {
                        EditorPrefs.SetString("TokenExpiresAt", responseData.expires_at);
                    }
                    
                    // Store user ID if available
                    if (!string.IsNullOrEmpty(responseData.user_id))
                    {
                        EditorPrefs.SetString("UserId", responseData.user_id);
                    }
                    
                    onSuccess?.Invoke(token);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Login response parse error: {ex.Message}");
                    onFailure?.Invoke("Error parsing login response.");
                }
            }
            else
            {
                Debug.LogError($"Login failed: {request.error}. Response: {request.downloadHandler?.text}");
                onFailure?.Invoke(request.error);
            }
        }


        [Serializable]
        private class LoginResponse
        {
            public string access_token;
            public string token_type;
            public string device_type;
            public string user_id;
            public string expires_at;
        }

        public static IEnumerator UploadFile(string filePath, string token, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                onFailure?.Invoke($"File not found or invalid: {filePath}");
                yield break;
            }

            byte[] fileData = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);

            WWWForm form = new WWWForm();
            form.AddBinaryData("file", fileData, fileName, "model/gltf-binary");

            UnityWebRequest request = UnityWebRequest.Post(BackendUrl + "/assets", form);
            request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke($"File uploaded successfully: {fileName}");
            }
            else
            {
                onFailure?.Invoke($"Upload failed: {request.error}");
            }
        }

        public static void StoreToken(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                EditorPrefs.SetString("AuthToken", token);
                Debug.Log("Token stored successfully");
            }
        }

        public static string GetStoredToken()
        {
            return EditorPrefs.GetString("AuthToken", "");
        }

        public static bool HasValidStoredToken()
        {
            string token = GetStoredToken();
            return !string.IsNullOrEmpty(token) && IsTokenValid();
        }

        public static void ClearStoredToken()
        {
            EditorPrefs.DeleteKey("AuthToken");
            EditorPrefs.DeleteKey("TokenExpiresAt");
            EditorPrefs.DeleteKey("UserId");
            Debug.Log("Token cleared");
        }

        public static void SetBackendUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                BackendUrl = url;
                Debug.Log($"Backend URL set to: {BackendUrl}");
            }
        }

        public static bool IsTokenValid()
        {
            string expiresAtString = EditorPrefs.GetString("TokenExpiresAt", "");
            
            if (string.IsNullOrEmpty(expiresAtString))
            {
                return true; // If no expiration is stored, assume token is valid
            }
            
            try
            {
                DateTime expiresAt = DateTime.Parse(expiresAtString);
                return DateTime.UtcNow < expiresAt;
            }
            catch
            {
                return true; // If we can't parse the date, assume token is valid
            }
        }
    
    
        [Serializable]
        public class ExportableAsset
        {
            public string id;         
            public string filename;
            public string content_type;
            public string presigned_url;
            public string created_at; 
        }

        [Serializable]
        public class ExportableAssetsResponse
        {
            public ExportableAsset[] assets;
        }

        public static IEnumerator GetExportableAssets(string token, Action<List<ExportableAsset>> onSuccess, Action<string> onFailure)
        {
            UnityWebRequest request = null;
            string apiUrl = BackendUrl + "/assets";
            
            Debug.Log($"[OOJU] Making API request to: {apiUrl}");
            Debug.Log($"[OOJU] Using token: {token?.Substring(0, Math.Min(10, token?.Length ?? 0))}...");
            
            try {
                request = UnityWebRequest.Get(apiUrl);
                request.SetRequestHeader("Authorization", $"Bearer {token}");
                Debug.Log($"[OOJU] Request headers set successfully");
            }
            catch (Exception ex) {
                Debug.LogError($"[OOJU] Error creating web request: {ex.Message}");
                onFailure?.Invoke("Error creating web request: " + ex.Message);
                yield break;
            }

            yield return request.SendWebRequest();

            try {
                Debug.Log($"[OOJU] Request completed with result: {request.result}");
                Debug.Log($"[OOJU] Response code: {request.responseCode}");
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"[OOJU] Raw response: {responseText}");
                    
                    if (string.IsNullOrWhiteSpace(responseText)) {
                        Debug.LogWarning("[OOJU] Empty response from server");
                        onSuccess?.Invoke(new List<ExportableAsset>());
                        yield break;
                    }
                    
                    string jsonResponse = responseText;
                    
                    if (!jsonResponse.TrimStart().StartsWith("["))
                    {
                        jsonResponse = "{ \"assets\": " + jsonResponse + "}";
                    }
                    else
                    {
                        jsonResponse = "{ \"assets\": " + jsonResponse + "}";
                    }
                    
                    Debug.Log($"[OOJU] Formatted JSON for parsing: {jsonResponse}");
                    
                    ExportableAssetsResponse response = JsonUtility.FromJson<ExportableAssetsResponse>(jsonResponse);
                    
                    if (response != null && response.assets != null)
                    {
                        Debug.Log($"[OOJU] Successfully parsed {response.assets.Length} assets");
                        if (response.assets.Length > 0)
                        {
                            Debug.Log($"[OOJU] First asset: id={response.assets[0].id}, name={response.assets[0].filename}, type={response.assets[0].content_type}");
                        }
                        
                        onSuccess?.Invoke(new List<ExportableAsset>(response.assets));
                    }
                    else
                    {
                        Debug.LogWarning("[OOJU] No assets found in response or failed to parse");
                        onSuccess?.Invoke(new List<ExportableAsset>());
                    }
                }
                else
                {
                    string errorDetails = $"Status: {request.responseCode}, Error: {request.error}";
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    {
                        errorDetails += $", Response: {request.downloadHandler.text}";
                    }
                    Debug.LogError($"[OOJU] Failed to get assets: {errorDetails}");
                    onFailure?.Invoke($"API request failed: {errorDetails}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OOJU] Error processing assets response: {ex.Message}");
                Debug.LogException(ex);
                onFailure?.Invoke("Error processing assets: " + ex.Message);
            }
        }


        [Serializable]
        public class PresignedUrlWithIdResponse
        {
            public string presigned_url;
            public string asset_id;
        }

        public static IEnumerator DownloadAsset(string assetId, string token, string destinationPath, Action<bool> onComplete, Action<float> onProgress)
        {
            // First, get the presigned URL
            var urlRequest = UnityWebRequest.Get($"{BackendUrl}/assets/download/{assetId}");
            urlRequest.SetRequestHeader("Authorization", $"Bearer {token}");
            yield return urlRequest.SendWebRequest();

            if (urlRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to retrieve presigned URL: " + urlRequest.error);
                onComplete?.Invoke(false);
                yield break;
            }

            // Parse the response outside of try-catch to avoid yield in try block
            string responseJson = urlRequest.downloadHandler.text;
            Debug.Log($"Download response: {responseJson}");
            
            string presignedUrl = "";
            string responseAssetId = "";
            
            try
            {
                // Parse the response - expecting both asset_id and presigned_url
                var response = JsonUtility.FromJson<PresignedUrlWithIdResponse>(responseJson);
                presignedUrl = response.presigned_url;
                responseAssetId = response.asset_id;
            }
            catch (Exception ex)
            {
                // If new format fails, try old format
                try
                {
                    var oldResponse = JsonUtility.FromJson<PresignedUrlWithIdResponse>(responseJson);
                    presignedUrl = oldResponse.presigned_url;
                    responseAssetId = ""; // No ID in old format
                }
                catch
                {
                    Debug.LogError($"Error parsing download response: {ex.Message}");
                    onComplete?.Invoke(false);
                    yield break;
                }
            }
            
            if (string.IsNullOrEmpty(presignedUrl))
            {
                Debug.LogError("No presigned URL found in response");
                onComplete?.Invoke(false);
                yield break;
            }
            
            // Store the asset ID
            string fileName = Path.GetFileName(destinationPath);
            if (!string.IsNullOrEmpty(responseAssetId))
            {
                // If response included the asset_id
                AssetDownloader.StoreAssetId(fileName, responseAssetId);
                Debug.Log($"Stored asset ID {responseAssetId} for file {fileName}");
            }
            else
            {
                // If the backend didn't return asset_id but we already know it from the request
                AssetDownloader.StoreAssetId(fileName, assetId);
                Debug.Log($"Stored provided asset ID {assetId} for file {fileName}");
            }

            // Now download the actual file
            var fileRequest = UnityWebRequest.Get(presignedUrl);
            fileRequest.downloadHandler = new DownloadHandlerFile(destinationPath);
            yield return fileRequest.SendWebRequest();

            if (fileRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("File downloaded successfully to " + destinationPath);
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError("Failed to download file: " + fileRequest.error);
                onComplete?.Invoke(false);
            }
        }

        //Update File 
        public static IEnumerator UpdateFile(string filePath, string assetId, string token, 
            Action<bool> onSuccess, Action<string> onError)
        {
            if (!File.Exists(filePath))
            {
                onError?.Invoke($"File not found: {filePath}");
                yield break;
            }
            
            byte[] fileData;
            string fileName;
            
            try
            {
                // Read the file
                fileData = File.ReadAllBytes(filePath);
                fileName = Path.GetFileName(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error reading file: {ex.Message}");
                onError?.Invoke($"Error reading file: {ex.Message}");
                yield break;
            }
            
            // Create form data
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", fileData, fileName);
            
            // Create request
            UnityWebRequest www = UnityWebRequest.Post(BackendUrl + $"/assets/{assetId}", form);
            www.method = "PUT"; // Override to use PUT method
            www.SetRequestHeader("Authorization", "Bearer " + token);
            
            EditorUtility.DisplayProgressBar("Updating Asset", "Uploading file...", 0);
            
            // Send request
            UnityWebRequestAsyncOperation operation = www.SendWebRequest();
            
            while (!operation.isDone)
            {
                EditorUtility.DisplayProgressBar("Updating Asset", "Uploading file...", operation.progress);
                yield return null;
            }
            
            // Handle response
            bool success = false;
            string errorMsg = "";
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                success = true;
            }
            else
            {
                errorMsg = www.error;
                if (!string.IsNullOrEmpty(www.downloadHandler.text))
                {
                    errorMsg += " - " + www.downloadHandler.text;
                }
                Debug.LogError($"Asset update failed: {errorMsg}");
            }
            
            www.Dispose();
            EditorUtility.ClearProgressBar();
            
            if (success)
            {
                onSuccess?.Invoke(true);
            }
            else
            {
                onError?.Invoke(errorMsg);
            }
        }
    }
}