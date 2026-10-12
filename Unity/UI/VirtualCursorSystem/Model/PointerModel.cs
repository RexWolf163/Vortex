using System;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Unity.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>
    /// Runtime-состояние виртуального курсора: экранная позиция (истина), маска активных действий
    /// (одновременность), итоговый скин, флаг «над UI», импульс скролла. Реактивно, НЕ сохраняется.
    /// Владение реактивными полями — внутри модели (<see cref="SetOwner"/>).
    /// </summary>
    public class PointerModel : IReactiveData
    {
        /// <summary>
        /// Изменение любого из полей <see cref="ScreenPosition"/>, <see cref="Actions"/>, <see cref="IsOverUI"/>,
        /// <see cref="ScrollDelta"/>. Смена <see cref="Skin"/> сюда не транслируется — подписка на <c>Skin.OnUpdate</c>.
        /// </summary>
        public event Action OnUpdateData;

        /// <summary>
        /// Значимый сдвиг указателя — решает фильтр дрейфа контроллера на входе репорта позиции
        /// (<c>VirtualCursorController.ReportPointer</c>): дрожание на месте и эхо варпа ОС-мыши его не поднимают.
        /// Источник не различается — свои переносы курсора (переход фокуса, сопровождение цели) тоже поднимают
        /// событие; подписчик, которому важен только пользовательский сдвиг, отсекает свои сам. Синхронное,
        /// приходит внутри репорта.
        /// </summary>
        public event Action OnPointerMoved;

        /// <summary>Позиция курсора в пикселях экрана — единственный источник истины.</summary>
        public Vector2Data ScreenPosition { get; } = new(Vector2.zero);

        /// <summary>Маска одновременно активных действий (кнопок).</summary>
        public PointerActionMaskData Actions { get; } = new(PointerActionMask.Empty);

        /// <summary>
        /// Итоговый скин курсора: <c>VirtualCursorBus.HideCursor</c> при скрытии, иначе имя запрошенного скина,
        /// пусто — базовый скин рендера. Сводится контроллером — владельцем поля.
        /// </summary>
        public StringData Skin { get; } = new(string.Empty);

        /// <summary>Курсор над интерактивным UGUI-элементом. Выставляется контроллером (<c>SetOverUI</c>).</summary>
        public BoolData IsOverUI { get; } = new(false);

        /// <summary>
        /// Импульс скролла на этот тик (пикс/тик, Vector2: X — горизонталь, Y — вертикаль;
        /// соглашение совпадает с UGUI <c>PointerEventData.scrollDelta</c>). Это НЕ состояние,
        /// а однокадровый импульс: драйвер пушит значение, обработчик подписывается на <c>OnUpdate</c>
        /// и реагирует на каждое событие. Между тиками значение «висит» (ReactiveValue не обнуляет сам);
        /// импульс, равный предыдущему, контроллер публикует принудительно (<c>ReportScroll</c>).
        /// </summary>
        public Vector2Data ScrollDelta { get; } = new(Vector2.zero);

        /// <summary>Модель в нулевом состоянии; изменения полей (кроме <see cref="Skin"/>) транслируются в <see cref="OnUpdateData"/>.</summary>
        public PointerModel()
        {
            ScreenPosition.OnUpdateData += Raise;
            Actions.OnUpdateData += Raise;
            IsOverUI.OnUpdateData += Raise;
            ScrollDelta.OnUpdateData += Raise;
        }

        /// <summary>Поднять <see cref="OnPointerMoved"/>. Зовёт контроллер по решению фильтра дрейфа.</summary>
        internal void RaisePointerMoved() => OnPointerMoved?.Invoke();

        /// <summary>Поднять <see cref="OnUpdateData"/>.</summary>
        private void Raise() => OnUpdateData?.Invoke();

        /// <summary>
        /// Закрепить владельца за всеми полями модели (<see cref="ScreenPosition"/>, <see cref="Actions"/>,
        /// <see cref="Skin"/>, <see cref="IsOverUI"/>, <see cref="ScrollDelta"/>). Зовётся контроллером один раз.
        /// </summary>
        public void SetOwner(object owner)
        {
            ScreenPosition.SetOwner(owner);
            Actions.SetOwner(owner);
            Skin.SetOwner(owner);
            IsOverUI.SetOwner(owner);
            ScrollDelta.SetOwner(owner);
        }
    }
}