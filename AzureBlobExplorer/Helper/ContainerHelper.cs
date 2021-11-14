using AzureBlobExplorer.Models;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Core.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AzureBlobExplorer.Helper
{
    public static class ContainerHelper
    {
        public static List<CloudBlobContainer> ListContainers(string sasUri)
        {
            List<CloudBlobContainer> results = new List<CloudBlobContainer>
            {
                new CloudBlobContainer(new Uri(sasUri))
            };

            return results;
        }

        public static async Task CreateBlobAsync(string sasUri, string fileName, Stream fileContent)
        {
            CloudBlobContainer container = new CloudBlobContainer(new Uri(sasUri));
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
            Startup.UpProgress.Add(fileName, 0);

            IProgress<StorageProgress> progressHandler = new Progress<StorageProgress>(progress =>
               Startup.UpProgress[fileName] = (int)((float)progress.BytesTransferred / (float)fileContent.Length * 100.0));

            await blob.UploadFromStreamAsync(fileContent, default, default, default, progressHandler, new CancellationToken()).ConfigureAwait(false);
        }

        public static async Task DeleteBlobAsync(string sasUri, string blobUrl)
        {
            if (string.IsNullOrEmpty(blobUrl))
                return;

            CloudBlobContainer container = new CloudBlobContainer(new Uri(sasUri));
            ICloudBlob blobRef = await container.GetBlobReferenceFromServerAsync(blobUrl).ConfigureAwait(false);

            await blobRef.DeleteAsync().ConfigureAwait(false);
        }

        public static async Task<string> DownloadBlobAsync(string sasUri, string blobUrl)
        {
            if (string.IsNullOrEmpty(blobUrl))
                return null;

            CloudBlobContainer container = new CloudBlobContainer(new Uri(sasUri));
            ICloudBlob blob = await container.GetBlobReferenceFromServerAsync(blobUrl).ConfigureAwait(false);

            if (blob.Properties.Length < 20 * 1024 * 1024)
            {
                string tmpPath = Path.GetTempFileName();
                Startup.DownProgress.Add(blobUrl, 100);
                await blob.DownloadToFileAsync(tmpPath, FileMode.Create).ConfigureAwait(false);

                return tmpPath;
            }
            else
                return DownloadLargeBlob(sasUri, blobUrl);
        }

        public static string DownloadLargeBlob(string sasUri, string blobUrl)
        {
            if (string.IsNullOrEmpty(blobUrl))
                return null;

            Startup.DownProgress.Add(blobUrl, 0);

            CloudBlobContainer container = new CloudBlobContainer(new Uri(sasUri));
            var blob = container.GetBlockBlobReference(blobUrl);
            blob.FetchAttributes();

            int segmentSize = 5 * 1024 * 1024; //5 MB chunk
            long blobLengthRemaining = blob.Properties.Length, startPosition = 0;
            string tmpPath = Path.GetTempFileName();
            long blockSize;
            byte[] blobContents;
            do
            {
                blockSize = Math.Min(segmentSize, blobLengthRemaining);
                blobContents = new byte[blockSize];
                using (MemoryStream ms = new MemoryStream())
                {
                    blob.DownloadRangeToStream(ms, startPosition, blockSize);
                    ms.Position = 0;
                    ms.Read(blobContents, 0, blobContents.Length);
                    using FileStream fs = new FileStream(tmpPath, FileMode.OpenOrCreate)
                    {
                        Position = startPosition
                    };
                    fs.Write(blobContents, 0, blobContents.Length);
                }
                startPosition += blockSize;
                blobLengthRemaining -= blockSize;

                Startup.DownProgress[blobUrl] = (int)((float)startPosition / (float)blob.Properties.Length * 100.0);
            }
            while (blobLengthRemaining > 0);

            return tmpPath;
        }

        public static async Task<List<BlobViewModel>> ListBlobsAsync(string prefix, string sasUri)
        {
            prefix += !string.IsNullOrWhiteSpace(prefix) ? "/" : "";

            CloudBlobContainer container = new CloudBlobContainer(new Uri(sasUri));
            List<BlobViewModel> results = new List<BlobViewModel>();

            IEnumerable<IListBlobItem> blobs = container.ListBlobs(prefix, false, BlobListingDetails.None);
            List<IListBlobItem> folders = blobs.Where(b => b as CloudBlobDirectory != null).ToList();

            foreach (var folder in folders)
            {
                results.Add(new BlobViewModel()
                {
                    Uri = folder.Uri,
                    IsDirectory = true
                });
            }

            BlobContinuationToken continuationToken = null;
            do
            {
                var response = await container.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.All, null, continuationToken, new BlobRequestOptions(), new Microsoft.Azure.Storage.OperationContext()).ConfigureAwait(false);
                continuationToken = response.ContinuationToken;
                int count = 0;
                foreach (var file in response.Results)
                {
                    if (((CloudBlob)file).IsDeleted) continue;

                    count = folders.Where(f => file.Uri.ToString().Contains(f.Uri.ToString())).Count();
                    if (count == 0)
                    {
                        results.Add(new BlobViewModel()
                        {
                            Uri = file.Uri,
                            IsDirectory = false,
                            Created = ((CloudBlob)file).Properties.Created,
                            LastModified = ((CloudBlob)file).Properties.LastModified,
                            Size = ((CloudBlob)file).Properties.Length
                        });
                    }
                    count = 0;
                }
            }
            while (continuationToken != null);

            return results;
        }

        public static async Task<bool> ExistsBlobAsync(string sasUri, string blobUrl)
        {
            if (string.IsNullOrEmpty(blobUrl))
                return false;

            CloudBlobContainer container = new CloudBlobContainer(new Uri(sasUri));
            try
            {
                await container.GetBlobReferenceFromServerAsync(blobUrl).ConfigureAwait(false);
                return true;
            }
            catch (Microsoft.Azure.Storage.StorageException)
            {
                return false;
            }
        }

        public static List<SASViewModel> QueryTable(string accountName, string accountKey, string tableName, string query)
        {
            StorageCredentials storageCredentials = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount cloudStorageAccount = new CloudStorageAccount(storageCredentials, true);

            CloudTableClient tableClient = cloudStorageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference(tableName);

            return table.ExecuteQuerySegmentedAsync(new TableQuery<SASViewModel>() { FilterString = query }, null).GetAwaiter().GetResult().ToList();
        }
    }
}