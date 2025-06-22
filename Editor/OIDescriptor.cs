using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Net.Http;
using UnityEngine.Networking;
using OOJUPlugin;

namespace OOJUPlugin
{
    public static class OIDescriptor
    {
        public static async Task<string> GenerateSceneDescription()
        {
            try
            {
                // Get all objects in the scene
                var allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                var sceneInfo = new StringBuilder();
                
                sceneInfo.AppendLine("Unity Scene Analysis:");
                sceneInfo.AppendLine($"Total objects in scene: {allObjects.Length}");
                
                // Categorize objects
                var meshRenderers = new List<string>();
                var lights = new List<string>();
                var cameras = new List<string>();
                var others = new List<string>();
                
                foreach (var obj in allObjects)
                {
                    if (obj.GetComponent<MeshRenderer>() != null || obj.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        meshRenderers.Add(obj.name);
                    }
                    else if (obj.GetComponent<Light>() != null)
                    {
                        lights.Add(obj.name);
                    }
                    else if (obj.GetComponent<Camera>() != null)
                    {
                        cameras.Add(obj.name);
                    }
                    else
                    {
                        others.Add(obj.name);
                    }
                }
                
                if (meshRenderers.Count > 0)
                {
                    sceneInfo.AppendLine($"\nRendered Objects ({meshRenderers.Count}):");
                    foreach (var name in meshRenderers.Take(10)) // Limit to first 10
                    {
                        sceneInfo.AppendLine($"- {name}");
                    }
                    if (meshRenderers.Count > 10)
                        sceneInfo.AppendLine($"... and {meshRenderers.Count - 10} more");
                }
                
                if (lights.Count > 0)
                {
                    sceneInfo.AppendLine($"\nLights ({lights.Count}): {string.Join(", ", lights)}");
                }
                
                if (cameras.Count > 0)
                {
                    sceneInfo.AppendLine($"\nCameras ({cameras.Count}): {string.Join(", ", cameras)}");
                }
                
                string prompt = $"Analyze this Unity scene and provide a brief, descriptive summary of what kind of environment or setting this appears to be. Focus on the overall scene context and purpose:\n\n{sceneInfo.ToString()}\n\nProvide a concise description of the scene environment in 2-3 sentences.";
                
                return await RequestLLMInteraction(prompt);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating scene description: {ex.Message}");
                return "Unable to analyze scene. Please check your LLM API settings.";
            }
        }
        
        // Test Claude API key directly using original settings
        public static async Task<string> TestClaudeAPIKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "Error: No API key provided.";
                
            Debug.Log($"[OOJU] Testing Claude API (Original Settings) with key: {apiKey.Substring(0, Math.Min(15, apiKey.Length))}...");
            
            try
            {
                string result = await CallClaudeAPIWithOriginalSettings("Say 'Hello, this is a test'", apiKey);
                Debug.Log($"[OOJU] Claude API (Original Settings) test result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OOJU] Claude API (Original Settings) test failed: {ex.Message}");
                return $"Test failed: {ex.Message}";
            }
        }

