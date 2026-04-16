using System;
using System.Collections.Generic;
using System.Linq;
using AppScripts.Camera.View;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;

namespace AppScripts.Camera
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
            if (Index.ContainsKey(storage.gameObject.name))
            {
                Index.Remove(storage.gameObject.name);
                OnRemove?.Invoke(storage);
            }
            else
                Debug.LogError($"[CameraBus] {storage.gameObject.name} was not registered");
        }

        /// <summary>
        /// Получить объекта данных управляемой камеры
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
        /// Возвращает список всех зарегистрированных камер
        /// </summary>
        /// <returns></returns>
        public static string[] GetKeys() => Index.Keys.ToArray();

        /// <summary>
        /// Возвращает любую камеру (первую в списке)
        /// </summary>
        /// <returns></returns>
        public static CameraDataStorage GetAny() => Index.First().Value;

        /// <summary>
        /// Возвращает все зарегистрированные управляемые камеры
        /// </summary>
        /// <returns></returns>
        internal static CameraDataStorage[] GetAll() => Index.Values.ToArray();
    }
}