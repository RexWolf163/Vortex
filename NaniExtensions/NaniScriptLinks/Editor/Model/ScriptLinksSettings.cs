using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor.Model
{
    /// <summary>Что исключение выводит из анализа</summary>
    internal enum ExclusionScope
    {
        /// <summary>Папка: все сценарии под путём вместе с их метками. Запись оканчивается на <c>/</c></summary>
        Folder,

        /// <summary>Сценарий вместе с его метками</summary>
        Script,

        /// <summary>Одна метка: <c>{сценарий}.{метка}</c></summary>
        Label
    }

    /// <summary>
    /// Исключение: точки входа под целью не попадают в «Сироты» и «Неиспользуемые метки». Битые ссылки не исключаются.
    /// Причина обязательна — через полгода видно, почему исключение есть.
    /// </summary>
    [Serializable]
    internal sealed class ScriptExclusion
    {
        /// <summary>Путь в формате реестра: <c>DEBUG/</c>, <c>DLC1/GYMIntro</c> или <c>DLC1/GYMIntro.play</c></summary>
        public string target = string.Empty;

        public string comment = string.Empty;

        public ExclusionScope Scope => GetScope(target);

        /// <summary>Папка — оканчивается на <c>/</c>; метка — есть точка (в пути сценария Naninovel точки нет)</summary>
        public static ExclusionScope GetScope(string target)
        {
            if (target.EndsWith("/", StringComparison.Ordinal))
                return ExclusionScope.Folder;
            return target.IndexOf('.') >= 0 ? ExclusionScope.Label : ExclusionScope.Script;
        }
    }

    /// <summary>
    /// Настройки валидатора связей — файл в <c>ProjectSettings</c>: только редактор, в контроле версий, общий для
    /// команды. По умолчанию список исключений пуст. Изменения — только через методы настроек, окно лишь передаёт
    /// действие пользователя.
    /// </summary>
    [FilePath("ProjectSettings/NaniScriptLinks.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ScriptLinksSettings : ScriptableSingleton<ScriptLinksSettings>
    {
        [SerializeField] private List<ScriptExclusion> exclusions = new();

        public IReadOnlyList<ScriptExclusion> Exclusions => exclusions;

        /// <summary>Растёт при каждом изменении списка — признак для пересчёта анализа</summary>
        public int Revision { get; private set; }

        /// <summary>
        /// Добавить исключение. Пустая цель или причина не принимаются; запись на ту же цель обновляет причину.
        /// Возвращает true, если список изменён.
        /// </summary>
        public bool Add(string target, string comment)
        {
            target = target?.Trim();
            comment = comment?.Trim();
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(comment))
                return false;

            var existing = exclusions.Find(x => x.target == target);
            if (existing != null)
                existing.comment = comment;
            else
                exclusions.Add(new ScriptExclusion { target = target, comment = comment });
            Changed();
            return true;
        }

        /// <summary>Сменить причину; пустая причина не принимается</summary>
        public bool SetComment(ScriptExclusion exclusion, string comment)
        {
            comment = comment?.Trim();
            if (string.IsNullOrEmpty(comment) || !exclusions.Contains(exclusion) || exclusion.comment == comment)
                return false;
            exclusion.comment = comment;
            Changed();
            return true;
        }

        public void Remove(ScriptExclusion exclusion)
        {
            if (exclusions.Remove(exclusion))
                Changed();
        }

        private void Changed()
        {
            Revision++;
            Save(true);
        }
    }
}
