# AssetCacheSystem

**Namespace:** `Vortex.Unity.AssetCacheSystem.*`
**Сборка:** `ru.vortex.unity.assetcachesystem` (defineConstraints: `ENABLE_ADDRESSABLES`)

---

## Назначение

Управляемый кэш Addressables-ассетов с четырьмя ключевыми механиками:

- **Owner-tracking** — кто из потребителей какие `AssetReference` держит.
- **Inflight-дедупликация** — параллельные запросы одного ref резолвятся одним вызовом `Addressables.LoadAssetAsync`.
- **LRU survivors** — отпущенные, но ещё не выгруженные ассеты лежат в буфере; повторный запрос даёт мгновенный HIT.
- **Sweep уничтоженных Unity-владельцев** — при каждом `Release` ловит `MonoBehaviour`'ы, удалённые без вызова Release.

Consumer-API ровно из двух методов:
```csharp
UniTask<T> AssetCache.Load<T>(object owner, AssetReference reference, CancellationToken ct = default);
void       AssetCache.Release(object owner);
```

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
| `Vortex.Core.SettingsSystem` + `Vortex.Unity.SettingsSystem` | Чтение `SurvivorCapacity`, debug-флага |
| `Vortex.Core.Extensions` | `InitValve.Create` |
| `Vortex.Unity.Extensions` | `AssetDatabaseExt.GetSingletonAsset` (Editor-menu) |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` для SO-маркера |

`defineConstraints: ["ENABLE_ADDRESSABLES"]` в asmdef — пакет компилируется только в проектах с включёнными Addressables.

---

## Архитектура

```
Bus/
  AssetCache (static)               — public API: Load / Release / OnReady / OnRelease
                                      bootstrap через [RuntimeInitializeOnLoadMethod]
                                      подписка на Settings.OnInit → CreateController → Controller.Init

Controllers/
  AssetCacheController : Singleton  — реализация IAssetCacheController, partial по темам
    ├── .cs            — Init / Cleanup / IsInitialized / Model / OnInitialized / OnReleased
    ├── .Loading.cs    — Load<T> / StartLoad / RegisterOwner / ReviveFromSurvivor
    └── .Releasing.cs  — Release / ReleaseOwner / IsHeldByAnyOwner / PushSurvivor / EvictIfOverflow

Abstractions/
  IAssetCacheController             — контракт consumer-API + lifecycle

Models/
  AssetCacheModel                   — runtime-state: 4 словаря (Locks/Handles/Inflight/Survivors)
  InflightLoad                      — DTO незавершённой загрузки: Handle + UniTaskCompletionSource

Config/
  AssetCacheSettings : SettingsPreset, ICoreAsset
                                    — SO в Resources/Settings/: SurvivorCapacity (default 32)
  SettingsExt/                      — partial-расширение SettingsModel через .asmref
    AssetCacheConfig                — immutable runtime-конфиг
    SettingsModelExtAssetCache      — поля AssetCache + AssetCacheDebugLogs
  DebugExt/                         — partial-расширение DebugSettings через .asmref
    DebugSettingsExtAssetsCache     — toggle AssetCacheDebugLogs (учитывает DebugMode)

Editor/
  MenuController                    — Vortex/Configs/AssetCache Settings → ping ассета
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
    ├── Settings.Data().AssetCache → Config
    ├── Controller = AssetCacheController.Instance
    ├── Controller.OnInitialized += NotifyReady     ← открытие InitValve OnReady
    ├── Controller.OnReleased    += NotifyReleased
    └── Controller.Init()
    ↓
Controller.Init()
    ├── читает SurvivorCapacity + debugLogging из Settings.Data()
    ├── создаёт пустой AssetCacheModel
    ├── IsInitialized = true
    └── OnInitialized?.Invoke() → AssetCache.OnReady открывается
    ↓
Consumer:
    AssetCache.OnReady.Subscribe(handler) — выполнится немедленно если уже Ready
