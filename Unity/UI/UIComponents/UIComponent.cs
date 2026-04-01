using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using Vortex.Core.LocalizationSystem;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.UI.UIComponents
{
    public partial class UIComponent : MonoBehaviour
    {
        /// <summary>
        /// Перечень текстовых полей компонента
        /// </summary>
        [SerializeField, HideIf("EmptyTexts"), HorizontalGroup("Link"),
         VerticalGroup("Link/Components")]
        private UIComponentText[] uiComponentTexts;

        /// <summary>
        /// Перечень текстовых полей компонента
        /// </summary>
        [SerializeField, HideIf("EmptyTexts"), HorizontalGroup("Link"),
         VerticalGroup("Link/Components")]
        private bool useLocalization = true;

        /// <summary>
        /// Перечень кнопок компонента
        /// </summary>
        [SerializeField, HideIf("EmptyButtons"), HorizontalGroup("Link"),
         VerticalGroup("Link/Components")]
        private UIComponentButton[] uiComponentButtons;

        /// <summary>
        /// Перечень графических элементов компонента
        /// </summary>
        [SerializeField, HideIf("EmptyGraphics"), HorizontalGroup("Link"),
         VerticalGroup("Link/Components")]
        private UIComponentGraphic[] uiComponentGraphics;

        /// <summary>
        /// Перечень графических элементов компонента
        /// </summary>
        [SerializeField, HideIf("EmptySwitchers"), HorizontalGroup("Link"),
         VerticalGroup("Link/Components")]
        private UIComponentSwitcher[] uiComponentSwitchers;

        #region Private

        /*
        private void OnEnable()
        {
            var count = uiComponentTexts?.Length ?? 0;
            for (var i = 0; i < count; i++)
                SetText(String.Empty, i);

            count = uiComponentGraphics?.Length ?? 0;
            for (var i = 0; i < count; i++)
                SetSprite(null, i);
        }
        */

        #endregion

        #region Public

        public void PutData(UIComponentData data)
        {
            var count = uiComponentTexts?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                var text = String.Empty;
                if (data.texts?.Length > i)
                    text = data.texts[i];
                SetText(text, i);
            }

            count = uiComponentButtons?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                UnityAction action = null;
                if (data.actions?.Length > i)
                    action = data.actions[i];
                SetAction(action, i);
            }

            count = uiComponentGraphics?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                Sprite sprite = null;
                if (data.sprites?.Length > i)
                    sprite = data.sprites[i];
                SetSprite(sprite, i);
            }

            count = uiComponentSwitchers?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                if (!(data.enumValues?.Length > i))
                    break;
                var enumValue = data.enumValues[i];
                SetSwitcher(enumValue, i);
            }
        }

        /// <summary>
        /// Упрощенное добавление текста в компонент
        /// </summary>
        /// <param name="text">Текст для внедрения в компонент</param>
        /// <param name="position">Номер части компонента</param>
        public void SetText(string text, int position)
        {
            if (uiComponentTexts == null || uiComponentTexts.Length <= position)
            {
                Debug.LogError($"[UIComponent: {transform.name}] No UI components for this content]");
                return;
            }

            uiComponentTexts[position].PutData(useLocalization ? text.Translate() : text);
        }

        /// <summary>
        /// Упрощенное добавление текста в компонент во все точки разом
        /// </summary>
        /// <param name="text">Текст для внедрения в компонент</param>
        public void SetText(string text)
        {
            text = useLocalization ? text.Translate() : text;
            foreach (var uiComponentText in uiComponentTexts)
                uiComponentText.PutData(text);
        }

        /// <summary>
        /// Упрощенное добавление экшена на кнопку
        /// </summary>
        /// <param name="action">Событие для внедрения в компонент</param>
        /// <param name="position">Номер части компонента</param>
        public void SetAction(UnityAction action, int position = 0)
        {
            if (uiComponentButtons == null || uiComponentButtons.Length <= position)
            {
                Debug.LogError($"[UIComponent: {transform.name}] No UI components for this content]");
                return;
            }

            uiComponentButtons[position].PutData(action);
        }

        /// <summary>
        /// Добавление спрайта на все графические компоненты
        /// (Например для дальнейшего переключения с одной картинкой но разным материалом)
        /// </summary>
        /// <param name="sprite">Спрайт для внедрения в компонент</param>
        public void SetSprite(Sprite sprite)
        {
            foreach (var graphic in uiComponentGraphics)
                graphic.PutData(sprite);
        }

        /// <summary>
        /// Добавление спрайта на все графические компоненты
        /// (Например для дальнейшего переключения с одной картинкой но разным материалом)
        /// </summary>
        /// <param name="texture2D">Текстура для внедрения в компонент</param>
        public void SetSprite(Texture2D texture2D)
        {
            foreach (var graphic in uiComponentGraphics)
                graphic.PutData(texture2D);
        }

        /// <summary>
        /// Упрощенное добавление спрайта на кнопку
        /// </summary>
        /// <param name="sprite">Спрайт для внедрения в компонент</param>
        /// <param name="position">Номер части компонента</param>
        public void SetSprite(Sprite sprite, int position)
        {
            if (uiComponentGraphics == null || uiComponentGraphics.Length <= position)
            {
                Debug.LogError($"[UIComponent: {transform.name}] No UI components for this content]");
                return;
            }

            uiComponentGraphics[position].PutData(sprite);
        }

        /// <summary>
        /// Упрощенное добавление текстуры на кнопку
        /// </summary>
        /// <param name="texture2D">Текстура для внедрения в компонент</param>
        /// <param name="position">Номер части компонента</param>
        public void SetSprite(Texture2D texture2D, int position)
        {
            if (uiComponentGraphics == null || uiComponentGraphics.Length <= position)
            {
                Debug.LogError($"[UIComponent: {transform.name}] No UI components for this content]");
                return;
            }

            uiComponentGraphics[position].PutData(texture2D);
        }

        /// <summary>
        /// Упрощенное выставление свитчера
        /// </summary>
        /// <param name="enumValue">Значение для выставления свитчера</param>
        public void SetSwitcher(int enumValue) => SetSwitcher(enumValue, 0);

        /// <summary>
        /// Упрощенное выставление свитчера
        /// </summary>
        /// <param name="enumValue">Значение для выставления свитчера</param>
        /// <param name="position">Номер части компонента</param>
        public void SetSwitcher(int enumValue, int position = 0)
        {
            if (uiComponentSwitchers == null || uiComponentSwitchers.Length <= position)
            {
                Debug.LogError($"[UIComponent: {transform.name}] No UI components for this content]");
                return;
            }

            uiComponentSwitchers[position].PutData(enumValue);
        }

        /// <summary>
        /// Упрощенное выставление свитчера
        /// </summary>
        /// <param name="enumValue">Значение для выставления свитчера</param>
        /// <param name="position">Номер части компонента</param>
        public void SetSwitcher(Enum enumValue, int position = 0) => SetSwitcher(enumValue.GetHashCode(), position);

        #endregion
    }
}