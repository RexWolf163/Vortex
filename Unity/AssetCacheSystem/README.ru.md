# AssetCacheSystem

**Namespace:** `Vortex.Unity.AssetCacheSystem.*`
**Сборка:** `ru.vortex.unity.assetcachesystem` (defineConstraints: `ENABLE_ADDRESSABLES`)

---

## Назначение

Управляемый кэш Addressables-ассетов с пятью ключевыми механиками:

- **Owner-tracking** — кто из потребителей какие `AssetReference` держит и сколькими удержаниями (по одному на каждый `Load`).
- **Inflight-дедупликация** — параллельные запросы одного ref резолвятся одним вызовом `Addressables.LoadAssetAsync`.
- **LRU survivors** — отпущенные, но ещё не выгруженные ассеты лежат в буфере; повторный запрос даёт мгновенный HIT.
- **Sweep уничтоженных Unity-владельцев** — при каждом `Release` ловит `UnityEngine.Object`-владельцев (`MonoBehaviour` и т. п.), удалённых без вызова Release.
- **Группы** — изолированные реестры со своей ёмкостью survivors: поток ассетов одной группы (например, реплик) не вытесняет survivors другой.

Consumer-API — `Load` и `Release` плюс синхронный `LoadSync` (группа — опциональна, без неё работа идёт с дефолтной группой):
```csharp
UniTask<T> AssetCache.Load<T>(object owner, AssetReference reference, CancellationToken ct = default,
                              string group = null);
T          AssetCache.LoadSync<T>(object owner, AssetReference reference, string group = null);
void       AssetCache.Release(object owner, string group = null);
```

Поверх кэша пакет поставляет двух готовых потребителей: `AddressableAssetHandle<T>` (ленивая реализация `AssetHandle<T>`) и `DataStorageTransport` (префаб, подгружаемый на включение).

Вне ответственности:
- Скрытое преобразование `AssetReference` → конкретный T (`Load<T>` ожидает что вызывающий знает реальный тип ассета).
- Sweep POCO-владельцев (только Unity-объекты; для POCO — дисциплина явного Release).
- Streaming, Addressables-labels, scene-load — это уровень Addressables API.

---

## Зависимости

| Зависимость | Назначение |
|---|---|
| `Unity.Addressables` + `Unity.ResourceManager` | Реальная загрузка/выгрузка ассетов |
| `UniTask` + `UniTask.Addressables` | Async API |
| `Vortex.Core.System` | `Singleton<T>` |
| `Vortex.Core.AppSystem` | `App.OnExit` для Dispose |
| `Vortex.Core.SettingsSystem` + `Vortex.Unity.SettingsSystem` | Чтение `SurvivorCapacity`, групп, debug-флага; `SettingsPreset` |
| `Vortex.Core.Extensions` | `InitValve.Create`, `IsNullOrWhitespace` |
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<T>` — база `AddressableAssetHandle<T>` |
| `Vortex.Core.LoggerSystem` | `Log.Print` — предупреждение о повторной загрузке в `AddressableAssetHandle<T>` |
| `Vortex.Unity.UI.Misc` | `DataStorage` — база `DataStorageTransport` |
| `Vortex.Unity.Extensions` | `AssetDatabaseExt.GetSingletonAsset` (Editor-menu, drawer групп) |
| `Vortex.Unity.EditorTools` | `DrawingUtility.DrawSelector` — выпадашка `[AssetCacheGroup]` |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` для SO-маркера |
| Odin Inspector (`Sirenix`) | Атрибуты инспектора настроек и `DataStorageTransport`, drawer `[AssetCacheGroup]` |

`defineConstraints: ["ENABLE_ADDRESSABLES"]` в asmdef — пакет компилируется только в проектах с включёнными Addressables.

---

## Использование

### Регистрация настроек

Ассет `AssetCacheSettings` в `Resources/Settings/` создаётся системой настроек в редакторе, если его нет (`Assets/Resources/Settings/AssetCacheSettings.asset`); `Tools/Vortex/AssetsCache/Config` подсвечивает его в Project window.
- Заполнить `SurvivorCapacity` дефолтной группы (default 32).
- При необходимости добавить именованные группы в `groups`: ключ + `SurvivorCapacity` группы.
- `SettingsDriver` подхватит автоматически.

### Базовый паттерн в MonoBehaviour

