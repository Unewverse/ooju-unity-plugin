using UnityEngine;
using UnityEditor;

namespace OOJUPlugin
{
    public class OISettings : ScriptableObject
    {
        private static OISettings _instance;
        public static OISettings Instance 
        { 
            get 
            {
                if (_instance == null)
                {
                    // Try to load existing settings
                    _instance = LoadSettings();
                    if (_instance == null)
                    {
                        _instance = ScriptableObject.CreateInstance<OISettings>();
                    }
                }
                return _instance;
            }
        }
        
        public string SelectedLLMType = "OpenAI";
        public string ApiKey = "";
        public string ClaudeApiKey = "";
        public string GeminiApiKey = "";
        
        private const string SELECTED_LLM_KEY = "OOJU_SelectedLLMType";
        private const string OPENAI_API_KEY = "OOJU_OpenAI_ApiKey";
        private const string CLAUDE_API_KEY = "OOJU_Claude_ApiKey";
        private const string GEMINI_API_KEY = "OOJU_Gemini_ApiKey";
        
        public void SaveSettings() 
        {
            EditorPrefs.SetString(SELECTED_LLM_KEY, SelectedLLMType);
            EditorPrefs.SetString(OPENAI_API_KEY, ApiKey);
            EditorPrefs.SetString(CLAUDE_API_KEY, ClaudeApiKey);
            EditorPrefs.SetString(GEMINI_API_KEY, GeminiApiKey);
        }
        
        private static OISettings LoadSettings()
        {
            var settings = ScriptableObject.CreateInstance<OISettings>();
            settings.SelectedLLMType = EditorPrefs.GetString(SELECTED_LLM_KEY, "OpenAI");
            settings.ApiKey = EditorPrefs.GetString(OPENAI_API_KEY, "");
            settings.ClaudeApiKey = EditorPrefs.GetString(CLAUDE_API_KEY, "");
            settings.GeminiApiKey = EditorPrefs.GetString(GEMINI_API_KEY, "");
            return settings;
        }
    }
} 