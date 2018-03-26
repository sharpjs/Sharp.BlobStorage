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
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace Sharp.BlobStorage
{
    [TestFixture]
    public class BlobStorageTests
    {
        private class TestConfiguration : BlobStorageConfiguration { }

        [Test]
        public void Put()
        {
            var configuration = new TestConfiguration();
            var storage       = new Mock<BlobStorage>(MockBehavior.Strict, configuration);
            var stream        = new Mock<Stream     >(MockBehavior.Strict);
            var uri           = new Uri("foo://bar/baz");

            storage
                .Setup(s => s.Put(It.IsAny<Stream>(), It.IsAny<string>()))
                .CallBase();

            storage
                .Setup(s => s.PutAsync(stream.Object, ".dat"))
                .ReturnsAsync(uri);

            var result = storage.Object.Put(stream.Object, ".dat");
            
            result.Should().BeSameAs(uri);
        }

        [Test]
        public void Get()
        {
            var configuration = new TestConfiguration();
            var storage       = new Mock<BlobStorage>(MockBehavior.Strict, configuration);
            var stream        = new Mock<Stream     >(MockBehavior.Strict);
            var uri           = new Uri("foo://bar/baz");

            storage
                .Setup(s => s.Get(It.IsAny<Uri>()))
                .CallBase();

            storage
                .Setup(s => s.GetAsync(uri))
                .ReturnsAsync(stream.Object);

            var result = storage.Object.Get(uri);
            
            result.Should().BeSameAs(stream.Object);
        }
    }
}
