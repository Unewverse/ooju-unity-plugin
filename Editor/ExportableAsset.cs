using System;

namespace OojiCustomPlugin
{
    [Serializable]
    public class ExportableAsset
    {
        public string id;        
        public string filename;
        public string content_type;
        public string presigned_url;
        public string created_at; 
    }
}