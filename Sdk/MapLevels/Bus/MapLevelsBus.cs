using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.Core.GameCore;
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
        /// Контроллер инициализирован и готов к работе.
        /// </summary>
        public static bool IsReady => Controller is { IsInitialized: true };

        /// <summary>
        /// Контроллер инициализирован.
        /// </summary>
        public static event Action OnReady;

        /// <summary>
        /// Контроллер очищен (Cleanup).
        /// </summary>
        public static event Action OnRelease;

        /// <summary>
        /// Создание контроллера и подписка на жизненный цикл игры.
        /// Запускается автоматически при загрузке домена.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GameController.OnNewGame  -= OnGameLifecycleEvent;
            GameController.OnLoadGame -= OnGameLifecycleEvent;
            GameController.OnNewGame  += OnGameLifecycleEvent;
            GameController.OnLoadGame += OnGameLifecycleEvent;

            // Попытка создать контроллер из настроек сразу.
            // Если SettingsDriver ещё не успел инициализироваться (порядок RuntimeInit между ними
            // не гарантирован Unity) — повторная попытка случится при первом game-событии.
            TryCreateController();
        }

        /// <summary>
        /// Замена реализации контроллера (для моков в тестах).
        /// Должна вызываться до первого события жизненного цикла игры.
        /// </summary>
        public static void OverrideController(IMapLevelsController controller)
        {
            if (controller == null)
            {
                Debug.LogError("[MapLevelsBus] OverrideController получил null. Отмена.");
                return;
            }
            if (Controller is { IsInitialized: true })
            {
                Debug.LogError("[MapLevelsBus] OverrideController вызван после Init. Отмена.");
                return;
            }
            Controller = controller;
        }

        /// <summary>
        /// Сообщение от контроллера: инициализация завершена.
        /// </summary>
        internal static void NotifyReady() => OnReady?.Invoke();

        /// <summary>
        /// Сообщение от контроллера: ресурсы освобождены.
        /// </summary>
        internal static void NotifyReleased() => OnRelease?.Invoke();

        private static void OnGameLifecycleEvent()
        {
            // Если контроллер ещё не создан — повторная попытка резолвить тип из настроек.
            // К моменту первого game-события Settings гарантированно загружен.
            if (Controller == null)
                TryCreateController();

            if (Controller == null)
            {
                Debug.LogError("[MapLevelsBus] Контроллер не создан — пакет не активирован.");
                return;
            }

            // Если контроллер уже инициализирован — он сам отработает событие
            // через свои внутренние подписки.
            if (Controller.IsInitialized) return;

            Controller.Init();
        }

        /// <summary>
        /// Резолвит тип контроллера из Settings.Data().MapLevelsControllerTypeName,
        /// получает Singleton.Instance через рефлексию и присваивает Controller.
        /// Идемпотентен: если Controller уже задан (в т.ч. через OverrideController) — не трогает.
        /// </summary>
        private static void TryCreateController()
        {
            if (Controller != null) return;

            var settingsData = Settings.Data();
            if (settingsData == null) return;

            var typeName = settingsData.MapLevelsControllerTypeName;
            var resolvedType = ResolveControllerType(typeName);
            if (resolvedType == null)
            {
                Debug.LogError(
                    $"[MapLevelsBus] Тип контроллера \"{typeName}\" не разрешён. " +
                    "Проверьте MapLevelsSettings в Resources/Settings.");
                return;
            }

            Controller = ResolveSingletonInstance(resolvedType);
        }

        /// <summary>
        /// Резолвит тип по AssemblyQualifiedName. Fallback на дефолтный MapLevelsController
        /// при пустом имени или невалидном/несоответствующем интерфейсу типе.
        /// </summary>
        private static Type ResolveControllerType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeof(MapLevelsController);

            var type = Type.GetType(typeName);
            if (type == null) return null;
            if (!typeof(IMapLevelsController).IsAssignableFrom(type)) return null;
            if (type.GetConstructor(Type.EmptyTypes) == null) return null;
            if (type.IsAbstract || type.IsInterface) return null;

            return type;
        }

        /// <summary>
        /// Получает Singleton&lt;T&gt;.Instance через рефлексию для произвольного T.
        /// Контракт: тип реализует IMapLevelsController и наследует Singleton&lt;T&gt;.
        /// </summary>
        private static IMapLevelsController ResolveSingletonInstance(Type controllerType)
        {
            var singletonOpenType = typeof(Singleton<>);
            var singletonClosedType = singletonOpenType.MakeGenericType(controllerType);
            var instanceProp = singletonClosedType.GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.Static);

            if (instanceProp == null)
            {
                Debug.LogError(
                    $"[MapLevelsBus] Тип \"{controllerType.FullName}\" не наследует Singleton<T>.");
                return null;
            }

            return instanceProp.GetValue(null) as IMapLevelsController;
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
