using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace Vortex.Unity.Extensions
{
    /// <summary>
    /// Утилиты поиска ассетов. Имеет две внутренние реализации, выбираемые по контексту сборки:
    /// <list type="bullet">
    /// <item>
    /// <b>Editor</b> — через <see cref="AssetDatabase.FindAssets(string)"/>: ищет по всему проекту,
    /// независимо от того, лежит ли ассет в <c>Resources/</c>.
    /// </item>
    /// <item>
    /// <b>Runtime</b> — через <see cref="Resources.LoadAll(string,System.Type)"/>: ищет только в
    /// <c>Resources/</c>-папках. Чтобы singleton-ассет был доступен в build, он <b>обязан</b>
    /// лежать под <c>Resources/</c>.
    /// </item>
    /// </list>
    ///
    /// Семантика разнится между Editor и Runtime — это компромисс, не баг. Если проект зависит
    /// от runtime-доступа к singleton-ассету, он должен жить в <c>Resources/</c>. Альтернатива —
    /// Addressables (вне ответственности этой утилиты).
    /// </summary>
    public static class AssetDatabaseExt
    {
        /// <summary>
        /// Находит единственный ассет типа <typeparamref name="T"/>.
        ///
        /// Возврат:
        /// <list type="bullet">
        /// <item>0 ассетов → <c>null</c> без логов.</item>
        /// <item>1 ассет → возвращается.</item>
        /// <item>2+ ассетов → <see cref="Debug.LogError"/> и <c>null</c>.</item>
        /// </list>
        /// </summary>
        /// <param name="canBeChildren">
        /// Если <c>true</c> (по умолчанию) — учитываются и наследники <typeparamref name="T"/>.
        /// Если <c>false</c> — только ассеты с точным типом <typeparamref name="T"/>.
        /// </param>
        public static T GetSingletonAsset<T>(bool canBeChildren = true) where T : Object
        {
#if UNITY_EDITOR
            return ResolveViaAssetDatabase<T>(canBeChildren);
#else
            return ResolveViaResources<T>(canBeChildren);
#endif
        }

#if UNITY_EDITOR
        private static T ResolveViaAssetDatabase<T>(bool canBeChildren) where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            T result = null;
            int count = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue; // не наш тип / повреждённый ассет / stale GUID
                if (!canBeChildren && asset.GetType() != typeof(T)) continue;
                result = asset;
                count++;
            }

            if (count > 1)
            {
                Debug.LogError(
                    $"[AssetDatabaseExt] Найдено {count} ассетов типа {typeof(T).FullName}. Должен быть один.");
                return null;
            }

            return result;
        }
#endif

        private static T ResolveViaResources<T>(bool canBeChildren) where T : Object
        {
            // Пустая строка — корень всех Resources/-папок проекта.
            var assets = Resources.LoadAll<T>(string.Empty);
            T result = null;
            int count = 0;

            foreach (var asset in assets)
            {
                if (asset == null) continue;
                if (!canBeChildren && asset.GetType() != typeof(T)) continue;
                result = asset;
                count++;
            }

            if (count > 1)
            {
                Debug.LogError(
                    $"[AssetDatabaseExt] Найдено {count} ассетов типа {typeof(T).FullName}. Должен быть один.");
                return null;
            }

            return result;
        }
    }
}
