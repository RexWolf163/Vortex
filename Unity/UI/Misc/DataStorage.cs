using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Object = System.Object;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Хранилище данных.
    /// Может хранить НЕСКОЛЬКО объектов одновременно.
    /// При запросе данных вернется ПЕРВЫЙ подходящий по типу по принципу FIFO
    /// </summary>
    public class DataStorage : MonoBehaviour, IDataStorage
    {
        [SerializeField]
        [InfoBox("$GetContent", "VisibleIf")]
        [InfoBubble("Переключает свитчер по факту наличия-отсутствия данных в контейнере\n<b>Опционально</b>")]
        [StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher dataSwitcher;

        private List<Object> _data = new();
        public event Action OnUpdateLink;

        private void OnEnable() => dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);

        public void SetData(Object data)
        {
            _data.Clear();
            _data.Add(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
            OnUpdateLink?.Invoke();
        }

        public void SetData(Object[] data)
        {
            _data.Clear();
            if (data is { Length: > 0 })
                _data.AddRange(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
            OnUpdateLink?.Invoke();
        }

        public void AddData(Object data)
        {
            foreach (var o in _data.ToArray())
            {
                if (o.GetType() != data.GetType())
                    continue;
                _data.Remove(o);
            }

            _data.Add(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
        }

        public T GetData<T>() where T : class => _data.FirstOrDefault(o => o is T) as T;

        /// <summary>
        /// Возвращает TRUE, если имеется хотя бы один NULL элемент в данных
        /// либо данных вообще нет
        /// Используется для проверки "отзыва данных".
        /// По этому сигналу производится переключение состояния UIStateSwitcher
        /// (показать/скрыть UI, включить/выключить элемент).
        /// </summary>
        private bool IsEmpty()
        {
            if (_data == null || _data.Count == 0)
                return true;
            foreach (var o in _data)
                if (o == null)
                    return true;

            return false;
        }

#if UNITY_EDITOR

        private string GetContent() => string.Join('\n', _data?.Select(o => o != null ? GetTypeLabel(o, 0) : "[NULL]"));

        private string GetTypeLabel(object o, int depth)
        {
            if (o == null) return "[NULL]";
            if (depth > 3) return "...";

            var type = o.GetType();
            var name = type.Name;
            var indent = new string(' ', depth * 2);

            switch (o)
            {
                case IntData intData:
                    return $"{indent}{name}: {intData.Value}";
                case FloatData floatData:
                    return $"{indent}{name}: {floatData.Value}";
                case BoolData boolData:
                    return $"{indent}{name}: {(boolData.Value ? "TRUE" : "FALSE")}";
                case StringData stringData:
                    return $"{indent}{name}: «{Truncate(stringData.Value)}»";
                case string str:
                    return $"{indent}string: «{Truncate(str)}»";
                case int or float or double or long or bool or byte or short:
                    return $"{indent}{name}: {o}";
                case Enum e:
                    return $"{indent}{name}: {e}";
            }

            var lines = new List<string> { $"{indent}<b>{name}</b>" };
            var childIndent = new string(' ', (depth + 1) * 2);
            const System.Reflection.BindingFlags pub = System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.Instance;

            foreach (var field in type.GetFields(pub))
            {
                var value = field.GetValue(o);
                lines.Add($"{childIndent}{FormatMember(field.Name, value, depth + 1)}");
            }

            foreach (var prop in type.GetProperties(pub))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                try
                {
                    var value = prop.GetValue(o);
                    lines.Add($"{childIndent}{FormatMember(prop.Name, value, depth + 1)}");
                }
                catch
                {
                    /* getter threw */
                }
            }

            return string.Join('\n', lines);
        }

        private string FormatMember(string memberName, object value, int depth)
        {
            if (value == null) return $"{memberName}: [NULL]";

            switch (value)
            {
                case IntData d: return $"{memberName}: {d.Value}";
                case FloatData d: return $"{memberName}: {d.Value}";
                case BoolData d: return $"{memberName}: {(d.Value ? "TRUE" : "FALSE")}";
                case StringData d: return $"{memberName}: «{Truncate(d.Value)}»";
                case string s: return $"{memberName}: «{Truncate(s)}»";
                case int or float or double or long or bool or byte or short:
                    return $"{memberName}: {value}";
                case Enum e: return $"{memberName}: {e}";
            }

            if (depth > 3) return $"{memberName}: ...";
            return $"{memberName}:\n{GetTypeLabel(value, depth)}";
        }

        private static string Truncate(string s, int max = 12)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s[..max];
        }

        private bool VisibleIf() => App.GetState() == AppStates.None;
#endif
    }
}