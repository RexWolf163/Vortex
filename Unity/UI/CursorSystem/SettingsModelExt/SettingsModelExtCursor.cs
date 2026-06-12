using UnityEngine;

namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        public Sprite CursorDefault { get; private set; }

        public Sprite CursorLeftMouseDown { get; private set; }
        public Sprite CursorRightMouseDown { get; private set; }
        public Sprite[] CursorOnHover { get; private set; }
    }
}