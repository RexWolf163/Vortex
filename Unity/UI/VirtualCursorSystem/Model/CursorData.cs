using System;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>
    /// Render-агностичный дескриптор текущего вида курсора: спрайт + hotspot + флаг скрытия.
    /// Резолвится <see cref="CursorSkinResolver"/> из (тема + тир + hover + доминант-действие),
    /// потребляется реализациями ICursorRenderer.
    /// </summary>
    public readonly struct CursorData : IEquatable<CursorData>
    {
        public static readonly CursorData None = new(String.Empty, false);
        public static readonly CursorData Hidden = new(String.Empty, true);

        public string Key { get; }

        /// <summary>Скрыть курсор (набор с HideCursor).</summary>
        public readonly bool Hide;

        public CursorData(string key, bool hide)
        {
            Key = key;
            Hide = hide;
        }

        public bool Equals(CursorData other) =>
            Key == other.Key && Hide == other.Hide;

        public override bool Equals(object obj) => obj is CursorData v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(Key, Hide);
    }
}