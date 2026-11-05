using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model
{
    /// <summary>
    /// Исключение: не показывать переменную в указанных видах потерь. Причина обязательна по смыслу — через полгода
    /// видно, почему исключение есть.
    /// </summary>
    [Serializable]
    internal sealed class VariableExclusion
    {
        public string variableName = string.Empty;

        public LossKinds kinds;

        public string comment = string.Empty;
    }

    /// <summary>
    /// Настройки панели переменных — файл в <c>ProjectSettings</c>: только редактор, в контроле версий, общий для
    /// команды. По умолчанию список исключений пуст.
    /// </summary>
    [FilePath("ProjectSettings/CustomVariablesDashboard.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class DashboardSettings : ScriptableSingleton<DashboardSettings>
    {
        [SerializeField] private List<VariableExclusion> exclusions = new();

        public List<VariableExclusion> Exclusions => exclusions;

        /// <summary>
        /// Виды потерь, исключённые для переменной: объединение всех записей с этим именем (с учётом регистра).
        /// </summary>
        public LossKinds GetExcluded(string variableName)
        {
            var kinds = LossKinds.None;
            foreach (var exclusion in exclusions)
                if (exclusion.variableName == variableName)
                    kinds |= exclusion.kinds;
            return kinds;
        }

        /// <summary>
        /// Исключить вид потери: дополнить первую запись с этим именем или завести новую с пустой причиной.
        /// </summary>
        public void Exclude(string variableName, LossKinds kind)
        {
            foreach (var exclusion in exclusions)
                if (exclusion.variableName == variableName)
                {
                    exclusion.kinds |= kind;
                    Save();
                    return;
                }

            exclusions.Add(new VariableExclusion { variableName = variableName, kinds = kind });
            Save();
        }

        public void Save() => Save(true);
    }
}
