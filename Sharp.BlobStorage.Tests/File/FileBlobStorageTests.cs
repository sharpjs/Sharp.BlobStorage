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
using Moq;
using NUnit.Framework;
using Sharp.BlobStorage.Internal;
using File_ = System.IO.File;

namespace Sharp.BlobStorage.File
{
    [TestFixture]
    [SingleThreaded] // Becuase PutAsync_FileSystemChanged needs to damage the repository.
    public class FileBlobStorageTests
    {
        const string TestText = "Testing, testing, one two three.";

        private FileBlobStorageConfiguration Configuration;

        private static readonly Encoding Utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false, // no BOM
            throwOnInvalidBytes:             true
        );

        [SetUp]
        public void SetUp()
        {
            var context = TestContext.CurrentContext;

            var path = Path.Combine(
                context.WorkDirectory, "Blobs", context.Test.MethodName
            );

            Configuration = new FileBlobStorageConfiguration
            {
                Path            = path,
                ReadBufferSize  = 8192,
                WriteBufferSize = 4096,
            };
        }

        [Test]
        public void Construct_NullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new FileBlobStorage(null);
            });
        }

        [Test]
        public void Construct_NullConfigurationPath()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration());
            });
        }

        [Test]
        public void Construct_EmptyConfigurationPath()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path = string.Empty
                });
            });
        }

        [Test]
        public void Construct_InvalidConfigurationPath()
        {
            Assert.Throws(Is.AssignableTo<Exception>(), () =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path = "<*> not a valid path <*>"
                });
            });
        }

        [Test]
        public void Construct_ConfigurationReadBufferSizeOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path           = Configuration.Path,
                    ReadBufferSize = -1,
                });
            });
        }

        [Test]
        public void Construct_ConfigurationWriteBufferSizeOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new FileBlobStorage(new FileBlobStorageConfiguration
                {
                    Path            = Configuration.Path,
                    WriteBufferSize = -1,
                });
            });
        }

        [Test]
        public async Task GetAsync()
        {
            var storage = new FileBlobStorage(Configuration);
            var path    = Path.Combine(Configuration.Path, "file.txt");
            var uri     = new Uri(storage.BaseUri, "file.txt");

            WriteFile(path);

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
            var storage = new FileBlobStorage(Configuration);

            Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                return storage.GetAsync(null);
            });
        }

        [Test]
        public void GetAsync_RelativeUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri("relative/file.txt", UriKind.Relative);

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_NotMyUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(@"some://other/base/file.txt");

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        public void GetAsync_NotFound()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "does/not/exist.txt");

            Assert.ThrowsAsync(Is.AssignableTo<IOException>(), () =>
            {
                return storage.GetAsync(uri);
            });
        }

        [Test]
        [TestCase("txt")]
        [TestCase(".txt")]
        public async Task PutAsync(string extension)
        {
            var storage = new FileBlobStorage(Configuration);
            var bytes   = Utf8.GetBytes(TestText);

            Uri uri;
            using (var stream = new MemoryStream(bytes))
                uri = await storage.PutAsync(stream, extension);

            uri              .Should().NotBeNull();
            uri              .Should().Match<Uri>(u => storage.BaseUri.IsBaseOf(u));
            uri.AbsolutePath .Should().EndWith(".txt");

            var realBaseUri = new Uri(Configuration.Path).EnsurePathTrailingSlash();
            var path        = uri.ChangeBase(storage.BaseUri, realBaseUri).LocalPath;
            ReadFile(path).Should().Be(TestText);
        }

        [Test]
        public void PutAsync_NullStream()
        {
            var storage = new FileBlobStorage(Configuration);

            Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                return storage.PutAsync(null, ".dat");
            });
        }

        [Test]
        public void PutAsync_UnreadableStream()
        {
            var storage = new FileBlobStorage(Configuration);
            var stream  = Mock.Of<Stream>(s => s.CanRead == false);

            Assert.ThrowsAsync<ArgumentException>(() =>
            {
                return storage.PutAsync(stream, ".dat");
            });
        }

        [Test]
        public void PutAsync_FileSystemChanged()
        {
            try
            {
                var storage = new FileBlobStorage(Configuration);
                var bytes   = Utf8.GetBytes(TestText);

                Directory.Delete(Configuration.Path, recursive: true);
                File_.WriteAllText(Configuration.Path, "same name as desired directory");

                using (var stream = new MemoryStream(bytes))
                    Assert.ThrowsAsync(
                        Is.AssignableTo<IOException>().And.Message.Contains("with the same name already exists"),
                        () => storage.PutAsync(stream, ".txt")
                    );
            }
            finally
            {
                DeleteFile(Configuration.Path);
            }
        }

        [Test]
        public async Task DeleteAsync_Exists()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, @"a/b/file.txt");

            CreateDirectory (@"a\b");
            WriteFile       (@"a\b\file.txt");
            WriteFile       (@"a\other.txt");

            var result = await storage.DeleteAsync(uri);

            result.Should().BeTrue();

            FileExists      (@"a\b\file.txt") .Should().BeFalse("file should have been deleted");
            FileExists      (@"a\other.txt")  .Should().BeTrue ("other file should NOT have been deleted");
            DirectoryExists (@"a\b")          .Should().BeFalse("empty subdirectory should have been deleted");
            DirectoryExists (@"a")            .Should().BeTrue ("non-empty subdirectory should NOT have been deleted");
            DirectoryExists (@"")             .Should().BeTrue ("repository base directory should NOT have been deleted");
        }

        [Test]
        public async Task DeleteAsync_DoesNotExist()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, @"a/b/file.txt");

            var result = await storage.DeleteAsync(uri);

            result.Should().BeFalse();
        }

        [Test]
        public void DeleteAsync_NullUri()
        {
            var storage = new FileBlobStorage(Configuration);

            storage
                .Awaiting(s => s.DeleteAsync(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void DeleteAsync_RelativeUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri("relative/file.txt", UriKind.Relative);

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void DeleteAsync_NotMyUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(@"some://other/base/file.txt");

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        private bool FileExists(string path)
            => File_.Exists(Path.Combine(Configuration.Path, path));

        private void WriteFile(string path)
            => File_.WriteAllText(Path.Combine(Configuration.Path, path), TestText, Utf8);

        private string ReadFile(string path)
            => File_.ReadAllText(Path.Combine(Configuration.Path, path), Utf8);

        private void DeleteFile(string path)
            => File_.Delete(Path.Combine(Configuration.Path, path));

        private bool DirectoryExists(string path)
            => Directory.Exists(Path.Combine(Configuration.Path, path));

        private void CreateDirectory(string path)
            => Directory.CreateDirectory(Path.Combine(Configuration.Path, path));
    }
}
