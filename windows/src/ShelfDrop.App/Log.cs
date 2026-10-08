using System;
using System.IO;

namespace ShelfDrop.App
{
    /// <summary>
    /// A small log file, <c>%LocalAppData%\ShelfDrop\shelfdrop.log</c>. Nothing about the shelf's content goes in it: only what
    /// happened (a shake, a drop, an error), so that a report of "it did not open" can be looked into without a debugger.
    /// </summary>
    internal static class Log
    {
        private const long MaxBytes = 256 * 1024;
        private static readonly object Gate = new object();

        public static string FilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShelfDrop", "shelfdrop.log");

        public static void Info(string message) => Write("INFO ", message);

        public static void Error(string message, Exception? exception = null) =>
            Write("ERROR", exception is null ? message : message + " | " + exception.GetType().Name + ": " + exception.Message);

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                    {
                        File.Copy(FilePath, FilePath + ".old", overwrite: true);
                        File.Delete(FilePath);
                    }
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + level + " " + message + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // A log that cannot be written must never take the app down.
            }
        }
    }
}
