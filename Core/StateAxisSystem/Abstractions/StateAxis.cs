using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Core.StateAxisSystem.Abstractions
{
    /// <summary>
    /// База для типобезопасных enum-подобных осей состояния.
    /// Наследник объявляет статические <c>readonly</c>-инстансы — значения оси.
    /// Регистрация инстансов автоматическая через конструктор базы:
    /// каждый <c>new MoveState("Run", 2)</c> на статическом поле помещает себя в реестр
    /// типа-наследника по ключу.
    ///
    /// Сгенерированные классы создаются <c>Vortex.Unity.StateAxisSystem</c> из парных
    /// <c>StateAxisPreset</c>-ассетов и не должны редактироваться вручную.
    /// </summary>
    public abstract class StateAxis : IEquatable<StateAxis>
    {
        public string Key { get; }
        public int Order { get; }

        // Реестр: тип-наследник → ключ → инстанс
        private static readonly Dictionary<Type, Dictionary<string, StateAxis>> ByKey = new();
        // Реестр: тип-наследник → список инстансов (в порядке добавления, сортируется при выдаче)
        private static readonly Dictionary<Type, List<StateAxis>> Ordered = new();

        /// <summary>
        /// Регистрирует custom-конвертер сериализации для семейства StateAxis-типов.
        /// Срабатывает при первой загрузке любого наследника (.NET-гарантия:
        /// static-инициализатор базы выполняется до static-инициализаторов наследника,
        /// до создания первого инстанса).
        /// </summary>
        static StateAxis()
        {
            SerializeController.RegisterCustomSerializer(
                t => typeof(StateAxis).IsAssignableFrom(t),
                obj => ((StateAxis)obj).Serialize(),
                (t, s) => Deserialize(s)
            );
        }

        protected StateAxis(string key, int order)
        {
            Key = key;
            Order = order;

            var type = GetType();
            if (!ByKey.TryGetValue(type, out var keyMap))
                ByKey[type] = keyMap = new Dictionary<string, StateAxis>();
            keyMap[key] = this;

            if (!Ordered.TryGetValue(type, out var list))
                Ordered[type] = list = new List<StateAxis>();
            list.Add(this);
        }

        public bool Equals(StateAxis other) =>
            other != null && GetType() == other.GetType() && Key == other.Key;

        public override bool Equals(object obj) => obj is StateAxis o && Equals(o);

        public override int GetHashCode() => HashCode.Combine(GetType(), Key);

        /// <summary>Короткое представление для логов: <c>"MoveState.Run"</c>.</summary>
        public override string ToString() => $"{GetType().Name}.{Key}";

        /// <summary>
        /// Сериализованное представление: <c>"{Namespace.AxisName}.{Key}"</c>.
        /// Используется конвертером SerializeController для save/load.
        /// </summary>
        public string Serialize() => $"{GetType().FullName}.{Key}";

        /// <summary>
        /// Поиск инстанса по ключу в указанной оси (типобезопасная версия).
        /// Возвращает <c>null</c>, если ключ не зарегистрирован или ось не загружена.
        /// </summary>
        public static T GetByKey<T>(string key) where T : StateAxis =>
            ByKey.TryGetValue(typeof(T), out var m) && m.TryGetValue(key, out var v) ? (T)v : null;

        /// <summary>Поиск инстанса по ключу с указанием Type оси (для рефлексионных сценариев).</summary>
        public static StateAxis GetByKey(Type axisType, string key) =>
            ByKey.TryGetValue(axisType, out var m) && m.TryGetValue(key, out var v) ? v : null;

        /// <summary>Все значения оси в порядке возрастания <see cref="Order"/>.</summary>
        public static IReadOnlyList<T> GetAll<T>() where T : StateAxis =>
            Ordered.TryGetValue(typeof(T), out var list)
                ? list.OrderBy(v => v.Order).Cast<T>().ToArray()
                : Array.Empty<T>();

        /// <summary>Все значения оси по Type (для рефлексионных сценариев).</summary>
        public static IReadOnlyList<StateAxis> GetAll(Type axisType) =>
            Ordered.TryGetValue(axisType, out var list)
                ? list.OrderBy(v => v.Order).ToArray()
                : Array.Empty<StateAxis>();

        /// <summary>Карта <c>ключ → инстанс</c> для указанной оси, либо <c>null</c>, если ось не зарегистрирована.</summary>
        public static IReadOnlyDictionary<string, StateAxis> GetMap(Type axisType) =>
            ByKey.TryGetValue(axisType, out var m) ? m : null;

        /// <summary>
        /// Восстанавливает singleton-инстанс по строке формата <c>"{FullName}.{Key}"</c>.
        /// Возвращает <c>null</c>, если тип не найден или ключ не зарегистрирован.
        /// </summary>
        public static StateAxis Deserialize(string serialized)
        {
            if (string.IsNullOrEmpty(serialized)) return null;
            var lastDot = serialized.LastIndexOf('.');
            if (lastDot <= 0 || lastDot == serialized.Length - 1) return null;

            var typeName = serialized.Substring(0, lastDot);
            var key = serialized.Substring(lastDot + 1);
            var type = StateAxisTypeCache.Resolve(typeName);

            if (type == null) return null;
            return ByKey.TryGetValue(type, out var m) && m.TryGetValue(key, out var v) ? v : null;
        }

        /// <summary>Типизированная версия <see cref="Deserialize(string)"/>.</summary>
        public static T Deserialize<T>(string serialized) where T : StateAxis =>
            Deserialize(serialized) as T;
    }
}
