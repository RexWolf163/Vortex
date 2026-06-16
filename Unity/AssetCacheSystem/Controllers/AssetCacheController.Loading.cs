using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vortex.Unity.AssetCacheSystem.Models;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AssetCacheSystem.Controllers
{
    public sealed partial class AssetCacheController
    {
        /// <inheritdoc/>
        public async UniTask<T> Load<T>(object owner, AssetReference reference,
            CancellationToken ct = default) where T : Object
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            RegisterOwner(owner, reference);

            try
            {
                // 1. Уже в кэше (active или survivor) — мгновенный возврат + revive из survivors.
                if (Model.Handles.TryGetValue(reference, out var ready))
                {
                    ReviveFromSurvivor(reference);
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] HIT  ref={ReferenceLabel(reference)} owner={OwnerLabel(owner)}");
                    return (T)ready.Result;
                }

                // 2. Идёт inflight — присоединяемся к нему.
                if (Model.Inflight.TryGetValue(reference, out var slot))
                {
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] JOIN ref={ReferenceLabel(reference)} owner={OwnerLabel(owner)}");
                    var joined = await slot.Completion.Task.AttachExternalCancellation(ct);
                    return (T)joined;
                }

                // 3. Новая загрузка.
                return await StartLoad<T>(reference, ct);
            }
            catch
            {
                // Загрузка провалилась или waiter отменён — снимаем lock владельца, иначе
                // фантомный lock не даст ассету уйти в survivors/выгрузиться. Реальную загрузку
                // для остальных waiter'ов это не затрагивает (она живёт в StartLoad).
                UnregisterOwner(owner, reference);
                throw;
            }
        }

        /// <inheritdoc/>
        public T LoadSync<T>(object owner, AssetReference reference) where T : Object
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            RegisterOwner(owner, reference);

            // 1. Уже в кэше (active или survivor) — мгновенный возврат + revive из survivors.
            if (Model.Handles.TryGetValue(reference, out var ready))
            {
                ReviveFromSurvivor(reference);
                if (_debugLogging)
                    Debug.Log($"[{LogTag}] HIT  ref={ReferenceLabel(reference)} owner={OwnerLabel(owner)}");
                return (T)ready.Result;
            }

            // 2. Идёт inflight — присоединяемся к нему.
            if (Model.Inflight.TryGetValue(reference, out var slot))
            {
                slot.Handle.WaitForCompletion();
                if (slot.Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    UnregisterOwner(owner, reference);
                    return null;
                }

                return (T)slot.Handle.Result; // Handles проставит StartLoad, owner уже зарегистрирован
            }

            // 3. Новая загрузка.
            var handle = Addressables.LoadAssetAsync<T>(reference);
            handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                UnregisterOwner(owner, reference);
                return null;
            }

            Model.Handles[reference] = handle;
            return handle.Result;
        }

        /// <summary>
        /// Запуск новой загрузки через <see cref="Addressables.LoadAssetAsync{TObject}(object)"/>.
        /// Создаёт <see cref="InflightLoad"/>-слот, на который подключаются все параллельные
        /// waiter'ы того же <paramref name="reference"/>. По завершении handle помещается в
        /// <see cref="AssetCacheModel.Handles"/>, при отсутствии active-владельцев — в survivors.
        ///
        /// Отмена <paramref name="ct"/> waiter'а НЕ прерывает реальную загрузку — она нужна
        /// другим waiter'ам. После завершения <c>handle.ToUniTask()</c> ct-проверка бросит OCE,
        /// но handle уже корректно учтён в реестре.
        /// </summary>
        private async UniTask<T> StartLoad<T>(AssetReference reference, CancellationToken ct)
            where T : Object
        {
            var handle = Addressables.LoadAssetAsync<Object>(reference);
            var slot = new InflightLoad
            {
                Handle = handle,
                Completion = new UniTaskCompletionSource<Object>()
            };
            Model.Inflight[reference] = slot;

            if (_debugLogging)
                Debug.Log($"[{LogTag}] LOAD ref={ReferenceLabel(reference)}");

            try
            {
                // Внимание: реальная загрузка НЕ привязана к ct waiter'а — она нужна другим waiter'ам.
                var loaded = await handle.ToUniTask();

                // Пока шла загрузка, мог пройти Cleanup: Model обнулён, наш handle уже освобождён
                // в Cleanup, waiter'ы там же отменены. Больше делать нечего, выходим отменой.
                if (Model == null)
                    throw new OperationCanceledException();

                Model.Handles[reference] = handle;
                slot.Completion.TrySetResult(loaded);

                // Если все waiter'ы отменились до завершения — никаких active owner'ов нет.
                // Помещаем в survivors на случай, если кто-то запросит ref повторно.
                if (!IsHeldByAnyOwner(reference))
                    PushSurvivor(reference);

                // Каллер отменил waiter — бросаем OCE, но handle уже корректно лежит в Handles.
                ct.ThrowIfCancellationRequested();
                return (T)loaded;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Реальный сбой загрузки (не отмена waiter'а). Чистим handle и сообщаем всем waiter'ам.
                slot.Completion.TrySetException(ex);
                if (handle.IsValid()) Addressables.Release(handle);
                Model?.Handles.Remove(reference);
                throw;
            }
            finally
            {
                Model?.Inflight.Remove(reference);
            }
        }

        /// <summary>
        /// Регистрирует владельца как держателя ассета. Идемпотентно по паре (owner, ref).
        /// </summary>
        private void RegisterOwner(object owner, AssetReference reference)
        {
            if (!Model.Locks.TryGetValue(owner, out var refs))
            {
                refs = new HashSet<AssetReference>();
                Model.Locks[owner] = refs;
            }

            refs.Add(reference);
        }

        /// <summary>
        /// Снимает удержание <paramref name="reference"/> с <paramref name="owner"/> после
        /// провала/отмены загрузки. Если ассет всё же успел загрузиться, но активных владельцев
        /// не осталось — отправляет его в survivors (иначе handle осиротеет: не в survivors —
        /// не выгрузится по LRU). Null-safe относительно Model (мог пройти Cleanup).
        /// </summary>
        private void UnregisterOwner(object owner, AssetReference reference)
        {
            if (Model == null) return;
            if (!Model.Locks.TryGetValue(owner, out var refs)) return;
            if (!refs.Remove(reference)) return;

            if (refs.Count == 0)
                Model.Locks.Remove(owner);

            if (!IsHeldByAnyOwner(reference) && Model.Handles.ContainsKey(reference))
                PushSurvivor(reference);
        }

        /// <summary>
        /// Если ассет был в survivors — изъять, он снова active.
        /// </summary>
        private void ReviveFromSurvivor(AssetReference reference)
        {
            // LinkedList.Remove(value) — O(n), для типичных размеров (≤ capacity, обычно 32-128) копейки.
            Model.Survivors.Remove(reference);
        }

        private static string ReferenceLabel(AssetReference r) => r?.AssetGUID ?? "<null>";
        private static string OwnerLabel(object o) => o?.ToString() ?? "<null>";
    }
}