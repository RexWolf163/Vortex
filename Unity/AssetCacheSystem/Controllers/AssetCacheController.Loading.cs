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
            CancellationToken ct = default, string group = null) where T : Object
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            var cache = ResolveGroup(group);
            var record = RegisterOwner(cache, owner, reference);

            try
            {
                T result;

                // 1. Уже в кэше (active или survivor) — мгновенный возврат + revive из survivors.
                if (cache.Handles.TryGetValue(reference, out var ready))
                {
                    ReviveFromSurvivor(cache, reference);
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] HIT  ref={ReferenceLabel(reference)} owner={OwnerLabel(owner)} " +
                                  $"group={GroupLabel(cache)}");
                    result = (T)ready.Result;
                }
                // 2. Идёт inflight — присоединяемся к нему.
                else if (cache.Inflight.TryGetValue(reference, out var slot))
                {
                    if (_debugLogging)
                        Debug.Log($"[{LogTag}] JOIN ref={ReferenceLabel(reference)} owner={OwnerLabel(owner)} " +
                                  $"group={GroupLabel(cache)}");
                    result = (T)await slot.Completion.Task.AttachExternalCancellation(ct);
                }
                // 3. Новая загрузка.
                else
                {
                    result = await StartLoad<T>(cache, reference, ct);
                }

                // Пока грузили, владельца отпустили (Release удаляет запись целиком) — удержания этого Load
                // больше нет. Отдать ассет без удержания нельзя: его вытеснят/выгрузят из-под держателя.
                if (!IsSameRecord(cache, owner, record))
                    throw new OperationCanceledException(
                        $"[{LogTag}] Owner {OwnerLabel(owner)} was released while loading {ReferenceLabel(reference)}");

                return result;
            }
            catch
            {
                // Загрузка провалилась или waiter отменён — снимаем удержание этого Load, иначе
                // фантомный lock не даст ассету уйти в survivors/выгрузиться. Реальную загрузку
                // для остальных waiter'ов это не затрагивает (она живёт в StartLoad).
                UnregisterOwner(cache, owner, reference, record);
                throw;
            }
        }

        /// <inheritdoc/>
        public T LoadSync<T>(object owner, AssetReference reference, string group = null) where T : Object
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            var cache = ResolveGroup(group);
            var record = RegisterOwner(cache, owner, reference);

            // 1. Уже в кэше (active или survivor) — мгновенный возврат + revive из survivors.
            if (cache.Handles.TryGetValue(reference, out var ready))
            {
                ReviveFromSurvivor(cache, reference);
                if (_debugLogging)
                    Debug.Log($"[{LogTag}] HIT  ref={ReferenceLabel(reference)} owner={OwnerLabel(owner)} " +
                              $"group={GroupLabel(cache)}");
                return (T)ready.Result;
            }

            // 2. Идёт inflight — присоединяемся к нему.
            if (cache.Inflight.TryGetValue(reference, out var slot))
            {
                slot.Handle.WaitForCompletion();
                if (slot.Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    UnregisterOwner(cache, owner, reference, record);
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
                UnregisterOwner(cache, owner, reference, record);
                return null;
            }

            cache.Handles[reference] = handle;
            return handle.Result;
        }

        /// <summary>
        /// Запуск новой загрузки через <see cref="Addressables.LoadAssetAsync{TObject}(object)"/>.
        /// Создаёт <see cref="InflightLoad"/>-слот, на который подключаются все параллельные
        /// waiter'ы того же <paramref name="reference"/> в группе. По завершении handle помещается в
        /// <see cref="AssetCacheGroupModel.Handles"/>, при отсутствии active-владельцев — в survivors.
        ///
        /// Отмена <paramref name="ct"/> waiter'а НЕ прерывает реальную загрузку — она нужна
        /// другим waiter'ам. После завершения <c>handle.ToUniTask()</c> ct-проверка бросит OCE,
        /// но handle уже корректно учтён в реестре.
        /// </summary>
        private async UniTask<T> StartLoad<T>(AssetCacheGroupModel cache, AssetReference reference,
            CancellationToken ct) where T : Object
        {
            var model = Model;
            var handle = Addressables.LoadAssetAsync<T>(reference);
            var slot = new InflightLoad
            {
                Handle = handle,
                Completion = new UniTaskCompletionSource<Object>()
            };
            cache.Inflight[reference] = slot;

            if (_debugLogging)
                Debug.Log($"[{LogTag}] LOAD ref={ReferenceLabel(reference)} group={GroupLabel(cache)}");

            try
            {
                // Внимание: реальная загрузка НЕ привязана к ct waiter'а — она нужна другим waiter'ам.
                var loaded = await handle.ToUniTask();

                // Пока шла загрузка, мог пройти Cleanup: наш handle уже освобождён в Cleanup, waiter'ы
                // там же отменены, а реестр группы принадлежит старой модели. Больше делать нечего.
                if (Model != model)
                    throw new OperationCanceledException();

                cache.Handles[reference] = handle;
                slot.Completion.TrySetResult(loaded);

                // Если все waiter'ы отменились до завершения — никаких active owner'ов нет.
                // Помещаем в survivors на случай, если кто-то запросит ref повторно.
                if (!IsHeldByAnyOwner(cache, reference))
                    PushSurvivor(cache, reference);

                // Каллер отменил waiter — бросаем OCE, но handle уже корректно лежит в Handles.
                ct.ThrowIfCancellationRequested();
                return (T)loaded;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Реальный сбой загрузки (не отмена waiter'а). Чистим handle и сообщаем всем waiter'ам.
                slot.Completion.TrySetException(ex);
                if (handle.IsValid()) Addressables.Release(handle);
                cache.Handles.Remove(reference);
                throw;
            }
            finally
            {
                cache.Inflight.Remove(reference);
            }
        }

        /// <summary>
        /// Регистрирует удержание ассета владельцем в группе: каждый Load добавляет одно удержание.
        /// Счётчик, а не флаг: отмена/провал одного Load снимает только своё удержание и не трогает
        /// другие Load того же владельца на тот же ref — иначе отменённая, но ещё летящая загрузка
        /// сняла бы замок живой (повторный Load после отмены).
        /// Возвращает запись владельца, в которую легло удержание, — поколение: <see cref="Release"/>
        /// удаляет запись целиком, следующий Load заводит новую. По ней откат и успех Load отличают
        /// «своё» удержание от удержаний Load, зарегистрированных после Release.
        /// </summary>
        private static Dictionary<AssetReference, int> RegisterOwner(AssetCacheGroupModel cache, object owner,
            AssetReference reference)
        {
            if (!cache.Locks.TryGetValue(owner, out var refs))
            {
                refs = new Dictionary<AssetReference, int>();
                cache.Locks[owner] = refs;
            }

            refs[reference] = refs.TryGetValue(reference, out var holds) ? holds + 1 : 1;
            return refs;
        }

        /// <summary>
        /// Запись владельца — та же, в которой регистрировалось удержание: с момента регистрации
        /// владельца не отпускали (<see cref="Release"/>).
        /// </summary>
        private static bool IsSameRecord(AssetCacheGroupModel cache, object owner,
            Dictionary<AssetReference, int> record) =>
            cache.Locks.TryGetValue(owner, out var current) && ReferenceEquals(current, record);

        /// <summary>
        /// Снимает одно удержание <paramref name="reference"/> с <paramref name="owner"/> после
        /// провала/отмены загрузки; остальные удержания того же владельца сохраняются. Если удержаний
        /// ref не осталось ни у кого, а ассет всё же успел загрузиться — отправляет его в survivors
        /// (иначе handle осиротеет: не в survivors — не выгрузится по LRU). Снимает только в записи
        /// <paramref name="record"/>, где удержание регистрировалось: после Release владельца или Cleanup
        /// записи нет либо она уже другая (новые Load того же владельца) — её удержания чужие, выходим.
        /// </summary>
        private void UnregisterOwner(AssetCacheGroupModel cache, object owner, AssetReference reference,
            Dictionary<AssetReference, int> record)
        {
            if (!IsSameRecord(cache, owner, record)) return;
            var refs = record;
            if (!refs.TryGetValue(reference, out var holds)) return;

            if (holds > 1)
            {
                refs[reference] = holds - 1;
                return;
            }

            refs.Remove(reference);
            if (refs.Count == 0)
                cache.Locks.Remove(owner);

            if (!IsHeldByAnyOwner(cache, reference) && cache.Handles.ContainsKey(reference))
                PushSurvivor(cache, reference);
        }

        /// <summary>
        /// Если ассет был в survivors группы — изъять, он снова active.
        /// </summary>
        private static void ReviveFromSurvivor(AssetCacheGroupModel cache, AssetReference reference)
        {
            // LinkedList.Remove(value) — O(n), для типичных размеров (≤ capacity, обычно 32-128) копейки.
            cache.Survivors.Remove(reference);
        }

        private static string ReferenceLabel(AssetReference r) => r?.AssetGUID ?? "<null>";
        private static string OwnerLabel(object o) => o?.ToString() ?? "<null>";
    }
}
