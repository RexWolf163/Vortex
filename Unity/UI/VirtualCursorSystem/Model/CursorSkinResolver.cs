using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>
    /// Резолв текущего вида курсора: (тема → тир по разрешению → hover-скин/база → доминант-действие → спрайт)
    /// с фолбэком ВВЕРХ по цепочке до заданного или дефолта. Hotspot — из pivot спрайта (инверсия по Y).
    /// </summary>
    public static class CursorSkinResolver
    {
        public static CursorData Resolve(CursorSkinSettings settings, string setKey, string hoverKey,
            PointerActionMask actions, int screenHeight)
        {
            if (settings == null)
                return CursorData.None;

            var pack = settings.GetPack(setKey);
            if (pack == null)
                return CursorData.None;

            var hover = pack.FindHover(hoverKey);
            var skin = hover ?? pack.Base;
            if (skin == null)
                return CursorData.None;

            if (skin.HideCursor)
                return new CursorData(string.Empty, true);

            var action = actions.Dominant();

            // Фолбэк ВВЕРХ по цепочке: hover-скин → базовый скин пакета → дефолт базового.
            var mode = skin.Resolve(action);
            if (mode.IsNullOrWhitespace() && hover != null)
                mode = pack.Base?.Resolve(action);
            if (mode.IsNullOrWhitespace())
                mode = pack.Base?.Resolve(PointerAction.None);
            if (mode.IsNullOrWhitespace())
                return CursorData.None;

            return new CursorData(mode, false);
        }
    }
}