```csharp
public class WeaponView : MonoBehaviour
{
    [SerializeField] private AssetReference muzzleFlashRef;
    private GameObject _muzzleFlashPrefab;

    private async void OnEnable()
    {
        // owner = this (Unity-объект — sweep подхватит при уничтожении)
        _muzzleFlashPrefab = await AssetCache.Load<GameObject>(this, muzzleFlashRef, destroyCancellationToken);
    }

    private void OnDestroy()
    {
        // Явный Release не обязателен (sweep ловит уничтожение),
        // но полезен для немедленного освобождения / попадания в survivors.
        AssetCache.Release(this);
    }
}
```

### Загрузка в группу

```csharp
public class VoiceLine : MonoBehaviour
{
    [SerializeField] private AssetReference clipRef;

    // Выпадашка с группами из AssetCacheSettings. Пусто — дефолтная группа.
    [SerializeField, AssetCacheGroup] private string cacheGroup;

    private async UniTask PlayAsync(CancellationToken ct)
    {
        // Release(this) во время загрузки завершит её OperationCanceledException.
        var clip = await AssetCache.Load<AudioClip>(this, clipRef, ct, cacheGroup);
        // ...
    }

    private void OnDisable()
    {
        // Освобождает кладущий — в той же группе, где грузил.
        AssetCache.Release(this, cacheGroup);
    }
}
```

Поле группы уже есть у `DataStorageTransport` (`cacheGroup`) и `AddressableAssetHandle<T>` (`group`).

### POCO-владелец

```csharp
public class AudioCue
{
    private readonly object _ownerKey = new();   // owner = неуничтожимый ключ
    private AudioClip _clip;

    public async UniTask PreloadAsync(AssetReference clipRef, CancellationToken ct)
    {
        _clip = await AssetCache.Load<AudioClip>(_ownerKey, clipRef, ct);
    }

    public void Dispose()
    {
        // Обязательно — sweep не сработает для POCO-владельца.
        AssetCache.Release(_ownerKey);
    }
}
```

### `AddressableAssetHandle<T>`

Реализация `AssetHandle<T>` поверх кэша: поля `reference` (`AssetReference`) и `group` (`[AssetCacheGroup]`). Владелец в кэше — сам handle (`owner = this`), поэтому у handle в `Locks` всегда ровно один ключ; перекрывающиеся загрузки одного handle считаются удержаниями этого ключа. `LoadAsync` → `AssetCache.Load<T>(this, reference, ct, group)`; повторный `LoadAsync` без `Release` отдаёт тот же экземпляр с warning; `Release` → `AssetCache.Release(this, group)` (no-op, пока ничего не загружено). Полный контракт — в README `AssetHandleSystem`.

### `DataStorageTransport`

Наследник `DataStorage` для подключения/отключения тяжёлого префаба вместе с GameObject:
- `OnEnable` → `AssetCache.Load<GameObject>(this, prefabReference, token, cacheGroup)` → `Instantiate` под `targetForInstance` (пусто — свой `Transform`) → если `Data` не пуст, `SetData` в `DataStorage` инстанса (если он есть).
- `OnDisable` → отмена токена, `Destroy` инстанса, `AssetCache.Release(this, cacheGroup)`. `OperationCanceledException` прерванной загрузки глотается.
- В редакторе: кнопки `Connect` / `Disconnect` — инстанцировать префаб через `editorAsset` / удалить детей таргета для работы с вёрсткой. Пока у таргета есть дети, показывается InfoBox-ошибка.

### Подписка на готовность шины

```csharp
AssetCache.OnReady.Subscribe(() =>
{
    // Выполнится немедленно, если bootstrap уже завершён.
    Debug.Log($"AssetCache ready. Survivor capacity = {AssetCache.Config.SurvivorCapacity}");
});
```

### Параллельные запросы одного ref

```csharp
// Все 100 одновременных запросов отработают через ОДИН Addressables-вызов.
var tasks = Enumerable.Range(0, 100).Select(i =>
    AssetCache.Load<Sprite>(owners[i], iconRef, ct));

var sprites = await UniTask.WhenAll(tasks);
```

### Отмена waiter'а

