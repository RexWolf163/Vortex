using UnityEngine;
using UnityEngine.UI;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Дефолтный рендер: UGUI-<see cref="Image"/> в позиции <c>ScreenPosition</c> на оверлей-канвасе
    /// (ОС-курсор скрыт). Спрайт — из <see cref="CursorVisual"/>, hotspot — через pivot RectTransform.
    /// Расцеплено от ОС-мыши: следует за виртуальным курсором для любого источника, без warp.
    /// Требования: RectTransform курсора на Screen Space - Overlay канвасе выше всего UI, Raycast Target off.
    /// </summary>
    public class UiImageCursorRenderer : MonoBehaviour, ICursorRenderer
    {
        [SerializeField, Tooltip("RectTransform курсора на оверлей-канвасе.")]
        private RectTransform cursor;

        [SerializeField, Tooltip("Image курсора (Raycast Target off).")]
        private Image image;

        [SerializeField, Tooltip("Скрывать системный курсор пока активен этот рендер.")]
        private bool hideSystemCursor = true;

        private bool _subscribed;

        // Гейт первой отрисовки. До первого РЕАЛЬНОГО репорта позиции (от любого InputDriver'а)
        // визуал держим скрытым: иначе дефолтный спрайт темы мигнул бы в (0, 0) — на тач-платформах
        // это флэш в углу экрана до первого касания. Сбрасывается в OnEnable (повторный Enable →
        // снова ждём первого репорта), выставляется только из OnPosition.
        private bool _firstReportReceived;

        private void OnEnable()
        {
            if (hideSystemCursor)
                Cursor.visible = false;

            // Стартуем явно скрытым — независимо от того, когда подоспеет Bus.IsReady и первый репорт.
            // Image до этого момента не отрисуется, даже если ApplyCurrent где-то спровоцируется.
            _firstReportReceived = false;
            if (image != null)
                image.enabled = false;

            TrySubscribe();
            VirtualCursorBus.OnReady += TrySubscribe;
        }

        private void OnDisable()
        {
            if (hideSystemCursor)
                Cursor.visible = true; // восстановить ОС-курсор — симметрично OnEnable (иначе останется скрыт)

            VirtualCursorBus.OnReady -= TrySubscribe;
            if (!_subscribed) return;
            VirtualCursorBus.Visual.OnUpdate -= OnVisual;
            VirtualCursorBus.Data.ScreenPosition.OnUpdate -= OnPosition;
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed || !VirtualCursorBus.IsReady) return;
            VirtualCursorBus.Visual.OnUpdate += OnVisual;
            VirtualCursorBus.Data.ScreenPosition.OnUpdate += OnPosition;
            _subscribed = true;
            // НЕ зовём ApplyCurrent на подписке: модель ещё в стартовом состоянии ((0,0) + дефолтный
            // скин), ранняя отрисовка = тот же initial-flash, от которого защищает _firstReportReceived.
            // Первый OnPosition от драйвера снимет гейт и позовёт ApplyCurrent.
        }

        private void OnVisual(CursorVisual _)
        {
            // До первого репорта позиции не рисуем вообще — чтобы смена темы/действия в стартовом
            // (0,0) не открывала визуал. После — обычный путь.
            if (_firstReportReceived) ApplyCurrent();
        }

        private void OnPosition(Vector2 _)
        {
            _firstReportReceived = true;
            ApplyCurrent();
        }

        private void ApplyCurrent() =>
            Apply(VirtualCursorBus.Visual.Value, VirtualCursorBus.Data.ScreenPosition.Value);

        public void Apply(in CursorVisual visual, Vector2 screenPosition)
        {
            if (image == null || cursor == null)
                return;

            // visual.Hide = скин.Hide || _pointerHidden (источник) || _externalHidden (API)
            // — OR-композиция собирается в VirtualCursorController.Recompute. Initial-flash
            // (дефолтный спрайт в (0,0) до первого ввода) гасится на уровне OnEnable/OnPosition
            // через _firstReportReceived — Apply сюда просто не вызывается до первого репорта.
            var show = !visual.Hide && visual.HasSprite;
            image.enabled = show;

            if (show)
            {
                image.sprite = visual.Sprite;
                var rect = visual.Sprite.rect; // rect спрайта, не texture — корректно и для атласных спрайтов
                if (rect.width > 0 && rect.height > 0)
                    // hotspot(top-left px, rect-local) → pivot(bottom-left, нормализованный): точка совпадёт с позицией
                    cursor.pivot = new Vector2(visual.Hotspot.x / rect.width, 1f - visual.Hotspot.y / rect.height);
            }

            cursor.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
        }
    }
}
