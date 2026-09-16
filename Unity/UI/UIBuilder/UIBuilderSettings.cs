using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.UI.UIBuilder.Base;

namespace Vortex.Unity.UI.UIBuilder
{
    /// <summary>
    /// Настройки UIBuilder в проекте: по одному <see cref="UIBuilderModuleSettings"/> на каждый модуль.
    /// Файл лежит в <c>ProjectSettings/</c> — вне <c>Assets</c>, в сборку не попадает, хранится в VCS.
    /// Редактируется на странице <c>Project Settings → Vortex/UIBuilder</c>.
    /// </summary>
    [FilePath("ProjectSettings/VortexUIBuilderSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class UIBuilderSettings : ScriptableSingleton<UIBuilderSettings>
    {
        private const string LogPrefix = "[UIBuilder]";

        [SerializeReference] private List<UIBuilderModuleSettings> modules = new();

        internal UIBuilderModuleSettings GetFor(Type settingsType)
        {
            var settings = modules.FirstOrDefault(m => m != null && m.GetType() == settingsType);
            if (settings != null)
                return settings;

            Sync();
            return modules.First(m => m.GetType() == settingsType);
        }

        internal void SaveToDisk() => Save(true);

        /// <summary>
        /// Приводит список к найденным модулям: добавляет недостающие настройки, удаляет записи без модуля
        /// (класс удалён или не найден) и дубликаты. Каждое удаление — в лог.
        /// </summary>
        internal void Sync()
        {
            var changed = false;

            if (SerializationUtility.HasManagedReferencesWithMissingTypes(this))
            {
                SerializationUtility.ClearAllManagedReferencesWithMissingTypes(this);
                Debug.Log($"{LogPrefix} Удалены настройки модулей, классы которых не найдены.");
                changed = true;
            }

            if (modules.RemoveAll(m => m == null) > 0)
                changed = true;

            var required = new HashSet<Type>(UIBuilderController.CreateModules().Select(m => m.SettingsType));
            var present = new HashSet<Type>();
            var kept = new List<UIBuilderModuleSettings>();
            foreach (var settings in modules)
            {
                var type = settings.GetType();
                if (required.Contains(type) && present.Add(type))
                {
                    kept.Add(settings);
                    continue;
                }

                Debug.Log(required.Contains(type)
                    ? $"{LogPrefix} Удалён дубликат настроек {type.Name}."
                    : $"{LogPrefix} Удалены настройки {type.Name}: модуль не найден.");
            }

            if (kept.Count != modules.Count)
            {
                modules = kept;
                changed = true;
            }

            foreach (var type in required.Where(t => !present.Contains(t)))
            {
                modules.Add((UIBuilderModuleSettings)Activator.CreateInstance(type));
                changed = true;
            }

            if (changed)
                SaveToDisk();
        }
    }
}
