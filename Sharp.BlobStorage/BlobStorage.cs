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

namespace Sharp.BlobStorage
{
    /// <summary>
    ///   Base class for blob storage providers.
    /// </summary>
    public abstract class BlobStorage : IBlobStorage
    {
        private const string DefaultBaseUri = "blob:///";

        /// <summary>
        ///   Creates a new <see cref="BlobStorage"/> instance with the
        ///   specified configuration.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        public BlobStorage(BlobStorageConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
        }

        /// <inheritdoc/>
        public virtual Stream Get(Uri uri)
        {
            using (new AsyncScope())
                return GetAsync(uri).Result;
        }

        /// <inheritdoc/>
        public abstract Task<Stream> GetAsync(Uri uri);

        /// <inheritdoc/>
        public virtual Uri Put(Stream stream, string extension = null)
        {
            using (new AsyncScope())
                return PutAsync(stream, extension).Result;
        }

        /// <inheritdoc/>
        public abstract Task<Uri> PutAsync(Stream stream, string extension = null);
    }
}
