using System.Text;

namespace PD2Shared.Utils
{
    public static class Env
    {
        static Env()
        {
            // In theory this could return null (https://learn.microsoft.com/en-us/dotnet/api/system.environment.processpath#remarks),
            // but it's unlikely on Windows as a running process should have an associated handle open, preventing the file from being altered.
            ProcessPath = Environment.ProcessPath!;

            ProcessFileName = Path.GetFileName(ProcessPath);
            // ProcessPath will never be null, empty nor a root directory
            ProcessDirPath = Path.GetDirectoryName(ProcessPath)!;

            // Retrieves system-default ANSI encoding for non-Unicode programs. Will correctly return UTF-8 when forced system-wide.
            // (Taken from https://stackoverflow.com/a/70258850)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            AnsiEncoding = Encoding.GetEncoding(0);
        }

        public static string ProcessFileName { get; }
        public static string ProcessPath { get; }
        public static string ProcessDirPath { get; }

        public static Encoding AnsiEncoding { get; }

        public static string GetCwd()
        {
            // While this method isn't much on its own, it's used for consistency
            return Environment.CurrentDirectory;
        }

        public static string GetLauncherFilesRootDirPath()
        {
            return Path.Combine(GetCwd(), "launcher.files");
        }

        public static Exception? CheckIfDirectoryIsWritable(string directoryPath)
        {
            var filePath = Path.Combine(directoryPath, Path.GetRandomFileName());

            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 0,
                    FileOptions.DeleteOnClose);
            }
            catch(Exception ex)
            {
                return ex;
            }

            return null;
        }

        public static void EnsureDirectoryExists(string fileFullPath)
        {
            ArgumentNullException.ThrowIfNull(fileFullPath, nameof(fileFullPath));

            if (!Path.IsPathFullyQualified(fileFullPath))
            {
                throw new ArgumentException("Must be a fully qualified path", nameof(fileFullPath));
            }

            string? dirPath = Path.GetDirectoryName(fileFullPath);

            // Skip when in root directory
            if (dirPath != null)
            {
                Directory.CreateDirectory(dirPath);
            }
        }

        public static async Task<bool> FileExistsAsync(string path)
        {
            return await Task.Run(() =>
            {
                return File.Exists(path);
            }).ConfigureAwait(false);
        }

        public static Tuple<long?, Exception?> TryGetFileSize(string path)
        {
            long? fileSize = null;
            Exception? exception = null;

            try
            {
                fileSize = new FileInfo(path).Length;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            return Tuple.Create(fileSize, exception);
        }

        public static async Task<Tuple<long?, Exception?>> TryGetFileSizeAsync(string path)
        {
            return await Task.Run(() => TryGetFileSize(path)).ConfigureAwait(false);
        }
    }
}
