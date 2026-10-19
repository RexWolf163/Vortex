using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Presets;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.InputBusSystem.Handlers;
#if USING_VORTEX_CURSOR
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
#endif
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UI.UIComponents.Attributes;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Представление клавиши управления экшена: заполняет связанный <see cref="UIComponent"/> глифом клавиши,
    /// назначенной экшену связанного <see cref="InputActionHandler"/>, и строкой по паттерну.
    ///
    /// Клавиша — первая назначенная клавиша команды среди активных групп-приоритета; в них клавиши нет — первая среди
    /// всех активных групп, в порядке групп конфига (действующая раскладка). Активной клавиши нет — тексты пустые,
    /// спрайтов нет.
    ///
    /// Приоритет — группы последнего устройства ввода (<c>VirtualCursorBus.Data.LastDevice</c>, пакет VirtualCursorSystem):
    /// после геймпада — клавиша геймпада, после клавиатуры или мыши — клавиатуры и мыши (одна группа). Устройство вне
    /// групп (тачскрин, перо) приоритет не меняет. Пока устройство не определено (или VirtualCursorSystem не подключена,
    /// define <c>USING_VORTEX_CURSOR</c>) — <see cref="defaultGroup"/>.
    ///
    /// Глифы (текст-символ и спрайт) берутся из <see cref="RebindGlyphTable"/> — и для основной клавиши, и для
    /// модификаторов. Клавиши нет в таблице — текст по читаемому имени контрола Input System, спрайта нет.
    /// Текст клавиши локализуется по ключу <see cref="localePrefix"/> + текст (по умолчанию документ <c>GlyphLabels</c>):
    /// перевод есть — выводится он, нет — текст как есть; смена языка обновляет вывод.
    ///
    /// Паттерн принадлежит представлению: <c>{0}</c> — текст клавиши, <c>{1}</c> — текст модификаторов, каждый
    /// с разделителем после себя (пусто без модификаторов). Пример: «Прыжок (клавиши {1}{0}) позволит перепрыгнуть
    /// пропасть» → «Прыжок (клавиши Ctrl+A) …».
    ///
    /// Спрайт клавиши — в часть <see cref="keySpritePosition"/> компонента, спрайты модификаторов (система допускает
    /// до двух) — в части <see cref="modifierSpritePosition"/> и <see cref="secondModifierSpritePosition"/>. Слои
    /// модификаторов переключает свитчер компонента в части <see cref="modifiersSwitcherPosition"/>: состояние —
    /// число модификаторов клавиши (0 — нет, 1 — один, 2 — два).
    /// Обновляется при изменении слотов команды, переключении активности групп и пересборке системы.
    /// </summary>
    public class InputActionView : MonoBehaviour
    {
        [SerializeField, AutoLink,
         Tooltip("Хэндлер экшена: его экшен «Карта/Экшен» — команда, клавиша которой выводится.")]
        private InputActionHandler actionHandler;

        [SerializeField, Tooltip("Таблица глифов: текст-символ и спрайт клавиш и модификаторов.")]
        private RebindGlyphTable glyphs;

        [SerializeField, Tooltip("Префикс ключа локализации подписи клавиши: ключ — префикс + текст клавиши (из таблицы " +
                                 "глифов или читаемое имя контрола). Перевод есть — выводится он, нет — текст как есть. " +
                                 "Пусто — подписи не локализуются.")]
        private string localePrefix = "GlyphLabels.";

        [SerializeField, AutoLink] private UIComponent component;

        [SerializeField, ValueDropdown("GroupKeys"),
         Tooltip("Группа-приоритет, пока устройство ввода не определено (ввода через VirtualCursor ещё не было или " +
                 "система курсора не подключена). Не задана — без приоритета: порядок групп конфига.")]
        private string defaultGroup;

        [SerializeField, Tooltip("Паттерн строки: {0} — текст клавиши, {1} — текст модификаторов (каждый с " +
                                 "разделителем после себя, пусто без модификаторов). Проходит локализацию UIComponent.")]
        private string pattern = "{1}{0}";

        [SerializeField, Tooltip("Разделитель после текста каждого модификатора в {1}.")]
        private string modifierSeparator = "+";

        [SerializeField, UIComponentLink(typeof(UIComponentGraphic), "component", "Спрайт клавиши не выводится"),
         Tooltip("Часть-графика компонента для спрайта клавиши. -1 — не выводить.")]
        private int keySpritePosition = -1;

        // Имена условий строкой, не nameof: методы живут под UNITY_EDITOR, в билде имён нет.
        [SerializeField, UIComponentLink(typeof(UIComponentGraphic), "component", "Спрайт модификатора не выводится"),
         InfoBox("Спрайт первого модификатора выводится в ту же часть-графику, что и спрайт клавиши: модификатор " +
                 "перезапишет клавишу. Выберите другую часть или -1.", InfoMessageType.Warning, "ModifierOverlapsKey"),
         Tooltip("Часть-графика компонента для спрайта первого модификатора. -1 — не выводить.")]
        private int modifierSpritePosition = -1;

        [SerializeField,
         UIComponentLink(typeof(UIComponentGraphic), "component", "Спрайт второго модификатора не выводится"),
         InfoBox("Спрайт второго модификатора выводится в ту же часть-графику, что и спрайт клавиши или первого " +
                 "модификатора: перезапишет его. Выберите другую часть или -1.", InfoMessageType.Warning,
             "SecondModifierOverlaps"),
         Tooltip("Часть-графика компонента для спрайта второго модификатора. -1 — не выводить.")]
        private int secondModifierSpritePosition = -1;

        [SerializeField,
         UIComponentLink(typeof(UIComponentSwitcher), "component", "Слои модификаторов не переключаются"),
         Tooltip("Часть-свитчер компонента для слоёв модификаторов: состояние 0 — нет модификаторов, 1 — один, " +
                 "2 — два. -1 — не переключать.")]
        private int modifiersSwitcherPosition = -1;

        /// <summary>Команда «Карта/Экшен» — экшен связанного хэндлера; <c>null</c> — хэндлер не связан.</summary>
        private string Command => actionHandler != null ? actionHandler.InputAction : null;

        /// <summary>Группы-приоритет, с которыми выведена клавиша (последний <see cref="Refresh"/>).</summary>
        private IReadOnlyList<string> _shownPriority = Array.Empty<string>();

#if USING_VORTEX_CURSOR
        /// <summary>
        /// Layout последнего устройства ввода, входящего хотя бы в одну группу, — общий для всех представлений.
        /// Устройство вне групп его не меняет: приоритет остаётся прежним.
        /// </summary>
        private static string _priorityLayout;

        /// <summary>Сброс на старте Play Mode (при выключенной перезагрузке домена статика переживает сессию).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPriority() => _priorityLayout = null;
#endif

        private void OnEnable()
        {
            if (actionHandler == null)
                Debug.LogError("[InputActionView] Не связан InputActionHandler — клавиша не выводится.", this);

            RebindBus.OnSlotsChanged += OnSlotsChanged;
            RebindBus.OnGroupsChanged += Refresh;
            RebindBus.OnRebuilt += Refresh;
            Localization.OnLocalizationChanged += OnLocalizationChanged;
#if USING_VORTEX_CURSOR
            VirtualCursorBus.Data.LastDevice.OnUpdate += OnDeviceChanged;
#endif
            // До загрузки обновит OnRebuilt, который система поднимает по её завершении.
            if (RebindBus.IsReady)
                Refresh();
        }

        private void OnDisable()
        {
            RebindBus.OnSlotsChanged -= OnSlotsChanged;
            RebindBus.OnGroupsChanged -= Refresh;
            RebindBus.OnRebuilt -= Refresh;
            Localization.OnLocalizationChanged -= OnLocalizationChanged;
#if USING_VORTEX_CURSOR
            VirtualCursorBus.Data.LastDevice.OnUpdate -= OnDeviceChanged;
#endif
        }

        /// <summary>Изменились слоты — обновиться, если среди них есть слот своей команды.</summary>
        private void OnSlotsChanged(IReadOnlyList<string> bindKeys)
        {
            var prefix = $"{Command}#";
            if (bindKeys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)))
                Refresh();
        }

