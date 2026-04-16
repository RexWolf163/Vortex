using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.Camera.View;

namespace Vortex.Unity.Camera
{
    /// <summary>
    /// Шина для работы с управляемыми камерами
    /// </summary>
    public static class CameraBus
    {
        public static event Action<CameraDataStorage> OnRegistration;
        public static event Action<CameraDataStorage> OnRemove;

        /// <summary>
        /// Индекс данных камер
        /// Регистрация по названию GameObject
        /// </summary>
        private static readonly Dictionary<string, CameraDataStorage> Index = new();

        /// <summary>
        /// Регистрация объекта данных управляемой камеры
        /// </summary>
        /// <param name="storage"></param>
        public static void Registration(CameraDataStorage storage)
        {
            Index.AddNew(storage.gameObject.name, storage);
            OnRegistration?.Invoke(storage);
        }

        /// <summary>
        /// Снятие объекта данных управляемой камеры с регистрации
        /// </summary>
        /// <param name="storage"></param>
        public static void Remove(CameraDataStorage storage)
        {
            if (Index.Remove(storage.gameObject.name))
                OnRemove?.Invoke(storage);
            else
                Debug.LogError($"[CameraBus] {storage.gameObject.name} was not registered");
        }

        /// <summary>
        /// Получить объекта данных управляемой камеры.
        /// Если не найдено - выводится лог
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static CameraDataStorage Get(string key)
        {
            if (Index.TryGetValue(key, out var storage)) return storage;
            Debug.LogError($"[CameraBus] No camera storage registered for key: {key}");
            return null;
        }

        /// <summary>
        /// Получить объекта данных управляемой камеры, если он существует
        /// </summary>
        /// <param name="key"></param>
        /// <param name="storage"></param>
        /// <returns></returns>
        public static bool TryGet(string key, out CameraDataStorage storage) => Index.TryGetValue(key, out storage);

        /// <summary>
        /// Возвращает список всех зарегистрированных камер
        /// </summary>
        /// <returns></returns>
        public static string[] GetKeys() => Index.Keys.ToArray();

        /// <summary>
        /// Возвращает любую камеру (первую в списке)
        /// </summary>
        /// <returns></returns>
        public static CameraDataStorage GetAny() => Index.Count == 0 ? null : Index.First().Value;

        /// <summary>
        /// Возвращает все зарегистрированные управляемые камеры
        /// </summary>
        /// <returns></returns>
        internal static CameraDataStorage[] GetAll() => Index.Values.ToArray();
    }
}