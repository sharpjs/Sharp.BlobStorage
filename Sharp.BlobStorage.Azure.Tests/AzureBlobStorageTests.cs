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
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using NUnit.Framework;

namespace Sharp.BlobStorage.Azure
{
    [TestFixture]
    public class AzureBlobStorageTests
    {
        const string TestText = "Testing, testing, one two three.";

        private static readonly Encoding Utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false, // no BOM
            throwOnInvalidBytes:             true
        );

        private AzureBlobStorageConfiguration Configuration;
        private CloudStorageAccount           Account;
        private CloudBlobClient               Client;
        private CloudBlobContainer            Container;

        [SetUp]
        public void SetUp()
        {
            Configuration = new AzureBlobStorageConfiguration
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName    = "blob-storage-tests",
            };

            Account   = CloudStorageAccount.Parse(Configuration.ConnectionString);
            Client    = Account.CreateCloudBlobClient();
            Container = Client.GetContainerReference(Configuration.ContainerName);

            Container.DeleteIfExists();
            Container.Create();
        }

        [Test]
        public void Construct_NullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new AzureBlobStorage(null);
            });
        }

        [Test]
        public void Construct_NullConfigurationConnectionString()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var configuration = Configuration.Clone();
                configuration.ConnectionString = null;
                new AzureBlobStorage(configuration);
            });
        }

        [Test]
        public void Construct_NullConfigurationContainerName()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var configuration = Configuration.Clone();
                configuration.ContainerName = null;
                new AzureBlobStorage(configuration);
            });
        }

        [Test]
        public async Task GetAsync()
        {
            var blob = Container.GetBlockBlobReference("test/file.txt");
            await blob.UploadTextAsync(TestText, Utf8, null, null, null);
            var uri = blob.Uri;

            var storage = new AzureBlobStorage(Configuration);

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
            var storage = new AzureBlobStorage(Configuration);

            Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                return storage.GetAsync(null);
            });
        }

        [Test]
        public void GetAsync_RelativeUri()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri("relative/file.txt", UriKind.Relative);

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_NotMyUri()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri("https://example.com/not/my/uri");

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_MaliciousUri()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(Container.Uri, "../other-container/file.txt");

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_NotFound()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(Container.Uri, "does/not/exist.txt");

            Assert.ThrowsAsync(Is.AssignableTo<StorageException>(), () =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public async Task PutAsync()
        {
            var storage = new AzureBlobStorage(Configuration);
            var bytes   = Utf8.GetBytes(TestText);

            Uri uri;
            using (var stream = new MemoryStream(bytes))
                uri = await storage.PutAsync(stream, ".txt");

            uri              .Should().NotBeNull();
            uri              .Should().Match<Uri>(u => Container.Uri.IsBaseOf(u));
            uri.AbsolutePath .Should().EndWith(".txt");

            var blob = new CloudBlockBlob(uri, Account.Credentials);
            var text = await blob.DownloadTextAsync(Utf8, null, null, null);
            text.Should().Be(TestText);
        }

        [Test]
        public void PutAsync_NullStream()
        {
            var storage = new AzureBlobStorage(Configuration);

            Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                return storage.PutAsync(null, ".dat");
            });
        }

        [Test]
        public void PutAsync_UnreadableStream()
        {
            var storage = new AzureBlobStorage(Configuration);
            var stream  = Mock.Of<Stream>(s => s.CanRead == false);

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.PutAsync(stream, ".dat");
            });
        }
    }
}