```

### Поток `Load<T>(owner, reference, ct)`

```
1. ArgNullCheck: owner / reference
2. RegisterOwner(owner, reference)      ← Locks[owner] += reference
3. Поиск в Model.Handles
   ├── HIT  → ReviveFromSurvivor + return (T)result   (мгновенно)
   └── miss → продолжаем
4. Поиск в Model.Inflight
   ├── JOIN → await slot.Completion (с поддержкой ct)
   └── miss → продолжаем
5. StartLoad<T>:
   ├── handle = Addressables.LoadAssetAsync<Object>(reference)
   ├── Inflight[reference] = { Handle, Completion }
   ├── await handle.ToUniTask()             ← ct waiter'а НЕ прерывает загрузку
   ├── Handles[reference] = handle
   ├── slot.Completion.TrySetResult(loaded) ← broadcast всем JOIN-waiter'ам
   ├── if (!IsHeldByAnyOwner(ref))          ← все waiter'ы отменились до завершения
   │     PushSurvivor(ref)
   ├── ct.ThrowIfCancellationRequested()    ← OCE waiter'а после успешной загрузки
   └── finally: Inflight.Remove(ref)
```

### Поток `Release(owner)`

```
1. ArgNullCheck: owner
2. Sweep уничтоженных Unity-владельцев:
   ├── Перебор Locks, поиск `o is Object uo && uo == null`
   └── Для каждого — ReleaseOwner(dead, isSweep: true)
3. ReleaseOwner(owner, isSweep: false):
   ├── Locks.Remove(owner)
   └── Для каждого ref:
       ├── IsHeldByAnyOwner(ref)? → пропуск (ещё держит)
       └── Handles.Contains(ref)?  → PushSurvivor(ref)

PushSurvivor:
   ├── Survivors.Remove(ref)        ← refresh-положение
   ├── Survivors.AddLast(ref)
   └── EvictIfOverflow:
       while (Survivors.Count > SurvivorCapacity):
         head = Survivors.First
         Survivors.RemoveFirst
         if (IsHeldByAnyOwner(head)) continue  ← кто-то взял снова, handle остаётся
         Addressables.Release(handle)
         Handles.Remove(head)
