using System.IO;

namespace Sharp.BlobStorage.File
{
    using File = System.IO.File;

    // Implements strategy pattern for filesystem operations.  This allows
    // tests to override methods to induce failure modes that are difficult to
    // achieve.
    internal class FileSystem
    {
        public static FileSystem Default { get; } = new FileSystem();

        protected FileSystem() { }

        public virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public virtual Stream OpenFileForRead(string path, int bufferSize)
        {
            return new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize, useAsync: true
            );
        }

        public virtual Stream OpenFileForWrite(string path, int bufferSize)
        {
            return new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize, useAsync: true
            );
        }

        public virtual void MoveFile(string sourcePath, string targetPath)
        {
            File.Move(sourcePath, targetPath);
        }

        public virtual void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public virtual string CreateDirectory(string path)
        {
            return Directory.CreateDirectory(path).FullName;
        }

        public virtual void DeleteDirectory(string path)
        {
            Directory.Delete(path); 
        }
    }
}
