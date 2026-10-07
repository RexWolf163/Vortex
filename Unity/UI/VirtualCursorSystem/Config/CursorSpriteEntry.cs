using System;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Config
{
    [Serializable]
    public struct CursorSpriteEntry
    {
        public PointerAction action;
        public string name;
    }
}