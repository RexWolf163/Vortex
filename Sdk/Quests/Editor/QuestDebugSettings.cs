using System;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using Vortex.Sdk.Quests.Conditions;
#endif

namespace Vortex.Sdk.Quests.Editor
{
    /// <summary>
    /// Настройки монитора квестов (<c>Tools/Vortex/Debug/Quest Monitor</c>). Ассет ищется по типу; без ассета —
    /// ёмкость журнала по умолчанию и подписи условий именем класса.
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex/Quest Debug Settings", fileName = "QuestDebugSettings")]
    public class QuestDebugSettings : ScriptableObject
    {
        public const int DefaultJournalCapacity = 500;

        // Читается только в редакторе; в билде поле остаётся ради формата ассета
#pragma warning disable CS0414
        [SerializeField, Min(1), InfoBox("Применяется при входе в Play Mode")]
        private int journalCapacity = DefaultJournalCapacity;
#pragma warning restore CS0414

        [SerializeField, TableList(AlwaysExpanded = true)]
        [InfoBox("Подпись условия в мониторе. {поле} — значение сериализованного поля условия; " +
                 "неизвестное поле выводится как {поле?}. Нет записи для типа — имя класса.")]
        private ConditionLabel[] conditionLabels = new ConditionLabel[0];

#if UNITY_EDITOR
        private static QuestDebugSettings _instance;

        private Dictionary<string, string> _templates;

        private static readonly Regex Placeholder = new(@"\{(\w+)\}");

        public static QuestDebugSettings GetInstance()
        {
            if (_instance != null) return _instance;

            var guids = AssetDatabase.FindAssets($"t:{nameof(QuestDebugSettings)}");
            if (guids.Length == 0) return null;

            _instance = AssetDatabase.LoadAssetAtPath<QuestDebugSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return _instance;
        }

        public static int GetJournalCapacity()
        {
            var settings = GetInstance();
            return settings != null ? settings.journalCapacity : DefaultJournalCapacity;
        }

        /// <summary>
        /// Подпись условия: шаблон для его типа с подставленными полями, иначе имя класса.
        /// </summary>
        public static string GetConditionLabel(QuestConditionLogic condition)
        {
            if (condition == null)
                return "null";

            var type = condition.GetType();
            var settings = GetInstance();
            if (settings == null || !settings.Templates().TryGetValue(type.FullName, out var template))
                return type.Name;

            return Placeholder.Replace(template, m => FieldText(condition, m.Groups[1].Value));
        }

        private Dictionary<string, string> Templates()
        {
            if (_templates != null)
                return _templates;

            _templates = new Dictionary<string, string>();
            foreach (var label in conditionLabels)
                if (!string.IsNullOrEmpty(label.ConditionType))
                    _templates[label.ConditionType] = label.Template;
            return _templates;
        }

        private void OnValidate() => _templates = null;

        private static string FieldText(object target, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, flags);
                if (field != null)
                    return ValueText(field.GetValue(target));
            }

            return $"{{{fieldName}?}}";
        }

        private static string ValueText(object value) => value switch
        {
            null => "null",
            UnityEngine.Object obj => obj != null ? obj.name : "None",
            string text => text,
            ICollection collection => $"[{collection.Count}]",
            _ => value.ToString()
        };

        internal static IEnumerable<string> ConditionTypeNames() =>
            TypeCache.GetTypesDerivedFrom<QuestConditionLogic>()
                .Where(t => !t.IsAbstract)
                .Select(t => t.FullName)
                .OrderBy(n => n);
#endif
    }

    /// <summary>
    /// Подпись типа условия в мониторе квестов. Тип хранится по полному имени — настройки SDK не ссылаются
    /// на типы проекта.
    /// </summary>
    [Serializable]
    public class ConditionLabel
    {
        [SerializeField, ValueDropdown("ConditionTypes")]
        private string conditionType;

        [SerializeField] private string template;

        public string ConditionType => conditionType;
        public string Template => template;

#if UNITY_EDITOR
        private IEnumerable<string> ConditionTypes() => QuestDebugSettings.ConditionTypeNames();
#endif
    }
}
