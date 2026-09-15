using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.CoreAssetsSystem;
#if UNITY_EDITOR
using System.Linq;
using UnityEngine.InputSystem;
#endif

namespace Vortex.Sdk.RebindSystem.Presets
{
    /// <summary>
    /// Правила системы переназначения клавиш. Экземпляр один (<see cref="ICoreAsset"/>, создаётся в
    /// Resources/Settings), в рантайме неизменяем. Защиты и исключения формирует разработчик: по умолчанию
    /// списки защищённых, пропускаемых и исключений пусты.
    /// </summary>
    [Serializable]
    public class RebindSettings : ScriptableObject, ICoreAsset
    {
        [InfoBox("Группы устройств. У каждой команды — свои слоты в каждой группе.")]
        [SerializeField]
        private DeviceGroupSettings[] groups =
        {
            new("Keyboard&Mouse", 2, "Keyboard", "Mouse"),
            new("Gamepad", 2, "Gamepad")
        };

        [InfoBox("Пары команд одной карты, которым разрешено делить клавишу.")]
        [SerializeField]
        private CommandPair[] allowedPairs = new CommandPair[0];

        [InfoBox("Защищённые команды: операция не может снять у них последнюю клавишу.")]
        [SerializeField, ValueDropdown("CommandIds")]
        private string[] protectedCommands = new string[0];

        [InfoBox("Пропускаемые команды: целиком вне системы — не переназначаются и не участвуют в проверке.")]
        [SerializeField, ValueDropdown("CommandIds")]
        private string[] skippedCommands = new string[0];

        [InfoBox("Сквозные команды: действуют поверх любой карты (например, UI/Click имитирует клик мыши). " +
                 "Совпадение их клавиши с командой другой карты — отдельный уровень конфликта Common:" +
                 "допустимо, подсвечивается.")]
        [SerializeField, ValueDropdown("CommandIds")]
        private string[] commonCommands = new string[0];

        [InfoBox("Запрещённые клавиши и комбинации.")]
        [SerializeField]
        private ForbiddenEntry[] forbidden = DefaultForbidden();

        [SerializeField, Tooltip("Различать левый и правый модификатор. Выключено — общий Ctrl/Shift/Alt.")]
        private bool distinguishModifierSides;

        [SerializeField, Tooltip("Папка файла снимка относительно корня данных приложения (файловый драйвер).")]
        private string snapshotFolder = "Controls";

        public IReadOnlyList<DeviceGroupSettings> Groups => groups ?? Array.Empty<DeviceGroupSettings>();

        public IReadOnlyList<CommandPair> AllowedPairs => allowedPairs ?? Array.Empty<CommandPair>();

        public IReadOnlyList<string> ProtectedCommands => protectedCommands ?? Array.Empty<string>();

        public IReadOnlyList<string> SkippedCommands => skippedCommands ?? Array.Empty<string>();

        public IReadOnlyList<string> CommonCommands => commonCommands ?? Array.Empty<string>();

        public IReadOnlyList<ForbiddenEntry> Forbidden => forbidden ?? Array.Empty<ForbiddenEntry>();

        public bool DistinguishModifierSides => distinguishModifierSides;

        public string SnapshotFolder => snapshotFolder;

        /// <summary>Единственный ассет настроек из Resources. <c>null</c> — ассета нет.</summary>
        internal static RebindSettings Find()
        {
            var res = Resources.LoadAll<RebindSettings>("");
            return res.Length > 0 ? res[0] : null;
        }

        /// <summary>
        /// Дефолт запрещённых. Системные кнопки геймпада (Guide / PS / Home) не включены: их пути требуют
        /// проверки раскладок на устройствах (Steam Deck, DualSense).
        /// </summary>
        private static ForbiddenEntry[] DefaultForbidden()
        {
            var list = new List<ForbiddenEntry>();

            // Без модификаторов: синтетические контролы, перехват ОС, отсутствующие на ноутбуках клавиши,
            // скриншот Steam, нестандартные OEM-клавиши.
            foreach (var key in new[]
                     {
                         "anyKey", "IMESelected", "leftMeta", "rightMeta", "contextMenu", "printScreen",
                         "numLock", "scrollLock", "pause", "f12", "OEM1", "OEM2", "OEM3", "OEM4", "OEM5"
                     })
                list.Add(new ForbiddenEntry($"<Keyboard>/{key}"));

            // С любым модификатором: Alt+Enter переключает полноэкранный режим, F-клавиши, Tab и Esc — системные
            // и оверлейные сочетания (Alt+F4, Alt+Tab, Shift+Tab — оверлей Steam, Ctrl+Esc, Ctrl+Shift+Esc).
            for (var i = 1; i <= 12; i++)
                list.Add(new ForbiddenEntry($"<Keyboard>/f{i}", ModifierMask.AnyNonEmpty));
            foreach (var key in new[] { "tab", "enter", "numpadEnter", "escape" })
                list.Add(new ForbiddenEntry($"<Keyboard>/{key}", ModifierMask.AnyNonEmpty));

            // Alt+Space — системное меню окна.
            list.Add(new ForbiddenEntry("<Keyboard>/space", ModifierMask.Specific, ModifierSet.Alt));

            return list.ToArray();
        }

#if UNITY_EDITOR
        private IEnumerable<string> CommandIds() => RebindEditorLists.CommandIds();

