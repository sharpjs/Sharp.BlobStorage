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
using Moq;
using NUnit.Framework;
using static System.IO.File;

namespace Sharp.BlobStorage.File
{
    [TestFixture]
    [SingleThreaded] // Becuase PutAsync_FileSystemChanged needs to damage the repository.
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
        public void Construct_NullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new FileBlobStorage(null);
            });
        }

        [Test]
        public void Construct_NullConfigurationPath()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration());
            });
        }

        [Test]
        public void Construct_EmptyConfigurationPath()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path = string.Empty
                });
            });
        }

        [Test]
        public void Construct_InvalidConfigurationPath()
        {
            Assert.Throws(Is.AssignableTo<Exception>(), () =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path = "<!-- not a valid path -->"
                });
            });
        }

        [Test]
        public void Construct_ConfigurationReadBufferSizeOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path           = Configuration.Path,
                    ReadBufferSize = -1,
                });
            });
        }

        [Test]
        public void Construct_ConfigurationWriteBufferSizeOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path            = Configuration.Path,
                    WriteBufferSize = -1,
                });
            });
        }

        [Test]
        public async Task GetAsync()
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

        [Test]
        public void GetAsync_NullUri()
        {
            var storage = new FileBlobStorage(Configuration);

            Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                return storage.GetAsync(null);
            });
        }

        [Test]
        public void GetAsync_NonFileUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri("https://example.com");

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_RelativeUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri("File.txt", UriKind.Relative);

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_NotMyUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(@"Z:\Some\Other\Path.txt");

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_MaliciousUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(
                new Uri(Configuration.Path),
                new Uri(@"..\..\..\..\Sharp.BlobStorage.sln", UriKind.Relative)
            );

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_NotFound()
        {
            var storage = new FileBlobStorage(Configuration);
            var path    = Path.Combine(Configuration.Path, @"does\not\exist.txt");
            var uri     = new Uri(path);

            Assert.ThrowsAsync(Is.AssignableTo<IOException>(), () =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public async Task PutAsync()
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
        public void PutAsync_NullStream()
        {
            var storage = new FileBlobStorage(Configuration);

            Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                return storage.PutAsync(null, ".dat");
            });
        }

        [Test]
        public void PutAsync_UnreadableStream()
        {
            var storage = new FileBlobStorage(Configuration);
            var stream  = Mock.Of<Stream>(s => s.CanRead == false);

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            {
                return storage.PutAsync(stream, ".dat");
            });
        }

        [Test]
        public void PutAsync_FileSystemChanged()
        {
            try
            {
                var storage = new FileBlobStorage(Configuration);
                var bytes   = Utf8.GetBytes(TestText);

                Directory.Delete(Configuration.Path, recursive: true);
                WriteAllText(Configuration.Path, "same name as desired directory");

                using (var stream = new MemoryStream(bytes))
                    Assert.ThrowsAsync(
                        Is.AssignableTo<IOException>().And.Message.Contains("with the same name already exists"),
                        () => storage.PutAsync(stream, ".txt")
                    );
            }
            finally
            {
                Delete(Configuration.Path);
            }
        }
    }
}
