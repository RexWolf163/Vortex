#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.FileSystem.Bus;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.SettingsSystem
{
    public partial class SettingsDriver
    {
        [InitializeOnLoadMethod]
        private static void EditorRegister() => Defer();

        /// <summary>
        /// Отложить создание до устойчивого состояния редактора. <c>InitializeOnLoadMethod</c>
        /// выполняется в середине загрузки домена, а при смене платформы следом идёт массовый
        /// реимпорт: выборка существующих ассетов в этот момент неполна, и уже настроенный ассет
        /// посчитался бы отсутствующим — <c>CreateAsset</c> затёр бы его пустым, с новым guid.
        /// Ждём, пока AssetDatabase перестанет обновляться, перевешивая вызов на следующий тик.
        /// </summary>
        private static void Defer() => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                Defer();
                return;
            }

            CreateMissingPresets();
        };

        private static void CreateMissingPresets()
        {
            FileBus.CreateFolders($"{Application.dataPath}/Resources/{Path}");
            //Создание ассетов настроек
            var assetType = typeof(SettingsPreset);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var typeList = new List<Type>();
            foreach (var assembly in assemblies)
            {
                try
                {
                    typeList.AddRange(assembly.GetTypes());
                }
                catch (ReflectionTypeLoadException e)
                {
                    typeList.AddRange(e.Types.Where(t => t != null));
                }
                catch (Exception e)
                {
                    // Смена платформы: сборка с платформенными зависимостями может не грузиться
                    // целиком (TypeLoadException / FileNotFoundException / BadImageFormatException).
                    // Пропускаем её, а не роняем весь InitializeOnLoad.
                    Debug.LogException(e);
                }
            }
            // Поиск по всему проекту, а не по Resources/<Path>: ассет настроек могли перенести в
            // другую папку, и Resources.LoadAll по фиксированному пути его бы не увидел — вместо
            // «уже есть» получилось бы «нет» и рядом лёг бы дубликат.
            var resources = AssetDatabase.FindAssets($"t:{assetType.Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SettingsPreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(x => x != null)
                .Select(x => x.GetType())
                .ToArray();
            foreach (var type in typeList)
            {
                if (!type.IsSubclassOf(assetType) || resources.Contains(type))
                    continue;
                var so = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(so, $"Assets/Resources/{Path}/{type.Name}.asset");
                Debug.Log($"Create new settings preset {type.Name}");
            }
        }
    }
}
#endif