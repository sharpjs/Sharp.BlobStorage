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
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using static System.IO.File;

namespace Sharp.BlobStorage.File
{
    [TestFixture]
    public class FileBlobStorageTests
    {
        const string TestText = "Testing, testing, one two three.";

        private FileBlobStorageConfiguration Configuration;

        private static readonly Encoding Utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false, // no BOM
            throwOnInvalidBytes:             true
        );

        [SetUp]
        public void SetUp()
        {
            var path = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "Blobs"
            );

            Configuration = new FileBlobStorageConfiguration
            {
                Path            = path,
                ReadBufferSize  = 8192,
                WriteBufferSize = 4096,
            };
        }

        [Test]
        public async Task Put()
        {
            var storage = new FileBlobStorage(Configuration);
            var bytes   = Utf8.GetBytes(TestText);

            Uri uri;
            using (var stream = new MemoryStream(bytes))
                uri = await storage.PutAsync(stream, ".txt");

            uri           .Should().NotBeNull();
            uri.IsFile    .Should().BeTrue();
            uri.LocalPath .Should().StartWith(Configuration.Path + @"\");
            uri.LocalPath .Should().EndWith(".txt");

            ReadAllText(uri.LocalPath, Utf8).Should().Be(TestText);
        }

        [Test]
        public async Task Get()
        {
            var storage = new FileBlobStorage(Configuration);
            var path    = Path.Combine(Configuration.Path, "TestBlob.txt");
            var uri     = new Uri(path);

            WriteAllText(path, TestText, Utf8);

            byte[] bytes;

            using (var stream = await storage.GetAsync(uri))
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                bytes = memory.ToArray();
            }

            Utf8.GetString(bytes).Should().Be(TestText);
        }
    }
}
