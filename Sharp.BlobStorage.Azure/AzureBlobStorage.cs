/*
    Copyright (C) 2018 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Sharp.BlobStorage.Internal;

[assembly: InternalsVisibleTo("Sharp.BlobStorage.Azure.Tests")]

namespace Sharp.BlobStorage.Azure
{
    /// <summary>
    ///   A blob storage provider that saves blobs in a blob container in an
    ///   Azure storage account.
    /// </summary>
    public class AzureBlobStorage : BlobStorage
    {
        private readonly CloudStorageAccount _account;
        private readonly CloudBlobClient     _client;
        private readonly CloudBlobContainer  _container;

        /// <summary>
        ///   Creates a new <see cref="AzureBlobStorage"/> instance with the
        ///   specified configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        public AzureBlobStorage(AzureBlobStorageConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _account = CloudStorageAccount.Parse(configuration.ConnectionString);

            _client = _account.CreateCloudBlobClient();
            _client.DefaultRequestOptions = RequestOptions;

            _container = _client.GetContainerReference(configuration.ContainerName);

            //Log.Information("Using Azure blob storage: {0}", _container.Uri);

            using (new AsyncScope())
                _container.CreateIfNotExistsAsync().Wait();
        }

        /// <inheritdoc/>
        public override Task<Stream> GetAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (!uri.IsAbsoluteUri)
                throw new ArgumentException($"The URI {uri} is not an absolute URI.", nameof(uri));
            if (!_container.Uri.IsBaseOf(uri))
                throw new ArgumentException($"The URI '{uri}' is not contained by the storage provider.");

            var blob = new CloudBlockBlob(uri, _account.Credentials);
            blob.StreamMinimumReadSizeInBytes = DownloadBlockSizeInBytes;
            return blob.OpenReadAsync();
        }

        /// <inheritdoc/>
        public override async Task<Uri> PutAsync(Stream stream, string extension = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("The stream is not readable.", nameof(stream));

            // Ensure extension is dot-prefixed
            if (extension?.Length > 0 && extension[0] != '.')
                extension = "." + extension;

            var name = GenerateFileName(extension);

            //Log.Information("Uploading blob: {0}", name);

            var blob = _container.GetBlockBlobReference(name);
            blob.StreamWriteSizeInBytes = UploadBlockSizeInBytes;

            await blob.UploadFromStreamAsync(
                stream,
                AccessCondition.GenerateIfNotExistsCondition(),
                options:          null,
                operationContext: null
            );

            return blob.Uri;
        }

        private static string GenerateFileName(string extension)
            => RandomFileNames.Next(separator: '/', extension);

        private const int
            DownloadBlockSizeInBytes = 1 * 1024 * 1024, // block size in multi-block download
            UploadBlockSizeInBytes   = 1 * 1024 * 1024; // block size in multi-block upload (see below)

        private static readonly BlobRequestOptions
            RequestOptions = new BlobRequestOptions
            {
                // Official documentation:
                // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.storage.blob.blobrequestoptions?view=azure-dotnet
                //
                // Better documentation:
                // https://www.red-gate.com/simple-talk/cloud/platform-as-a-service/azure-blob-storage-part-4-uploading-large-blobs/

                // Minimum upload speeds to prevent timeout:
                // * 68 KB/s for blobs <  2MB
                // * 34 KB/s for blobs >= 2MB

                // Splitting
                ParallelOperationThreadCount     = 1,               // concurrent uploads
                SingleBlobUploadThresholdInBytes = 2 * 1024 * 1024, // threshold for multi-block upload
                //blob.StreamWriteSizeInBytes    = 1 * 1024 * 1024, // block size in multi-block upload

                // Timeouts
                ServerTimeout        = TimeSpan.FromSeconds(30),    // individual request timeout
                MaximumExecutionTime = null,                        // overall operation timeout

                // Retries
                RetryPolicy = new ExponentialRetry(
                    deltaBackoff: TimeSpan.FromSeconds(5),
                    maxAttempts:  3
                ),

                // Hashing
                StoreBlobContentMD5  = true,    // calculate and store hash of uploaded blob
                UseTransactionalMD5  = false,   // do not validate hash of every request; HTTPS does this

                // Defaults taken for other properties:
                // * AbsorbConditionalErrorsOnRetry (not applicable)
                // * DisableContentMD5Validation    (not applicable)
                // * LocationMode                   (not applicable)
            };
    }
}
