using System;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>
    /// Runtime-модель жестов мультикасания: последний кадр жеста. Реактивно, НЕ сохраняется. Пишет
    /// <c>VirtualCursorController</c> (владелец поля) по репортам <c>GestureInputDriver</c>; потребители
    /// подписываются на <c>Frame.OnUpdate</c> и сами решают, что значит жест (зум камеры, прокрутка карты, поворот).
    /// </summary>
    public class GestureModel : IReactiveData
    {
        /// <summary>Изменение <see cref="Frame"/>.</summary>
        public event Action OnUpdateData;

        /// <summary>
        /// Последний кадр жеста — импульс: публикуется на начало и конец жеста и на кадры с изменением; между
        /// публикациями значение «висит».
        /// </summary>
        public GestureFrameData Frame { get; } = new(GestureFrame.None);

        /// <summary>Модель без жеста; изменения кадра транслируются в <see cref="OnUpdateData"/>.</summary>
        public GestureModel()
        {
            Frame.OnUpdateData += Raise;
        }

        /// <summary>Поднять <see cref="OnUpdateData"/>.</summary>
        private void Raise() => OnUpdateData?.Invoke();

        /// <summary>Закрепить владельца за <see cref="Frame"/>. Зовётся контроллером один раз.</summary>
        public void SetOwner(object owner) => Frame.SetOwner(owner);
    }
}
