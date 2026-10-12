using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Render
{
    /// <summary>
    /// Опциональный рендер через системный ОС-курсор (<c>Cursor.SetCursor</c>, ForceSoftware) — для mouse-only
    /// сценариев, где нужен нативный ОС-курсор. Позицию игнорирует (ОС-курсор в позиции ОС-мыши), рисует лишь
    /// спрайт: набор выбирается по скину модели (<c>Skin</c>), спрайт в наборе — по доминирующему действию
    /// (<c>Actions</c>). Перерисовывается на смене <c>Skin</c>. Для виртуального курсора от геймпада не годится —
    /// там нужен <see cref="UiCursorRenderer"/>.
    /// ТРЕБУЕТ standalone-текстуру спрайта (не атлас): <c>Cursor.SetCursor</c> берёт целую <c>Texture2D</c>,
    /// поэтому атласный спрайт нарисует всю атлас-текстуру. Для атласных курсоров — <see cref="UiCursorRenderer"/>.
    /// </summary>
    public class OsCursorRenderer : MonoBehaviour
    {
        /// <summary>Набор спрайтов одного скина.</summary>
        [Serializable]
        private struct CursorSet
        {
            /// <summary>Имя скина (ключ <c>Skin</c> модели).</summary>
            public string name;

            /// <summary>Скин скрывает ОС-курсор.</summary>
            [ToggleButton] public bool hidden;

            /// <summary>Спрайт без нажатых действий.</summary>
            [HideIf("hidden")] public Sprite idle;

            /// <summary>
            /// Спрайты действий по индексу: 0 — BaseClick, 1 — ContextClick, 2 — AltClick. Пустой слот
            /// берёт спрайт предыдущего, затем <see cref="idle"/>.
            /// </summary>
            [HideIf("hidden")]
            [InfoBox("До трех спрайтов соответствующих действиям виртуальной мыши LeftClick, RightClick и MiddleClick")]
            public Sprite[] actionsOverride;
        }

        /// <summary>Наборы скинов; первый — скин по умолчанию (неизвестное имя скина).</summary>
        [SerializeField] private CursorSet[] cursorSets = new CursorSet[0];

        /// <summary>Имя скина по умолчанию — первого набора.</summary>
        private string _defaultCursor;

        /// <summary>Наборы по имени скина.</summary>
        private readonly Dictionary<string, CursorSet> _index = new();

        /// <summary>Подписка на <c>Skin</c> активна.</summary>
        private bool _subscribed;

        /// <summary>Построить индекс наборов; первый набор — скин по умолчанию.</summary>
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
                _index[set.name] = set;
        }

        /// <summary>Подписка на смену скина и немедленное применение.</summary>
        private void OnEnable()
        {
            VirtualCursorBus.Data.Skin.OnUpdate += OnVisual;
            _subscribed = true;
            Apply(Vector2.zero);
        }

        /// <summary>Отписка и возврат системного ОС-курсора (видимый, без кастомной текстуры).</summary>
        private void OnDisable()
        {
            if (!_subscribed) return;
            VirtualCursorBus.Data.Skin.OnUpdate -= OnVisual;
            _subscribed = false;
            // Симметрия с Apply: вернуть ОС-курсор в дефолт, иначе после Hide он остаётся невидим,
            // а кастомная текстура — установленной (рассинхрон с UiImageCursorRenderer).
            Cursor.visible = true;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>Смена скина — перерисовать.</summary>
        private void OnVisual(string s) => Apply(Vector2.zero);

        /// <summary>
        /// Применить набор текущего скина (неизвестный — набор по умолчанию): скрытый набор прячет ОС-курсор,
        /// иначе ставится спрайт доминирующего действия с хотспотом в pivot спрайта. Позиция не используется.
        /// </summary>
        private void Apply(Vector2 screenPosition)
        {
            Cursor.visible = true;
            if (!_index.TryGetValue(VirtualCursorBus.Data.Skin, out var cursorSet))
                cursorSet = _index[_defaultCursor];

            if (cursorSet.hidden)
            {
                Cursor.visible = false;
                return;
            }

            var actionMode = VirtualCursorBus.Data.Actions.Value.Dominant();
            var sprite = cursorSet.idle;
            switch (actionMode)
            {
                case PointerAction.None:
                    break;
                case PointerAction.BaseClick:
                    sprite = cursorSet.actionsOverride[0];
                    if (sprite == null)
                        sprite = cursorSet.idle;
                    break;
                case PointerAction.ContextClick:
                    sprite = cursorSet.actionsOverride[1];
                    if (sprite == null)
                        sprite = cursorSet.actionsOverride[0];
                    if (sprite == null)
                        sprite = cursorSet.idle;
                    break;
                case PointerAction.AltClick:
                    sprite = cursorSet.actionsOverride[2];
                    if (sprite == null)
                        sprite = cursorSet.actionsOverride[1];
                    if (sprite == null)
                        sprite = cursorSet.actionsOverride[0];
                    if (sprite == null)
                        sprite = cursorSet.idle;
                    break;
            }

            var hotspot = sprite.pivot;
            hotspot.y = sprite.rect.height - hotspot.y;
            Cursor.SetCursor(sprite.texture, hotspot, CursorMode.ForceSoftware);
        }
    }
}