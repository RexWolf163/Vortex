using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.MapLevels.Bus;
using Vortex.Sdk.MapLevels.Model;
using Object = UnityEngine.Object;

namespace Vortex.Sdk.MapLevels.Controllers
{
    public partial class MapLevelsController
    {
        public event Action<string> OnLevelLoaded;
        public event Action<string> OnLevelUnloaded;

        private static Transform _voidParent;

        /// <summary>
        /// Складской контейнер для загруженных префабов.
        /// Используется если не назначен контейнер-представление MapLevelsBus.MapsParent.  
        /// </summary>
        private static Transform VoidParent
        {
            get
            {
                if (_voidParent is null)
                {
                    var go = new GameObject("MapVoid");
                    go.SetActive(false);
                    Object.DontDestroyOnLoad(go);
                    _voidParent = go.transform;
                }

                return _voidParent;
            }
        }

        private static Transform GetMapsParent() => MapLevelsBus.MapsParent ?? VoidParent;

        /// <summary>
        /// Проверяет наличие загруженного префаба и загружает его при необходимости
        /// </summary>
        private void EnsureLoaded(string levelGuid)
        {
            var container = Model.GetContainer(levelGuid);
            if (container == null) return;
            if (container.State.Value == MapContainerState.Loaded && container.Instance != null) return;

            var level = Model.GetLevel(levelGuid);
            if (level == null || level.Prefab == null)
            {
                Debug.LogError($"[MapLevelsController] Префаб уровня \"{levelGuid}\" пуст или не найден.");
                return;
            }

            var parent = GetMapsParent();
            container.State.Set(MapContainerState.Loading, this);
            container.Instance = Object.Instantiate(level.Prefab, Vector3.zero, Quaternion.identity, parent);
            container.Instance.name = level.Name;
            container.State.Set(MapContainerState.Loaded, this);

            OnLevelLoaded?.Invoke(levelGuid);
        }

        /// <summary>
        /// Уничтожает инстанс уровня и переводит контейнер в Empty.
        /// </summary>
        private void Unload(string levelGuid)
        {
            var container = Model.GetContainer(levelGuid);
            if (container == null) return;
            if (container.State.Value == MapContainerState.Empty) return;

            container.State.Set(MapContainerState.Unloading, this);
            if (container.Instance != null)
            {
                Object.Destroy(container.Instance);
                container.Instance = null;
            }

            container.State.Set(MapContainerState.Empty, this);

            OnLevelUnloaded?.Invoke(levelGuid);
        }

        /// <summary>
        /// Уничтожает все инстансы. Используется в Cleanup и OnNewGame.
        /// </summary>
        private void UnloadAll()
        {
            if (Model?.Containers == null) return;

            foreach (var key in new List<string>(Model.Containers.Keys))
                Unload(key);

            Model.ActiveLevelGuid.Set(string.Empty, this);
        }

        /// <summary>
        /// План стриминга при смене активного уровня:
        ///   - подгружаем прямых соседей (hop=1), которых ещё нет,
        ///   - выгружаем всё, что оказалось на дистанции >= unloadDistance.
        /// Подгрузка через UniTask.Yield для распределения по кадрам.
        /// </summary>
        private void ApplyStreamingPlan(string activeGuid) =>
            ApplyStreamingPlanAsync(activeGuid).Forget();

        private async UniTaskVoid ApplyStreamingPlanAsync(string activeGuid)
        {
            // Защита от состояния гонки: если контроллер был очищен пока шёл план — выходим.
            if (!IsInitialized) return;
            if (Model.ActiveLevelGuid.Value.IsNullOrWhitespace()) return;
            if (Model.ActiveLevelGuid.Value != activeGuid) return;

            var unloadDistance = MapLevelsBus.Config.UnloadDistance;

            // BFS от активного уровня до глубины unloadDistance - 1 — всё что в зоне удержания.
            var distances = ComputeDistances(activeGuid, unloadDistance - 1);

            // Загрузка прямых соседей (hop=1).
            var activeLevel = Model.GetLevel(activeGuid);
            if (activeLevel == null)
            {
                Debug.LogError($"[MapLevelsController] ApplyStreamingPlan: уровень \"{activeGuid}\" не найден в каталоге.");
                return;
            }

            var neighbors = activeLevel.NeighborGuids;
            if (neighbors == null)
            {
                Debug.LogError($"[MapLevelsController] ApplyStreamingPlan: NeighborGuids уровня \"{activeGuid}\" равен null.");
                return;
            }

            foreach (var neighbor in neighbors)
            {
                var container = Model.GetContainer(neighbor);
                if (container == null) continue;
                if (container.State.Value == MapContainerState.Loaded) continue;

                EnsureLoaded(neighbor);
                await UniTask.Yield();
            }

            // Выгрузка всего, что не попало в зону удержания.
            foreach (var key in new List<string>(Model.Containers.Keys))
            {
                if (!IsInitialized) return;
                if (distances.ContainsKey(key)) continue;

                var container = Model.Containers[key];
                if (container.State.Value == MapContainerState.Empty) continue;

                Unload(key);
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// BFS по графу соседей. Возвращает GUID → дистанция от origin (0 для самого origin),
        /// ограничено maxDepth.
        /// </summary>
        private Dictionary<string, int> ComputeDistances(string origin, int maxDepth)
        {
            var result = new Dictionary<string, int> { [origin] = 0 };
            if (maxDepth <= 0) return result;

            var queue = new Queue<string>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDistance = result[current];
                if (currentDistance >= maxDepth) continue;

                var level = Model.GetLevel(current);
                if (level?.NeighborGuids == null) continue;

                foreach (var neighborGuid in level.NeighborGuids)
                {
                    if (neighborGuid.IsNullOrWhitespace()) continue;
                    if (!Model.Catalog.ContainsKey(neighborGuid)) continue;
                    if (result.ContainsKey(neighborGuid))
                    {
                        if (result[neighborGuid] <= currentDistance) continue;
                        result[neighborGuid] = currentDistance + 1;
                        continue;
                    }

                    result[neighborGuid] = currentDistance + 1;
                    queue.Enqueue(neighborGuid);
                }
            }

            return result;
        }
    }
}