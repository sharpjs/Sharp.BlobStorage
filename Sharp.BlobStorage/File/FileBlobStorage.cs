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
using System.Threading.Tasks;
using Sharp.BlobStorage.Internal;
using File_ = System.IO.File;

namespace Sharp.BlobStorage.File
{
    /// <summary>
    ///   A blob storage provider that saves blobs as files in the file system.
    /// </summary>
    public class FileBlobStorage : BlobStorage
    {
        private const int DefaultBufferSize = 1 * 1024 * 1024; // 1 MB

        private readonly string _basePath;
        private readonly Uri    _baseUri;
        private readonly int    _readBufferSize;
        private readonly int    _writeBufferSize;

        /// <summary>
        ///   Creates a new <see cref="FileBlobStorage"/> instance with the
        ///   specified configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="configuration"/> is invalid.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="configuration"/> is invalid.
        /// </exception>
        public FileBlobStorage(FileBlobStorageConfiguration configuration)
            : base(configuration)
        {
            if (configuration.Path == null)
                throw new ArgumentNullException("configuration.Path");
            if (configuration.Path.Length == 0)
                throw new ArgumentException("Value cannot be empty.", "configuration.Path");
            if (configuration.ReadBufferSize < 1)
                throw new ArgumentOutOfRangeException("configuration.ReadBufferSize");
            if (configuration.WriteBufferSize < 1)
                throw new ArgumentOutOfRangeException("configuration.WriteBufferSize");

            _readBufferSize  = configuration.ReadBufferSize  ?? DefaultBufferSize;
            _writeBufferSize = configuration.WriteBufferSize ?? DefaultBufferSize;
            _basePath        = Directory.CreateDirectory(configuration.Path).FullName;

            if (_basePath[_basePath.Length - 1] != Path.DirectorySeparatorChar)
                _basePath += Path.DirectorySeparatorChar;

            _baseUri = new Uri(_basePath);

            //Log.Information("Using file-based blob storage at '{0}'.", _basePath);
        }

        /// <inheritdoc />
        public override Task<Stream> GetAsync(Uri uri)
        {
            uri = uri.ChangeBase(BaseUri, _baseUri); // also validates uri

            return Task.FromResult(OpenFileForRead(uri.LocalPath));
        }

        /// <inheritdoc />
        public override async Task<Uri> PutAsync(Stream stream, string extension = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException(nameof(stream));

            // Ensure extension is dot-prefixed
            if (extension?.Length > 0 && extension[0] != '.')
                extension = "." + extension;

            // Compute paths
            var realPath   = Path.Combine(_basePath, GenerateFileName(extension));
            var tempPath   = Path.ChangeExtension(realPath, ".upl");
            var parentPath = Path.GetDirectoryName(realPath);

            //Log.Information("Writing file: {0}", realPath);

            try
            {
                // Ensure directory exists to contain the file
                Directory.CreateDirectory(parentPath);

                // Write temp file
                using (var target = OpenFileForWrite(tempPath))
                    await stream.CopyToAsync(target, _writeBufferSize);

                // Rename fully-written temp file to final path
                // NOTE: throws if the final path exists
                File_.Move(tempPath, realPath);

                // Convert to 'file:' URI
                return new Uri(realPath).ChangeBase(_baseUri, BaseUri);
            }
            finally
            {
                // Make best effort to clean up, but do not allow an exception
                // thrown here to obscure any exception from the try block.
                try { File_.Delete(tempPath); } catch { }
            }
        }

        /// <inheritdoc />
        public override async Task<bool> DeleteAsync(Uri uri)
        {
            var path = uri
                .ChangeBase(BaseUri, _baseUri) // also validates uri
                .LocalPath;

            // Check for existence first, as File_.Delete does not return any
            // indicator of prior existence.  Does not throw.
            if (!File_.Exists(path))
                return false;

            // Delete the file, plus any directories left empty afterwards
            await DeleteFileAsync(path);
            CleanUpSubdirectories(path);
            return true;
        }

        private Stream OpenFileForRead(string path)
            => new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                _readBufferSize, useAsync: true
            );

        private Stream OpenFileForWrite(string path)
            => new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                _writeBufferSize, useAsync: true
            );

        private static string GenerateFileName(string extension)
            => RandomFileNames.Next(separator: '\\', extension);

        private async Task DeleteFileAsync(string path)
        {
            const int
                RetryLimit  = 3,
                RetryWaitMs = 10 * 1000;

            for (var retries = 0;; retries++)
            {
                try
                {
                    File_.Delete(path);
                    return;
                }
                catch (DirectoryNotFoundException) // : IOException
                {
                    // Consider 'not found' same as deleted
                    return;
                }
                catch (PathTooLongException) // : IOException
                {
                    // Retry would not help
                    throw;
                }
                catch (IOException)
                {
                    // File in use
                    if (retries >= RetryLimit)
                        throw;
                }

                // Allow time for retryable condition to pass
                await Task.Delay(RetryWaitMs);
            }
        }

        private void CleanUpSubdirectories(string path)
        {
            for (;;)
            {
                // Go up to parent directory
                path = Path.GetDirectoryName(path);

                // Stop when the repository base path is reached
                if (!IsSubdirectory(path))
                    return;

                // Delete directory with best effort.  Do not report errors
                // (ex: directory not empty), because the delete operation has
                // succeeded at this point.
                try { Directory.Delete(path); } catch { return; }
            }
        }

        private bool IsSubdirectory(string path)
        {
            const StringComparison Comparison
                = StringComparison.OrdinalIgnoreCase;

            return path.StartsWith(_basePath, Comparison)
                && path.Length > _basePath.Length;
        }
    }
}
