using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.UI.CursorSystem
{
    [Serializable]
    public class CursorSettings : SettingsPreset
    {
        [BoxGroup("Cursor Settings")] [SerializeField]
        private Sprite cursorDefault;

        [BoxGroup("Cursor Settings")] [SerializeField]
        private Sprite cursorLeftMouseDown;

        [BoxGroup("Cursor Settings")] [SerializeField]
        private Sprite cursorRightMouseDown;

        [BoxGroup("Cursor Settings")] [SerializeField] [InfoBox("Список вариантов отображения ховера")]
        private Sprite[] cursorOnHover = new Sprite[0];

        public Sprite CursorDefault => cursorDefault;

        public Sprite CursorLeftMouseDown => cursorLeftMouseDown;

        public Sprite CursorRightMouseDown => cursorRightMouseDown;

        public Sprite[] CursorOnHover => cursorOnHover;
    }
}