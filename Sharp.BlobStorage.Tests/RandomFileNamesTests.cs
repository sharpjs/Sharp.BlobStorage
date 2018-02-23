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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Sharp.BlobStorage.Tests
{
    [TestFixture]
    public class RandomFileNamesTests
    {
        [Test]
        public void Next_Defaults()
        {
            var names = new ConcurrentBag<string>();

            Parallel.For(0, 100, _ => names.Add(RandomFileNames.Next()));

            names.Should().HaveCount(100).And.OnlyHaveUniqueItems();
        }

        [Test]
        public void Next_Separator()
        {
            var names = new ConcurrentBag<string>();

            Parallel.For(0, 100, _ => names.Add(RandomFileNames.Next(separator: '!')));

            names.Should().HaveCount(100).And.OnlyHaveUniqueItems();

            NamesShouldHaveCharAtSameIndexes(names, '!');
        }

        [Test]
        public void Next_Suffix()
        {
            var names = new ConcurrentBag<string>();

            Parallel.For(0, 100, _ => names.Add(RandomFileNames.Next(suffix: "SUFFIX")));

            names.Should().HaveCount(100).And.OnlyHaveUniqueItems();

            names.Should().OnlyContain(n => n.EndsWith("SUFFIX", StringComparison.Ordinal));
        }

        private void NamesShouldHaveCharAtSameIndexes(IEnumerable<string> names, char separator)
        {
            for (var start = 0;;)
            {
                var index = names.First().IndexOf(separator, start);

                names.Should().OnlyContain(
                    predicate: n => n.IndexOf(separator, start) == index,
                    because: "Separators should appear at the same indexes in every generated name."
                );

                if (index < 0) break;
                start = index + 1;
            }
        }
    }
}
