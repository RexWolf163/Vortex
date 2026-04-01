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

        [Button(ButtonHeight = 50), GUIColor("GetInitColor"), HorizontalGroup("Link", Width = 40f)]
        private void Init()
        {
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