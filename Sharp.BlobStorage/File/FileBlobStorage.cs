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
        public FileBlobStorage(FileBlobStorageConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (configuration.Path == null)
                throw new ArgumentNullException("configuration.Path");
            if (configuration.Path.Length == 0)
                throw new ArgumentOutOfRangeException("configuration.Path");
            if (configuration.ReadBufferSize < 1)
                throw new ArgumentOutOfRangeException("configuration.ReadBufferSize");
            if (configuration.WriteBufferSize < 1)
                throw new ArgumentOutOfRangeException("configuration.WriteBufferSize");

            _readBufferSize  = configuration.ReadBufferSize  ?? DefaultBufferSize;
            _writeBufferSize = configuration.WriteBufferSize ?? DefaultBufferSize;
            _basePath        = Directory.CreateDirectory(configuration.Path).FullName;
            _baseUri         = new Uri(_basePath);

            //Log.Information("Using file-based blob storage at '{0}'.", _basePath);
        }

        /// <inheritdoc />
        public override Task<Stream> GetAsync(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (!uri.IsFile)
                throw new ArgumentOutOfRangeException(nameof(uri));

            var relative = _baseUri.MakeRelativeUri(uri);
            if (relative.IsAbsoluteUri)
                throw new ArgumentOutOfRangeException(nameof(uri));

            return Task.FromResult(OpenFileForRead(uri.LocalPath));
        }

        /// <inheritdoc />
        public override async Task<Uri> PutAsync(Stream stream, string extension = null)
        {
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
                return new Uri(realPath);
            }
            finally
            {
                // Make best effort to clean up, but do not allow an exception
                // thrown here to obscure any exception from the try block.
                try { File_.Delete(tempPath); } catch { }
            }
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
    }
}
