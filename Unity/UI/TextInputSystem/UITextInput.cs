using System;
using System.Linq;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.TextInputSystem.Filters;

namespace Vortex.Unity.UI.TextInputSystem
{
    /// <summary>
    /// Режим ввода текста с клавиатуры поверх отображаемого значения. Данных не знает: <see cref="Begin"/> получает
    /// исходный текст, итог уходит в <see cref="OnSave"/> — что с ним делать, решает владелец значения.
    ///
    /// На время ввода свитчер в <see cref="SwitcherState.On"/>: показать поле (фон и текст поверх значения). Глушение
    /// игровых клавиш — тоже в состоянии On, хэндлерами вёрстки (заглушки карт ввода, отключение ввода
    /// Naninovel): поле читает клавиатуру само, мимо карт ввода, и о них не знает.
    /// Enter в однострочном поле — <see cref="Save"/>, Esc — <see cref="Rollback"/>. Клик мимо поля ввод не
    /// завершает: это делает тот, кому пролинкованы <see cref="Save"/> / <see cref="Rollback"/> (подложка).
    /// Ввод одновременно идёт только в одном поле: начало ввода в другом откатывает текущий.
    /// Маска — список <see cref="ITextInputFilter"/>, проверяется на каждый символ и при сохранении
    /// </summary>
    public class UITextInput : MonoBehaviour
    {
        [SerializeField, Required, InfoBox("Объект поля может выключаться свитчером, этот компонент — нет")]
        private TMP_InputField field;

        [SerializeField, Required, StateSwitcher(typeof(SwitcherState)), InfoBox("Off — просмотр, On — ввод")]
        private UIStateSwitcher switcher;

        [SerializeReference, LabelText("Маска"), ListDrawerSettings(DefaultExpandedState = true)]
        private ITextInputFilter[] filters = new ITextInputFilter[0];

        /// <summary>Текущее поле ввода: начало ввода в другом откатывает его</summary>
        private static UITextInput _active;

        /// <summary>Ввод сохранён, аргумент — итоговый текст (прошёл маску)</summary>
        public event Action<string> OnSave;

        /// <summary>Ввод откатан: значение не меняется</summary>
        public event Action OnRollback;

        /// <summary>Идёт ввод</summary>
        public bool IsEditing { get; private set; }

        private void OnEnable()
        {
            field.onValidateInput = ValidateChar;
            field.onSubmit.AddListener(OnFieldSubmit);
            field.onEndEdit.AddListener(OnFieldEndEdit);
        }

        /// <summary>Выключение во время ввода — откат без события: вернуть свитчер и снять фокус поля</summary>
        private void OnDisable()
        {
            field.onValidateInput = null;
            field.onSubmit.RemoveListener(OnFieldSubmit);
            field.onEndEdit.RemoveListener(OnFieldEndEdit);
            if (IsEditing)
                Finish();
        }

        /// <summary>
        /// Начать ввод с исходным текстом. Ввод в другом поле откатывается. Повторный вызов во время ввода
        /// заменяет текст
        /// </summary>
        public void Begin(string text)
        {
            if (_active != null && _active != this)
                _active.Rollback();

            if (!IsEditing)
            {
                IsEditing = true;
                _active = this;
                switcher.Set(SwitcherState.On);
            }

            field.text = text;
            field.ActivateInputField();
        }

        /// <summary>
        /// Сохранить ввод. Текст не прошёл маску — ввод продолжается. Вне ввода — ничего
        /// </summary>
        public void Save()
        {
            if (!IsEditing)
                return;

            var text = field.text;
            if (filters.Any(filter => !filter.IsValidFinal(text)))
            {
                field.ActivateInputField();
                return;
            }

            Finish();
            OnSave?.Invoke(text);
        }

        /// <summary>Откатить ввод. Вне ввода — ничего</summary>
        public void Rollback()
        {
            if (!IsEditing)
                return;

            Finish();
            OnRollback?.Invoke();
        }

        /// <summary>Выйти из ввода: снять фокус поля и показать значение</summary>
        private void Finish()
        {
            IsEditing = false;
            if (_active == this)
                _active = null;

            field.DeactivateInputField();
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == field.gameObject)
                eventSystem.SetSelectedGameObject(null);

            switcher.Set(SwitcherState.Off);
        }

        /// <summary>
        /// Символ проходит, если строку после его вставки пропускают все фильтры; <c>'\0'</c> — символ не вводится.
        /// <paramref name="text"/> приходит уже без выделенного фрагмента, который символ заменит
        /// </summary>
        private char ValidateChar(string text, int index, char added)
        {
            var candidate = text.Insert(index, added.ToString());
            return filters.All(filter => filter.IsValidPartial(candidate)) ? added : '\0';
        }

        /// <summary>Enter (однострочное поле или многострочное с отправкой по Enter)</summary>
        private void OnFieldSubmit(string _) => Save();

        /// <summary>
        /// Конец редактирования поля: по Esc (<see cref="TMP_InputField.wasCanceled"/>) — откат. Потеря фокуса
        /// кликом мимо ввод не завершает — решает подложка
        /// </summary>
        private void OnFieldEndEdit(string _)
        {
            if (field.wasCanceled)
                Rollback();
        }
    }
}
