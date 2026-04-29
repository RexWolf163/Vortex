using System.IO;
using AppFile = Vortex.Unity.FileSystem.Bus.File;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        /// <summary>
        /// Путь к папке хранения сейвов.
        /// </summary>
        private static string GetSavesDirectory() =>
            Path.Combine(AppFile.GetAppPath(), SavesFolder);

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
        /// Гарантирует существование папки сейвов.
        /// </summary>
        private static void EnsureSavesDirectory() => AppFile.CreateFolders(GetSavesDirectory());
    }
}
