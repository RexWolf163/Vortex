using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SettingsSystem.Model;
using Vortex.Sdk.MapLevels.Interfaces;
using Vortex.Unity.SettingsSystem.Presets;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.MapLevels.Config
{
    /// <summary>
    /// Конфигурация системы управления уровнями карты
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "MapLevelsSettings", menuName = "Vortex/Settings/MapLevels")]
    public sealed class MapLevelsSettings : SettingsPreset
    {
        [SerializeField, Range(1, 16)]
        [Tooltip("Глубина выгрузки. Уровни на дистанции >= unloadDistance от активного выгружаются.")]
        private int unloadDistance = 3;

        [SerializeField, ValueSelector("GetControllerTypes", Placeholder = "— Pick Controller —")]
        [Tooltip("AssemblyQualifiedName реализации IMapLevelsController. " +
                 "Контроллер создаётся в MapLevelsBus при старте приложения.")]
        private string controllerTypeName;

        public MapLevelsConfig MapLevels => new(controllerTypeName, unloadDistance);

        internal string ControllerTypeName => controllerTypeName;

#if UNITY_EDITOR
        /// <summary>
        /// Поставщик типов для ValueSelector — сканирует домены, фильтрует по IMapLevelsController.
        /// Ключ словаря (отображаемое имя) → значение (FullName для надёжной резолвции).
        /// </summary>
        private Dictionary<string, string> GetControllerTypes()
        {
            var dict = new Dictionary<string, string>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IMapLevelsController).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                    var displayName = type.FullName ?? type.Name;
                    if (!dict.ContainsKey(displayName) && !type.FullName.IsNullOrWhitespace())
                        dict[displayName] = type.FullName;
                }
            }

            return dict.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
#endif
    }
}