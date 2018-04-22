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
using Sharp.BlobStorage.Internal;

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
            const int ContainerNameMaxLength = 63;

            var name
                = "test-"
                + TestContext.CurrentContext.Test.MethodName
                    .ToLowerInvariant()
                    .Replace('_', '-');

            if (name.Length > ContainerNameMaxLength)
                name = name.Substring(0, ContainerNameMaxLength);

            Configuration = new AzureBlobStorageConfiguration
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName    = name,
            };

            Account   = CloudStorageAccount.Parse(Configuration.ConnectionString);
            Client    = Account.CreateCloudBlobClient();
            Container = Client.GetContainerReference(Configuration.ContainerName);

            Container.DeleteIfExists();
            Container.Create();
        }

        [TearDown]
        public void TearDown()
        {
            Container.DeleteIfExists();
        }

        [Test]
        public void Construct_NullConfiguration()
        {
            this.Invoking(_ => new AzureBlobStorage(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Construct_NullConfigurationConnectionString()
        {
            var configuration = Configuration.Clone();
            configuration.ConnectionString = null;

            this.Invoking(_ => new AzureBlobStorage(configuration))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Construct_NullConfigurationContainerName()
        {
            var configuration = Configuration.Clone();
            configuration.ContainerName = null;

            this.Invoking(_ => new AzureBlobStorage(configuration))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public async Task GetAsync()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/file.txt");

            await WriteBlobAsync("a/file.txt");

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

            storage
                .Awaiting(s => s.GetAsync(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void GetAsync_RelativeUri()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri("relative/file.txt", UriKind.Relative);

            storage
                .Awaiting(s => s.GetAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void GetAsync_NotMyUri()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri("some://other/base/file.txt");

            storage
                .Awaiting(s => s.GetAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void GetAsync_NotFound()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "does/not/exist.txt");

            storage
                .Awaiting(s => s.GetAsync(uri))
                .Should().Throw<StorageException>();
        }

        [Test]
        [TestCase( "txt")]
        [TestCase(".txt")]
        public async Task PutAsync(string extension)
        {
            var storage = new AzureBlobStorage(Configuration);
            var bytes   = Utf8.GetBytes(TestText);

            Uri uri;
            using (var stream = new MemoryStream(bytes))
                uri = await storage.PutAsync(stream, extension);

            uri              .Should().NotBeNull();
            uri              .Should().Match<Uri>(u => storage.BaseUri.IsBaseOf(u));
            uri.AbsolutePath .Should().EndWith(".txt");

            var blobUri = BlobUri(uri, storage);
            var text    = await ReadBlobAsync(blobUri);
            text.Should().Be(TestText);
        }

        [Test]
        public void PutAsync_NullStream()
        {
            var storage = new AzureBlobStorage(Configuration);

            storage
                .Awaiting(s => s.PutAsync(null, ".dat"))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void PutAsync_UnreadableStream()
        {
            var storage = new AzureBlobStorage(Configuration);
            var stream  = Mock.Of<Stream>(s => s.CanRead == false);

            storage
                .Awaiting(s => s.PutAsync(stream, ".dat"))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public async Task DeleteAsync_Exists()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            await WriteBlobAsync("a/b/file.txt");
            await WriteBlobAsync("a/other.txt" );

            (await BlobExistsAsync ("a/b/file.txt"))
                .Should().BeTrue("blob should exist prior to deletion");

            var result = await storage.DeleteAsync(uri);

            result.Should().BeTrue();

            (await BlobExistsAsync("a/b/file.txt"))
                .Should().BeFalse("blob should have been deleted");

            (await BlobExistsAsync("a/other.txt"))
                .Should().BeTrue("other blob should NOT have been deleted");
        }

        [Test]
        public async Task DeleteAsync_DoesNotExist()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            var result = await storage.DeleteAsync(uri);

            result.Should().BeFalse();
        }

        [Test]
        public void DeleteAsync_NullUri()
        {
            var storage = new AzureBlobStorage(Configuration);

            storage
                .Awaiting(s => s.DeleteAsync(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void DeleteAsync_RelativeUri()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri("relative/file.txt", UriKind.Relative);

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void DeleteAsync_NotMyUri()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri("some://other/base/file.txt");

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        // Blob helpers

        private Uri BlobUri(Uri uri, AzureBlobStorage storage)
            => uri
                .ChangeBase(
                    storage.BaseUri,
                    Container.Uri.EnsurePathTrailingSlash()
                );

        private Task<bool> BlobExistsAsync(string path)
            => Container
                .GetBlockBlobReference(path)
                .ExistsAsync();

        private Task<string> ReadBlobAsync(Uri uri)
            => new CloudBlockBlob(uri, Account.Credentials)
                .DownloadTextAsync(Utf8, null, null, null);

        private Task WriteBlobAsync(string path)
            => Container
                .GetBlockBlobReference(path)
                .UploadTextAsync(TestText, Utf8, null, null, null);
    }
}
