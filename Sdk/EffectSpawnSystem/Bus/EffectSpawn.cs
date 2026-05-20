using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.EffectSpawnSystem.Catalog;
using Vortex.Sdk.EffectSpawnSystem.Components;
using Vortex.Sdk.EffectSpawnSystem.Pool;
using Vortex.Unity.Extensions;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Vortex.Sdk.EffectSpawnSystem.Bus
{
    /// <summary>
    /// Static-фасад системы спауна эффектов.
    ///
    /// Контракт:
    ///  • <see cref="Spawn(Transform,GameObject)"/> и <see cref="Spawn(Transform,string)"/> — target обязателен.
    ///  • Эффекты паркуются в ближайший <see cref="EffectsLayer"/> в parent-цепочке target,
    ///    либо непосредственно в target, если маркер не найден.
    ///  • Идл-инстансы хранятся в неактивном <c>Storage</c> внутри авто-генерируемого <see cref="EffectPool"/>;
    ///    активация/деактивация — автоматически через переход между активным/неактивным parent.
    ///  • Подписан на <see cref="GameController.OnGameStateChanged"/>: при <see cref="GameStates.Paused"/>
    ///    замораживает все активные эффекты, при выходе из паузы — возобновляет.
    /// </summary>
    public static class EffectSpawn
    {
        private static EffectsCatalog _catalog;
        private static EffectPool _pool;

        static EffectSpawn()
        {
            GameController.OnGameStateChanged += OnGameStateChanged;
        }

        /// <summary>
        /// Текущее состояние паузы (по <see cref="GameController.GetState"/> == <see cref="GameStates.Paused"/>).
        /// Используется <see cref="EffectView.OnEnable"/> для фриза свежеспаунированного эффекта,
        /// если он стартует во время паузы.
        /// </summary>
        public static bool IsPaused => GameController.GetState() == GameStates.Paused;

        /// <summary>
        /// Все ключи зарегистрированного каталога. Пустой массив, если каталог не зарегистрирован.
        /// </summary>
        public static IReadOnlyList<string> AllKeys =>
            _catalog?.Keys ?? Array.Empty<string>();

        /// <summary>
        /// Регистрация каталога. Замена существующего допустима — вызывается из стартового кода.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        public static void RegisterCatalog()
        {
            var catalog = AssetDatabaseExt.GetSingletonAsset<EffectsCatalog>();
            _catalog = catalog;
        }

        /// <summary>
        /// Спаун эффекта по прямой ссылке на префаб.
        ///
        /// По умолчанию мировая позиция = <c>target.position</c>, ротация = <see cref="Quaternion.identity"/>.
        /// Опциональные <paramref name="position"/> / <paramref name="rotation"/> переопределяют дефолты.
        /// </summary>
        /// <param name="target">Точка начала поиска <see cref="EffectsLayer"/> + источник дефолтной позиции. Не может быть null.</param>
        /// <param name="prefab">Префаб эффекта с компонентом <see cref="EffectView"/>. Не может быть null.</param>
        /// <param name="position">Опциональная мировая позиция. По умолчанию — <c>target.position</c>.</param>
        /// <param name="rotation">Опциональная мировая ротация. По умолчанию — <see cref="Quaternion.identity"/>.</param>
        /// <returns>Активированный <see cref="EffectView"/> либо null при ошибке валидации.</returns>
        public static EffectView Spawn(Transform target, GameObject prefab,
            Vector3? position = null, Quaternion? rotation = null)
        {
            if (target == null)
            {
                Debug.LogError("[EffectSpawn] target == null");
                return null;
            }

            if (prefab == null)
            {
                Debug.LogError("[EffectSpawn] prefab == null");
                return null;
            }

            var pos = position ?? target.position;
            var rot = rotation ?? Quaternion.identity;
            return Pool.Acquire(prefab, target, pos, rot);
        }

        /// <summary>
        /// Спаун эффекта по ключу из зарегистрированного каталога.
        /// Без зарегистрированного каталога или для незнакомого ключа — Logger.Error + null.
        ///
        /// По умолчанию мировая позиция = <c>target.position</c>, ротация = <see cref="Quaternion.identity"/>.
        /// Опциональные <paramref name="position"/> / <paramref name="rotation"/> переопределяют дефолты.
        /// </summary>
        public static EffectView Spawn(Transform target, string key,
            Vector3? position = null, Quaternion? rotation = null)
        {
            if (target == null)
            {
                Debug.LogError("[EffectSpawn] target == null");
                return null;
            }

            if (_catalog == null)
            {
                Debug.LogError(
                    $"[EffectSpawn] Каталог не зарегистрирован, спаун по ключу '{key}' невозможен. Используйте RegisterCatalog или Spawn(target, prefab).");
                return null;
            }

            var prefab = _catalog.GetPrefab(key);
            if (prefab == null)
            {
                Debug.LogError($"[EffectSpawn] Ключ '{key}' не найден в каталоге '{_catalog.name}'.");
                return null;
            }

            var pos = position ?? target.position;
            var rot = rotation ?? Quaternion.identity;
            return Pool.Acquire(prefab, target, pos, rot);
        }

        /// <summary>
        /// Спаун эффекта по ключу из зарегистрированного каталога.
        /// Без зарегистрированного каталога или для незнакомого ключа — Logger.Error + null.
        ///
        /// По умолчанию мировая позиция = <c>target.position</c>, ротация = <see cref="Quaternion.identity"/>.
        /// Опциональные <paramref name="deltaPosition"/> / <paramref name="rotation"/> добавляют смещение и переопределяют дефолтный поворот.
        /// </summary>
        public static EffectView Spawn(RectTransform target, string key,
            Vector3? deltaPosition = null, Quaternion? rotation = null)
        {
            if (target == null)
            {
                Debug.LogError("[EffectSpawn] target == null");
                return null;
            }

            if (_catalog == null)
            {
                Debug.LogError(
                    $"[EffectSpawn] Каталог не зарегистрирован, спаун по ключу '{key}' невозможен. Используйте RegisterCatalog или Spawn(target, prefab).");
                return null;
            }

            var prefab = _catalog.GetPrefab(key);
            if (prefab == null)
            {
                Debug.LogError($"[EffectSpawn] Ключ '{key}' не найден в каталоге '{_catalog.name}'.");
                return null;
            }

            var pos = target.position;
            if (deltaPosition != null)
                pos += deltaPosition.Value;
            pos.x += Random.Range(-target.rect.width / 2, target.rect.width / 2);
            pos.y += Random.Range(-target.rect.height / 2, target.rect.height / 2);

            var rot = rotation ?? Quaternion.identity;
            return Pool.Acquire(prefab, target, pos, rot);
        }

        /// <summary>
        /// Досрочный возврат активного эффекта в пул. Идемпотентно для уже освобождённых view.
        /// </summary>
        public static void Release(EffectView view)
        {
            if (view == null) return;
            Pool.Return(view);
        }

        // ---- internals ----

        /// <summary>
        /// Авто-генерируемый пул. Создаётся при первом обращении (lazy), переживает смену сцен
        /// через <see cref="Object.DontDestroyOnLoad"/>.
        /// </summary>
        internal static EffectPool Pool
        {
            get
            {
                if (_pool != null) return _pool;

                var rootGo = new GameObject("[EffectVoid]");
                Object.DontDestroyOnLoad(rootGo);
                _pool = rootGo.AddComponent<EffectPool>();

                var storageGo = new GameObject("Storage");
                storageGo.transform.SetParent(rootGo.transform, false);
                storageGo.SetActive(false); // ключевое: idle-инстансы под inactive parent
                _pool.Storage = storageGo.transform;

                return _pool;
            }
        }

        private static void OnGameStateChanged()
        {
            if (_pool == null) return; // ни одного эффекта ещё не было — нечего ставить на паузу

            if (GameController.GetState() == GameStates.Paused)
                _pool.PauseAll();
            else
                _pool.ResumeAll();
        }
    }
}