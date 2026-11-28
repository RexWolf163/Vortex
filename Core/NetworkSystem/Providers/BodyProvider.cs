using System;
using System.Collections.Generic;
using Vortex.Core.NetworkSystem.Model;
using Vortex.Core.NetworkSystem.Model.Body;
using Vortex.Core.NetworkSystem.Model.Enums;

namespace Vortex.Core.NetworkSystem.Providers
{
    /// <summary>
    /// Преобразование «объект ⇄ тело»: сериализовать объект в байты с Content-Type, разобрать байты в объект
    /// указанного класса с итогом чтения, разложить объект в плоский набор параметров (если провайдер это умеет).
    /// Данные настройки — в наследниках.
    /// </summary>
    [Serializable]
    public abstract class BodyProvider
    {
        /// <summary>
        /// Сериализовать объект в байты и назвать их Content-Type. Объект, который нельзя сериализовать, —
        /// ошибка программиста: исключение.
        /// </summary>
        public abstract BytesBody Serialize(object value);

        /// <summary>
        /// Разобрать байты в объект класса <typeparamref name="T"/>. Неразбираемые байты — не исключение,
        /// а итог <see cref="ReadStatus.Failed"/> с сообщением.
        /// </summary>
        public abstract ReadResult<T> Deserialize<T>(byte[] bytes);

        /// <summary>Умеет ли провайдер раскладывать объект в плоский набор параметров.</summary>
        public abstract bool CanFlatten { get; }

        /// <summary>
        /// Разложить объект в плоский набор параметров «имя → значение». Вызывается, только если
        /// <see cref="CanFlatten"/>.
        /// </summary>
        public abstract IReadOnlyList<KeyValuePair<string, object>> Flatten(object value);
    }
}
