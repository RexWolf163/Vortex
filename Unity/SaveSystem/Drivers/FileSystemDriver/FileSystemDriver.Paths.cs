using System.IO;
using Vortex.Unity.FileSystem.Bus;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        /// <summary>
        /// Путь к папке хранения сейвов.
        /// </summary>
        private static string GetSavesDirectory() =>
            Path.Combine(FileBus.GetAppPath(), SavesFolder);

        /// <summary>
        /// Полный путь к файлу тела сейва.
        /// </summary>
        private static string GetSaveFilePath(string guid) =>
            Path.Combine(GetSavesDirectory(), $"{guid}{SaveExtension}");

        /// <summary>
        /// Полный путь к файлу summary сейва.
        /// </summary>
        private static string GetSummaryFilePath(string guid) =>
            Path.Combine(GetSavesDirectory(), $"{guid}{SummaryExtension}");

        /// <summary>
        /// Полный путь к файлу инкремента.
        /// </summary>
        private static string GetIncrementFilePath() =>
            Path.Combine(GetSavesDirectory(), IncrementFile);

        /// <summary>
        /// Гарантирует существование папки сейвов.
        /// </summary>
        private static void EnsureSavesDirectory() => FileBus.CreateFolders(GetSavesDirectory());
    }
}