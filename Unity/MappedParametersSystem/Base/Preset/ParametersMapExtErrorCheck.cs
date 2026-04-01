using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

namespace Vortex.Unity.MappedParametersSystem.Base.Preset
{
    public partial class ParametersMapStorage
    {
#if UNITY_EDITOR

        private bool validated = false;

        private bool Error { get; set; }

        [OnInspectorInit]
        private void OnValidate()
        {
            if (validated)
                return;
            validated = true;

            baseParams ??= new String[0];
            mappedParams ??= new MappedParameterPreset[0];

            foreach (var param in mappedParams)
            {
                if (param == null)
                    continue;

                param.EditorInit(this);
            }
        }

        private void OnListChanged()
        {
            validated = false;
            CheckErrors();
        }

        /// <summary>
        /// Проверка на ошибки 
        /// </summary>
        private void CheckErrors()
        {
            var list = new List<string>();
            var temp = baseParams;
            for (var i = 0; i < temp.Length; i++)
            {
                var n = temp[i];
                if (n.IsNullOrWhitespace())
                    continue;
                if (list.Contains(n))
                {
                    baseParams[i] = AddNumber(baseParams[i]);
                    list.Clear();
                    Debug.LogError($"\u2757 Неуникальное название параметра «{n}»");
                    CheckErrors();
                    return;
                }

                list.Add(n);
            }

            for (var i = 0; i < mappedParams.Length; i++)
            {
                var mappedParam = mappedParams[i];
                if (mappedParam == null)
                {
                    Debug.LogError($"\u2757 Null значение в конфиге");
                    continue;
                }

                var n = mappedParam.Name;
                if (n.IsNullOrWhitespace())
                    continue;
                if (list.Contains(n))
                {
                    mappedParams[i].name = AddNumber(mappedParams[i].name);
                    list.Clear();
                    Debug.LogError($"\u2757 Неуникальное название параметра «{n}»");
                    CheckErrors();
                    return;
                }

                list.Add(n);
            }

            foreach (var mappedParam in mappedParams)
            {
                if (mappedParam == null)
                    continue;

                var ar = mappedParam.Parents.Where(s => !list.Contains(s.Parent)).ToArray();
                foreach (var parameterLink in ar)
                {
                    if (parameterLink is MappedParameterLink p)
                        p.parent = "";
                    Debug.LogError($"\u2757 Несуществующий родитель у параметра {mappedParam.Name}");
                }
            }

            //проверка на цикл
            list.Clear();
            foreach (var mappedParam in mappedParams)
                SearchTop(mappedParam, list);

            Error = baseParams.Contains(string.Empty)
                    || mappedParams.Any(p => p != null
                                             && (p.Name.IsNullOrWhitespace()
                                                 || p.Parents.Any(l => l is { Parent: "" })));
        }

        /// <summary>
        /// Ищет верхнеуровневых родителей
        /// </summary>
        /// <param name="preset"></param>
        /// <param name="road"></param>
        /// <returns></returns>
        private List<string> SearchTop(MappedParameterPreset preset, List<string> road)
        {
            var result = new List<string>();
            if (preset == null)
                return result;
            var levelRoad = new List<string>();
            levelRoad.AddRange(road);
            levelRoad.Add(preset.Name);
            foreach (var parent in preset.Parents)
            {
                if (road.Contains(parent.Parent))
                {
                    if (parent is MappedParameterLink p)
                        p.parent = "";
                    Debug.LogError($"\u2757 Обнаружен цикличный указатель родителей для параметра «{preset.Name}»");
                    continue;
                }

                if (parent.Parent.IsNullOrWhitespace())
                {
                    result.Add(null);
                    continue;
                }

                if (baseParams.Contains(parent.Parent))
                {
                    result.Add(parent.Parent);
                    continue;
                }

                var target = mappedParams.FirstOrDefault(p => p != null && p.Name == parent.Parent);
                result.AddRange(SearchTop(target, levelRoad));
            }

            return result;
        }

        /// <summary>
        /// Увеличивает текстовый номер на 1
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string AddNumber(string name)
        {
            var ar = name.Split('_').ToList();
            var i = 0;
            if (ar.Count == 2)
                if (!Int32.TryParse(ar[^1], out i))
                    return name + $"_1";
            ar.RemoveAt(ar.Count - 1);
            return string.Join('_', ar) + $"_{++i}";
        }
#endif
    }
}