using UnityEngine;

namespace Vortex.Unity.FileSystem.Controllers
{
    /// <summary>
    /// Класс для получения пути до хранилища Android
    /// </summary>
    internal static class AndroidPathResolver
    {
        /// <summary>
        /// Токен разрешения на запись в хранилище
        /// </summary>
        private const string PermissionToken = "android.permission.WRITE_EXTERNAL_STORAGE";

        /// <summary>
        /// Возвращает путь до папки Downloads в Android
        /// </summary>
        /// <returns></returns>
        internal static string GetAndroidPath()
        {
            var externalStoragePath = "";
#if UNITY_ANDROID && !UNITY_EDITOR
            CheckPermissions();
            var environmentClass = new AndroidJavaClass("android.os.Environment");
            var externalStorageDirectory =
 environmentClass.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", "Download");
            externalStoragePath = externalStorageDirectory.Call<string>("getAbsolutePath");
#endif
            return externalStoragePath;
        }


        /// <summary>
        /// Проверяет наличие разрешений на сохранение в хранилище на Android
        /// </summary>
        private static void CheckPermissions()
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var permissionStatus = currentActivity.Call<int>("checkSelfPermission", PermissionToken);
            if (permissionStatus == 0) return;
            var permissions = new[] { PermissionToken };
            currentActivity.Call("requestPermissions", permissions, 0);
        }
    }
}