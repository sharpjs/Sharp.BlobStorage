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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Sharp.BlobStorage.Azure
{
    /// <summary>
    ///   A blob storage provider that saves blobs in a blob container in an
    ///   Azure storage account.
    /// </summary>
    public class AzureBlobStorage : BlobStorage
    {
        /// <inheritdoc/>
        public override Task<Stream> GetAsync(Uri uri)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override Task<Uri> PutAsync(Stream stream, string extension = null)
        {
            throw new NotImplementedException();
        }
    }
}