        /// <summary>
        /// Структурная валидация (fail-fast в редакторе). Лимит слотов против заводских биндингов проверяется при
        /// запуске системы: заводские биндинги живут в ассете ввода, и его правка OnValidate не вызывает.
        /// </summary>
        private void OnValidate()
        {
            foreach (var problem in Validate())
                Debug.LogError($"[RebindSettings] {problem}", this);
        }

        private IEnumerable<string> Validate()
        {
            var asset = InputSystem.actions;
            var commands = asset != null ? new HashSet<string>(RebindEditorLists.CommandIds()) : null;
            var layouts = new HashSet<string>(InputSystem.ListLayouts());

            // Группы
            var keys = new HashSet<string>();
            foreach (var group in Groups)
            {
                if (group == null)
                {
                    yield return "пустая запись в списке групп.";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(group.Key))
                    yield return "у группы пустой ключ.";
                else if (group.Key.Contains('#') || group.Key.Contains('/'))
                    yield return $"ключ группы «{group.Key}» содержит запрещённые символы # или /.";
                else if (!keys.Add(group.Key))
                    yield return $"ключ группы «{group.Key}» повторяется.";

                if (group.DeviceLayouts.Count == 0)
                    yield return $"у группы «{group.Key}» нет типов устройств.";
                foreach (var layout in group.DeviceLayouts)
                    if (!layouts.Contains(layout))
                        yield return $"у группы «{group.Key}» неизвестный layout «{layout}».";

                if (group.Slots < 1)
                    yield return $"у группы «{group.Key}» число слотов меньше 1.";
            }

            if (Groups.Count == 0)
                Debug.LogWarning("[RebindSettings] Список групп пуст — система ничего не обслуживает.", this);

            // Команды
            if (commands != null)
            {
                foreach (var id in ProtectedCommands.Concat(SkippedCommands).Concat(CommonCommands))
                    if (!commands.Contains(id))
                        yield return $"команды «{id}» нет в ассете ввода.";

                foreach (var pair in AllowedPairs)
                {
                    if (pair == null)
                        continue;
                    if (!commands.Contains(pair.First) || !commands.Contains(pair.Second))
                        yield return $"пара «{pair.First}» — «{pair.Second}»: команды нет в ассете ввода.";
                    else if (pair.First == pair.Second)
                        yield return $"пара «{pair.First}»: команда с самой собой.";
                    else if (MapOf(pair.First) != MapOf(pair.Second))
                        yield return $"пара «{pair.First}» — «{pair.Second}»: команды из разных карт.";
                }
            }

            foreach (var id in ProtectedCommands.Intersect(SkippedCommands))
                yield return $"команда «{id}» одновременно защищённая и пропускаемая.";

            foreach (var id in CommonCommands.Intersect(SkippedCommands))
                yield return $"команда «{id}» одновременно сквозная и пропускаемая — у пропускаемой нет слотов.";

            // Запрещённые
            foreach (var entry in Forbidden)
            {
                if (entry == null)
                    continue;
                if (string.IsNullOrWhiteSpace(entry.Control))
                    yield return "в запрещённых пустой путь контрола.";
                if (entry.Mask == ModifierMask.Specific)
                {
                    var count = CountModifiers(entry.Modifiers);
                    if (count == 0)
                        yield return $"запрещённая «{entry.Control}»: маска Specific без модификаторов.";
                    else if (count > 2)
                        yield return $"запрещённая «{entry.Control}»: больше двух модификаторов.";
                }
            }
        }

        private static string MapOf(string commandId)
        {
            var slash = commandId.IndexOf('/');
            return slash > 0 ? commandId.Substring(0, slash) : commandId;
        }

        private static int CountModifiers(ModifierSet set)
        {
            var count = 0;
            if ((set & ModifierSet.Shift) != 0) count++;
            if ((set & ModifierSet.Ctrl) != 0) count++;
            if ((set & ModifierSet.Alt) != 0) count++;
            return count;
        }
#endif
    }
}
