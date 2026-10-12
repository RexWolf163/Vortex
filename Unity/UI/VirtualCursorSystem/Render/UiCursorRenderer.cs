using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;

namespace Vortex.Unity.UI.VirtualCursorSystem.Render
{
    /// <summary>
    /// Дефолтный рендер: UGUI-курсор на оверлей-канвасе. RectTransform <see cref="cursor"/> ставится в
    /// <c>ScreenPosition</c> модели, скин переключается состоянием <see cref="UIStateSwitcher"/> по имени
    /// <c>Skin</c> (неизвестное имя — состояние 0). Отрисовка позиции и скина начинается с первого репорта позиции.
    /// Требования: RectTransform курсора на Screen Space - Overlay канвасе выше всего UI;
    /// raycast у <see cref="Image"/> и прочих UGUI-элементов курсора должен быть отключен.
    /// </summary>
    public class UiCursorRenderer : MonoBehaviour
    {
        [SerializeField, Tooltip("RectTransform курсора на оверлей-канвасе.")]
        private RectTransform cursor;

        [SerializeField, Tooltip("Скрывать системный курсор пока активен этот рендер.")]
        private bool hideSystemCursor = true;

        /// <summary>Свитчер скинов: имя состояния = имя скина, состояние 0 — скин по умолчанию.</summary>
        [HorizontalGroup("switcher")]
        [InfoBox("Первый слот свитчера должен соответствовать дефолтному курсору (если ключ не найден)." +
                 " Прочие по ключам пресета. Скрытый курсор - это скин с названием hidden")]
        [SerializeField]
        private UIStateSwitcher cursorSkin;

        /// <summary>Не используется.</summary>
        private bool _subscribed;

        /// <summary>Гейт отрисовки: до первого репорта позиции смена скина не применяется.</summary>
        private bool _firstReportReceived;

        /// <summary>Имена состояний свитчера — известные рендеру скины.</summary>
        private HashSet<string> _states = new();

#if UNITY_EDITOR
        /// <summary>Кнопка инспектора рядом со свитчером. Заготовка — пока ничего не делает.</summary>
        [HorizontalGroup("switcher", 100f), Button(ButtonSizes.Large)]
        private void Construct()
        {
        }
#endif
        /// <summary>Собрать имена состояний свитчера.</summary>
        private void Awake()
        {
            _states.Clear();
            var temp = cursorSkin.States;
            foreach (var stateData in temp)
                _states.Add(stateData.Name);
        }

        /// <summary>
        /// Скрыть системный курсор (по флагу), выставить скин hidden (нет такого — состояние 0), взвести гейт первого репорта
        /// и подписаться на скин и позицию.
        /// </summary>
        private void OnEnable()
        {
            if (hideSystemCursor)
                Cursor.visible = false;

            // Стартуем скрытым — до первого репорта позиции курсор не рисуется. Нет скина hidden — дефолтный (0).
            if (_states.Contains(VirtualCursorBus.HideCursor))
                cursorSkin.Set(VirtualCursorBus.HideCursor);
            else
                cursorSkin.Set(0);
            _firstReportReceived = false;

            VirtualCursorBus.Data.Skin.OnUpdate += OnVisual;
            VirtualCursorBus.Data.ScreenPosition.OnUpdate += OnPosition;
        }

        /// <summary>Вернуть системный курсор (по флагу) и отписаться.</summary>
        private void OnDisable()
        {
            if (hideSystemCursor)
                Cursor.visible = true; // восстановить ОС-курсор — симметрично OnEnable (иначе останется скрыт)

            VirtualCursorBus.Data.Skin.OnUpdate -= OnVisual;
            VirtualCursorBus.Data.ScreenPosition.OnUpdate -= OnPosition;
        }

        /// <summary>Смена скина — применить, если первый репорт позиции уже был.</summary>
        private void OnVisual(string s)
        {
            if (_firstReportReceived) Apply();
        }

        /// <summary>Смена позиции — снять гейт и применить.</summary>
        private void OnPosition(Vector2 _)
        {
            _firstReportReceived = true;
            Apply();
        }

        /// <summary>Выставить состояние свитчера по скину (неизвестный — 0) и переставить курсор в позицию модели.</summary>
        private void Apply()
        {
            if (cursor == null)
                return;

            var data = VirtualCursorBus.Data;

            if (_states.Contains(data.Skin))
                cursorSkin.Set(data.Skin);
            else
                cursorSkin.Set(0);

            cursor.position = new Vector3(data.ScreenPosition.Value.x, data.ScreenPosition.Value.y, 0f);
        }
    }
}