```

---

## Контракт

### Гарантии

- **Inflight-дедупликация**: N параллельных `Load(refX)` от разных владельцев → **один** `Addressables.LoadAssetAsync`, все ждут на общем `UniTaskCompletionSource`.
- **HIT мгновенный**: повторный `Load` для активного или survivor-ассета возвращается синхронно через `await`, без сетевого/диск-IO.
- **Survivor revive**: ассет, попавший в survivors, при повторном запросе возвращается мгновенно — `Addressables.Release` не вызывается.
- **Ct-семантика**: отмена waiter'а **не прерывает** реальную загрузку — она нужна другим waiter'ам. Отменённый waiter получает `OperationCanceledException`, загрузка продолжается.
- **Cleanup идемпотентен**: повторный вызов — no-op.
- **Один handle на ref**: внутри `Handles` всегда максимум одна запись на `AssetReference`.

### Дисциплина

- Один экземпляр `AssetCacheSettings` в `Resources/Settings/` — обязателен.
- **Owner обязан вызывать `Release`** для каждого `Load`-владения, иначе ассет не выгрузится (см. Sweep ниже).
- Sweep ловит только `UnityEngine.Object`-владельцев, уничтоженных без Release. **POCO-владельцы** (чистые C#-классы) — обязаны Release-discipline вручную, иначе их Locks-запись и связанные handle'ы живут до `Cleanup`.
- `AssetReference` должен запрашиваться **с одним и тем же T** между разными `Load<T>`. Каст к T выполняется на стороне waiter'а; для одного ref с разными T — `InvalidCastException` на втором запросе.

### Ограничения

- Поиск `IsHeldByAnyOwner` — O(L) по числу владельцев. Подходит для типичных проектов с десятками-сотнями владельцев. Для тысяч — потребуется обратный индекс (на момент v1 не реализовано — приоритет памяти).
- `ReviveFromSurvivor` — O(n) по `LinkedList.Remove(value)`. Для capacity 32-128 — копейки.
- Не потокобезопасен — все операции через Unity main-thread.

---

## Использование

### Регистрация настроек

В `Resources/Settings/` создать ассет `AssetCacheSettings`:
- Project window → Create → Vortex → Settings Preset → AssetCacheSettings.
- Заполнить `SurvivorCapacity` (default 32).
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

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| `Load(null, ref)` или `Load(owner, null)` | `ArgumentNullException` |
| `Release(null)` | `ArgumentNullException` |
| `Load` до bootstrap'а Settings | NRE на `Controller.Load` (Controller == null). Подписаться на `OnReady` перед использованием |
| Параллельные `Load(refX)` от 100 владельцев | Один `Addressables.LoadAssetAsync`, все ждут на общем `Completion`. HIT для последующих |
| `Load<T1>(...)` потом `Load<T2>(...)` для одного ref, T1 ≠ T2 | Первый каст работает, второй — `InvalidCastException` |
| Отмена `ct` waiter'а до завершения загрузки | OCE для этого waiter'а; реальная загрузка продолжается; result уходит другим waiter'ам или в survivors |
| Отмена `ct` единственного waiter'а | Загрузка завершается, ассет идёт в survivors, owner остаётся в Locks (нужно явно вызвать `Release(owner)`) |
| `Load(this, ref)` на MB; MB уничтожен без `Release` | Sweep при следующем `Release` любого владельца обнаружит `o is Object && o == null` и освободит |
| POCO-владелец без `Release` | Утечка: Locks/Handles живут до `Cleanup` |
| Survivor capacity = 0 | Каждый `Release` сразу выгружает handle через `Addressables.Release` |
| `Release(owner)` для owner'а без активных Locks | Warning в debug-mode, no-op в release |
| `Settings.Data().AssetCache == null` (нет SO) | `Debug.LogError` в `CreateController`/`Init`, контроллер не создаётся, `Load` упадёт NRE |
| `App.OnExit` | `Dispose` → `Controller.Cleanup` → освобождение всех Handles + Inflight |
| Повторный `Init` после `Cleanup` | Норма: создаётся новый `Model`, реестры с нуля |

---

## Editor

`Tools/Vortex/Configs/AssetCache Settings` — подсветить ассет настроек в Project window. Использует `AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>()` — ожидает один экземпляр в проекте, при множественности логирует ошибку.

Debug-трассировка: в `DebugSettings` → `Log Settings` → toggle `AssetCacheDebugLogs`. Учитывается только при включённом глобальном `DebugMode`. Вывод: `HIT / JOIN / LOAD / REL / SWEEP / EVICT-skip / EVICT`.

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
            CancellationToken ct = default) where T : UnityEngine.Object;
        public static void       Release(object owner);
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
        UniTask<T> Load<T>(object owner, AssetReference reference, CancellationToken ct = default)
            where T : UnityEngine.Object;
        void Release(object owner);
    }
}

namespace Vortex.Unity.AssetCacheSystem.Models
{
    public sealed class AssetCacheModel
    {
        public bool IsLoaded(AssetReference reference);
        public int  LoadedCount    { get; }
        public int  InflightCount  { get; }
        public int  SurvivorsCount { get; }
        public int  OwnersCount    { get; }
    }
}

namespace Vortex.Core.SettingsSystem.Model
{
    public class AssetCacheConfig
    {
        public AssetCacheConfig(int survivorCapacity);
        public int SurvivorCapacity { get; }
    }
}
```

`AssetCacheController` (Singleton), `InflightLoad`, `AssetCacheSettings` (SO), partial-расширения `SettingsModel`/`DebugSettings` — внутренние/конфиг-классы, не должны использоваться потребителями напрямую.