```csharp
var cts = new CancellationTokenSource();

var task = AssetCache.Load<GameObject>(owner, heavyRef, cts.Token);
cts.CancelAfter(100);   // дать 100ms

try { var go = await task; }
catch (OperationCanceledException) { /* waiter отменён */ }

// Реальная загрузка ПРОДОЛЖАЕТСЯ — после её завершения ассет уйдёт в survivors
// (если других active-владельцев нет) или сразу станет HIT для другого запроса.
```

---

## Архитектура

```
Bus/
  AssetCache (static)               — public API: Load / LoadSync / Release / OnReady / OnRelease
                                      bootstrap через [RuntimeInitializeOnLoadMethod]
                                      подписка на Settings.OnInit → CreateController → Controller.Init

Controllers/
  AssetCacheController : Singleton  — реализация IAssetCacheController, partial по темам
    ├── .cs            — Init / Cleanup / IsInitialized / Model / OnInitialized / OnReleased / ResolveGroup
    ├── .Loading.cs    — Load<T> / LoadSync<T> / StartLoad / RegisterOwner / IsSameRecord /
    │                    UnregisterOwner / ReviveFromSurvivor
    └── .Releasing.cs  — Release / SweepDeadOwners / ReleaseOwner / IsHeldByAnyOwner / PushSurvivor /
                         EvictIfOverflow

Abstractions/
  IAssetCacheController             — контракт consumer-API + lifecycle

Models/
  AssetCacheModel                   — runtime-state: набор групп (Default + именованные), агрегаты для отладки
  AssetCacheGroupModel              — реестр группы + ёмкость:
                                      Locks     — owner → (ref → число удержаний)
                                      Handles   — ref → Addressables-handle
                                      Inflight  — ref → незавершённая загрузка
                                      Survivors — LRU-очередь (LinkedList) ассетов без удержаний
  InflightLoad                      — DTO незавершённой загрузки: Handle + UniTaskCompletionSource

AddressableAssetHandle<T>           — AssetHandle<T> поверх AssetCache (owner = сам handle)

Handler/
  DataStorageTransport : DataStorage — на OnEnable грузит префаб через кэш, инстанцирует и передаёт ему
                                       свои Data; на OnDisable отпускает

Attributes/
  AssetCacheGroupAttribute          — string-поле = ключ группы, выпадашка в инспекторе

Config/
  AssetCacheSettings : SettingsPreset, ICoreAsset
                                    — SO в Resources/Settings/: SurvivorCapacity дефолтной группы
                                      (default 32) + список групп (ключ, SurvivorCapacity)
  SettingsExt/                      — partial-расширение SettingsModel через .asmref
    AssetCacheConfig                — immutable runtime-конфиг (+ AssetCacheGroupConfig)
    SettingsModelExtAssetCache      — поля AssetCache + AssetCacheDebugLogs
  DebugExt/                         — partial-расширение DebugSettings через .asmref
    DebugSettingsExtAssetsCache     — toggle AssetCacheDebugLogs (учитывает DebugMode)

Editor/
  MenuController                    — пункты меню пакета (AssetsCache/Runtime Index, AssetsCache/Config,
                                      Configs/AssetCache Settings)
  AssetCacheIndexWindow             — EditorWindow: runtime-инспектор индекса (read-only)
  AssetCacheGroupAttributeDrawer    — Odin-drawer выпадашки групп
```

### Lifecycle bootstrap

```
[RuntimeInitializeOnLoadMethod]
    ↓
AssetCache.Bootstrap()
    ├── Settings.OnInit += CreateController  (если Settings уже инициализированы — вызов сразу)
    └── App.OnExit += Dispose
    ↓
Settings.OnInit (через InitValve)
    ↓
CreateController()
    ├── контроллер уже создан → выход
    ├── Settings.Data() == null → выход
    ├── Settings.Data().AssetCache → Config; null → Debug.LogError, выход (Controller остаётся null)
    ├── Controller = AssetCacheController.Instance
    ├── Controller.OnInitialized += NotifyReady     ← открытие InitValve OnReady
    ├── Controller.OnReleased    += NotifyReleased
    └── Controller.Init()
    ↓
Controller.Init()
    ├── читает SurvivorCapacity, группы + debugLogging из Settings.Data()
    ├── создаёт AssetCacheModel: дефолтная группа + именованные
    │   (пустой ключ / дубликат ключа → Debug.LogError, группа пропускается)
    ├── IsInitialized = true
    └── OnInitialized?.Invoke() → AssetCache.OnReady открывается
    ↓
Consumer:
    AssetCache.OnReady.Subscribe(handler) — выполнится немедленно если уже Ready

App.OnExit → Dispose()
    ├── Settings.OnInit -= CreateController; App.OnExit -= Dispose
    └── Controller.Cleanup() → OnReleased → NotifyReleased: отписка от контроллера, AssetCache.OnRelease
        (OnReady остаётся открытым, IsReady == false; сама шина повторно не инициализируется)
```

