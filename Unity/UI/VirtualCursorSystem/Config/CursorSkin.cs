using System;
using UnityEngine;

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

        [SerializeField, Tooltip("Дефолт-спрайт: для None и локального фолбэка незаданных действий.")]
        private Sprite defaultSprite;

        [SerializeField, Tooltip("Разреженные переопределения: только отличающиеся от дефолта действия.")]
        private CursorSpriteEntry[] overrides = Array.Empty<CursorSpriteEntry>();

        public string Name => name;
        public bool HideCursor => hideCursor;
        public Sprite Default => defaultSprite;

        /// <summary>Спрайт под действие в пределах ЭТОГО скина: override → defaultSprite. null, если ничего не задано.</summary>
        public Sprite Resolve(PointerAction action)
        {
            if (action != PointerAction.None && overrides != null)
                foreach (var e in overrides)
                    if (e.action == action && e.sprite != null)
                        return e.sprite;
            return defaultSprite;
        }
    }
}
