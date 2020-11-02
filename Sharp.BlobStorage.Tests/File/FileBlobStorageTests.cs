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
    public class FileBlobStorageTests
    {
        const string TestText = "Testing, testing, one two three.";

        private static readonly Encoding Utf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false, // no BOM
            throwOnInvalidBytes:             true
        );

        private Mock<FileSystem>             FileSystem;
        private FileBlobStorageConfiguration Configuration;

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

            FileSystem = new Mock<FileSystem> { CallBase = true };
        }

        [TearDown]
        public void TearDown()
        {
            DeleteDirectory(Configuration.Path);
        }

        [Test]
        public void Construct_NullConfiguration()
        {
            this.Invoking(_ => new FileBlobStorage(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Construct_NullConfigurationPath()
        {
            var configuration = new FileBlobStorageConfiguration();

            this.Invoking(_ => new FileBlobStorage(configuration))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void Construct_EmptyConfigurationPath()
        {
            var configuration = new FileBlobStorageConfiguration
            {
                Path = string.Empty
            };

            this.Invoking(c => new FileBlobStorage(configuration))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void Construct_InvalidConfigurationPath()
        {
            var configuration = new FileBlobStorageConfiguration
            {
                Path = "<*> \0 not a valid path \0 <*>"
            };

            this.Invoking(c => new FileBlobStorage(configuration))
                .Should().Throw<Exception>();
        }

        [Test]
        public void Construct_ConfigurationReadBufferSizeOutOfRange()
        {
            var configuration = new FileBlobStorageConfiguration
            {
                Path           = Configuration.Path,
                ReadBufferSize = -1,
            };

            this.Invoking(c => new FileBlobStorage(configuration))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Construct_ConfigurationWriteBufferSizeOutOfRange()
        {
            var configuration = new FileBlobStorageConfiguration
            {
                Path            = Configuration.Path,
                WriteBufferSize = -1,
            };

            this.Invoking(c => new FileBlobStorage(configuration))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public async Task GetAsync()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/file.txt");

            CreateDirectory (@"a");
            WriteFile       (@"a", "file.txt");

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

            storage
                .Awaiting(s => s.GetAsync(null))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void GetAsync_RelativeUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri("relative/file.txt", UriKind.Relative);

            storage
                .Awaiting(s => s.GetAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void GetAsync_NotMyUri()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri("some://other/base/file.txt");

            storage
                .Awaiting(s => s.GetAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void GetAsync_NotFound()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "does/not/exist.txt");

            storage
                .Awaiting(s => s.GetAsync(uri))
                .Should().Throw<IOException>();
        }

        [Test]
        [TestCase( "txt")]
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

            var path = FilePath(uri, storage);
            ReadFile(path).Should().Be(TestText);
        }

        [Test]
        public void PutAsync_NullStream()
        {
            var storage = new FileBlobStorage(Configuration);

            storage
                .Awaiting(s => s.PutAsync(null, ".dat"))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void PutAsync_UnreadableStream()
        {
            var storage = new FileBlobStorage(Configuration);
            var stream  = Mock.Of<Stream>(s => s.CanRead == false);

            storage
                .Awaiting(s => s.PutAsync(stream, ".dat"))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void PutAsync_FileSystemChanged()
        {
            try
            {
                var storage = new FileBlobStorage(Configuration);
                var bytes   = Utf8.GetBytes(TestText);

                DeleteDirectory (@"");
                WriteFile       (@""); // same name as repository directory

                using (var stream = new MemoryStream(bytes))
                {
                    storage
                        .Awaiting(s => s.PutAsync(stream, ".txt"))
                        .Should().Throw<IOException>()
                        .Which.Message.Should().Contain("with the same name already exists");
                }
            }
            finally
            {
                DeleteFile(@"");
            }
        }

        [Test]
        public async Task DeleteAsync_Exists_DirectoryEmpty()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            CreateDirectory (@"a", "b");
            WriteFile       (@"a", "b", "file.txt");
            FileExists      (@"a", "b", "file.txt") .Should().BeTrue("file should exist prior to deletion");

            var result = await storage.DeleteAsync(uri);

            result.Should().BeTrue();

            FileExists      (@"a", "b", "file.txt") .Should().BeFalse("file should have been deleted");
            DirectoryExists (@"a", "b")             .Should().BeFalse("empty subdirectory should have been deleted");
            DirectoryExists (@"a")                  .Should().BeFalse("empty subdirectory should have been deleted");
            DirectoryExists (@"")                   .Should().BeTrue ("repository base directory should NOT have been deleted");
        }

        [Test]
        public async Task DeleteAsync_Exists_DirectoryNotEmpty()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            CreateDirectory (@"a", "b");
            WriteFile       (@"a", "b", "file.txt");
            WriteFile       (@"a", "other.txt");    // <-- additional file
            FileExists      (@"a", "b", "file.txt") .Should().BeTrue("file should exist prior to deletion");
            FileExists      (@"a", "other.txt")     .Should().BeTrue("file should exist");

            var result = await storage.DeleteAsync(uri);

            result.Should().BeTrue();

            FileExists      (@"a", "b", "file.txt") .Should().BeFalse("file should have been deleted");
            FileExists      (@"a", "other.txt")     .Should().BeTrue ("other file should NOT have been deleted");
            DirectoryExists (@"a", "b")             .Should().BeFalse("empty subdirectory should have been deleted");
            DirectoryExists (@"a")                  .Should().BeTrue ("non-empty subdirectory should NOT have been deleted");
            DirectoryExists (@"")                   .Should().BeTrue ("repository base directory should NOT have been deleted");
        }

        [Test]
        public async Task DeleteAsync_DoesNotExist()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            var result = await storage.DeleteAsync(uri);

            result.Should().BeFalse();
        }

        [Test]
        public async Task DeleteAsync_Retry_Succeed()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            storage.FileSystem = FileSystem.Object;

            FileSystem
                .Setup(f => f.FileExists(It.IsAny<string>()))
                .Returns(true);

            FileSystem
                .SetupSequence(f => f.DeleteFile(It.IsAny<string>()))
                .Throws<IOException>()
                .Pass();

            var result = await storage.DeleteAsync(uri);

            result.Should().BeTrue();
        }

        [Test]
        public void DeleteAsync_Retry_Fail()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            storage.FileSystem = FileSystem.Object;

            FileSystem
                .Setup(f => f.FileExists(It.IsAny<string>()))
                .Returns(true);

            FileSystem
                .Setup(f => f.DeleteFile(It.IsAny<string>()))
                .Throws<IOException>();

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<IOException>();
        }

        [Test]
        public async Task DeleteAsync_DirectoryNotFound()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            storage.FileSystem = FileSystem.Object;

            FileSystem
                .Setup(f => f.FileExists(It.IsAny<string>()))
                .Returns(true)
                .Verifiable();

            FileSystem
                .Setup(f => f.DeleteFile(It.IsAny<string>()))
                .Throws<DirectoryNotFoundException>()
                .Verifiable(); // directory deleted after existence check

            var result = await storage.DeleteAsync(uri);

            result.Should().BeTrue();
            FileSystem.Verify();
        }

        [Test]
        public void DeleteAsync_PathTooLong()
        {
            var storage = new FileBlobStorage(Configuration);
            var uri     = new Uri(storage.BaseUri, "a/b/file.txt");

            storage.FileSystem = FileSystem.Object;

            FileSystem
                .Setup(f => f.FileExists(It.IsAny<string>()))
                .Returns(true);

            FileSystem
                .Setup(f => f.DeleteFile(It.IsAny<string>()))
                .Throws<PathTooLongException>(); // should never happen, but coverage

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<PathTooLongException>();
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
            var uri     = new Uri("some://other/base/file.txt");

            storage
                .Awaiting(s => s.DeleteAsync(uri))
                .Should().Throw<ArgumentException>();
        }

        // File helpers

        private string FilePath(Uri uri, FileBlobStorage storage)
            => uri
                .ChangeBase(
                    storage.BaseUri,
                    new Uri(Configuration.Path).EnsurePathTrailingSlash()
                )
                .LocalPath;

        private string MakePath(params string[] path)
            => Path.Combine(Configuration.Path, Path.Combine(path));

        private bool FileExists(params string[] path)
            => File_.Exists(MakePath(path));

        private string ReadFile(params string[] path)
            => File_.ReadAllText(MakePath(path), Utf8);

        private void WriteFile(params string[] path)
            => File_.WriteAllText(MakePath(path), TestText, Utf8);

        private void DeleteFile(params string[] path)
            => File_.Delete(MakePath(path));

        // Directory helpers

        private bool DirectoryExists(params string[] path)
            => Directory.Exists(MakePath(path));

        private void CreateDirectory(params string[] path)
            => Directory.CreateDirectory(MakePath(path));

        private void DeleteDirectory(params string[] path)
        {
            try { Directory.Delete(MakePath(path), recursive: true); }
            catch (DirectoryNotFoundException) { }
        }
    }
}
