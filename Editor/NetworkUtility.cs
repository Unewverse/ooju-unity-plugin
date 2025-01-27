using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

    public static class NetworkUtility
    {
        private static string backendUrl = "https://demo-backend.abstractless.com/api/v1";

        [System.Serializable]
        public class LoginPayload
        {
            public string username;
            public string password;
        }

        public static IEnumerator Login(string username, string password, System.Action<string> onSuccess, System.Action<string> onFailure)
        {
            var payload = new LoginPayload
            {
                username = username, 
                password = password
            };
            string jsonPayload = JsonUtility.ToJson(payload);

            UnityWebRequest request = new UnityWebRequest(backendUrl + "/token", "POST");
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
                    var responseData = JsonUtility.FromJson<LoginResponse>(responseText);

                    string token = responseData.access_token;
                    onSuccess?.Invoke(token);
                }
                catch (System.Exception ex)
                {
                    onFailure?.Invoke("Error parsing login response.");
                    Debug.LogError($"Login response parse error: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"Login failed: {request.error}");
                onFailure?.Invoke(request.error);
            }
        }

        [System.Serializable]
        private class LoginResponse
        {
            public string access_token;
        }

        public static IEnumerator UploadFile(string filePath, string token, System.Action<string> onSuccess, System.Action<string> onFailure)
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

            UnityWebRequest request = UnityWebRequest.Post(backendUrl + "/assets", form);
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

    }
