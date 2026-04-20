using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.Extensions.ReactiveValues;

namespace Vortex.Sdk.UIs.RoofTransparentSystem
{
    /// <summary>
    /// Шина управления данными для обеспечения механики включения прозрачности
    /// над целевыми объектами
    /// </summary>
    public static class RoofTransparentBus
    {
        /// <summary>
        /// Ключ привязки TimeController
        /// </summary>
        private static readonly object Key = new();

        /// <summary>
        /// Поменялись координаты у кого-то из зарегистрированных целей
        /// </summary>
        public static event Action OnUpdatePositions;

        /// <summary>
        /// Индекс целей, при движении которых активируется прозрачности
        /// </summary>
        private static readonly Dictionary<Vector2Data, float> Index = new();

        /// <summary>
        /// Регистрация цели, при движении которой активируется прозрачность
        /// </summary>
        /// <param name="position">Реактивный контейнер с позицией цели в мировых координатах</param>
        /// <param name="size"></param>
        public static void Register(Vector2Data position, float size)
        {
            Index.AddNew(position, size);
            position.OnUpdateData += CallOnUpdatePosition;
        }

        /// <summary>
        /// Снятие цели с регистрации
        /// </summary>
        /// <param name="position"></param>
        public static void Unregister(Vector2Data position)
        {
            position.OnUpdateData -= CallOnUpdatePosition;
            Index.Remove(position);
        }

        /// <summary>
        /// Обнуление индекса подписок
        /// </summary>
        public static void ResetIndex()
        {
            foreach (var position in Index.Keys)
                position.OnUpdateData -= CallOnUpdatePosition;
            Index.Clear();
        }

        /// <summary>
        /// Возвращает перечень отслеживаемых целей
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyDictionary<Vector2Data, float> GetIndex() => Index;

        /// <summary>
        /// Запуск события
        /// </summary>
        private static void CallOnUpdatePosition() => TimeController.Accumulate(() => OnUpdatePositions?.Invoke(), Key);
    }
}