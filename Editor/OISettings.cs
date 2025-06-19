using UnityEngine;

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
                    _instance = ScriptableObject.CreateInstance<OISettings>();
                }
                return _instance;
            }
        }
        public string SelectedLLMType = "OpenAI";
        public string ApiKey = "";
        public string ClaudeApiKey = "";
        public string GeminiApiKey = "";
        public void SaveSettings() { }
    }
} 