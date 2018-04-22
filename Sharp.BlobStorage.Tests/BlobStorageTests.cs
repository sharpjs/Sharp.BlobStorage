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
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Sharp.BlobStorage
{
    [TestFixture]
    public class BlobStorageTests
    {
        private class TestConfiguration : BlobStorageConfiguration { }

        private class TestStorage : BlobStorage
        {
            public TestStorage(TestConfiguration configuration)
                : base(configuration) { }

            public override Task<Stream> GetAsync(Uri uri)
                => throw new NotImplementedException();

            public override Task<Uri> PutAsync(Stream stream, string extension = null)
                => throw new NotImplementedException();

            public override Task<bool> DeleteAsync(Uri uri)
                => throw new NotImplementedException();
        }

        [Test]
        public void Construct_NullConfiguration()
        {
            var configuration = null as TestConfiguration;

            configuration
                .Invoking(c => new TestStorage(c))
                .Should().ThrowExactly<ArgumentNullException>();
        }

        [Test]
        public void Construct_RelativeBaseUri()
        {
            var configuration = new TestConfiguration
            {
                BaseUri = new Uri("relative", UriKind.Relative)
            };

            configuration
                .Invoking(c => new TestStorage(c))
                .Should().ThrowExactly<ArgumentException>();
        }

        [Test]
        public void Construct_NullBaseUri()
        {
            var expected      = new Uri(BlobStorage.DefaultBaseUri);
            var configuration = new TestConfiguration();

            var storage = new TestStorage(configuration);

            storage.BaseUri.Should().Be(expected);
        }

        [Test]
        public void Construct_NonSlashedBaseUri()
        {
            var original = new Uri("foo://bar/baz",  UriKind.Absolute);
            var expected = new Uri("foo://bar/baz/", UriKind.Absolute);

            var configuration = new TestConfiguration
            {
                BaseUri = original
            };

            var storage = new TestStorage(configuration);

            storage.BaseUri.Should().Be(expected);
        }

        [Test]
        public void Construct_SlashedBaseUri()
        {
            var expected = new Uri("foo://bar/baz/", UriKind.Absolute);

            var configuration = new TestConfiguration
            {
                BaseUri = expected
            };

            var storage = new TestStorage(configuration);

            storage.BaseUri.Should().Be(expected);
        }

        [Test]
        public void Put()
        {
            var configuration = new TestConfiguration();
            var storage       = new Mock<BlobStorage>(MockBehavior.Strict, configuration);
            var stream        = new Mock<Stream     >(MockBehavior.Strict);
            var uri           = new Uri(BlobStorage.DefaultBaseUri + "TestFile.txt");

            storage
                .Setup(s => s.Put(It.IsAny<Stream>(), It.IsAny<string>()))
                .CallBase();

            storage
                .Setup(s => s.PutAsync(stream.Object, ".dat"))
                .ReturnsAsync(uri)
                .Verifiable();

            var result = storage.Object.Put(stream.Object, ".dat");
            
            result.Should().BeSameAs(uri);
            storage.Verify();
        }

        [Test]
        public void Get()
        {
            var configuration = new TestConfiguration();
            var storage       = new Mock<BlobStorage>(MockBehavior.Strict, configuration);
            var stream        = new Mock<Stream     >(MockBehavior.Strict);
            var uri           = new Uri(BlobStorage.DefaultBaseUri + "TestFile.txt");

            storage
                .Setup(s => s.Get(It.IsAny<Uri>()))
                .CallBase();

            storage
                .Setup(s => s.GetAsync(uri))
                .ReturnsAsync(stream.Object)
                .Verifiable();

            var result = storage.Object.Get(uri);
            
            result.Should().BeSameAs(stream.Object);
            storage.Verify();
        }

        [Test]
        public void Delete()
        {
            var configuration = new TestConfiguration();
            var storage       = new Mock<BlobStorage>(MockBehavior.Strict, configuration);
            var uri           = new Uri(BlobStorage.DefaultBaseUri + "TestFile.txt");

            storage
                .Setup(s => s.Delete(It.IsAny<Uri>()))
                .CallBase();

            storage
                .Setup(s => s.DeleteAsync(uri))
                .ReturnsAsync(true)
                .Verifiable();

            var result = storage.Object.Delete(uri);

            result.Should().BeTrue();
            storage.Verify();
        }
    }
}
