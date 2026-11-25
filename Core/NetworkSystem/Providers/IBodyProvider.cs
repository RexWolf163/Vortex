using System;
using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Провайдер тела: превращает объект в данные для отправки и обратно (формат — JSON и т.п.).
    /// Выбирается в настройках связи по маске URL. Реализации хранятся как <c>[SerializeReference]</c>.
    /// </summary>
    public interface IBodyProvider
    {
        /// <summary>Content-Type сериализованного тела.</summary>
        string ContentType { get; }

        /// <summary>Объект → байты. Ошибка сериализации — исключение (ошибка программиста).</summary>
        byte[] Serialize(object value);

        /// <summary>Байты → объект класса <paramref name="type"/>. Неудача — <c>false</c> и сообщение, без исключения.</summary>
        bool TryDeserialize(byte[] data, Type type, out object value, out string error);

        /// <summary>Возможность: объект → плоский набор параметров (для GET/HEAD с объектом).</summary>
        bool CanFlatten { get; }

        /// <summary>
        /// Объект → пары «имя → значение» верхнего уровня; коллекции примитивов — повторяющимися ключами.
        /// Вложенный объект — исключение (ошибка программиста). Значения форматирует контроллер.
        /// </summary>
        IReadOnlyList<KeyValuePair<string, object>> Flatten(object value);
    }
}