### Поток `Load<T>(owner, reference, ct, group)`

Все структуры ниже — реестр группы, в которую резолвится `group`.

```
1. ArgNullCheck: owner / reference
2. ResolveGroup(group)                  ← пусто → Default; неизвестный ключ → LogError + Default
3. RegisterOwner(owner, reference)      ← Locks[owner][reference] += 1 (одно удержание на каждый Load);
                                           запоминает запись владельца (поколение)
4. Поиск в Handles
   ├── HIT  → ReviveFromSurvivor, result = (T)handle.Result   (мгновенно)
   └── miss → продолжаем
5. Поиск в Inflight
   ├── JOIN → result = await slot.Completion (с поддержкой ct)
   └── miss → продолжаем
6. StartLoad<T>:
   ├── handle = Addressables.LoadAssetAsync<T>(reference)
   ├── Inflight[reference] = { Handle, Completion }
   ├── await handle.ToUniTask()             ← ct waiter'а НЕ прерывает загрузку
   ├── прошёл Cleanup (модель сменилась)? → OCE
   ├── Handles[reference] = handle
   ├── slot.Completion.TrySetResult(loaded) ← broadcast всем JOIN-waiter'ам
   ├── if (!IsHeldByAnyOwner(ref))          ← все waiter'ы отменились до завершения
   │     PushSurvivor(ref)
   ├── ct.ThrowIfCancellationRequested()    ← OCE waiter'а после успешной загрузки
   ├── сбой загрузки → Completion.TrySetException (всем waiter'ам), Addressables.Release(handle),
   │                   Handles.Remove(ref); проброс исключения
   └── finally: Inflight.Remove(ref)
7. Перед возвратом: запись владельца всё та же?
   └── нет (Release(owner) во время загрузки) → OperationCanceledException: ассет без удержания не отдаётся
8. catch (любое исключение, включая OCE waiter'а и несовпадение каста к T) → UnregisterOwner(owner, ref, record)
   ├── запись сменилась (Release, затем новые Load владельца) → пропуск: её удержания — чужих Load
   ├── минус одно удержание; ref уходит из записи при нуле, владелец — из Locks при пустой записи
   └── удержаний ref не осталось ни у кого, ассет загружен → PushSurvivor(ref); проброс исключения
```

### Поток `LoadSync<T>(owner, reference, group)`

Синхронный вариант для мест, где `await` невозможен. Блокирует main-thread до завершения загрузки.

```
1. ArgNullCheck: owner / reference
2. ResolveGroup(group); RegisterOwner(owner, reference) ← одно удержание, запоминает запись
3. HIT      → ReviveFromSurvivor, return (T)handle.Result
4. Inflight → slot.Handle.WaitForCompletion()
   ├── сбой  → UnregisterOwner(owner, ref, record), return null
   └── успех → return (T)result
5. Новая загрузка → Addressables.LoadAssetAsync<T>(reference).WaitForCompletion()   ← без Inflight-слота
   ├── сбой  → Addressables.Release(handle), UnregisterOwner(owner, ref, record), return null
   └── успех → Handles[reference] = handle, return result
```

Отличия от `Load`: сбой возвращается `null`, а не исключением; проверки поколения на успешном пути нет (вызов синхронный); несовпадение каста к T на HIT/Inflight бросает `InvalidCastException` без отката — удержание остаётся до `Release(owner)`.

### Поток `Release(owner, group)`

