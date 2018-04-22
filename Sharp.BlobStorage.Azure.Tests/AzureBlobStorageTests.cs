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

            var storage = new AzureBlobStorage(Configuration);
            var uri     = blob.Uri.ChangeBase(Container.Uri.EnsurePathTrailingSlash(), storage.BaseUri);

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
            var uri     = new Uri("some://other/base/file.txt");

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_NotFound()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "does/not/exist.txt");

            Assert.ThrowsAsync(Is.AssignableTo<StorageException>(), () =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        [TestCase("txt")]
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

            var realBaseUri = Container.Uri.EnsurePathTrailingSlash();
            var realBlobUri = uri.ChangeBase(storage.BaseUri, realBaseUri);
            var blob        = new CloudBlockBlob(realBlobUri, Account.Credentials);
            var text        = await blob.DownloadTextAsync(Utf8, null, null, null);
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

        [Test]
        public async Task DeleteAsync_Exists()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = BlobUriFor("a/b/file.txt", storage);

            await WriteBlobAsync("a/b/file.txt");
            await WriteBlobAsync("a/other.txt");

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
            var uri     = BlobUriFor("a/b/file.txt", storage);

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
            var uri     = new Uri(@"some://other/base/file.txt");

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        private Task<bool> BlobExistsAsync(string path)
            => Container
                .GetBlockBlobReference(path)
                .ExistsAsync();

        private Task WriteBlobAsync(string path)
            => Container
                .GetBlockBlobReference(path)
                .UploadTextAsync(TestText, Utf8, null, null, null);

        private Uri BlobUriFor(string path, AzureBlobStorage storage)
            => Container
                .GetBlockBlobReference(path).Uri
                .ChangeBase(Container.Uri.EnsurePathTrailingSlash(), storage.BaseUri);
    }
}
