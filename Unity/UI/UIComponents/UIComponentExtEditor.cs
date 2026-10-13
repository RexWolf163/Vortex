#if UNITY_EDITOR
using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.UI.UIComponents
{
    public partial class UIComponent
    {
        private bool EmptyTexts() => uiComponentTexts?.Length == 0;
        private bool EmptyButtons() => uiComponentButtons?.Length == 0;
        private bool EmptyGraphics() => uiComponentGraphics?.Length == 0;
        private bool EmptySwitchers() => uiComponentSwitchers?.Length == 0;

        /// <summary>Связанных текстов нет (массив не задан или пуст) — блок выравнивания скрыт.</summary>
        private bool NoTexts() => uiComponentTexts == null || uiComponentTexts.Length == 0;

        /// <summary>
        /// Выравнивание текста внутри компонента: чтение — по первому связанному тексту, запись — всем связанным
        /// текстам (с Undo и записью prefab-оверрайдов). Не сериализуется: значение всегда берётся из самих текстов.
        /// Скрыто, если связанных текстов нет.
        /// </summary>
        [ShowInInspector, HideIf("NoTexts"), PropertyOrder(10), LabelText("Выравнивание текста")]
        private TMPro.TextAlignmentOptions TextAlignment
        {
            get => FindAlignment() ?? TMPro.TextAlignmentOptions.Center;
            set => ApplyAlignment(value);
        }

        /// <summary>
        /// Maskable текстов и графики компонента: чтение — по первой связанной части с UI-графикой (тексты, затем
        /// графика), запись — всем таким частям (с Undo и записью prefab-оверрайдов). Не сериализуется. Скрыто, если
        /// таких частей нет (3D-текст TextMeshPro и SpriteRenderer маскируемости UI не имеют).
        /// </summary>
        [ShowInInspector, HideIf("NoMaskables"), PropertyOrder(10), LabelText("Maskable")]
        private bool Maskable
        {
            get => FindMaskable() ?? true;
            set => ApplyMaskable(value);
        }

        private bool NoMaskables() => FindMaskable() == null;

        /// <summary>Выравнивание первого связанного текста; текстов нет — <c>null</c>.</summary>
        private TMPro.TextAlignmentOptions? FindAlignment()
        {
            if (uiComponentTexts != null)
                foreach (var text in uiComponentTexts)
                    if (text != null)
                        return text.GetAlignment();
            return null;
        }

        private void ApplyAlignment(TMPro.TextAlignmentOptions alignment)
        {
            foreach (var text in uiComponentTexts)
            {
                if (text == null)
                    continue;
                text.SetAlignment(alignment);
                text.SetDirty();
            }
        }

        /// <summary>Maskable первой связанной части с UI-графикой (тексты, затем графика); таких нет — <c>null</c>.</summary>
        private bool? FindMaskable()
        {
            if (uiComponentTexts != null)
                foreach (var text in uiComponentTexts)
                    if (text != null && text.GetMaskable() is { } textMaskable)
                        return textMaskable;
            if (uiComponentGraphics != null)
                foreach (var graphic in uiComponentGraphics)
                    if (graphic != null && graphic.GetMaskable() is { } graphicMaskable)
                        return graphicMaskable;
            return null;
        }

        private void ApplyMaskable(bool maskable)
        {
            if (uiComponentTexts != null)
                foreach (var text in uiComponentTexts)
                {
                    if (text == null)
                        continue;
                    text.SetMaskable(maskable);
                    text.SetDirty();
                }

            if (uiComponentGraphics != null)
                foreach (var graphic in uiComponentGraphics)
                {
                    if (graphic == null)
                        continue;
                    graphic.SetMaskable(maskable);
                    graphic.SetDirty();
                }
        }

        [Button(ButtonHeight = 50), GUIColor("GetInitColor"), HorizontalGroup("Link", Width = 40f)]
        private void Init()
        {
            // Выставленные до пересбора значения блоков «Выравнивание текста» и «Maskable» (по прежним связям) —
            // применяются к найденным частям. Связей не было — значения нет, части не трогаются.
            var alignment = FindAlignment();
            var maskable = FindMaskable();

            var uiComponentTextsTemp = GetComponentsInChildren<UIComponentText>(true).ToList();
            var uiComponentButtonsTemp = GetComponentsInChildren<UIComponentButton>(true).ToList();
            var uiComponentGraphicsTemp = GetComponentsInChildren<UIComponentGraphic>(true).ToList();
            var uiComponentSwitchersTemp = GetComponentsInChildren<UIComponentSwitcher>(true).ToList();

            var uiComponentsContainers = GetComponentsInChildren<UIComponent>(true);

            foreach (var container in uiComponentsContainers)
            {
                if (container == this)
                    continue;
                container.Init();
                foreach (var text in container.uiComponentTexts)
                    uiComponentTextsTemp.Remove(text);
                foreach (var btn in container.uiComponentButtons)
                    uiComponentButtonsTemp.Remove(btn);
                foreach (var graphic in container.uiComponentGraphics)
                    uiComponentGraphicsTemp.Remove(graphic);
                foreach (var switcher in container.uiComponentSwitchers)
                    uiComponentSwitchersTemp.Remove(switcher);
            }

            uiComponentTexts = uiComponentTextsTemp.ToArray();
            uiComponentButtons = uiComponentButtonsTemp.ToArray();
            uiComponentGraphics = uiComponentGraphicsTemp.ToArray();
            uiComponentSwitchers = uiComponentSwitchersTemp.ToArray();

            if (alignment.HasValue)
                ApplyAlignment(alignment.Value);
            if (maskable.HasValue)
                ApplyMaskable(maskable.Value);

            _testData.texts = new string[uiComponentTexts?.Length ?? 0];
            var count = uiComponentTexts?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                var uiComponentText = uiComponentTexts[i];
                _testData.texts[i] = uiComponentText.GetValue();
            }

            _testData.sprites = new Sprite[uiComponentGraphics?.Length ?? 0];
            count = uiComponentGraphics?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                var uiComponentGraphic = uiComponentGraphics[i];
                _testData.sprites[i] = uiComponentGraphic.GetValue();
            }

            _testData.enumValues = new int[uiComponentSwitchers?.Length ?? 0];
            count = uiComponentSwitchers?.Length ?? 0;
            for (var i = 0; i < count; i++)
            {
                var uiComponentSwitcher = uiComponentSwitchers[i];
                _testData.enumValues[i] = uiComponentSwitcher.GetValue();
            }
        }

        private Color GetInitColor()
        {
            var red = new Color(1, 0.3f, 0.3f, 1);
            var white = new Color(0.9f, 0.9f, 0.9f, 1);

            if (uiComponentButtons != null)
                foreach (var component in uiComponentButtons)
                    if (component == null)
                        return red;
            if (uiComponentTexts != null)
                foreach (var component in uiComponentTexts)
                    if (component == null)
                        return red;
            if (uiComponentGraphics != null)
                foreach (var component in uiComponentGraphics)
                    if (component == null)
                        return red;
            if (uiComponentSwitchers != null)
                foreach (var component in uiComponentSwitchers)
                    if (component == null)
                        return red;

            return (uiComponentButtons == null || uiComponentButtons.Length == 0)
                   && (uiComponentTexts == null || uiComponentTexts.Length == 0)
                   && (uiComponentGraphics == null || uiComponentGraphics.Length == 0)
                   && (uiComponentSwitchers == null || uiComponentSwitchers.Length == 0)
                ? red
                : TestNotReady()
                    ? Color.green
                    : white;
        }

        [TitleGroup("Debug"), ShowInInspector, PropertyOrder(999)]
        private UIComponentData _testData;

        [Button, PropertyOrder(1000), HideIf("TestNotReady")]
        private void Test() => PutData(_testData);

        private bool TestNotReady() => _testData.sprites == null || _testData.texts == null;

        public UIComponentText[] GetTexts() => uiComponentTexts;
        public UIComponentButton[] GetButtons() => uiComponentButtons;
        public UIComponentGraphic[] GetGraphics() => uiComponentGraphics;
        public UIComponentSwitcher[] GetSwitchers() => uiComponentSwitchers;

        /// <summary>
        /// Возвращает список линков указанного типа
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public UIComponentPart[] GetLinks(Type componentType)
        {
            if (componentType == typeof(UIComponentText))
                return uiComponentTexts;
            if (componentType == typeof(UIComponentButton))
                return uiComponentButtons;
            if (componentType == typeof(UIComponentGraphic))
                return uiComponentGraphics;
            if (componentType == typeof(UIComponentSwitcher))
                return uiComponentSwitchers;
            return Array.Empty<UIComponentPart>();
        }
    }
}
#endif