```
1. ArgNullCheck: owner
2. Model == null (до Init / после Cleanup) → выход
3. Sweep уничтоженных Unity-владельцев — во ВСЕХ группах:
   ├── Перебор Locks группы, поиск `o is Object uo && uo == null`
   └── Для каждого — ReleaseOwner(group, dead, isSweep: true)
4. ResolveGroup(group) → ReleaseOwner(owner, isSweep: false) в этой группе:
   ├── владелец не найден → warning в debug-mode, выход
   ├── Locks.Remove(owner)          ← запись со всеми удержаниями владельца разом
   └── Для каждого ref:
       ├── IsHeldByAnyOwner(ref)? → пропуск (ещё держит)
       └── Handles.Contains(ref)?  → PushSurvivor(ref)

PushSurvivor (в survivors группы):
   ├── Survivors.Remove(ref)        ← refresh-положение
   ├── Survivors.AddLast(ref)
   └── EvictIfOverflow:
       while (Survivors.Count > SurvivorCapacity группы):
         head = Survivors.First
         Survivors.RemoveFirst
         if (IsHeldByAnyOwner(head)) continue  ← кто-то взял снова, handle остаётся
         Addressables.Release(handle)
         Handles.Remove(head)
```

`AssetCache.Release` до bootstrap'а (`Controller == null`) — no-op.

---

## Контракт

### Гарантии

- **Inflight-дедупликация**: N параллельных `Load(refX)` от разных владельцев → **один** `Addressables.LoadAssetAsync`, все ждут на общем `UniTaskCompletionSource`.
- **HIT мгновенный**: повторный `Load` для активного или survivor-ассета возвращается синхронно через `await`, без сетевого/диск-IO.
- **Survivor revive**: ассет, попавший в survivors, при повторном запросе возвращается мгновенно — `Addressables.Release` не вызывается.
- **Ct-семантика**: отмена waiter'а **не прерывает** реальную загрузку — она нужна другим waiter'ам. Отменённый waiter получает `OperationCanceledException`, загрузка продолжается.
- **Счётчик удержаний**: каждый `Load` ref добавляет одно удержание в запись своего владельца; проваленный/отменённый `Load` снимает только своё, `Release(owner)` удаляет запись со всеми удержаниями разом. Откат трогает только ту запись, в которой `Load` регистрировался: если между ними был `Release(owner)` и владелец грузил снова, новые удержания остаются. Поэтому отменённая, но ещё летящая загрузка не снимает удержание живого `Load` того же ref у того же владельца — ни при повторном `Load` без `Release`, ни при `Release` + повторном `Load` (выключение/включение во время загрузки). Владелец остаётся одним ключом в `Locks` независимо от числа удержаний.
- **Release во время загрузки**: если `Release(owner)` прошёл, пока `Load` этого владельца был в полёте, `Load` завершается `OperationCanceledException` вместо возврата ассета без удержания; ассет уходит в survivors, если его больше никто не держит.
- **Cleanup идемпотентен**: повторный вызов — no-op.
- **Один handle на ref в группе**: внутри `Handles` группы всегда максимум одна запись на `AssetReference`.
- **Группы не ломают друг друга**: один ref, загруженный в двух группах, держит по handle в каждой. Общий счётчик ссылок ведёт сам Addressables (повторный `LoadAssetAsync` того же ключа — `IncrementReferenceCount` на кешированной операции), поэтому eviction/Release в одной группе только уменьшает счётчик — физически ассет выгрузится, когда его отпустят все группы.

### Группы

- **Дефолтная группа** — группа без идентификатора. Вызовы без `group` (или с пустой строкой) работают с ней, ёмкость — `AssetCacheSettings.SurvivorCapacity`.
- **Именованные группы** — список `groups` в `AssetCacheSettings`: ключ + собственный `SurvivorCapacity`. Пустой ключ и дубликаты отбрасываются на `Init` с `Debug.LogError`.
- **Изоляция**: у группы свои `Locks`/`Handles`/`Inflight`/`Survivors`. HIT, JOIN и вытеснение — только внутри группы.
- **Освобождает кладущий**: `Release(owner, group)` снимает владение только в указанной группе. Владелец, грузивший в нескольких группах, освобождает каждую отдельно.
- **Неизвестный ключ** (опечатка, группа удалена из настроек) — `Debug.LogError` и обработка как с дефолтной группой. Вызов не теряется, мисконфиг виден в логе.
- **Sweep** уничтоженных Unity-владельцев идёт по всем группам при любом `Release`.

### Дисциплина

