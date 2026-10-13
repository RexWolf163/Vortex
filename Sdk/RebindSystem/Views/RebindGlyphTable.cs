using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Таблица глифов: ассоциация клавиши (пути контрола) с текстом-символом и спрайтом. Только данные —
    /// подстановку в строку и запасной текст делает представление (<see cref="InputActionView"/>).
    /// Модификаторы (Shift/Ctrl/Alt) — такие же записи. Пути — в обобщённой форме, в которой их хранит система
    /// (<c>&lt;Keyboard&gt;/space</c>, <c>&lt;Gamepad&gt;/buttonSouth</c>); сравнение без учёта регистра.
    /// Разные таблицы — разные стили глифов (например, Xbox и PlayStation).
    /// </summary>
    [CreateAssetMenu(menuName = "Vortex/Rebind/Glyph Table", fileName = "RebindGlyphTable")]
    public class RebindGlyphTable : ScriptableObject
    {
        /// <summary>Глиф одной клавиши.</summary>
        [Serializable]
        public class Entry
        {
            [HorizontalGroup, HideLabel]
            [InputControl, Tooltip("Путь контрола клавиши (например, <Keyboard>/space, <Gamepad>/buttonSouth).")]
            public string path;

            [HorizontalGroup, HideLabel] [Tooltip("Текст (символ) клавиши.")]
            public string text;

            [HorizontalGroup, HideLabel] [Tooltip("Спрайт клавиши.")]
            public Sprite sprite;
        }

        [SerializeField, Tooltip("Глифы клавиш. Новая таблица (и Reset) заполняется полной раскладкой клавиатуры, " +
                                 "мыши и геймпада с текстами; спрайты назначаются вручную.")]
        private Entry[] entries = CreateDefaultEntries();

        /// <summary>Индекс «путь → запись», строится при первом запросе.</summary>
        private Dictionary<string, Entry> _index;

        /// <summary>Глиф клавиши по пути контрола; <c>null</c> — в таблице нет записи (или путь пуст).</summary>
        public Entry Find(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (_index == null)
            {
                _index = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.path))
                        _index[entry.path] = entry;
            }

            return _index.TryGetValue(path, out var found) ? found : null;
        }

        /// <summary>Правка таблицы в инспекторе — индекс пересоберётся при следующем запросе.</summary>
        private void OnValidate() => _index = null;

        /// <summary>
        /// Раскладка по умолчанию: клавиатура (буквы, цифры, F1–F12, цифровой блок, навигация, пунктуация,
        /// модификаторы — общие и по сторонам), мышь (кнопки, направления колеса), геймпад (кнопки, курок, бамперы,
        /// крестовина, нажатия и направления стиков). Тексты — надписи клавиш (геймпад — в стиле Xbox), спрайтов нет.
        /// </summary>
        private static Entry[] CreateDefaultEntries()
        {
            var list = new List<Entry>();

            void Add(string path, string text) => list.Add(new Entry { path = path, text = text });

            // ── Клавиатура ────────────────────────────────────────────────────
            for (var c = 'a'; c <= 'z'; c++)
                Add($"<Keyboard>/{c}", char.ToUpperInvariant(c).ToString());
            for (var i = 0; i <= 9; i++)
                Add($"<Keyboard>/digit{i}", i.ToString());
            for (var i = 1; i <= 12; i++)
                Add($"<Keyboard>/f{i}", $"F{i}");
            for (var i = 0; i <= 9; i++)
                Add($"<Keyboard>/numpad{i}", $"Num {i}");

            Add("<Keyboard>/numpadDivide", "Num /");
            Add("<Keyboard>/numpadMultiply", "Num *");
            Add("<Keyboard>/numpadMinus", "Num -");
            Add("<Keyboard>/numpadPlus", "Num +");
            Add("<Keyboard>/numpadPeriod", "Num .");
            Add("<Keyboard>/numpadEquals", "Num =");
            Add("<Keyboard>/numpadEnter", "Num Enter");

            Add("<Keyboard>/space", "Space");
            Add("<Keyboard>/enter", "Enter");
            Add("<Keyboard>/tab", "Tab");
            Add("<Keyboard>/escape", "Esc");
            Add("<Keyboard>/backspace", "Backspace");
            Add("<Keyboard>/delete", "Del");
            Add("<Keyboard>/insert", "Ins");
            Add("<Keyboard>/home", "Home");
            Add("<Keyboard>/end", "End");
            Add("<Keyboard>/pageUp", "PgUp");
            Add("<Keyboard>/pageDown", "PgDn");
            Add("<Keyboard>/upArrow", "↑");
            Add("<Keyboard>/downArrow", "↓");
            Add("<Keyboard>/leftArrow", "←");
            Add("<Keyboard>/rightArrow", "→");
            Add("<Keyboard>/capsLock", "Caps Lock");
            Add("<Keyboard>/numLock", "Num Lock");
            Add("<Keyboard>/scrollLock", "Scroll Lock");
            Add("<Keyboard>/printScreen", "PrtSc");
            Add("<Keyboard>/pause", "Pause");
            Add("<Keyboard>/contextMenu", "Menu");

            Add("<Keyboard>/backquote", "`");
            Add("<Keyboard>/minus", "-");
            Add("<Keyboard>/equals", "=");
            Add("<Keyboard>/leftBracket", "[");
            Add("<Keyboard>/rightBracket", "]");
            Add("<Keyboard>/backslash", "\\");
            Add("<Keyboard>/semicolon", ";");
            Add("<Keyboard>/quote", "'");
            Add("<Keyboard>/comma", ",");
            Add("<Keyboard>/period", ".");
            Add("<Keyboard>/slash", "/");

            Add("<Keyboard>/shift", "Shift");
            Add("<Keyboard>/ctrl", "Ctrl");
            Add("<Keyboard>/alt", "Alt");
            Add("<Keyboard>/leftShift", "Shift");
            Add("<Keyboard>/rightShift", "R Shift");
            Add("<Keyboard>/leftCtrl", "Ctrl");
            Add("<Keyboard>/rightCtrl", "R Ctrl");
            Add("<Keyboard>/leftAlt", "Alt");
            Add("<Keyboard>/rightAlt", "R Alt");
            Add("<Keyboard>/leftMeta", "Win");
            Add("<Keyboard>/rightMeta", "R Win");

            // ── Мышь ──────────────────────────────────────────────────────────
            Add("<Mouse>/leftButton", "ЛКМ");
            Add("<Mouse>/rightButton", "ПКМ");
            Add("<Mouse>/middleButton", "СКМ");
            Add("<Mouse>/backButton", "Мышь 4");
            Add("<Mouse>/forwardButton", "Мышь 5");
            Add("<Mouse>/scroll/up", "Колесо ↑");
            Add("<Mouse>/scroll/down", "Колесо ↓");
            Add("<Mouse>/scroll/left", "Колесо ←");
            Add("<Mouse>/scroll/right", "Колесо →");

            // ── Геймпад (обозначения в стиле Xbox) ────────────────────────────
            Add("<Gamepad>/buttonSouth", "A");
            Add("<Gamepad>/buttonEast", "B");
            Add("<Gamepad>/buttonWest", "X");
            Add("<Gamepad>/buttonNorth", "Y");
            Add("<Gamepad>/leftShoulder", "LB");
            Add("<Gamepad>/rightShoulder", "RB");
            Add("<Gamepad>/leftTrigger", "LT");
            Add("<Gamepad>/rightTrigger", "RT");
            Add("<Gamepad>/start", "Menu");
            Add("<Gamepad>/select", "View");
            Add("<Gamepad>/leftStickPress", "LS");
            Add("<Gamepad>/rightStickPress", "RS");
            Add("<Gamepad>/dpad/up", "D-Pad ↑");
            Add("<Gamepad>/dpad/down", "D-Pad ↓");
            Add("<Gamepad>/dpad/left", "D-Pad ←");
            Add("<Gamepad>/dpad/right", "D-Pad →");
            Add("<Gamepad>/leftStick/up", "LS ↑");
            Add("<Gamepad>/leftStick/down", "LS ↓");
            Add("<Gamepad>/leftStick/left", "LS ←");
            Add("<Gamepad>/leftStick/right", "LS →");
            Add("<Gamepad>/rightStick/up", "RS ↑");
            Add("<Gamepad>/rightStick/down", "RS ↓");
            Add("<Gamepad>/rightStick/left", "RS ←");
            Add("<Gamepad>/rightStick/right", "RS →");

            return list.ToArray();
        }
    }
}