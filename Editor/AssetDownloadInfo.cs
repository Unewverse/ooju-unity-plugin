using System;

namespace OojiCustomPlugin
{

    [Serializable]
    public class AssetDownloadInfo
    {
        public string id;
        public string name;
        public string assetType;
        public bool isSelected;
        public bool isDownloaded;

        public AssetDownloadInfo() {}

        public AssetDownloadInfo(string id, string name, string assetType)
        {
            this.id = id;
            this.name = name;
            this.assetType = assetType;
            this.isSelected = false;
            this.isDownloaded = false;
        }
    }
}