- Один экземпляр `AssetCacheSettings` в `Resources/Settings/` — обязателен.
- **Owner обязан вызывать `Release`** для каждого `Load`-владения, иначе ассет не выгрузится (см. Sweep ниже).
- **`Release` — с той же группой, что и `Load`.** Release в другой группе не найдёт владельца (warning в debug-mode), lock останется до sweep'а (Unity-владелец) или до `Cleanup` (POCO).
- Sweep ловит только `UnityEngine.Object`-владельцев, уничтоженных без Release. **POCO-владельцы** (чистые C#-классы) — обязаны Release-discipline вручную, иначе их Locks-запись и связанные handle'ы живут до `Cleanup`.
- `AssetReference` должен запрашиваться **с одним и тем же T** между разными `Load<T>`. Каст к T выполняется на стороне waiter'а; для одного ref с разными T — `InvalidCastException` на втором запросе.
- **`Load`/`LoadSync` — только после `OnReady`.** До bootstrap'а `Controller == null`, вызов падает NRE. `[RuntimeInitializeOnLoadMethod]` срабатывает после загрузки первой сцены, поэтому `OnEnable` объектов этой сцены может прийти раньше.

### Ограничения

- Поиск `IsHeldByAnyOwner` — O(L) по числу владельцев. Подходит для типичных проектов с десятками-сотнями владельцев. Для тысяч — потребуется обратный индекс (на момент v1 не реализовано — приоритет памяти).
- `ReviveFromSurvivor` — O(n) по `LinkedList.Remove(value)`. Для capacity 32-128 — копейки.
- Не потокобезопасен — все операции через Unity main-thread.

---

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| `Load(null, ref)` или `Load(owner, null)` | `ArgumentNullException` |
| `Release(null)` | `ArgumentNullException` |
| `Load`/`LoadSync` до bootstrap'а Settings | NRE на `Controller.Load` (Controller == null). Подписаться на `OnReady` перед использованием |
| `Release` до bootstrap'а Settings | No-op (`Controller?.Release`) |
| Параллельные `Load(refX)` от 100 владельцев | Один `Addressables.LoadAssetAsync`, все ждут на общем `Completion`. HIT для последующих |
| `Load<T1>(...)` потом `Load<T2>(...)` для одного ref, T1 ≠ T2 | Первый каст работает, второй — `InvalidCastException`; удержание второго `Load` откатывается |
| Отмена `ct` waiter'а до завершения загрузки | OCE для этого waiter'а; реальная загрузка продолжается; result уходит другим waiter'ам или в survivors |
| Отмена `ct` единственного waiter'а | Загрузка завершается, ассет идёт в survivors. `Load` сам снимает удержание waiter'а (`UnregisterOwner`), явный `Release(owner)` не нужен |
| Один владелец: `Load(ref)` отменён, пока ещё в полёте, затем повторный `Load(ref)` | Два удержания одного владельца; отменённый снимает только своё, ассет держит живой `Load` |
| Один владелец: `Load(ref)` в полёте, `Release(owner)`, затем новый `Load(ref)` (выключение/включение во время загрузки) | Откат первого `Load` не трогает новую запись; ассет держит второй `Load` |
| `Release(owner)` во время `Load` этого владельца без отмены токена | `Load` завершается `OperationCanceledException`; ассет уходит в survivors, если его больше никто не держит |
| Один владелец, несколько успешных `Load(ref)` | Несколько удержаний; один `Release(owner)` снимает все |
| Сбой загрузки Addressables | Исключение получают все waiter'ы ref; handle освобождается, удержание каждого waiter'а откатывается |
| Сбой `LoadSync` | `null`, удержание откатывается |
| `LoadSync` с несовпадением T на HIT/Inflight | `InvalidCastException`; удержание остаётся до `Release(owner)` |
| `Load(this, ref)` на MB; MB уничтожен без `Release` | Sweep при следующем `Release` любого владельца обнаружит `o is Object && o == null` и освободит |
| POCO-владелец без `Release` | Утечка: Locks/Handles живут до `Cleanup` |
| Survivor capacity = 0 | Ассет, оставшийся без удержаний (`Release` или откат последнего удержания), сразу выгружается через `Addressables.Release` |
| `Release(owner)` для owner'а без активных Locks | Warning в debug-mode, no-op в release |
| `Settings.Data().AssetCache == null` (нет SO) | `Debug.LogError` в `CreateController`, контроллер не создаётся: `Load` упадёт NRE, `Dispose` на `App.OnExit` — тоже |
| `App.OnExit` | `Dispose` → `Controller.Cleanup` → освобождение всех Handles + Inflight, `OnRelease`; `OnReady` остаётся открытым при `IsReady == false` |
| Повторный `Init` после `Cleanup` | На уровне контроллера — новый `Model`, реестры с нуля. Шина его не вызывает: после `Dispose` она отписана от `Settings.OnInit` |
| `Cleanup` (+ повторный `Init`) во время загрузки | Загрузка завершается OCE; handle освобождён в `Cleanup`, в новую модель не попадает |
| `Load`/`Release` с неизвестной группой | `Debug.LogError`, работа идёт с дефолтной группой |
| Пустой ключ или дубликат ключа в `groups` | `Debug.LogError` на `Init`, группа пропускается (вызовы с этим ключом — как с неизвестным) |
| Один ref в двух группах | Два handle; HIT/eviction независимы; физическая выгрузка — после освобождения в обеих (refcount Addressables) |
| `Release(owner, B)` для владельца, грузившего в группе A | В B владелец не найден (warning в debug-mode); lock в A остаётся до своего `Release`/sweep'а |
| Владелец грузил в A и B | `Release` нужен для каждой группы; sweep уничтоженного Unity-владельца снимет обе |

---

## Editor

`Tools/Vortex/AssetsCache/Config` и `Tools/Vortex/Configs/AssetCache Settings` — подсветить ассет настроек в Project window. Два входа в одну команду: первый рядом с окном пакета, второй в общем списке конфигов проекта. Использует `AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>()` — ожидает один экземпляр в проекте, при множественности логирует ошибку.

### Runtime Index

`Tools/Vortex/AssetsCache/Runtime Index` — окно текущего состояния реестра. Данные есть только в Play Mode: до bootstrap'а модель не создана, вне игры окно показывает подсказку.

Сводка: загружено (active / survivors) и inflight — по всем группам, число уникальных владельцев и отдельно — сколько владельцев уничтожено без `Release` (их locks доживут до ближайшего sweep'а). Полоса заполнения survivors — на каждую группу относительно её `SurvivorCapacity`.

Таблица — строка на пару (группа, `AssetReference`): один ref в двух группах — две строки:

| Колонка | Значение |
|---|---|
| Ассет | Имя загруженного объекта, иначе имя файла по GUID |
| Группа | Ключ группы, `<default>` — дефолтная |
| Состояние | `LOAD` (inflight) / `ACTIVE` / `SURVIVOR` |
| Ссылок | Сколько владельцев группы держат ref; несколько удержаний одного владельца считаются за одного. `IsHeldByAnyOwner` решает по тому, больше ли это число нуля |
| Тип | Фактический тип загруженного объекта |
| LRU | Позиция в очереди survivors группы, `1/N` — голова, выгрузится первой |

Раскрытие строки даёт GUID, поимённый список владельцев (уничтоженные помечены) и кнопку ping'а ассета в Project window.

Сортировка: `LOAD` → `ACTIVE` → `SURVIVOR`; внутри active — по убыванию ссылок, внутри survivors — по группе и очереди выгрузки. Строка `ACTIVE` с нулём ссылок подсвечивается жёлтым: по контракту такой ref обязан лежать в survivors, иначе он не попадёт под eviction и провисит до `Cleanup` — индикатор утечки.

Снимок пересобирается в `OnInspectorUpdate` (~10 раз/с, только для видимого окна); тумблер «Авто» его выключает, рядом ручное «Обновить», кнопка «Конфиг» (подсветка `AssetCacheSettings` — той же командой, что и пункт меню) и поиск по имени/группе/GUID. Окно только читает: `Release`/eviction отсюда не вызываются.

Живёт в `Editor/` рантайм-сборки пакета под `#if UNITY_EDITOR` — поэтому читает `internal`-словари `AssetCacheGroupModel` напрямую, без расширения публичного API модели.

### Атрибут `[AssetCacheGroup]`

Ставится на `string`-поле с ключом группы. Odin-drawer заменяет поле выпадашкой с ключами групп из `AssetCacheSettings` (список кешируется на 1 с — правка конфига видна почти сразу). Визуал — как у `ValueSelector`:

- пустое значение (дефолтная группа) — красное `——[NULL]——`;
- ключ, которого нет в настройках, — красным, с пометкой `[нет в AssetCacheSettings]`;
- кнопка `×` справа сбрасывает выбор — поле возвращается к дефолтной группе.

### Debug-логи

Debug-трассировка: в `DebugSettings` → `Log Settings` → toggle `AssetCacheDebugLogs`. Учитывается только при включённом глобальном `DebugMode`. Вывод: `HIT / JOIN / LOAD / REL / SWEEP / EVICT-skip / EVICT`, у каждой записи — `group=<ключ>` (`<default>` для дефолтной).

---

## Публичный API

```csharp
namespace Vortex.Unity.AssetCacheSystem.Bus
{
    public static class AssetCache
    {
        public static IAssetCacheController Controller { get; }
        public static AssetCacheConfig      Config     { get; }
        public static AssetCacheModel       Data       { get; }
        public static bool                  IsReady    { get; }
        public static InitValve             OnReady    { get; }
        public static event Action          OnRelease;

        public static UniTask<T> Load<T>(object owner, AssetReference reference,
            CancellationToken ct = default, string group = null) where T : UnityEngine.Object;
        public static T          LoadSync<T>(object owner, AssetReference reference,
            string group = null) where T : UnityEngine.Object;
        public static void       Release(object owner, string group = null);
    }
}

namespace Vortex.Unity.AssetCacheSystem.Abstractions
{
    public interface IAssetCacheController
    {
        bool             IsInitialized  { get; }
        AssetCacheModel  Model          { get; }
        event Action     OnInitialized;
        event Action     OnReleased;

        void Init();
        void Cleanup();
        UniTask<T> Load<T>(object owner, AssetReference reference, CancellationToken ct = default,
            string group = null) where T : UnityEngine.Object;   // OCE: ct отменён или владельца отпустили
        T LoadSync<T>(object owner, AssetReference reference, string group = null)
            where T : UnityEngine.Object;                         // null при сбое
        void Release(object owner, string group = null);
    }
}

namespace Vortex.Unity.AssetCacheSystem.Models
{
    public sealed class AssetCacheModel
    {
        public AssetCacheGroupModel                Default { get; }
        public IReadOnlyList<AssetCacheGroupModel> GetGroups();          // дефолтная первой
        public AssetCacheGroupModel                GetGroup(string key); // пусто → Default, неизвестный → null

        // Агрегаты по всем группам
        public bool IsLoaded(AssetReference reference);
        public int  LoadedCount    { get; }
        public int  InflightCount  { get; }
        public int  SurvivorsCount { get; }
        public int  OwnersCount    { get; }   // уникальные владельцы
    }

    public sealed class AssetCacheGroupModel
    {
        public string Key              { get; }   // null — дефолтная
        public int    SurvivorCapacity { get; }
        public bool   IsDefault        { get; }

        public bool IsLoaded(AssetReference reference);
        public int  LoadedCount    { get; }
        public int  InflightCount  { get; }
        public int  SurvivorsCount { get; }
        public int  OwnersCount    { get; }
    }
}

namespace Vortex.Unity.AssetCacheSystem
{
    [Serializable]
    public sealed class AddressableAssetHandle<T> : AssetHandle<T> where T : UnityEngine.Object
    {
        public override bool       IsLoaded { get; }
        public override T          Asset    { get; }
        public override UniTask<T> LoadAsync(CancellationToken ct);
        public override void       Release();
    }
}

namespace Vortex.Unity.AssetCacheSystem.Handler
{
    public class DataStorageTransport : DataStorage { }   // настраивается в инспекторе
}

namespace Vortex.Unity.AssetCacheSystem.Attributes
{
    public class AssetCacheGroupAttribute : PropertyAttribute { }
}

namespace Vortex.Core.SettingsSystem.Model
{
    public class AssetCacheConfig
    {
        public AssetCacheConfig(int survivorCapacity, IReadOnlyList<AssetCacheGroupConfig> groups = null);
        public int                                  SurvivorCapacity { get; }   // дефолтная группа
        public IReadOnlyList<AssetCacheGroupConfig> Groups           { get; }
    }

    public class AssetCacheGroupConfig
    {
        public AssetCacheGroupConfig(string key, int survivorCapacity);
        public string Key              { get; }
        public int    SurvivorCapacity { get; }
    }
}
```

`AssetCacheController` (Singleton), `InflightLoad`, `AssetCacheSettings` (SO), partial-расширения `SettingsModel`/`DebugSettings` — внутренние/конфиг-классы, не должны использоваться потребителями напрямую.
