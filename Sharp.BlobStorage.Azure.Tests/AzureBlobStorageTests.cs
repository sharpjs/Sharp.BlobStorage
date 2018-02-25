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
using NUnit.Framework;

namespace Sharp.BlobStorage.Azure
{
    [TestFixture]
    public class AzureBlobStorageTests
    {
        const string TestText = "Testing, testing, one two three.";

        private AzureBlobStorageConfiguration Configuration;

        [SetUp]
        public void SetUp()
        {
            Configuration = new AzureBlobStorageConfiguration
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName    = "blob-storage-tests",
            };
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
        public void GetAsync_NullUri()
        {
            var storage = new AzureBlobStorage(Configuration);

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await storage.GetAsync(null);
            });
        }
    }
}
