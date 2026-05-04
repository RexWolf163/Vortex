using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Core.ExtensibleEnumSystem.Abstractions
{
    /// <summary>
    /// База для типобезопасных enum-подобных расширяемых наборов.
    /// Наследник объявляет статические <c>readonly</c>-инстансы — значения набора.
    /// Регистрация инстансов автоматическая через конструктор базы:
    /// каждый <c>new MoveState("Run", 2)</c> на статическом поле помещает себя в реестр
    /// типа-наследника по ключу.
    ///
    /// Инициализация реестра выполняется eagerly через
    /// <see cref="UnityEngine.RuntimeInitializeOnLoadMethodAttribute"/> до загрузки сцены
    /// и через <see cref="UnityEditor.InitializeOnLoadMethodAttribute"/> при загрузке домена
    /// в Editor-режиме. После этого <see cref="GetAll{T}"/>, <see cref="Deserialize(string)"/>
    /// и Inspector-дропдауны работают предсказуемо вне зависимости от того, в каком порядке
    /// загружаются типы и какой код первым к ним обратился.
    /// </summary>
    public abstract class ExtensibleEnum : IEquatable<ExtensibleEnum>
    {
        public string Key { get; }
        public int Order { get; }

        // Реестр: тип-наследник → ключ → инстанс
        private static readonly Dictionary<Type, Dictionary<string, ExtensibleEnum>> ByKey = new();

        // Реестр: тип-наследник → список инстансов (в порядке добавления, сортируется при выдаче)
        private static readonly Dictionary<Type, List<ExtensibleEnum>> Ordered = new();

        // Кеш: FullName → Type для всех не-абстрактных наследников. Заполняется eagerly в Initialize().
        private static readonly Dictionary<string, Type> ByFullName = new();

        /// <summary>
        /// Регистрирует custom-конвертер сериализации для семейства ExtensibleEnum-типов.
        /// Срабатывает при первой загрузке базы (что гарантирует Initialize или любая ссылка
        /// на наследника, в зависимости от того, что произойдёт раньше).
        /// </summary>
        static ExtensibleEnum()
        {
            SerializeController.RegisterCustomSerializer(
                t => typeof(ExtensibleEnum).IsAssignableFrom(t),
                obj => ((ExtensibleEnum)obj).Serialize(),
                (t, s) => Deserialize(s)
            );
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnRuntime() => Initialize();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeOnEditor() => Initialize();
#endif

        /// <summary>
        /// Идемпотентная инициализация: сканирует <c>AppDomain.CurrentDomain.GetAssemblies()</c>,
        /// для каждого не-абстрактного <see cref="ExtensibleEnum"/>-наследника:
        ///  • заполняет <see cref="ByFullName"/> карту <c>FullName → Type</c>;
        ///  • вызывает <see cref="RuntimeHelpers.RunClassConstructor"/>, что прогоняет
        ///    static-инициализаторы наследника и наполняет <see cref="ByKey"/>/<see cref="Ordered"/>
        ///    зарегистрированными инстансами.
        /// </summary>
        private static void Initialize()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(ExtensibleEnum).IsAssignableFrom(type)) continue;

                    ByFullName[type.FullName] = type;

                    try
                    {
                        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    }
                    catch (Exception)
                    {
                        // Ошибки инициализации типа диагностируются на уровне CLR.
                        // Здесь подавляем, чтобы один сбойный тип не ломал инициализацию остальных.
                    }
                }
            }
        }

        protected ExtensibleEnum(string key, int order)
        {
            Key = key;
            Order = order;

            var type = GetType();
            if (!ByKey.TryGetValue(type, out var keyMap))
                ByKey[type] = keyMap = new Dictionary<string, ExtensibleEnum>();
            keyMap[key] = this;

            if (!Ordered.TryGetValue(type, out var list))
                Ordered[type] = list = new List<ExtensibleEnum>();
            list.Add(this);
        }

        public bool Equals(ExtensibleEnum other) =>
            other != null && GetType() == other.GetType() && Key == other.Key;

        public override bool Equals(object obj) => obj is ExtensibleEnum o && Equals(o);

        public override int GetHashCode() => HashCode.Combine(GetType(), Key);

        /// <summary>Короткое представление для логов: <c>"MoveState.Run"</c>.</summary>
        public override string ToString() => $"{GetType().Name}.{Key}";

        /// <summary>
        /// Сериализованное представление: <c>"{Namespace.TypeName}.{Key}"</c>.
        /// Используется конвертером SerializeController для save/load.
        /// </summary>
        public string Serialize() => $"{GetType().FullName}.{Key}";

        /// <summary>
        /// Поиск инстанса по ключу (типобезопасная версия).
        /// Возвращает <c>null</c>, если ключ не зарегистрирован.
        /// </summary>
        public static T GetByKey<T>(string key) where T : ExtensibleEnum =>
            ByKey.TryGetValue(typeof(T), out var m) && m.TryGetValue(key, out var v) ? (T)v : null;

        /// <summary>Поиск инстанса по ключу с указанием Type (для рефлексионных сценариев).</summary>
        public static ExtensibleEnum GetByKey(Type type, string key) =>
            ByKey.TryGetValue(type, out var m) && m.TryGetValue(key, out var v) ? v : null;

        /// <summary>Все значения в порядке возрастания <see cref="Order"/>.</summary>
        public static IReadOnlyList<T> GetAll<T>() where T : ExtensibleEnum =>
            Ordered.TryGetValue(typeof(T), out var list)
                ? list.OrderBy(v => v.Order).Cast<T>().ToArray()
                : Array.Empty<T>();

        /// <summary>Все значения по Type (для рефлексионных сценариев).</summary>
        public static IReadOnlyList<ExtensibleEnum> GetAll(Type type) =>
            Ordered.TryGetValue(type, out var list)
                ? list.OrderBy(v => v.Order).ToArray()
                : Array.Empty<ExtensibleEnum>();

        /// <summary>Карта <c>ключ → инстанс</c> для указанного типа, либо <c>null</c>, если тип не зарегистрирован.</summary>
        public static IReadOnlyDictionary<string, ExtensibleEnum> GetMap(Type type) =>
            ByKey.TryGetValue(type, out var m) ? m : null;

        /// <summary>
        /// Восстанавливает singleton-инстанс по строке формата <c>"{FullName}.{Key}"</c>.
        /// Возвращает <c>null</c>, если тип не найден или ключ не зарегистрирован.
        /// </summary>
        public static ExtensibleEnum Deserialize(string serialized)
        {
            if (string.IsNullOrEmpty(serialized)) return null;
            var lastDot = serialized.LastIndexOf('.');
            if (lastDot <= 0 || lastDot == serialized.Length - 1) return null;

            var typeName = serialized.Substring(0, lastDot);
            var key = serialized.Substring(lastDot + 1);

            if (!ByFullName.TryGetValue(typeName, out var type)) return null;
            return ByKey.TryGetValue(type, out var m) && m.TryGetValue(key, out var v) ? v : null;
        }

        /// <summary>Типизированная версия <see cref="Deserialize(string)"/>.</summary>
        public static T Deserialize<T>(string serialized) where T : ExtensibleEnum =>
            Deserialize(serialized) as T;
    }
}
