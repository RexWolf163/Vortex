#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.CoreAssetsSystem.Editor;
using Vortex.Unity.FileSystem.Bus;

namespace Vortex.Unity.CoreAssetsSystem
{
    public static class CoreAssetsController
    {
        // Каноничное место Vortex-конфигов — Resources/Settings/. Сюда же SettingsDriver
        // кладёт SettingsPreset-наследники, так что все ассеты конфигурации лежат рядом.
        private const string Path = "Resources/Settings";

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var autoMode = CoreAssetsPreferences.GetCoreAssetAutoCreationMode();
            if (!autoMode)
                return;
            EditorRegister();
        }

        [MenuItem("Tools/Vortex/Debug/Check Core Assets")]
        private static void EditorRegister()
        {
            FileBus.CreateFolders($"{Application.dataPath}/{Path}");

            //Создание ассетов настроек
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var typeList = new List<Type>();
            foreach (var assembly in assemblies)
                try
                {
                    typeList.AddRange(assembly.GetTypes().Where(t =>
                        t.IsSubclassOf(typeof(ScriptableObject))
                        && t.GetInterfaces().Contains(typeof(ICoreAsset))));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            var resources = Resources.LoadAll("")?.Select(x => x.GetType()).ToArray() ??
                            Type.EmptyTypes;
            foreach (var type in typeList)
            {
                if (resources.Contains(type))
                    continue;

                // Guard от затирания настроенных ассетов. Resources.LoadAll видит тип
                // ассета ТОЛЬКО если класс сейчас скомпилирован и загружен в домен:
                // при compile-error в смежной сборке, тогглинге #if-дефайна (SDK on/off)
                // или гонке InitializeOnLoad vs индексация Resources файл на диске есть,
                // но Contains возвращает false — и без этого guard AssetDatabase.CreateAsset
                // перезапишет его пустым ScriptableObject, потеряв все SerializeReference-
                // ссылки, биндинги и т.п. Файловая проверка надёжнее: если ассет уже лежит,
                // не трогаем его в любом случае — настроит пользователь, пересоздадим только
                // когда диск реально пуст.
                var assetPath = $"Assets/{Path}/{type.Name}.asset";
                if (System.IO.File.Exists(assetPath))
                    continue;

                var so = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(so, assetPath);
                Debug.Log($"Create new settings preset {Path}/{type.Name}");
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif