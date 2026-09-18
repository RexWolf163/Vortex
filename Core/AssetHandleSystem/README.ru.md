# AssetHandleSystem

**Пакет:** driver-нейтральная обёртка над Unity-ассетом с единым async-контрактом

| Часть | Namespace | Assembly | Слой |
|---|---|---|---|
| Контракт + Direct | `Vortex.Core.AssetHandleSystem.*` | `ru.vortex.core.assethandle` | Core (Layer 3) |
| Addressable-реализация | `Vortex.Unity.AssetCacheSystem.*` | `ru.vortex.unity.assetcachesystem` (`ENABLE_ADDRESSABLES`) | Unity (Layer 2) |

Физически: `Assets/Vortex/Core/AssetHandleSystem/` (контракт + `DirectAssetHandle`), `Assets/Vortex/Unity/AssetCacheSystem/AddressableAssetHandle.cs` (тонкий адаптер живёт рядом с `AssetCache`, отдельного пакета не заслуживает).

---

## Назначение

Один тип для «прямая ссылка на ассет» и «lazy-подгрузка ассета» с одинаковым consumer-кодом. Consumer никогда не пишет `if (Addressables enabled) ... else ...` — переключение решается на уровне пресета через полиморфное `[SerializeReference]`-поле.

### Когда использовать

Ключевой use-case — **переиспользуемые системы**, поставляемые как пакеты и работающие в разных проектах с разной инфраструктурой ассетов. Такая система не должна навязывать consumer'ам конкретный способ подгрузки, но и не может заранее знать, есть ли у проекта Addressables. `AssetHandle<T>` даёт способ **отложить это решение до консюмера-пресета**.

**Пример.** Пакет `GallerySpritesSystem` объявляет пресет с полем `[SerializeReference] AssetHandle<Sprite> fullscreen`. Тот же пакет разворачивается в:

- **Проект A** (с Addressables): дизайнер выбирает в инспекторе `AddressableAssetHandle<Sprite>` — тяжёлая fullscreen-картинка грузится лениво.
- **Проект B** (без Addressables): дизайнер выбирает `DirectAssetHandle<Sprite>` — картинка держится прямой ссылкой.

Код `GallerySpritesSystem` (модель, viewer, вся логика Show/Hide) **не меняется ни в одном проекте**. Один и тот же `await preset.Fullscreen.LoadAsync()` работает в обоих случаях. Если завтра в проекте B включат Addressables — часть пресетов можно переключить на lazy без правки самого пакета галлереи.

Если пакет пишется под один конкретный проект, где инфраструктура фиксирована (например, только Addressables) — абстракция избыточна, используйте `AssetReference` / `AssetCache` напрямую.

Возможности:

- **Единый async-контракт** — `UniTask<T> LoadAsync(...)` для обеих реализаций.
- **`DirectAssetHandle<T>`** — прямая ссылка на Unity-ассет, работает в любом проекте (в том числе без Addressables).
- **`AddressableAssetHandle<T>`** — lazy через Addressables, делегирует в `Vortex.Unity.AssetCacheSystem.AssetCache` (dedup, LRU-survivors, sweep destroyed owners бесплатно).
- **`[SerializeReference]`-полиморфизм** — дизайнер выбирает реализацию для каждого поля в инспекторе через стандартный Odin picker.
- **Driver-нейтральность** — Core-контракт не тянет `UnityEngine.AddressableAssets`. `AssetReference` живёт только внутри Unity-реализации, domain-пресет держит только абстрактный `AssetHandle<T>`.

Вне ответственности:

- **Refcount между разными handle** — не делаем на уровне контракта. `AddressableAssetHandle` делегирует в `AssetCache`, тот делает свой owner-based refcount.
- **Автоматическая обработка отсутствия Addressables** — если проект отключит Addressables, `AddressableAssetHandle` в пресете десериализуется как `null`. Consumer при обращении получит NRE — это сознательное решение (маскирование конфиг-бага fallback'ом было бы вреднее).
- **Управление жизненным циклом контейнера handle** — держит его consumer (пресет / кэш моделей). Handle сам себе owner своего underlying-ресурса, не своего инстанса.

---

## Зависимости

| Зависимость | Часть | Назначение |
|-------------|---|-----------|
| `UniTask` | Core | Async-контракт |
| Unity Engine | Core | `UnityEngine.Object`, `Sprite` (пример), `SerializeField` |
| `Vortex.Unity.AssetCacheSystem` | Unity | `AssetCache.Load` / `AssetCache.Release` — underlying-механизм |
| `Vortex.Core.LoggerSystem` | Unity | Warning при `LoadAsync` после `Release` |
| Unity Addressables | Unity | `AssetReference`, только внутри `AddressableAssetHandle` |

---

## Контракт

```csharp
[Serializable]
public abstract class AssetHandle<T> where T : UnityEngine.Object
{
    public abstract bool IsLoaded { get; }
    public abstract T Asset { get; }
    public abstract UniTask<T> LoadAsync(CancellationToken ct);
    public UniTask<T> LoadAsync();                    // перегрузка без ct
    public abstract void Release();
}
```

**Инварианты:**

- **I1.** `Asset != null && IsLoaded == true` — эквивалентны.
- **I2.** До `Release`: повторный `LoadAsync` возвращает тот же экземпляр `T`.
- **I3.** `Release` идемпотентен.
- **I4.** `DirectAssetHandle.Release()` — гарантированно no-op.
- **I5.** Core-сборка не содержит ссылок на `UnityEngine.AddressableAssets`.
- **I6.** `AddressableAssetHandle` — sole owner своего Addressables-handle через `AssetCache` (`owner = this`).

---

## Реализации

### `DirectAssetHandle<T>`

Одно поле `[SerializeField] T asset`. `LoadAsync` возвращает `UniTask.FromResult(asset)`. `Release` — пустой метод.

### `AddressableAssetHandle<T>`

- `[SerializeField] AssetReference reference` — ссылка на Addressables-ассет.
- `[NonSerialized] T _cached` — кэш loaded-ассета.
- `[NonSerialized] bool _released` — терминальный флаг.

Flow `LoadAsync(ct)`:
1. Если `_released` → warning + `default(T)`.
2. Если `_cached != null` → мгновенно.
3. Иначе — `await AssetCache.Load<T>(this, reference, ct)` → сохранить в `_cached`.

Flow `Release()`:
1. Если `_released` — return.
2. `AssetCache.Release(this)` → освободить в кэше.
3. `_cached = null`, `_released = true`.

---

## Использование

### Пресет

```csharp
public class GallerySpritePreset : RecordPreset<GallerySpriteModel>
{
    [SerializeReference] private AssetHandle<Sprite> fullscreen;
    public AssetHandle<Sprite> Fullscreen => fullscreen;
}
```

В инспекторе Odin-picker для `fullscreen` даёт выбор между `DirectAssetHandle<Sprite>` (поле — прямая ссылка) и `AddressableAssetHandle<Sprite>` (поле — `AssetReference`).

### Consumer

```csharp
async UniTask ShowFullscreen(GallerySpritePreset preset)
{
    var sprite = await preset.Fullscreen.LoadAsync();  // работает и для Direct, и для Lazy
    _imageComponent.sprite = sprite;
}

void CloseFullscreen(GallerySpritePreset preset)
{
    preset.Fullscreen.Release();                        // Direct — no-op, Lazy — освобождает
}
```

### С CancellationToken

```csharp
async UniTask ShowFullscreen(GallerySpritePreset preset, CancellationToken ct)
{
    var sprite = await preset.Fullscreen.LoadAsync(ct);
    _imageComponent.sprite = sprite;
}
```

---

## Edge cases

| Ситуация | Поведение |
|---|---|
| `LoadAsync` до `Release` — повторный вызов | Возвращает тот же экземпляр `T`; для Lazy — без обращения к `AssetCache` |
| `LoadAsync` после `Release` | Warning в логгер, возвращает `default(T)` |
| `Release` до `LoadAsync` (Lazy) | No-op — освобождать нечего, флаг `_released` выставляется |
| Повторный `Release` | Идемпотентно, no-op |
| `DirectAssetHandle` с `asset == null` | `IsLoaded == false`, `LoadAsync` возвращает `null`. Валидна как «пусто по замыслу» |
| `AddressableAssetHandle.reference == null` | `AssetCache.Load` бросает `ArgumentNullException` — fail-fast конфиг-баг |
| Проект без Addressables: сцена содержала `AddressableAssetHandle` | Поле десериализуется как `null`. Consumer обязан явно проверять null-handle или ловить NRE. Автоматического fallback нет |
| Отмена через `ct` во время подгрузки (Lazy) | `AssetCache.Load` выбрасывает `OperationCanceledException`. Handle остаётся не-loaded (retry возможен) |

---

## Editor

Стандартный Odin `[SerializeReference]`-picker в инспекторе. Кастомный drawer не пишется.
