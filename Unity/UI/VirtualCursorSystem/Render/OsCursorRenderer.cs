using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Render
{
    /// <summary>
    /// Опциональный рендер через системный ОС-курсор (<c>Cursor.SetCursor</c>, ForceSoftware) — для mouse-only
    /// сценариев, где нужен нативный ОС-курсор. Позицию игнорирует (ОС-курсор в позиции ОС-мыши), рисует лишь
    /// спрайт по <see cref="CursorData"/>. Для виртуального курсора от геймпада не годится — там нужен
    /// <see cref="UiCursorRenderer"/>.
    /// ТРЕБУЕТ standalone-текстуру спрайта (не атлас): <c>Cursor.SetCursor</c> берёт целую <c>Texture2D</c>,
    /// поэтому атласный спрайт нарисует всю атлас-текстуру. Для атласных курсоров — <see cref="UiCursorRenderer"/>.
    /// </summary>
    public class OsCursorRenderer : MonoBehaviour
    {
        [Serializable]
        private struct CursorSet
        {
            public string name;
            public Sprite texture;
        }

        [SerializeField] private CursorSet[] cursorSets = new CursorSet[0];

        private string _defaultCursor;

        private readonly Dictionary<string, Sprite> _index = new();

        private bool _subscribed;

        private void Awake()
        {
            _index.Clear();
            _defaultCursor = "";

            if (cursorSets == null)
            {
                Debug.LogError($"[OsCursorRenderer] There is no cursor config on {gameObject.name}", this);
                return;
            }

            _defaultCursor = cursorSets[0].name;

            foreach (var set in cursorSets)
                _index[set.name] = set.texture;
        }

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
            // Симметрия с Apply: вернуть ОС-курсор в дефолт, иначе после Hide он остаётся невидим,
            // а кастомная текстура — установленной (рассинхрон с UiImageCursorRenderer).
            Cursor.visible = true;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void TrySubscribe()
        {
            if (_subscribed || !VirtualCursorBus.IsReady) return;
            VirtualCursorBus.Visual.OnUpdate += OnVisual;
            _subscribed = true;
            Apply(VirtualCursorBus.Visual.Value, Vector2.zero);
        }

        private void OnVisual(CursorData v) => Apply(v, Vector2.zero);

        private void Apply(in CursorData data, Vector2 screenPosition)
        {
            if (data.Hide)
            {
                Cursor.visible = false;
                return;
            }

            Cursor.visible = true;
            if (!_index.TryGetValue(data.Key, out var sprite))
                sprite = _index[_defaultCursor];
            if (sprite.texture == null) return;
            var hotspot = sprite.pivot;
            hotspot.y = sprite.rect.height - hotspot.y;
            Cursor.SetCursor(sprite.texture, hotspot, CursorMode.ForceSoftware);
        }
    }
}