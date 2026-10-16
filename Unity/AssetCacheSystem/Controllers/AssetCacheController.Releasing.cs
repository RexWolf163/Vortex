using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vortex.Unity.AssetCacheSystem.Models;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AssetCacheSystem.Controllers
{
    public sealed partial class AssetCacheController
    {
        /// <inheritdoc/>
        public void Release(object owner, string group = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            if (Model == null)
                return;

            // 1. Sweep "тихо умерших" UnityEngine.Object-владельцев — по всем группам: уничтоженный
            //    владелец мог держать ассеты в любой группе, а Release для неё уже никто не позовёт.
            foreach (var cache in Model.GetGroups())
                SweepDeadOwners(cache);

            // 2. Освобождаем сам запрошенный owner в его группе.
            ReleaseOwner(ResolveGroup(group), owner, isSweep: false);
        }

        /// <summary>
        /// Освобождает в группе всех <see cref="Object"/>-владельцев, уничтоженных без вызова Release.
        /// </summary>
        private void SweepDeadOwners(AssetCacheGroupModel cache)
        {
            // Собираем в отдельный список — модифицировать Locks во время foreach нельзя.
            List<object> deadOwners = null;
            foreach (var kvp in cache.Locks)
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
                    ReleaseOwner(cache, dead, isSweep: true);
        }

        /// <summary>
        /// Освобождает одного владельца в группе: удаляет его из <see cref="AssetCacheGroupModel.Locks"/> со всеми удержаниями,
        /// для каждого удерживаемого ref проверяет — остался ли активный держатель. Если нет —
        /// ref уходит в survivors группы через <see cref="PushSurvivor"/>.
        /// </summary>
        /// <param name="isSweep">
        /// <c>true</c> — вызов из автоматического sweep'а уничтоженных Unity-владельцев;
        /// в этом случае предупреждение «unknown owner» подавляется (норма для повторного sweep'а).
        /// </param>
        private void ReleaseOwner(AssetCacheGroupModel cache, object owner, bool isSweep)
        {
            if (!cache.Locks.TryGetValue(owner, out var refs))
            {
                if (!isSweep && _debugLogging)
                    Debug.LogWarning($"[{LogTag}] Release for unknown owner {OwnerLabel(owner)} " +
                                     $"in group {GroupLabel(cache)}");
                return;
            }

            cache.Locks.Remove(owner);
            if (_debugLogging)
                Debug.Log($"[{LogTag}] {(isSweep ? "SWEEP" : "REL ")} owner={OwnerLabel(owner)} " +
                          $"refs={refs.Count} group={GroupLabel(cache)}");

            foreach (var reference in refs.Keys)
            {
                // Если ассет ещё держит кто-то — оставить active, ничего не делаем.
                if (IsHeldByAnyOwner(cache, reference))
                    continue;

                // Активных владельцев нет: ассет должен либо лечь в survivors, либо ждать там
                // (inflight попадёт в survivors при завершении StartLoad).
                if (cache.Handles.ContainsKey(reference))
                    PushSurvivor(cache, reference);
            }
        }

        /// <summary>
        /// Проверка: держит ли хоть один владелец группы этот AssetReference.
        /// Другие группы не учитываются — у них свои handle'ы, а физическую выгрузку общего ассета
        /// Addressables не допустит, пока жив хоть один handle.
        /// </summary>
        private static bool IsHeldByAnyOwner(AssetCacheGroupModel cache, AssetReference reference)
        {
            foreach (var kvp in cache.Locks)
                if (kvp.Value.ContainsKey(reference))
                    return true;
            return false;
        }

        /// <summary>
        /// Поместить ref в хвост survivors-очереди группы. Если уже там — переместить в хвост (refresh).
        /// При переполнении — eviction с головы.
        /// </summary>
        private void PushSurvivor(AssetCacheGroupModel cache, AssetReference reference)
        {
            cache.Survivors.Remove(reference); // refresh-положение, если уже был
            cache.Survivors.AddLast(reference);
            EvictIfOverflow(cache);
        }

        /// <summary>
        /// Eviction по ёмкости группы: с головы списка снимаем самые старые.
        /// Если ref внезапно снова кому-то нужен (revive не сработал) — удаляем из survivors без Release.
        /// Иначе реальный Addressables.Release + удаление из Handles.
        /// </summary>
        private void EvictIfOverflow(AssetCacheGroupModel cache)
        {
            while (cache.Survivors.Count > cache.SurvivorCapacity)
            {
                var node = cache.Survivors.First;
                if (node == null) break;
                var reference = node.Value;
                cache.Survivors.RemoveFirst();

                if (IsHeldByAnyOwner(cache, reference))
                {
                    // Кто-то снова взял в работу — survivor больше не нужен, handle остаётся в Handles.
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] EVICT-skip ref={ReferenceLabel(reference)} (revived) " +
                                  $"group={GroupLabel(cache)}");
                    continue;
                }

                if (cache.Handles.TryGetValue(reference, out var handle))
                {
                    if (handle.IsValid())
                        Addressables.Release(handle);
                    cache.Handles.Remove(reference);
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] EVICT ref={ReferenceLabel(reference)} group={GroupLabel(cache)}");
                }
            }
        }
    }
}
