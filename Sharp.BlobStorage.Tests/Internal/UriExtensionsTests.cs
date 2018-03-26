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
using FluentAssertions;
using NUnit.Framework;

namespace Sharp.BlobStorage.Internal
{
    [TestFixture]
    public class UriExtensionsTests
    {
        private const string
            Example    = "http://example.com/",
            ExampleOld = Example + "old/",
            ExampleNew = Example + "new/",
            ExampleEtc = Example + "etc/",
            NotSlashed = "http://user:pass@example.com:1234/foo/bar?a=1&b=2#c",
            Slashed    = "http://user:pass@example.com:1234/foo/bar/?a=1&b=2#c";

        private static readonly Uri
            NullUri       = null,
            AnyUri        = new Uri(Example,              UriKind.Absolute),
            RelativeUri   = new Uri("relative",           UriKind.Relative),
            OldBaseUri    = new Uri(ExampleOld,           UriKind.Absolute),
            OldFileUri    = new Uri(ExampleOld + "a/b.c", UriKind.Absolute),
            NewBaseUri    = new Uri(ExampleNew,           UriKind.Absolute),
            NewFileUri    = new Uri(ExampleNew + "a/b.c", UriKind.Absolute),
            OtherUri      = new Uri(ExampleEtc + "a/b.c", UriKind.Absolute),
            NotSlashedUri = new Uri(NotSlashed,           UriKind.Absolute),
            SlashedUri    = new Uri(Slashed,              UriKind.Absolute);

        [Test]
        public void EnsurePathTrailingSlash_Null()
        {
            NullUri
                .Invoking(u => u.EnsurePathTrailingSlash())
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void EnsurePathTrailingSlash_Relative()
        {
            RelativeUri
                .Invoking(u => u.EnsurePathTrailingSlash())
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void EnsurePathTrailingSlash_NoSlash()
        {
            NotSlashedUri.EnsurePathTrailingSlash()
                .Should().Be(SlashedUri);
        }

        [Test]
        public void EnsurePathTrailingSlash_ExistingSlash()
        {
            SlashedUri.EnsurePathTrailingSlash()
                .Should().Be(SlashedUri);
        }

        [Test]
        public void ChangeBase_NullUri()
        {
            NullUri
                .Invoking(u => u.ChangeBase(AnyUri, AnyUri))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void ChangeBase_NullOldBaseUri()
        {
            NullUri
                .Invoking(u => AnyUri.ChangeBase(u, AnyUri))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void ChangeBase_NullNewBaseUri()
        {
            NullUri
                .Invoking(u => AnyUri.ChangeBase(AnyUri, u))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void ChangeBase_RelativeUri()
        {
            RelativeUri
                .Invoking(u => u.ChangeBase(AnyUri, AnyUri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void ChangeBase_RelativeOldBaseUri()
        {
            RelativeUri
                .Invoking(u => AnyUri.ChangeBase(u, AnyUri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void ChangeBase_RelativeNewBaseUri()
        {
            RelativeUri
                .Invoking(u => AnyUri.ChangeBase(AnyUri, u))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void ChangeBase_OldBaseMismatch()
        {
            OtherUri
                .Invoking(u => u.ChangeBase(OldBaseUri, NewBaseUri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void ChangeBase_Changed()
        {
            OldFileUri.ChangeBase(OldBaseUri, NewBaseUri)
                .Should().Be(NewFileUri);
        }

        [Test]
        public void ChangeBase_Unchanged()
        {
            NewFileUri.ChangeBase(NewBaseUri, NewBaseUri)
                .Should().Be(NewFileUri);
        }
    }
}
