using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Vortex.Core.MappedParametersSystem.Base;
using Vortex.Core.MappedParametersSystem.Bus;

#if UNITY_EDITOR
using System.Reflection;
using Sirenix.Utilities;
using Vortex.Core.MappedParametersSystem;
#endif

namespace Vortex.Unity.MappedParametersSystem.Base.Preset
{
    /// <summary>
    /// Класс карты параметров
    /// Является платформо-связаннм хранилищем данных схемы параметров
    /// Для переноса в центральную шину преобразуется в ParametersMap
    /// </summary>
    [Serializable]
    public partial class ParametersMapStorage : ScriptableObject
    {
        [SerializeField, ValueDropdown("GetGuidList")]
        internal string guid;

        /// <summary>
        /// Перечень опорных (дефолтных) параметров
        /// </summary>
        [InfoBox("Обнаружены пустые значения!", InfoMessageType.Error, "Error")]
        [BoxGroup("Базовые параметры")]
        [SerializeField]
        internal string[] baseParams;

        [SerializeReference, OnValueChanged("OnListChanged", true), BoxGroup("Производные параметры")]
        internal MappedParameterPreset[] mappedParams;

#if UNITY_EDITOR

        [Button, BoxGroup("Производные параметры")]
        private void Sort()
        {
            var sortList = new List<string>();
            sortList.AddRange(baseParams);

            foreach (var parent in baseParams)
            {
                var temp = mappedParams
                    .Where(p => p != null && p.Parents.Any(l => l.Parent == parent))
                    .Where(p => !sortList.Contains(p.Name))
                    .OrderBy(p => p.Name)
                    .Select(p => p.Name);
                sortList.AddRange(temp);
            }

            foreach (var mappedParam in mappedParams)
                mappedParam?.Sort();

            mappedParams = mappedParams
                .OrderBy(x => sortList.IndexOf(x?.Parents.Length > 0 ? x.Parents[0].Parent : ""))
                .ThenBy(x => x?.Name)
                .ToArray();
        }

        internal List<string> GetParamsNames()
        {
            var result = new List<string>();
            result.AddRange(baseParams);
            foreach (var param in mappedParams)
            {
                if (param == null || param.Name.IsNullOrWhitespace())
                    continue;
                result.Add(param.Name);
            }

            return result;
        }

        private ValueDropdownList<string> GetGuidList()
        {
            var result = new ValueDropdownList<string>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types = assemblies.SelectMany(a =>
                a.GetTypes().Where(t => t != typeof(object)
                                        && !t.IsAbstract
                                        && !t.IsInterface
                                        && typeof(IMappedModel).IsAssignableFrom(t)));
            foreach (var type in types)
                result.Add(type.FullName?.Replace("." + type.Name, "/" + type.Name), type.FullName);

            return result;
        }

        [Button]
        private void ReloadMaps()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

#endif
    }
}