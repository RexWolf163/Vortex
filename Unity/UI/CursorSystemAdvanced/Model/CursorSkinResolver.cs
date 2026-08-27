using UnityEngine;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Резолв текущего вида курсора: (тема → тир по разрешению → hover-скин/база → доминант-действие → спрайт)
    /// с фолбэком ВВЕРХ по цепочке до заданного или дефолта. Hotspot — из pivot спрайта (инверсия по Y).
    /// </summary>
    public static class CursorSkinResolver
    {
        public static CursorVisual Resolve(CursorSkinSettings settings, string setKey, string hoverKey,
            PointerActionMask actions, int screenHeight)
        {
            if (settings == null)
                return CursorVisual.None;

            var set = settings.GetSet(setKey);
            var tier = set?.GetTier(settings.SelectTierIndex(screenHeight));
            if (tier == null)
                return CursorVisual.None;

            var hover = tier.FindHover(hoverKey);
            var skin = hover ?? tier.Base;
            if (skin == null)
                return CursorVisual.None;

            if (skin.HideCursor)
                return new CursorVisual(null, Vector2.zero, true);

            var action = actions.Dominant();

            // Фолбэк ВВЕРХ по цепочке: hover-скин → базовый скин пакета → дефолт базового.
            var sprite = skin.Resolve(action);
            if (sprite == null && hover != null)
                sprite = tier.Base?.Resolve(action);
            if (sprite == null)
                sprite = tier.Base?.Resolve(PointerAction.None);
            if (sprite == null || sprite.texture == null)
                return CursorVisual.None;

            var hotspot = sprite.pivot;
            hotspot.y = sprite.texture.height - hotspot.y; // pivot(bottom-left) → hotspot(top-left)
            return new CursorVisual(sprite, hotspot, false);
        }
    }
}
