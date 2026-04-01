using System.IO;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Vortex.Unity.FileSystem.Controllers;
#endif

namespace Vortex.Unity.FileSystem.Bus
{
    public static class File
    {
        /// <summary>
        /// Путь к основному контейнеру для хранения файлов на устройстве
        /// </summary>
        private static string _path;

        private static void MakeAppPath()
        {
            var root = Application.dataPath.Split('/', '\\');
            root = root[.. ^1];
            root[0] += Path.DirectorySeparatorChar;
            root[^1] = "_OutputFiles";
            _path = Path.Combine(root);
#if UNITY_ANDROID && !UNITY_EDITOR
            _path = AndroidPathResolver.GetAndroidPath();
#endif
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Init() => MakeAppPath();

        /// <summary>
        /// Возвращает основной контейнер для хранения файлов на устройстве
        /// </summary>
        /// <returns></returns>
        public static string GetAppPath()
        {
            if (_path == null)
                MakeAppPath();
            return _path ??= "";
        }

        /// <summary>
        /// Создать папки по указанному в переменной пути
        /// </summary>
        /// <param name="directory"></param>
        public static void CreateFolders(string directory)
        {
            if (Directory.Exists(directory))
                return;
            Directory.CreateDirectory(directory);
            Debug.Log($"[FileSystemController] Create folders for root {directory}");
        }
    }
}