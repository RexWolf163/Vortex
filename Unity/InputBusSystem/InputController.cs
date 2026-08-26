using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Unity.InputBusSystem
{
    /// <summary>
    /// Контроллер для регистрации/отписки хэндлеров клавиш
    ///
    /// Хранит индексы карт ввода и их пользователей.
    /// Активирует и деактивирует карты в зависимости от того кто зарегистрировался на них
    /// 
    /// Хранит индексы экшенов.
    /// Проводит подписку хэндлеров по принципу LIFO
    /// </summary>
    public static class InputController
    {
        private static readonly Dictionary<string, InputAction> Actions = new();
        private static readonly Dictionary<string, InputActionMap> Maps = new();
        private static readonly Dictionary<string, List<object>> MapsUsers = new();
        private static readonly Dictionary<string, List<InputSubscriber>> ActionsUsers = new();

        /// <summary>
        /// Объект на которого пришелся performed
        /// Нужно дял того чтобы cancel обрабатывался им же или не обрабатывался вообще
        /// </summary>
        private static readonly Dictionary<string, object> CatchPerformed = new();

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            App.OnExit -= Dispose;
            App.OnExit += Dispose;
            Dispose();
            var asset = InputSystem.actions;
            var maps = asset.actionMaps;
            foreach (var map in maps)
            {
                MapsUsers.AddNew(map.name, new List<object>());
                Maps.Add(map.name, map);
                map.Disable();

                foreach (var inputAction in map.actions)
                {
                    // Составной id «Карта/Экшен»: даёт уникальность при одноимённых экшенах в разных
                    // картах и папки в дропдауне (SearchablePopup группирует по «/»).
                    var actionId = $"{map.name}/{inputAction.name}";
                    ActionsUsers.AddNew(actionId, new List<InputSubscriber>());
                    Actions.Add(actionId, inputAction);
                    CatchPerformed.Add(actionId, null);
                    inputAction.performed += OnPerformed;
                    inputAction.canceled += OnCanceled;
                }
            }
        }

        /// <summary>
        /// Очистка данных
        /// </summary>
        private static void Dispose()
        {
            App.OnExit -= Dispose;
            foreach (var inputAction in Actions.Values)
            {
                inputAction.performed -= OnPerformed;
                inputAction.canceled -= OnCanceled;
            }

            CatchPerformed.Clear();
            MapsUsers.Clear();
            Maps.Clear();
            ActionsUsers.Clear();
            Actions.Clear();
        }

        /// <summary>
        /// Возвращает перечень название карт ввода
        /// </summary>
        /// <returns></returns>
        public static string[] GetMaps()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Init();
#endif

            if (Maps == null || Maps.Count == 0)
                Init();
            return Maps?.Keys.ToArray();
        }

        /// <summary>
        /// Добавить пользователя карты ввода
        /// Карта ввода будет активирована
        /// </summary>
        /// <param name="mapId"></param>
        /// <param name="inputMapUser"></param>
        public static void AddMapUser(string mapId, object inputMapUser)
        {
            MapsUsers[mapId].AddOnce(inputMapUser);
            Maps[mapId].Enable();
        }

        /// <summary>
        /// Удалить пользователя карты ввода
        /// Если пользователей не осталось - карта отключится
        /// </summary>
        /// <param name="mapId"></param>
        /// <param name="inputMapUser"></param>
        public static void RemoveMapUser(string mapId, object inputMapUser)
        {
            if (MapsUsers[mapId].Remove(inputMapUser) && MapsUsers[mapId].Count == 0)
                Maps[mapId].Disable();
        }

        /// <summary>
        /// Возвращает перечень название экшенов ввода
        /// </summary>
        /// <returns></returns>
        public static string[] GetActions()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Init();
#endif

            if (Actions == null || Actions.Count == 0)
                Init();
            return Actions?.Keys.ToArray();
        }

        /// <summary>
        /// Добавить пользователя экшена ввода
        /// Пользователь будет добавлен в очередь на получение сигнала
        /// </summary>
        /// <param name="actionInputId"></param>
        /// <param name="actionInputUser"></param>
        /// <param name="onPerformedCallback">Колбэк на срабатывание ввода</param>
        /// <param name="onCanceledCallback">Колбэк на прекращение ввода</param>
        public static void AddActionUser(string actionInputId, object actionInputUser,
            Action onPerformedCallback,
            Action onCanceledCallback)
        {
            ActionsUsers[actionInputId]
                .AddOnce(new InputSubscriber(actionInputUser, onPerformedCallback, onCanceledCallback));
        }

        /// <summary>
        /// Удалить пользователя карты ввода
        /// Пользователь будет удален из очереди на получение сигнала
        /// </summary>
        /// <param name="actionInputId"></param>
        /// <param name="actionInputUser"></param>
        public static void RemoveActionUser(string actionInputId, object actionInputUser)
        {
            var item = ActionsUsers[actionInputId].FirstOrDefault(x => x.Owner == actionInputUser);
            if (item != null)
                ActionsUsers[actionInputId].Remove(item);
        }

        /// <summary>
        /// Обработка события нажатия
        /// </summary>
        /// <param name="ctx"></param>
        private static void OnPerformed(InputAction.CallbackContext ctx)
        {
            var actionId = $"{ctx.action.actionMap.name}/{ctx.action.name}";
            if (Settings.Data().InputDebugMode)
                Debug.Log($"[InputController] {actionId} performed by '{ctx.control.name}'");
            var subscribers = ActionsUsers[actionId];
            if (subscribers == null || subscribers.Count == 0)
                return;
            var subscriber = subscribers[^1];
            CatchPerformed[actionId] = subscriber.Owner;
            subscriber.OnPerformed?.Invoke();
        }

        /// <summary>
        /// Обработка события отпускания кнопки
        /// Если подпсиант - не тот кто ловил OnPerformed - прерывание
        /// </summary>
        /// <param name="ctx"></param>
        private static void OnCanceled(InputAction.CallbackContext ctx)
        {
            var actionId = $"{ctx.action.actionMap.name}/{ctx.action.name}";
            if (Settings.Data().InputDebugMode)
                Debug.Log($"[InputController] {actionId} canceled by '{ctx.control.name}'");
            var subscribers = ActionsUsers[actionId];
            if (subscribers == null || subscribers.Count == 0)
            {
                CatchPerformed[actionId] = null;
                return;
            }

            var subscriber = subscribers[^1];
            if (CatchPerformed[actionId] != subscriber.Owner)
            {
                CatchPerformed[actionId] = null;
                return;
            }

            subscriber.OnCanceled?.Invoke();
            CatchPerformed[actionId] = null;
        }
    }
}