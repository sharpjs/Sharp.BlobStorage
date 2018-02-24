using System;
using System.IO;
using System.Threading.Tasks;

namespace Sharp.BlobStorage
{
    /// <summary>
    ///   Base class for blob storage providers.
    /// </summary>
    public abstract class BlobStorage : IBlobStorage
    {
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
