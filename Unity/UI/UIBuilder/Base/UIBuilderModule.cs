using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.UIBuilder.Base
{
    /// <summary>
    /// Модуль вида элемента (текст, кнопка, …): свои настройки, свой набор секций, свой шаг сборки.
    /// Ядро находит неабстрактных наследников через <c>TypeCache</c> и создаёт для каждого настройки.
    /// Пункт меню модуль объявляет сам — статическим <c>[MenuItem("GameObject/Vortex Primitives/Create …")]</c>, который
    /// вызывает <see cref="UIBuilderController.Open{TModule}"/>. Модуль без состояния, нужен конструктор без параметров.
    /// </summary>
    public abstract class UIBuilderModule
    {
        /// <summary>Тип настроек модуля. Ровно один экземпляр на модуль в <see cref="UIBuilderSettings"/>.</summary>
        public abstract Type SettingsType { get; }

        /// <summary>Название вида элемента (заголовок в Project Settings и окне).</summary>
        public abstract string Title { get; }

        /// <summary>Свежие экземпляры всех секций, доступных модулю. Окно покажет только применимые к примитиву.</summary>
        public abstract IEnumerable<UIBuilderSection> CreateSections();

        /// <summary>
        /// Индивидуальный шаг сборки: вызывается после создания слоя, экземпляра примитива и сбора частей.
        /// По умолчанию применяет секции, у которых после сбора есть хотя бы одна часть нужного типа.
        /// </summary>
        public virtual void Apply(UIComponent component, IReadOnlyList<UIBuilderSection> sections)
        {
            foreach (var section in sections)
            {
                if (component.GetLinks(section.PartType).Length == 0)
                {
                    Debug.LogWarning($"[UIBuilder] {Title}: у «{component.name}» нет частей " +
                                     $"{section.PartType.Name} — секция «{section.Title}» пропущена.", component);
                    continue;
                }

                section.Apply(component);
            }
        }
    }

    /// <inheritdoc />
    public abstract class UIBuilderModule<TSettings> : UIBuilderModule where TSettings : UIBuilderModuleSettings
    {
        public sealed override Type SettingsType => typeof(TSettings);

        protected TSettings Settings => (TSettings)UIBuilderSettings.instance.GetFor(SettingsType);
    }
}
