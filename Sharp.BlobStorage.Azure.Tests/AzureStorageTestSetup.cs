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

using NUnit.Framework;

namespace Sharp.BlobStorage.Azure
{
    [SetUpFixture]
    public class AzureStorageTestSetup
    {
        private bool _emulatorWasRunning;

        [OneTimeSetUp]
        public void SetUp()
        {
            _emulatorWasRunning = AzureStorageEmulator.IsRunning;

            if (!_emulatorWasRunning)
                AzureStorageEmulator.Start();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (!_emulatorWasRunning)
                AzureStorageEmulator.Stop();
        }
    }
}
