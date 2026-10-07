using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Render
{
    /// <summary>
    /// Дефолтный рендер: UGUI-<see cref="Image"/> в позиции <c>ScreenPosition</c> на оверлей-канвасе
    /// Требования: RectTransform курсора на Screen Space - Overlay канвасе выше всего UI,
    ///
    /// Raycast Image или иных UGUI элементов должен быть отелючен!.
    /// </summary>
    public class UiCursorRenderer : MonoBehaviour
    {
        [SerializeField, Tooltip("RectTransform курсора на оверлей-канвасе.")]
        private RectTransform cursor;

        [SerializeField, Tooltip("Скрывать системный курсор пока активен этот рендер.")]
        private bool hideSystemCursor = true;

        [HorizontalGroup("switcher")]
        [InfoBox("Первый слот свитчера должен соответствовать положению «Нет Курсора». " +
                 "Второй - дефолтный курсор (если ключ не найден)." +
                 " Прочие по ключам пресета")]
        [SerializeField]
        private UIStateSwitcher cursorMode;

        private bool _subscribed;

        //Гейт заморозки отрисовки до первого смещения
        private bool _firstReportReceived;

        private HashSet<string> _states = new();

#if UNITY_EDITOR
        [HorizontalGroup("switcher", 100f), Button(ButtonSizes.Large)]
        private void Construct()
        {
            
        }
#endif
        private void Awake()
        {
            _states.Clear();
            var temp = cursorMode.States;
            foreach (var stateData in temp)
                _states.Add(stateData.Name);
        }

        private void OnEnable()
        {
            if (hideSystemCursor)
                Cursor.visible = false;

            // Стартуем явно скрытым — независимо от того, когда подоспеет Bus.IsReady и первый репорт.
            // Курсор до этого момента не рисуется.
            cursorMode.Set(0);
            _firstReportReceived = false;

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

        private void OnVisual(CursorData _)
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

        private void Apply(in CursorData data, Vector2 screenPosition)
        {
            if (cursor == null)
                return;

            var show = !data.Hide;
            if (!show)
            {
                cursorMode.Set(0);
                return;
            }

            if (_states.Contains(data.Key))
                cursorMode.Set(data.Key);
            else
                cursorMode.Set(1);
            
            cursor.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
        }
    }
}