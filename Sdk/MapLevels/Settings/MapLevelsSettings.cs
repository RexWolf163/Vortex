using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Sdk.MapLevels.Interfaces;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Sdk.MapLevels.Settings
{
    /// <summary>
    /// Настройки пакета MapLevels. Лежит в Resources/Settings/, автоматически загружается
    /// SettingsDriver и копируется в SettingsModel через CopyFrom.
    /// Доступ из кода: Settings.Data().MapLevelsControllerTypeName, MapLevelsUnloadDistance.
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "MapLevelsSettings", menuName = "Vortex/Settings/MapLevels")]
    public sealed class MapLevelsSettings : SettingsPreset
    {
        [SerializeField, Range(1, 16)]
        [Tooltip("Глубина выгрузки. Уровни на дистанции >= unloadDistance от активного выгружаются.")]
        private int mapLevelsUnloadDistance = 3;

        [SerializeField,
         ValueSelector(nameof(GetControllerTypes), Placeholder = "— Pick Controller —")]
        [Tooltip("AssemblyQualifiedName реализации IMapLevelsController. " +
                 "Контроллер создаётся в MapLevelsBus при старте приложения.")]
        private string mapLevelsControllerTypeName;

        public int MapLevelsUnloadDistance => mapLevelsUnloadDistance;
        public string MapLevelsControllerTypeName => mapLevelsControllerTypeName;

#if UNITY_EDITOR
        /// <summary>
        /// Поставщик типов для ValueSelector — сканирует домены, фильтрует по IMapLevelsController.
        /// Ключ словаря (отображаемое имя) → значение (AssemblyQualifiedName для надёжной резолвции).
        /// </summary>
        private Dictionary<string, string> GetControllerTypes()
        {
            var dict = new Dictionary<string, string>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IMapLevelsController).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    var displayName = type.FullName ?? type.Name;
                    if (!dict.ContainsKey(displayName))
                        dict[displayName] = type.AssemblyQualifiedName;
                }
            }

            return dict.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
#endif
    }
}
