# AssetHandleSystem (Unity)

**Пакет:** Unity-реализации контракта `AssetHandle<T>` из `Vortex.Core.AssetHandleSystem`

| Часть | Namespace | Assembly |
|---|---|---|
| Прямая ссылка | `Vortex.Unity.AssetHandleSystem.*` | `ru.vortex.unity.assethandle` |
| Ленивая подгрузка через Addressables | `Vortex.Unity.AssetCacheSystem.*` | `ru.vortex.unity.assetcachesystem` (`ENABLE_ADDRESSABLES`) |

Физически: `Assets/Vortex/Unity/AssetHandleSystem/` (`DirectAssetHandle`), `Assets/Vortex/Unity/AssetCacheSystem/AddressableAssetHandle.cs` (тонкий адаптер живёт рядом с `AssetCache`, отдельного пакета не заслуживает).

Контракт, инварианты и правила жизненного цикла handle — в [README ядра](../../Core/AssetHandleSystem/README.ru.md).

---

## Назначение

Дизайнер выбирает реализацию `AssetHandle<T>` для каждого поля пресета в инспекторе — код consumer'а не меняется.

**Пример.** Пакет `GallerySpritesSystem` объявляет пресет с полем `[SerializeReference] AssetHandle<Sprite> fullscreen`. Тот же пакет разворачивается в:

- **Проект A** (с Addressables): дизайнер выбирает `AddressableAssetHandle<Sprite>` — тяжёлая fullscreen-картинка грузится лениво.
- **Проект B** (без Addressables): дизайнер выбирает `DirectAssetHandle<Sprite>` — картинка держится прямой ссылкой.

Код `GallerySpritesSystem` (модель, viewer, логика Show/Hide) **одинаков в обоих проектах**: один и тот же `await preset.Fullscreen.LoadAsync()`. Если в проекте B включат Addressables — часть пресетов можно переключить на ленивую подгрузку без правки самого пакета.

Если проект использует только Addressables, абстракция избыточна — используйте `AssetReference` / `AssetCache` напрямую.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.AssetHandleSystem` | Контракт `AssetHandle<T>` |
| `UniTask` | Async-контракт |
| Unity Engine | `UnityEngine.Object`, `SerializeField` |
| `Vortex.Unity.AssetCacheSystem` (для `AddressableAssetHandle`) | `AssetCache.Load` / `AssetCache.Release` — underlying-механизм |
| Unity Addressables (для `AddressableAssetHandle`) | `AssetReference` |

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

В инспекторе Odin-picker для `fullscreen` даёт выбор между `DirectAssetHandle<Sprite>` (поле — прямая ссылка) и `AddressableAssetHandle<Sprite>` (поле — `AssetReference`). Кастомный drawer не пишется.

### Consumer

```csharp
async UniTask ShowFullscreen(GallerySpritePreset preset, CancellationToken ct)
{
    var sprite = await preset.Fullscreen.LoadAsync(ct);  // работает и для Direct, и для Addressable
    _imageComponent.sprite = sprite;
}

void CloseFullscreen(GallerySpritePreset preset)
{
    preset.Fullscreen.Release();                          // Direct — no-op, Addressable — освобождает
}
```

---

## Реализации

Обе сужают ограничение контракта до `where T : UnityEngine.Object`.

### `DirectAssetHandle<T>`

Прямая ссылка на ассет. Одно поле `[SerializeField] T asset`. `LoadAsync` возвращает `UniTask.FromResult(asset)`, `Release` — пустой метод. Ассет в памяти, пока жив пресет. Работает в любом проекте, в том числе без Addressables; подходит для лёгких и всегда нужных ассетов.

### `AddressableAssetHandle<T>`

Ленивая подгрузка через Addressables поверх `AssetCache` (dedup, LRU-survivors, sweep destroyed owners — бесплатно). Собирается только при `ENABLE_ADDRESSABLES`.

- `[SerializeField] AssetReference reference` — ссылка на Addressables-ассет.
- `[NonSerialized] T _cached` — кэш загруженного ассета; единственное состояние (handle переиспользуем).
- Handle — sole owner своего Addressables-handle через `AssetCache` (`owner = this`): в кэше у handle всегда ровно один ключ. Перекрывающиеся загрузки одного handle (например, повтор после отмены, пока отменённая загрузка ещё в полёте) кэш считает отдельными удержаниями этого ключа.

Flow `LoadAsync(ct)`:
1. Если `_cached != null` → warning (повторный Load без Release) + тот же экземпляр, без обращения к `AssetCache`.
2. Иначе — `await AssetCache.Load<T>(this, reference, ct)` → сохранить в `_cached`.

Flow `Release()`:
1. Если `_cached == null` — no-op.
2. `AssetCache.Release(this)`; `_cached = null`. Handle снова готов к `LoadAsync`.

---

## Edge cases

| Ситуация | Поведение |
|---|---|
| `DirectAssetHandle` с `asset == null` | `IsLoaded == false`, `LoadAsync` возвращает `null` — валидно как «пусто по замыслу» |
| Повторный `LoadAsync` у `AddressableAssetHandle` без `Release` | Тот же экземпляр + warning, без обращения к `AssetCache` |
| `AddressableAssetHandle.reference == null` | `AssetCache.Load` бросает `ArgumentNullException` — fail-fast конфиг-баг |
| Проект без Addressables, пресет содержал `AddressableAssetHandle` | Поле десериализуется как `null`. Consumer обязан явно проверять null-handle. Автоматического fallback нет — маскирование конфиг-бага было бы вреднее |
| Отмена через `ct` во время подгрузки (Addressable) | `AssetCache.Load` выбрасывает `OperationCanceledException` и сам снимает удержание этой загрузки; handle остаётся не-loaded (retry возможен) |
| Повторный `LoadAsync` у `AddressableAssetHandle`, пока предыдущий (в т.ч. отменённый) ещё в полёте | Второе удержание того же ключа (`owner = this`). Отменённая загрузка снимает только своё удержание, ассет держит живая |
| Два параллельных успешных `LoadAsync` у `AddressableAssetHandle` | Два удержания одного ключа; один `Release` снимает оба |
| `Release` у `AddressableAssetHandle` во время подгрузки | No-op (`_cached == null`): после завершения загрузки ассет загружен и удерживается до следующего `Release` |
