using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.UIComponents;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Набор вкладок на <see cref="AdvancedButton"/>: держит список элементов, ведёт выбранный и
    /// переключает свитчер набора — по одному состоянию на вкладку, в порядке списка.
    ///
    /// Выбор делает <see cref="Set"/>: он переключает свитчер
    ///
    /// При <see cref="cyclic"/> перебор идёт по кругу, иначе
    /// упирается в край.
    ///
    /// Клик игрока по кнопке приходит тем же путём: в <see cref="OnEnable"/> каждой кнопке
    /// назначается вызов <see cref="Set"/> со своим индексом.
    /// </summary>
    public class TabsHandler : MonoBehaviour
    {
        /// <summary>Вкладка: кнопка и флаг доступности.</summary>
        [Serializable]
        private class TabData
        {
            [SerializeField, Tooltip("Кнопка вкладки. Получает Press()+Release() при выборе."), HorizontalGroup,
             HideLabel]
            private UIComponent tab;

            [SerializeField, Tooltip("Вкладка доступна. Выключенную нельзя выбрать, перебор её пропускает."),
             HorizontalGroup(30f), HideLabel]
            private bool isEnabled = true;

            public UIComponent Tab => tab;

            public bool IsEnabled
            {
                get => isEnabled;
                internal set => isEnabled = value;
            }
        }

        [SerializeField, Tooltip("Вкладки по порядку перебора.")]
        private List<TabData> tabs = new();

        [SerializeField, Tooltip("Свитчер набора: одно состояние на вкладку, в том же порядке. " +
                                 "Заполняется кнопкой ниже.")]
        private UIStateSwitcher switcher;

        [SerializeField, Tooltip("Перебор по кругу: с последней вкладки Next идёт на первую.")]
        private bool cyclic;

        [SerializeField, Tooltip("Вкладка, выбранная при включении. Выключенная — возьмётся ближайшая доступная.")]
        private int startIndex;

        private readonly object _lock = new();

        /// <summary>Индекс выбранной вкладки. <c>-1</c> — доступных вкладок нет.</summary>
        public IntData Selected { get; private set; } = new IntData(-1);

        private void Awake()
        {
            Selected.SetOwner(_lock);
        }

        private void OnEnable()
        {
            Validate();
            Subscribe();

            // Стартовое состояние применяем без нажатия: Press — следствие действия пользователя,
            // а не включения набора.
            Selected.Set(Resolve(startIndex), _lock);
            Apply();
        }

        private void OnDisable() => Unsubscribe();

        /// <summary>
        /// Выбрать вкладку: переключить свитчер набора и нажать её кнопку. Выключенная вкладка и
        /// выход за границы — ошибка настройки, выбор не меняется.
        /// </summary>
        public void Set(int index)
        {
            if (index < 0 || index >= tabs.Count)
            {
                Debug.LogError($"[TabsHandler] {name}: индекс {index} вне списка вкладок ({tabs.Count}).", this);
                return;
            }

            var tab = tabs[index];
            if (tab == null || tab.Tab == null)
            {
                Debug.LogError($"[TabsHandler] {name}: вкладка [{index}] не настроена.", this);
                return;
            }

            if (!tab.IsEnabled)
            {
                Debug.LogError($"[TabsHandler] {name}: вкладка [{index}] выключена — выбрать нельзя.", this);
                return;
            }

            Selected.Set(index, _lock);
            Apply();
        }

        /// <summary>Следующая доступная вкладка. На краю без <c>cyclic</c> — без эффекта.</summary>
        public void Next() => Step(1);

        /// <summary>Предыдущая доступная вкладка. На краю без <c>cyclic</c> — без эффекта.</summary>
        public void Prev() => Step(-1);

        /// <summary>
        /// Переключить доступность вкладки. Если выключается выбранная — выбор уходит на ближайшую
        /// доступную, а при её отсутствии снимается.
        /// </summary>
        public void SetEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= tabs.Count || tabs[index] == null)
            {
                Debug.LogError($"[TabsHandler] {name}: индекс {index} вне списка вкладок ({tabs.Count}).", this);
                return;
            }

            if (tabs[index].IsEnabled == enabled)
                return;

            tabs[index].IsEnabled = enabled;

            if (enabled || index != Selected)
            {
                Apply();
                return;
            }

            var next = FindEnabled(index, 1);
            if (next < 0)
            {
                Selected.Set(-1, _lock);
                Apply();
                return;
            }

            Set(next);
        }

        private void Step(int direction)
        {
            var index = FindEnabled(Selected, direction);
            if (index < 0 || index == Selected)
                return;

            Set(index);
        }

        /// <summary>
        /// Раздать каждой кнопке её состояние (выключена / доступна / выбрана) и переключить свитчер
        /// набора в индекс выбранной вкладки.
        /// </summary>
        private void Apply()
        {
            for (var i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i]?.Tab;
                if (tab == null)
                    continue;
                var state = !tabs[i].IsEnabled
                    ? TabState.Disabled
                    : i == Selected
                        ? TabState.Selected
                        : TabState.Enabled;
                tab.SetSwitcher(state);
            }

            if (switcher != null && Selected >= 0)
                switcher.Set(Selected);
        }

        /// <summary>Назначить каждой кнопке вызов <see cref="Set"/> со своим индексом.</summary>
        private void Subscribe()
        {
            Unsubscribe();

            for (var i = 0; i < tabs.Count; i++)
            {
                var button = tabs[i]?.Tab;
                if (button == null)
                    continue;

                var index = i;
                button.SetAction(() => Set(index));
            }
        }

        private void Unsubscribe()
        {
            foreach (var t in tabs)
            {
                var button = t?.Tab;
                if (button != null)
                    button.SetAction(null);
            }
        }

        /// <summary>
        /// Ближайшая доступная вкладка в направлении <paramref name="direction"/> от <paramref name="from"/>.
        /// <c>-1</c> — доступных нет. Без <see cref="cyclic"/> перебор останавливается на краю.
        /// </summary>
        private int FindEnabled(int from, int direction)
        {
            if (tabs.Count == 0)
                return -1;

            var index = from;
            for (var step = 0; step < tabs.Count; step++)
            {
                index += direction;

                if (index < 0 || index >= tabs.Count)
                {
                    if (!cyclic)
                        return -1;
                    index = (index + tabs.Count) % tabs.Count;
                }

                if (IsSelectable(index))
                    return index;
            }

            return -1;
        }

        /// <summary>Выбор при включении: заданный индекс, если доступен, иначе ближайший доступный.</summary>
        private int Resolve(int preferred)
        {
            if (tabs.Count == 0)
                return -1;

            var index = Mathf.Clamp(preferred, 0, tabs.Count - 1);
            if (IsSelectable(index))
                return index;

            for (var i = 0; i < tabs.Count; i++)
                if (IsSelectable(i))
                    return i;

            return -1;
        }

        private bool IsSelectable(int index) =>
            index >= 0 && index < tabs.Count && tabs[index] != null && tabs[index].Tab != null &&
            tabs[index].IsEnabled;

        private void Validate()
        {
            if (tabs.Count == 0)
            {
                Debug.LogError($"[TabsHandler] {name}: список вкладок пуст.", this);
                return;
            }

            if (switcher == null)
            {
                Debug.LogError($"[TabsHandler] {name}: не назначен свитчер набора.", this);
                return;
            }

            if (switcher.States.Length < tabs.Count)
                Debug.LogError($"[TabsHandler] {name}: в свитчере состояний {switcher.States.Length}, " +
                               $"вкладок {tabs.Count} — соберите состояния кнопкой в инспекторе.", this);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Привести состояния <see cref="switcher"/> к списку вкладок: лишние удалить, недостающие
        /// добавить, имена взять с трансформов кнопок. Содержимое существующих состояний не трогается —
        /// переименовывается только подпись.
        /// </summary>
        [Button("Собрать состояния по кнопкам", ButtonSizes.Medium), PropertyOrder(10)]
        private void BuildSwitcherStates()
        {
            if (switcher == null)
            {
                Debug.LogError($"[TabsHandler] {name}: не назначен свитчер набора.", this);
                return;
            }

            var serialized = new SerializedObject(switcher);
            var states = serialized.FindProperty("states");
            if (states == null || !states.isArray)
            {
                Debug.LogError($"[TabsHandler] {name}: в UIStateSwitcher не найдено поле состояний — " +
                               "структура компонента изменилась, метод надо обновить.", this);
                return;
            }

            var previous = states.arraySize;
            states.arraySize = tabs.Count;

            for (var i = 0; i < tabs.Count; i++)
            {
                var element = states.GetArrayElementAtIndex(i);

                // Новые элементы Unity копирует с последнего — чужие элементы состояния были бы
                // сюрпризом, поэтому у добавленных чистим список.
                if (i >= previous)
                {
                    var items = element.FindPropertyRelative("stateItems");
                    if (items != null && items.isArray)
                        items.arraySize = 0;
                }

                var nameProperty = element.FindPropertyRelative("name");
                if (nameProperty == null)
                    continue;

                var button = tabs[i]?.Tab;
                nameProperty.stringValue = button == null ? $"Tab {i}" : button.name;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(switcher);

            Debug.Log($"[TabsHandler] {name}: состояний в свитчере {previous} → {tabs.Count}.", this);
        }
#endif
    }
}