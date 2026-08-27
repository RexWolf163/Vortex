using System;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Unity.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Runtime-состояние виртуального курсора: экранная позиция (истина), активный источник,
    /// маска активных действий (одновременность), hover-ключ скина, флаг «над UI».
    /// Реактивно, НЕ сохраняется. Владение реактивными полями — внутри модели (<see cref="SetOwner"/>).
    /// </summary>
    public class PointerModel : IReactiveData
    {
        public event Action OnUpdateData;

        /// <summary>Позиция курсора в пикселях экрана — единственный источник истины.</summary>
        public Vector2Data ScreenPosition { get; } = new(Vector2.zero);

        /// <summary>Активный вид источника (last-source-wins на репорте).</summary>
        public EnumData<PointerSourceKind> ActiveSource { get; } = new(PointerSourceKind.Analog);

        /// <summary>Маска одновременно активных действий (кнопки/скролл).</summary>
        public PointerActionMaskData Actions { get; } = new(PointerActionMask.Empty);

        /// <summary>Активный hover-ключ скина (пусто = база).</summary>
        public StringData HoverKey { get; } = new(string.Empty);

        /// <summary>Курсор над интерактивным UGUI-элементом (из EventSystem).</summary>
        public BoolData IsOverUI { get; } = new(false);

        public PointerModel()
        {
            ScreenPosition.OnUpdateData += Raise;
            ActiveSource.OnUpdateData += Raise;
            Actions.OnUpdateData += Raise;
            HoverKey.OnUpdateData += Raise;
            IsOverUI.OnUpdateData += Raise;
        }

        private void Raise() => OnUpdateData?.Invoke();

        /// <summary>Закрепить владельца за всеми реактивными полями (зовётся контроллером один раз).</summary>
        public void SetOwner(object owner)
        {
            ScreenPosition.SetOwner(owner);
            ActiveSource.SetOwner(owner);
            Actions.SetOwner(owner);
            HoverKey.SetOwner(owner);
            IsOverUI.SetOwner(owner);
        }
    }
}
