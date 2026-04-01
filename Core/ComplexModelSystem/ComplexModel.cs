using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.ComplexModelSystem
{
    /// <summary>
    /// Класс-основа для комплексной модели данных.
    /// То есть для модели, чья структура зависит от подключенных пакетов расширения
    /// </summary>
    /// <typeparam name="T">Интерфейс или базовый класс данных</typeparam>
    [Serializable]
    public abstract class ComplexModel<T> where T : class
    {
        /// <summary>
        /// Защита от реиспользования на одинаковый T
        /// </summary>
        private static readonly Dictionary<Type, Type[]> Cache = new();

        protected Dictionary<Type, T> Index { get; set; } = new();

        /// <summary>
        /// Инициализация новой модели структурой
        /// </summary>
        public void Init()
        {
            Index.Clear();

            if (Cache.ContainsKey(typeof(T)))
            {
                var types = Cache[typeof(T)];
                foreach (var t in types)
                    Index.Add(t, Activator.CreateInstance(t) as T);

                return;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => !t.IsAbstract
                                    && !t.IsInterface
                                    && typeof(T).IsAssignableFrom(t)
                                    && t.GetConstructor(Type.EmptyTypes) != null);

                    foreach (var t in types)
                        Index.Add(t, Activator.CreateInstance(t) as T);
                }
                catch (Exception ex)
                {
                    Log.Print(LogLevel.Warning, ex.Message, this);
                }
            }

            Cache[typeof(T)] = Index.Keys.ToArray();
        }

        /// <summary>
        /// Возвращает значение указанного типа если оно есть в индексе
        /// </summary>
        /// <typeparam name="TU"></typeparam>
        /// <returns></returns>
        public TU Get<TU>() where TU : class, T
        {
            if (Index.TryGetValue(typeof(TU), out var instance))
                return instance as TU;

            Log.Print(LogLevel.Error,
                $"Incorrect data request. Model not have {typeof(TU).Name} data.",
                this);
            return null;
        }

        /// <summary>
        /// Сериализует модель в Json строку Vortex
        /// </summary>
        /// <returns></returns>
        public virtual string Serialize()
        {
            BeforeSerialization();
            var json = Index.SerializeProperties();
            AfterSerialization();
            return json;
        }

        /// <summary>
        /// Десериализует модель из json строки Vortex
        /// </summary>
        public virtual void Deserialize(string data)
        {
            BeforeDeserialization();
            if (string.IsNullOrEmpty(data))
            {
                Log.Print(LogLevel.Error, $"Attempt to deserialize from null or empty data.", this);
                return;
            }

            Index = data.DeserializeProperties<Dictionary<Type, T>>();
            AfterDeserialization();
        }

        protected abstract void BeforeSerialization();
        protected abstract void BeforeDeserialization();
        protected abstract void AfterSerialization();
        protected abstract void AfterDeserialization();
    }
}