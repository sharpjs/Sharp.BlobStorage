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
using System.Globalization;

namespace Sharp.BlobStorage.Internal
{
    internal static class RandomFileNames
    {
        private static readonly Random
            Random = CreateRandom();

        internal static string Next(char separator = '.', string suffix = null)
        {
            // Format: yyyy/MMdd/yyyyMMdd_HHmmss_xxxxxxxx.ext

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy}{1}{0:MMdd}{1}{0:yyyyMMdd}_{0:HHmmss}_{2:x8}{3}",
                DateTime.UtcNow,
                separator,
                GetRandomInt32(),
                suffix
            );
        }

        private static Random CreateRandom()
        {
            // Random by default uses a time-based seed, which could cause
            // collisions if two processes create their Random simultaneously.
            // To avoid this, obtain a random-ish seed from some existing
            // generator.  Guid is convenient here.

            var seed = Guid.NewGuid().GetHashCode();
            return new Random(seed);
        }

        private static int GetRandomInt32()
        {
            lock (Random)
                return Random.Next();
        }
    }
}
