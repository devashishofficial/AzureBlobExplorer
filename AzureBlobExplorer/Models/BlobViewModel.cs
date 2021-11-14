using System;

namespace AzureBlobExplorer.Models
{
    public class BlobViewModel
    {
        public Uri Uri { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }
}