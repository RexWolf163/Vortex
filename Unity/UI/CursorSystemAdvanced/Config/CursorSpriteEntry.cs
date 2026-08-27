using System;
using UnityEngine;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>Именованная пара «действие → спрайт» — разреженная запись скина (struct Enum⇒Sprite).</summary>
    [Serializable]
    public struct CursorSpriteEntry
    {
        public PointerAction action;
        public Sprite sprite;
    }
}
