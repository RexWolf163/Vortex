using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Опциональный рендер через системный ОС-курсор (<c>Cursor.SetCursor</c>, ForceSoftware) — для mouse-only
    /// сценариев, где нужен нативный ОС-курсор. Позицию игнорирует (ОС-курсор в позиции ОС-мыши), рисует лишь
    /// спрайт по <see cref="CursorVisual"/>. Для виртуального курсора от геймпада не годится — там нужен
    /// <see cref="UiImageCursorRenderer"/>.
    /// ТРЕБУЕТ standalone-текстуру спрайта (не атлас): <c>Cursor.SetCursor</c> берёт целую <c>Texture2D</c>,
    /// поэтому атласный спрайт нарисует всю атлас-текстуру. Для атласных курсоров — <see cref="UiImageCursorRenderer"/>.
    /// </summary>
    public class OsCursorRenderer : MonoBehaviour, ICursorRenderer
    {
        private bool _subscribed;

        private void OnEnable()
        {
            TrySubscribe();
            VirtualCursorBus.OnReady += TrySubscribe;
        }

        private void OnDisable()
        {
            VirtualCursorBus.OnReady -= TrySubscribe;
            if (!_subscribed) return;
            VirtualCursorBus.Visual.OnUpdate -= OnVisual;
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed || !VirtualCursorBus.IsReady) return;
            VirtualCursorBus.Visual.OnUpdate += OnVisual;
            _subscribed = true;
            Apply(VirtualCursorBus.Visual.Value, Vector2.zero);
        }

        private void OnVisual(CursorVisual v) => Apply(v, Vector2.zero);

        public void Apply(in CursorVisual visual, Vector2 screenPosition)
        {
            if (visual.Hide)
            {
                Cursor.visible = false;
                return;
            }

            Cursor.visible = true;
            if (visual.HasSprite && visual.Sprite.texture != null)
                Cursor.SetCursor(visual.Sprite.texture, visual.Hotspot, CursorMode.ForceSoftware);
        }
    }
}
