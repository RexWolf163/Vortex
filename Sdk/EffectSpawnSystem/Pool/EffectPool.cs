using System.Collections.Generic;
using UnityEngine;
using Vortex.Sdk.EffectSpawnSystem.Components;

namespace Vortex.Sdk.EffectSpawnSystem.Pool
{
    /// <summary>
    /// Двухслойное хранилище эффектов:
    ///  • сам объект-владелец (этот компонент) — активный слой, родитель Storage;
    ///  • <see cref="Storage"/> — неактивный child, в нём паркуются idle-инстансы.
    ///
    /// Активация / деактивация инстансов автоматическая через перенос между активным target
    /// и неактивным Storage — Unity сам вызывает <c>OnEnable</c>/<c>OnDisable</c> на
    /// <see cref="EffectView"/>.
    ///
    /// Создаётся lazy в <c>EffectSpawn</c> по образцу <c>MapLevelsController.VoidParent</c>.
    /// </summary>
    public class EffectPool : MonoBehaviour
    {
        /// <summary>Неактивный child-узел, хранилище idle-инстансов.</summary>
        public Transform Storage { get; internal set; }

        // idle: prefab → стек освобождённых view-инстансов из этого префаба
        private readonly Dictionary<GameObject, Stack<EffectView>> _idle = new();
        // active: view → prefab, чтобы знать в какой стек класть при Return
        private readonly Dictionary<EffectView, GameObject> _activePrefab = new();

        /// <summary>
        /// Достать эффект из пула (или создать новый), припарковать в найденный target-layer
        /// и выставить заданные мировые <paramref name="position"/> / <paramref name="rotation"/>.
        /// </summary>
        internal EffectView Acquire(GameObject prefab, Transform target, Vector3 position, Quaternion rotation)
        {
            if (prefab == null || target == null) return null;

            if (!_idle.TryGetValue(prefab, out var stack))
                _idle[prefab] = stack = new Stack<EffectView>();

            EffectView view = null;
            while (stack.Count > 0 && view == null)
                view = stack.Pop();   // защита от потерянных view (Destroy в чужом коде)

            if (view == null)
            {
                view = InstantiateNew(prefab);
                if (view == null) return null;
            }

            _activePrefab[view] = prefab;

            var layer = ResolveLayerTarget(target);
            var t = view.transform;
            t.SetParent(layer, worldPositionStays: false);
            t.SetAsLastSibling();
            t.SetPositionAndRotation(position, rotation);
            // Активация автоматическая: layer active → OnEnable на EffectView сработает сам.

            return view;
        }

        /// <summary>
        /// Вернуть эффект в Storage. Если view не отслеживается — no-op.
        /// </summary>
        internal void Return(EffectView view)
        {
            if (view == null) return;
            if (!_activePrefab.TryGetValue(view, out var prefab)) return;
            _activePrefab.Remove(view);

            view.transform.SetParent(Storage, worldPositionStays: false);
            // Деактивация автоматическая: Storage inactive → OnDisable на EffectView сработает сам.

            if (_idle.TryGetValue(prefab, out var stack))
                stack.Push(view);
        }

        /// <summary>Pause всем активным эффектам (broadcast от шины при <c>GameStates.Paused</c>).</summary>
        internal void PauseAll()
        {
            foreach (var kv in _activePrefab)
                kv.Key.OnPause();
        }

        /// <summary>Resume всем активным эффектам (broadcast от шины при выходе из паузы).</summary>
        internal void ResumeAll()
        {
            foreach (var kv in _activePrefab)
                kv.Key.OnResume();
        }

        private EffectView InstantiateNew(GameObject prefab)
        {
            var go = Object.Instantiate(prefab, Storage);
            var view = go.GetComponent<EffectView>();
            if (view == null)
            {
                Debug.LogError($"[EffectPool] Префаб '{prefab.name}' не имеет EffectView. Эффект не будет работать.");
                Object.Destroy(go);
                return null;
            }
            return view;
        }

        /// <summary>
        /// Поиск точки парковки эффекта: ближайший <see cref="EffectsLayer"/> вверх по цепочке от
        /// <paramref name="target"/>; если найден и у него задан <see cref="EffectsLayer.Target"/> —
        /// он же; иначе transform самого маркера. Если маркер не найден — fallback на сам target.
        /// </summary>
        private static Transform ResolveLayerTarget(Transform target)
        {
            var layer = target.GetComponentInParent<EffectsLayer>();
            if (layer == null) return target;
            return layer.Target != null ? layer.Target : layer.transform;
        }
    }
}
