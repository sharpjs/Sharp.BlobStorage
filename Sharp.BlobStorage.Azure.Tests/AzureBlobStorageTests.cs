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
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Sharp.BlobStorage.Internal;

namespace Sharp.BlobStorage.Azure
{
    using static FluentActions;

    [TestFixture, NonParallelizable]
    public class AzureBlobStorageTests
    {
        #region SetUp/TearDown

        const string TestText = "Testing, testing, one two three.";

        private static readonly Encoding
            Utf8 = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false, // no BOM
                throwOnInvalidBytes:             true
            );

        private static readonly byte[]
            TestBytes = Utf8.GetBytes(TestText);

        private AzureBlobStorageConfiguration Configuration;
        private BlobContainerClient           Container;

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

            Container = new BlobContainerClient(
                Configuration.ConnectionString,
                Configuration.ContainerName
            );

            Container.DeleteIfExists();
            Container.Create();
        }

        [TearDown]
        public void TearDown()
        {
            Container.DeleteIfExists();
        }

        #endregion
        #region Construct

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

        #endregion
        #region GetAsync

        [Test]
        public async Task GetAsync()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/file.txt");

            await WriteBlobAsync("a/file.txt");

            using var stream = await storage.GetAsync(uri);

            var text = await ReadUtf8Async(stream);

            text.Should().Be(TestText);
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

            async Task<string> GetAsync()
                => await ReadUtf8Async(await storage.GetAsync(uri));

            Awaiting(GetAsync).Should()
                .Throw<RequestFailedException>()
                .Which.Status.Should().Be(404);
        }

        #endregion
        #region PutAsync

        [Test]
        [TestCase( "txt")]
        [TestCase(".txt")]
        public async Task PutAsync(string extension)
        {
            var storage = new AzureBlobStorage(Configuration);

            Uri uri;
            using (var stream = new MemoryStream(TestBytes))
                uri = await storage.PutAsync(stream, extension);

            uri.Should().NotBeNull();
            uri.Should().Match<Uri>(u => storage.BaseUri.IsBaseOf(u));

            uri.AbsolutePath.Should().MatchRegex(
                @"^/[0-9]{4}/[0-9]{4}/[0-9]{8}_[0-9]{6}_[0-9a-fA-F]{8}\.txt$"
            );

            var name = GetBlobName(uri, storage);
            var text = await ReadBlobAsync(name);
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

        #endregion
        #region DeleteAsync

        [Test]
        public async Task DeleteAsync_Exists()
        {
            var storage = new AzureBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            await WriteBlobAsync("a/b/file.txt");
            await WriteBlobAsync("a/other.txt" );

            (await BlobExistsAsync("a/b/file.txt"))
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

        #endregion
        #region Helpers

        private static string GetBlobName(Uri uri, AzureBlobStorage storage)
        {
            return uri.ToRelative(storage.BaseUri).ToString();
        }

        private async Task<bool> BlobExistsAsync(string name)
        {
            var result = await Container.GetBlockBlobClient(name).ExistsAsync();

            return result.Value;
        }

        private async Task<string> ReadBlobAsync(string name)
        {
            var response = await Container.GetBlobClient(name).DownloadAsync();

            return await ReadUtf8Async(response.Value.Content);
        }

        private async Task WriteBlobAsync(string name)
        {
            using var memory = new MemoryStream(TestBytes);

            await Container.GetBlobClient(name).UploadAsync(memory);
        }

        private static async Task<string> ReadUtf8Async(Stream stream)
        {
            using var reader = new StreamReader(
                stream,
                Utf8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: TestText.Length
            );

            return await reader.ReadToEndAsync();
        }

        #endregion
    }
}