#if USING_VORTEX_CURSOR
        /// <summary>Сменилось устройство ввода — обновиться, если сменились группы-приоритет.</summary>
        private void OnDeviceChanged(string layout)
        {
            // До готовности обновит OnRebuilt: Refresh сам подхватит текущее устройство.
            if (!RebindBus.IsReady)
                return;
            TrackDevice(layout);
            if (!_shownPriority.SequenceEqual(PriorityGroups()))
                Refresh();
        }

        /// <summary>Запомнить устройство как приоритетное, если оно входит хотя бы в одну группу.</summary>
        private static void TrackDevice(string layout)
        {
            if (!string.IsNullOrEmpty(layout) && RebindBus.Data.GroupsOf(layout).Count > 0)
                _priorityLayout = layout;
        }
#endif

        /// <summary>
        /// Группы-приоритет: группы последнего устройства ввода; устройство не определено — <see cref="defaultGroup"/>;
        /// она не задана — приоритета нет.
        /// </summary>
        private IReadOnlyList<string> PriorityGroups()
        {
#if USING_VORTEX_CURSOR
            if (_priorityLayout != null)
                return RebindBus.Data.GroupsOf(_priorityLayout);
#endif
            return string.IsNullOrEmpty(defaultGroup) ? Array.Empty<string>() : new[] { defaultGroup };
        }

        /// <summary>Вывести глиф и строку для действующей клавиши команды.</summary>
        private void Refresh()
        {
            if (component == null)
                return;

            var data = RebindBus.Data;
#if USING_VORTEX_CURSOR
            TrackDevice(VirtualCursorBus.Data.LastDevice.Value);
#endif
            _shownPriority = PriorityGroups();
            var slots = RebindBus.Controller.GetAll(Command);
            var slot = slots.FirstOrDefault(s => _shownPriority.Contains(s.Group) && data.IsGroupActive(s.Group))
                       ?? slots.FirstOrDefault(s => data.IsGroupActive(s.Group));
            var value = slot?.Value;
            var trigger = value?.Trigger;
            var modifiers = value?.Modifiers ?? Array.Empty<string>();

            var modifiersText = new StringBuilder();
            foreach (var modifier in modifiers)
                modifiersText.Append(GetText(modifier)).Append(modifierSeparator);

            try
            {
                component.SetText(string.Format(pattern, GetText(trigger), modifiersText));
            }
            catch (FormatException)
            {
                Debug.LogError($"[InputActionView] Некорректный паттерн «{pattern}»: допустимы {{0}} и {{1}}.", this);
            }

            if (keySpritePosition >= 0)
                component.SetSprite(glyphs != null ? glyphs.Find(trigger)?.sprite : null, keySpritePosition);
            if (modifierSpritePosition >= 0)
                component.SetSprite(ModifierSprite(modifiers, 0), modifierSpritePosition);
            if (secondModifierSpritePosition >= 0)
                component.SetSprite(ModifierSprite(modifiers, 1), secondModifierSpritePosition);
            if (modifiersSwitcherPosition >= 0)
                component.SetSwitcher(Math.Min(modifiers.Count, 2), modifiersSwitcherPosition);
        }

        /// <summary>Спрайт модификатора с номером <paramref name="i"/>; нет модификатора или глифа — <c>null</c>.</summary>
        private Sprite ModifierSprite(IReadOnlyList<string> modifiers, int i) =>
            i < modifiers.Count && glyphs != null ? glyphs.Find(modifiers[i])?.sprite : null;

        /// <summary>
        /// Текст клавиши: текст из таблицы глифов, иначе читаемое имя контрола Input System без устройства; затем
        /// перевод по ключу <see cref="localePrefix"/> + текст, если он есть (нет перевода или префикс пуст — текст как
        /// есть). Пустой путь — пустая строка.
        /// </summary>
        private string GetText(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            var text = glyphs != null ? glyphs.Find(path)?.text : null;
            if (string.IsNullOrEmpty(text))
                text = InputControlPath.ToHumanReadableString(path,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            return Localize(text);
        }

        /// <summary>Перевод подписи клавиши по ключу <see cref="localePrefix"/> + текст; перевода нет — текст как есть.</summary>
        private string Localize(string text)
        {
            if (string.IsNullOrEmpty(localePrefix) || string.IsNullOrEmpty(text))
                return text;
            var key = localePrefix + text;
            return Localization.HasTranslate(key) ? Localization.GetTranslate(key) : text;
        }

        /// <summary>Сменился язык — подписи клавиш переводятся заново.</summary>
        private void OnLocalizationChanged()
        {
            if (RebindBus.IsReady)
                Refresh();
        }

#if UNITY_EDITOR
        private IEnumerable<string> GroupKeys() =>
            Resources.LoadAll<RebindSettings>("").FirstOrDefault()?.Groups.Select(g => g.Key)
            ?? Enumerable.Empty<string>();

        /// <summary>Спрайт первого модификатора нацелен в ту же часть-графику, что и спрайт клавиши.</summary>
        private bool ModifierOverlapsKey() =>
            modifierSpritePosition >= 0 && modifierSpritePosition == keySpritePosition;

        /// <summary>Спрайт второго модификатора нацелен в часть-графику клавиши или первого модификатора.</summary>
        private bool SecondModifierOverlaps() => secondModifierSpritePosition >= 0
                                                 && (secondModifierSpritePosition == keySpritePosition
                                                     || secondModifierSpritePosition == modifierSpritePosition);
#endif
    }
}