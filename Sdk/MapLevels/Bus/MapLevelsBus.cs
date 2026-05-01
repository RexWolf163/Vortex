using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.SettingsSystem.Model;
using Vortex.Sdk.MapLevels.Controllers;
using Vortex.Sdk.MapLevels.Interfaces;
using Vortex.Sdk.MapLevels.Model;

namespace Vortex.Sdk.MapLevels.Bus
{
    /// <summary>
    /// Статическая шина пакета MapLevels.
    /// При старте приложения создаёт контроллер согласно типу из настроек
    /// (Settings.Data().MapLevelsControllerTypeName), получая его через рефлексию.
    /// Init контроллера откладывается до первого события жизненного цикла игры
    /// (GameController.OnNewGame / OnLoadGame).
    /// </summary>
    public static class MapLevelsBus
    {
        /// <summary>
        /// Активный контроллер пакета.
        /// </summary>
        public static IMapLevelsController Controller { get; private set; }

        /// <summary>
        /// Runtime-модель пакета (доступна после инициализации контроллера).
        /// </summary>
        public static MapLevelsModel Data => Controller?.Model;

        /// <summary>
        /// Конфиг системы управления уровнями карты
        /// </summary>
        public static MapLevelsConfig Config { get; private set; }

        /// <summary>
        /// Контроллер инициализирован и готов к работе.
        /// </summary>
        public static bool IsReady => Controller is { IsInitialized: true };

        /// <summary>
        /// Контроллер инициализирован.
        /// </summary>
        public static InitValve OnReady { get; } = InitValve.Create(out Open);

        private static readonly Action Open;

        /// <summary>
        /// Контроллер очищен (Cleanup).
        /// </summary>
        public static event Action OnRelease;

        /// <summary>
        /// Снятие регистрации контейнера карт
        /// </summary>
        public static event Action OnMapsContainerReleased;

        /// <summary>
        /// Регистрация контейнера карт
        /// </summary>
        public static event Action OnMapsContainerRegistered;

        /// <summary>
        /// Контейнер-родитель для префабов уровней
        /// </summary>
        public static Transform MapsParent { get; private set; }

        /// <summary>
        /// Создание контроллера и подписка на жизненный цикл игры.
        /// Запускается автоматически при загрузке домена.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap()
        {
            Settings.OnInit -= CreateController;
            Settings.OnInit += CreateController;

            App.OnExit -= Dispose;
            App.OnExit += Dispose;
        }

        private static void Dispose()
        {
            Settings.OnInit -= CreateController;
            App.OnExit -= Dispose;
        }

        /// <summary>
        /// Сообщение от контроллера: инициализация завершена.
        /// </summary>
        private static void NotifyReady() => Open?.Invoke();

        /// <summary>
        /// Сообщение от контроллера: ресурсы освобождены.
        /// </summary>
        private static void NotifyReleased()
        {
            Controller.OnInitialized += NotifyReady;
            Controller.OnReleased += NotifyReleased;

            OnRelease?.Invoke();
        }

        /// <summary>
        /// Получает тип контроллера из settingsData.MapLevels.Controller
        /// и создает экземпляр.
        ///
        /// По дефолту, контроллер должен наследоваться от класса Singleton,
        /// для защиты от мультиинстансинга (логирование ошибки)
        /// </summary>
        private static void CreateController()
        {
            if (Controller != null) return;

            var settingsData = Settings.Data();
            if (settingsData == null) return;

            Config = settingsData.MapLevels;

            var typeName = Config.Controller;
            var resolvedType = ResolveControllerType(typeName);
            if (resolvedType == null)
            {
                Debug.LogError($"[MapLevelBus] Controller {typeName} not found!");
                return;
            }

            Controller = (IMapLevelsController)Activator.CreateInstance(resolvedType);
            Controller.OnInitialized += NotifyReady;
            Controller.OnReleased += NotifyReleased;
            Controller.Init();
        }

        /// <summary>
        /// Ищет тип по FullName.
        /// </summary>
        private static Type ResolveControllerType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeof(MapLevelsController);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var type = assemblies.SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == typeName
                                     && typeof(IMapLevelsController).IsAssignableFrom(t)
                                     && t.GetConstructor(Type.EmptyTypes) != null
                                     && !t.IsAbstract
                                     && !t.IsInterface);
            return type;
        }

        /// <summary>
        /// Регистрация контейнера карт
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static bool RegisterMapsParent(Transform parent)
        {
            if (MapsParent != null)
            {
                Debug.LogError("[MapLevelsBus] Контейнер-родитель для уровней карт уже зарегистрирован!");
                return false;
            }

            MapsParent = parent;
            OnMapsContainerRegistered?.Invoke();
            return true;
        }

        /// <summary>
        /// Снятие с регистрации контейнера карт
        /// </summary>
        /// <param name="parent"></param>
        public static void UnregisterMapsParent(Transform parent)
        {
            if (MapsParent != parent)
            {
                Debug.LogError(
                    "[MapLevelsBus] Попытка дерегистрации незарегистрированного контейнера-родителя для уровней карт!");
                return;
            }

            MapsParent = null;
            OnMapsContainerReleased?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: полный список рабочих копий уровней для инструментария верстальщика.
        /// </summary>
        public static IReadOnlyDictionary<string, MapLevelModel> Editor_GetAllLevels() =>
            Data?.GetCatalog();
#endif
    }
}