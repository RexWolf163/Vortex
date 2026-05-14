using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AssetCacheSystem.Controllers
{
    public sealed partial class AssetCacheController
    {
        /// <inheritdoc/>
        public void Release(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            // 1. Sweep "тихо умерших" UnityEngine.Object-владельцев.
            //    Их собираем в отдельный список — модифицировать Locks во время foreach нельзя.
            List<object> deadOwners = null;
            if (Model == null)
                return;

            foreach (var kvp in Model.Locks)
            {
                var o = kvp.Key;
                if (o is Object uo && uo == null)
                {
                    deadOwners ??= new List<object>();
                    deadOwners.Add(o);
                }
            }

            if (deadOwners != null)
                foreach (var dead in deadOwners)
                    ReleaseOwner(dead, isSweep: true);

            // 2. Освобождаем сам запрошенный owner.
            ReleaseOwner(owner, isSweep: false);
        }

        /// <summary>
        /// Освобождает одного владельца: удаляет его из <see cref="AssetCacheModel.Locks"/>,
        /// для каждого удерживаемого ref проверяет — остался ли активный держатель. Если нет —
        /// ref уходит в survivors через <see cref="PushSurvivor"/>.
        /// </summary>
        /// <param name="isSweep">
        /// <c>true</c> — вызов из автоматического sweep'а уничтоженных Unity-владельцев;
        /// в этом случае предупреждение «unknown owner» подавляется (норма для повторного sweep'а).
        /// </param>
        private void ReleaseOwner(object owner, bool isSweep)
        {
            if (!Model.Locks.TryGetValue(owner, out var refs))
            {
                if (!isSweep && _debugLogging)
                    Debug.LogWarning($"[{LogTag}] Release for unknown owner {OwnerLabel(owner)}");
                return;
            }

            Model.Locks.Remove(owner);
            if (_debugLogging)
                Debug.Log($"[{LogTag}] {(isSweep ? "SWEEP" : "REL ")} owner={OwnerLabel(owner)} refs={refs.Count}");

            foreach (var reference in refs)
            {
                // Если ассет ещё держит кто-то — оставить active, ничего не делаем.
                if (IsHeldByAnyOwner(reference))
                    continue;

                // Активных владельцев нет: ассет должен либо лечь в survivors, либо ждать там
                // (inflight попадёт в survivors при завершении StartLoad).
                if (Model.Handles.ContainsKey(reference))
                    PushSurvivor(reference);
            }
        }

        /// <summary>
        /// Проверка: держит ли хоть один зарегистрированный владелец этот AssetReference.
        /// </summary>
        private bool IsHeldByAnyOwner(AssetReference reference)
        {
            foreach (var kvp in Model.Locks)
                if (kvp.Value.Contains(reference))
                    return true;
            return false;
        }

        /// <summary>
        /// Поместить ref в хвост survivors-очереди. Если уже там — переместить в хвост (refresh).
        /// При переполнении — eviction с головы.
        /// </summary>
        private void PushSurvivor(AssetReference reference)
        {
            Model.Survivors.Remove(reference); // refresh-положение, если уже был
            Model.Survivors.AddLast(reference);
            EvictIfOverflow();
        }

        /// <summary>
        /// Eviction по capacity: с головы списка снимаем самые старые.
        /// Если ref внезапно снова кому-то нужен (revive не сработал) — удаляем из survivors без Release.
        /// Иначе реальный Addressables.Release + удаление из Handles.
        /// </summary>
        private void EvictIfOverflow()
        {
            while (Model.Survivors.Count > _survivorCapacity)
            {
                var node = Model.Survivors.First;
                if (node == null) break;
                var reference = node.Value;
                Model.Survivors.RemoveFirst();

                if (IsHeldByAnyOwner(reference))
                {
                    // Кто-то снова взял в работу — survivor больше не нужен, handle остаётся в Handles.
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] EVICT-skip ref={ReferenceLabel(reference)} (revived)");
                    continue;
                }

                if (Model.Handles.TryGetValue(reference, out var handle))
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    Model.Handles.Remove(reference);
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] EVICT ref={ReferenceLabel(reference)}");
                }
            }
        }
    }
}