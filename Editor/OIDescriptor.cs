using System.Threading.Tasks;

namespace OOJUPlugin
{
    public static class OIDescriptor
    {
        public static Task<string> GenerateSceneDescription()
        {
            return Task.FromResult("Dummy scene description.");
        }
        
        public static Task<string> RequestLLMInteraction(string prompt)
        {
            return Task.FromResult("Dummy LLM interaction result.");
        }
    }
} 