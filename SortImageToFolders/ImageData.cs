using Java.IO;
using System;

namespace SortImageToFolders
{
    internal class ImageData
    {
        public ImageData(string imageName, string imagePath, int status, DateTime lastModifiedData)
        {
            ImageName = imageName;
            ImagePath = imagePath;
            Status = status;
            LastModifiedData = lastModifiedData;
        }

        public string ImageName { get; set; }

        public string ImagePath { get; set; }

        public int Status { get; set; }

        public DateTime LastModifiedData { get; set; }
    }
}