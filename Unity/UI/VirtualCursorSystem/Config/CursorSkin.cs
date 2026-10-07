using System;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.UI.VirtualCursorSystem.Config;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Один скин курсора: имя-ключ (для hover), флаг скрытия, дефолт-спрайт (для None и локального фолбэка)
    /// и РАЗРЕЖЕННЫЙ список переопределений «действие → спрайт». Незаданное действие берётся из defaultSprite;
    /// если и его нет — фолбэк уходит выше по цепочке (базовый скин пакета) в <see cref="CursorSkinResolver"/>.
    /// </summary>
    [Serializable]
    public class CursorSkin
    {
        [SerializeField, Tooltip("Ключ hover-набора (для базового — не используется).")]
        private string name;

        [SerializeField, Tooltip("Скрыть курсор на этом скине (под кастомный оверлей).")]
        private bool hideCursor;

        [SerializeField,
         Tooltip("Вариации на действия виртуального указателя: только отличающиеся от дефолта действия.")]
        private CursorSpriteEntry[] overrides = new CursorSpriteEntry[0];

        public string Name => name;
        public bool HideCursor => hideCursor;

        /// <summary>Спрайт под действие в пределах ЭТОГО скина: override → defaultSprite. null, если ничего не задано.</summary>
        public string Resolve(PointerAction action)
        {
            if (action != PointerAction.None && overrides != null)
                foreach (var e in overrides)
                    if (e.action == action && !e.name.IsNullOrWhitespace())
                        return e.name;
            return Name;
        }
    }
}