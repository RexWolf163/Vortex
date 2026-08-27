using System;
using UnityEngine;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Render-агностичный дескриптор текущего вида курсора: спрайт + hotspot + флаг скрытия.
    /// Резолвится <see cref="CursorSkinResolver"/> из (тема + тир + hover + доминант-действие),
    /// потребляется реализациями ICursorRenderer.
    /// </summary>
    public readonly struct CursorVisual : IEquatable<CursorVisual>
    {
        public static readonly CursorVisual None = new(null, Vector2.zero, false);

        /// <summary>Спрайт курсора (null → скрыть/ничего не рисовать).</summary>
        public readonly Sprite Sprite;

        /// <summary>Hotspot в пикселях, top-left (уже инвертирован по Y относительно pivot).</summary>
        public readonly Vector2 Hotspot;

        /// <summary>Скрыть курсор (набор с HideCursor).</summary>
        public readonly bool Hide;

        public CursorVisual(Sprite sprite, Vector2 hotspot, bool hide)
        {
            Sprite = sprite;
            Hotspot = hotspot;
            Hide = hide;
        }

        public bool HasSprite => Sprite != null;

        public bool Equals(CursorVisual other) =>
            ReferenceEquals(Sprite, other.Sprite) && Hotspot == other.Hotspot && Hide == other.Hide;

        public override bool Equals(object obj) => obj is CursorVisual v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(Sprite, Hotspot, Hide);
    }
}