        public static async Task<string> RequestLLMInteraction(string prompt)
        {
            var settings = OISettings.Instance;
            
            try
            {
                // Log which provider and key (first 10 chars) we're using for debugging
                string keyPreview = "";
                string apiKey = "";
                
                switch (settings.SelectedLLMType)
                {
                    case "OpenAI":
                        apiKey = settings.ApiKey;
                        keyPreview = string.IsNullOrEmpty(apiKey) ? "NOT_SET" : apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...";
                        Debug.Log($"[OOJU] Using OpenAI API with key: {keyPreview}");
                        return await CallOpenAIAPI(prompt, apiKey);
                        
                    case "Claude":
                        apiKey = settings.ClaudeApiKey;
                        keyPreview = string.IsNullOrEmpty(apiKey) ? "NOT_SET" : apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...";
                        Debug.Log($"[OOJU] Using Claude API with key: {keyPreview}");
                        return await CallClaudeAPIWithOriginalSettings(prompt, apiKey);
                        
                    case "Gemini":
                        apiKey = settings.GeminiApiKey;
                        keyPreview = string.IsNullOrEmpty(apiKey) ? "NOT_SET" : apiKey.Substring(0, Math.Min(10, apiKey.Length)) + "...";
                        Debug.Log($"[OOJU] Using Gemini API with key: {keyPreview}");
                        return await CallGeminiAPI(prompt, apiKey);
                        
                    default:
                        return "Error: Unknown LLM provider selected.";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error calling LLM API: {ex.Message}");
                return $"Error calling {settings.SelectedLLMType} API: {ex.Message}";
            }
        }
        
        private static async Task<string> CallOpenAIAPI(string prompt, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "Error: OpenAI API key not configured.";
                
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                
                // Manually create JSON string to avoid JsonUtility limitations
                var escapedPrompt = prompt.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                var json = $@"{{
                    ""model"": ""gpt-3.5-turbo"",
                    ""messages"": [
                        {{""role"": ""user"", ""content"": ""{escapedPrompt}""}}
                    ],
                    ""max_tokens"": 500,
                    ""temperature"": 0.7
                }}";
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var responseData = JsonUtility.FromJson<OpenAIResponse>(responseText);
                    return responseData.choices[0].message.content.Trim();
                }
                else
                {
                    Debug.LogError($"OpenAI API error: {responseText}");
                    return $"OpenAI API error: {response.StatusCode}";
                }
            }
        }
        
        // Original Claude API implementation with exact settings from working package
        private static async Task<string> CallClaudeAPIWithOriginalSettings(string prompt, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "Error: Claude API key not configured.";
                
            try
            {
                // Use EXACT settings from original working package
                var escapedPrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                
                // Original model and settings that worked
                var requestBody = "{" +
                    "\"model\":\"claude-opus-4-20250514\"," +
                    "\"max_tokens\":1024," +
                    "\"messages\":[" +
                        "{\"role\":\"user\",\"content\":\"" + escapedPrompt + "\"}" +
                    "]" +
                "}";
                
                Debug.Log($"[OOJU] Claude API (Original Settings) Body: {requestBody}");
                
                // Use UnityWebRequest exactly as in original
                using (var request = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST"))
                {
                    byte[] bodyData = Encoding.UTF8.GetBytes(requestBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyData);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    
                    // Headers exactly as in original package
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-api-key", apiKey);
                    request.SetRequestHeader("anthropic-version", "2023-06-01");
                    
                    Debug.Log($"[OOJU] Claude API (Original Settings) Headers:");
                    Debug.Log($"[OOJU] - x-api-key: {apiKey.Substring(0, Math.Min(15, apiKey.Length))}...");
                    Debug.Log($"[OOJU] - anthropic-version: 2023-06-01");
                    
                    // Send and wait for completion 
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Delay(50);
                    }
                    
                    var responseText = request.downloadHandler.text;
                    var responseCode = request.responseCode;
                    
                    Debug.Log($"[OOJU] Claude API (Original Settings) Response Code: {responseCode}");
                    Debug.Log($"[OOJU] Claude API (Original Settings) Response: {responseText}");
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var responseData = JsonUtility.FromJson<ClaudeResponse>(responseText);
                            if (responseData?.content != null && responseData.content.Length > 0 &&
                                !string.IsNullOrEmpty(responseData.content[0].text))
                            {
                                return responseData.content[0].text.Trim();
                            }
                            else
                            {
                                Debug.LogError($"Claude API (Original Settings): Invalid response structure");
                                return "Error: Invalid response structure from Claude API.";
                            }
                        }
                        catch (System.ArgumentException ex)
                        {
                            Debug.LogError($"Claude API (Original Settings): JSON parsing failed: {ex.Message}");
                            return "Error: Failed to parse Claude API response.";
                        }
                    }
                    else
                    {
                        Debug.LogError($"Claude API (Original Settings) Error {responseCode}: {responseText}");
                        return $"Claude API error {responseCode}: {GetErrorMessage(responseText)}";
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Claude API (Original Settings) Exception: {ex.Message}");
                return $"Error: Unexpected error calling Claude API: {ex.Message}";
            }
        }

        private static async Task<string> CallClaudeAPI(string prompt, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "Error: Claude API key not configured.";
                
            try
            {
                // Use original model name from working package
                var escapedPrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                
                // Use original settings from working package
                var requestBody = "{" +
                    "\"model\":\"claude-opus-4-20250514\"," +
                    "\"max_tokens\":1024," +
                    "\"messages\":[" +
                        "{\"role\":\"user\",\"content\":\"" + escapedPrompt + "\"}" +
                    "]" +
                "}";
                
                Debug.Log($"[OOJU] Claude API UnityWebRequest Body: {requestBody}");
                
                // Use UnityWebRequest instead of HttpClient
                using (var request = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST"))
                {
                    // Set request body
                    byte[] bodyData = Encoding.UTF8.GetBytes(requestBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyData);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    
                    // Set headers exactly as Claude API documentation specifies
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-api-key", apiKey);
                    request.SetRequestHeader("anthropic-version", "2023-06-01");
                    
                    Debug.Log($"[OOJU] Claude API UnityWebRequest Headers:");
                    Debug.Log($"[OOJU] - Content-Type: application/json");
                    Debug.Log($"[OOJU] - x-api-key: {apiKey.Substring(0, Math.Min(15, apiKey.Length))}...");
                    Debug.Log($"[OOJU] - anthropic-version: 2023-06-01");
                    
                    // Send request asynchronously
                    var operation = request.SendWebRequest();
                    
                    // Wait for completion
                    while (!operation.isDone)
                    {
                        await Task.Delay(50);
                    }
                    
                    var responseText = request.downloadHandler.text;
                    var responseCode = request.responseCode;
                    
                    Debug.Log($"[OOJU] Claude API UnityWebRequest Response Code: {responseCode}");
                    Debug.Log($"[OOJU] Claude API UnityWebRequest Response: {responseText}");
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // Parse simple response
                        try
                        {
                            var responseData = JsonUtility.FromJson<ClaudeResponse>(responseText);
                            if (responseData?.content != null && responseData.content.Length > 0 &&
                                !string.IsNullOrEmpty(responseData.content[0].text))
                            {
                                return responseData.content[0].text.Trim();
                            }
                            else
                            {
                                Debug.LogError($"Claude API UnityWebRequest: Invalid response structure");
                                return "Error: Invalid response structure from Claude API.";
                            }
                        }
                        catch (System.ArgumentException ex)
                        {
                            Debug.LogError($"Claude API UnityWebRequest: JSON parsing failed: {ex.Message}");
                            return "Error: Failed to parse Claude API response.";
                        }
                    }
                    else
                    {
                        Debug.LogError($"Claude API UnityWebRequest Error {responseCode}: {responseText}");
                        Debug.LogError($"Claude API UnityWebRequest Result: {request.result}");
                        Debug.LogError($"Claude API UnityWebRequest Error: {request.error}");
                        
                        // Check for specific error types
                        if (responseText.Contains("credit balance") || responseText.Contains("insufficient_credits"))
                        {
                            return "Error: Claude API credit balance too low. Please add credits to your Anthropic account.";
                        }
                        else if (responseText.Contains("invalid") && responseText.Contains("key"))
                        {
                            return "Error: Invalid Claude API key. Please verify your API key in Settings.";
                        }
                        else if (responseText.Contains("rate_limit"))
                        {
                            return "Error: Claude API rate limit exceeded. Please try again later.";
                        }
                        else
                        {
                            return $"Claude API UnityWebRequest error {responseCode}: {GetErrorMessage(responseText)}";
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Claude API UnityWebRequest Exception: {ex.Message}");
                return $"Error: Unexpected error calling Claude API: {ex.Message}";
            }
        }
        
        private static async Task<string> CallGeminiAPI(string prompt, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "Error: Gemini API key not configured.";
                
            using (var httpClient = new HttpClient())
            {
                // Escape prompt more carefully for JSON
                var escapedPrompt = prompt
                    .Replace("\\", "\\\\")  // Escape backslashes first
                    .Replace("\"", "\\\"")  // Escape quotes
                    .Replace("\r\n", "\\n") // Handle Windows line endings
                    .Replace("\n", "\\n")   // Handle Unix line endings
                    .Replace("\r", "\\n")   // Handle Mac line endings
                    .Replace("\t", "\\t");  // Handle tabs
                
                var json = $@"{{
                    ""contents"": [
                        {{
                            ""parts"": [
                                {{""text"": ""{escapedPrompt}""}}
                            ]
                        }}
                    ],
                    ""generationConfig"": {{
                        ""maxOutputTokens"": 1000,
                        ""temperature"": 0.7,
                        ""topK"": 40,
                        ""topP"": 0.8
                    }}
                }}";
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
                
                try
                {
                    var response = await httpClient.PostAsync(url, content);
                    var responseText = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var responseData = JsonUtility.FromJson<GeminiResponse>(responseText);
                            if (responseData?.candidates != null && responseData.candidates.Length > 0 &&
                                responseData.candidates[0]?.content?.parts != null && responseData.candidates[0].content.parts.Length > 0)
                            {
                                return responseData.candidates[0].content.parts[0].text.Trim();
                            }
                            else
                            {
                                Debug.LogError($"Gemini API: Invalid response structure: {responseText}");
                                return "Error: Invalid response structure from Gemini API.";
                            }
                        }
                        catch (System.ArgumentException ex)
                        {
                            Debug.LogError($"Gemini API: JSON parsing error: {ex.Message}\nResponse: {responseText}");
                            return "Error: Failed to parse Gemini API response.";
                        }
                    }
                    else
                    {
                        Debug.LogError($"Gemini API error {response.StatusCode}: {responseText}");
                        return $"Gemini API error: {response.StatusCode} - {GetErrorMessage(responseText)}";
                    }
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    Debug.LogError($"Gemini API network error: {ex.Message}");
                    return "Error: Network error connecting to Gemini API.";
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Gemini API unexpected error: {ex.Message}");
                    return $"Error: {ex.Message}";
                }
            }
        }
        
        private static string GetErrorMessage(string responseText)
        {
            try
            {
                // Try to extract error message from response
                if (responseText.Contains("\"message\""))
                {
                    int start = responseText.IndexOf("\"message\"") + 10;
                    int end = responseText.IndexOf("\"", start + 1);
                    if (end > start)
                    {
                        return responseText.Substring(start + 1, end - start - 1);
                    }
                }
                return "API request failed";
            }
            catch
            {
                return "API request failed";
            }
        }
        
        // Response classes for JSON deserialization
        [System.Serializable]
        private class OpenAIResponse
        {
            public Choice[] choices;
        }
        
        [System.Serializable]
        private class Choice
        {
            public Message message;
        }
        
        [System.Serializable]
        private class Message
        {
            public string content;
        }
        
        [System.Serializable]
        private class ClaudeResponse
        {
            public ContentItem[] content;
        }
        
        [System.Serializable]
        private class ContentItem
        {
            public string text;
        }
        
        // Tool response classes
        [System.Serializable]
        private class ClaudeToolResponse
        {
            public ToolContentItem[] content;
        }
        
        [System.Serializable]
        private class ToolContentItem
        {
            public string type;
            public string text;
            public string name;
            public object input;
        }
        
        [System.Serializable]
        private class ToolInput
        {
            public string response;
        }
        
        [System.Serializable]
        private class GeminiResponse
        {
            public Candidate[] candidates;
        }
        
        [System.Serializable]
        private class Candidate
        {
            public Content content;
        }
        
        [System.Serializable]
        private class Content
        {
            public Part[] parts;
        }
        
        [System.Serializable]
        private class Part
        {
            public string text;
        }
    }
} 