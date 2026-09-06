using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Выбор активной темы курсора — SAVE-АГНОСТИЧНО. Держит реактивный ключ; персист (IGameData)
    /// делает ПРОЕКТНЫЙ слой (L2 не зависит от L3-GameCore), зеркаля через <see cref="Select"/> и
    /// подписку на <see cref="Selected"/>.
    /// </summary>
    public static class CursorSkinSelector
    {
        private static readonly object Key = new();
        private static readonly StringData _selected = new(string.Empty, Key);

        /// <summary>Реактивный ключ активной темы (для чтения/подписки извне).</summary>
        public static StringData Selected => _selected;

        /// <summary>Выбрать тему по ключу.</summary>
        public static void Select(string setKey) => _selected.Set(setKey ?? string.Empty, Key);

        public static bool IsSelected(string setKey) => _selected.Value == setKey;
    }
}
