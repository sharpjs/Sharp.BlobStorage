/*
    Copyright 2020 Jeffrey Sharp

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
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Sharp.BlobStorage.Internal;

namespace Sharp.BlobStorage.Azure
{
    /// <summary>
    ///   A blob storage provider that saves blobs in a blob container in an
    ///   Azure storage account.
    /// </summary>
    public class AzureBlobStorage : BlobStorage
    {
        private readonly BlobContainerClient _container;

        /// <summary>
        ///   Creates a new <see cref="AzureBlobStorage"/> instance with the
        ///   specified configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="configuration"/> is invalid.
        /// </exception>
        public AzureBlobStorage(AzureBlobStorageConfiguration configuration)
            : base(configuration)
        {
            if (configuration.ConnectionString == null)
                throw new ArgumentNullException("configuration.ConnectionString");
            if (configuration.ContainerName == null)
                throw new ArgumentNullException("configuration.ContainerName");

            _container = new BlobContainerClient(
                configuration.ConnectionString,
                configuration.ContainerName
            );

            //Log.Information("Using Azure blob storage: {0}", _container.Uri);
            _container.CreateIfNotExists();
        }

        /// <inheritdoc/>
        public override Task<Stream> GetAsync(Uri uri)
        {
            var name = GetBlobName(uri); // also validates uri
            var blob = _container.GetBlobClient(name);

            //Log.Information("Downloading blob: {0}", name);
            return blob.OpenReadAsync(DownloadOptions);
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
            var blob = _container.GetBlobClient(name);

            //Log.Information("Uploading blob: {0}", name);
            await blob.UploadAsync(stream, UploadOptions);

            // NOTE: Azure SDK percent-encodes the slashes in `name`, but this
            // library returns URIs with slashes unencoded.  Therefore, do not
            // use `blob.Uri` here; construct the URI explicitly.
            return new Uri(BaseUri, blob.Name);
        }

        /// <inheritdoc/>
        public override async Task<bool> DeleteAsync(Uri uri)
        {
            var name = GetBlobName(uri); // also validates uri
            var blob = _container.GetBlobClient(name);

            //Log.Information("Deleting blob: {0}", name);
            var response = await blob.DeleteIfExistsAsync();

            return response.Value;
        }

        private static string GenerateFileName(string extension)
            => RandomFileNames.Next(separator: '/', extension);

        private string GetBlobName(Uri uri)
            => uri.ToRelative(BaseUri).ToString();

        // Default client options include:
        // - Timeout after 100 seconds for individual network operations
        // - Up to 3 retries
        // - Initial retry delay of  0.8 seconds, increasing exponentially
        // - Maximum retry delay of 60.0 seconds

        private static readonly BlobUploadOptions
            UploadOptions = new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions
                {
                    IfNoneMatch = ETag.All                  // Do not overwrite blob with same name
                },
                TransferOptions = new StorageTransferOptions
                {
                    MaximumConcurrency  = 1,                // Concurrent uploads for this instance
                    InitialTransferSize = 2 * 1024 * 1024,  // 2 MiB threshold for multi-block upload
                    MaximumTransferSize = 1 * 1024 * 1024   // 1 MiB block size in multi-block upload
                }
            };

        private static readonly BlobOpenReadOptions
            DownloadOptions = new BlobOpenReadOptions(allowModifications: false)
            {
                BufferSize = 1 * 1024 * 1024                // 1 MiB block size for download
            };
    }
}
