# RoofTransparentSystem

**Namespace:** `Vortex.Sdk.UIs.RoofTransparentSystem`
**Assembly:** `ru.vortex.sdk.ui.rooftransparent`

## Назначение

Механика плавного снижения непрозрачности верхних спрайтов («крыш», навесов, перекрытий), когда отслеживаемая цель (персонаж, NPC) попадает в зону триггера. Например, при заходе персонажа под крышу спрайт крыши становится полупрозрачным, а при выходе — возвращает полную непрозрачность.

Возможности:
- Регистрация произвольного числа целей через реактивную позицию `Vector2Data`
- Несколько триггерных зон на одном спрайте (массив `TriggerZone`)
- Плавный fade через `AsyncTween` с настраиваемой длительностью и порогом
- Дроссель проверки позиции цели (FixedUpdate, 0.1s)
- Батчинг событий обновления через `TimeController.Accumulate` — одно срабатывание на кадр независимо от количества зарегистрированных целей

Вне ответственности:
- Сама модель движения цели (даёт только позицию)
- Альфа-канал у компонентов, отличных от `SpriteRenderer`

## Активация

Пакет подключается через `SdkSettingsSystem`:

- Тоггл: `roofTransparentSdk` в инспекторе ассета `SdkSettings`
- Define-символ: `USING_VORTEX_ROOF_TRANSPARENCY`
- Меню: `Tools → Vortex → Configs → SDK Settings`

При выключенном тоггле define снимается из PlayerSettings, и пакет не компилируется (asmdef содержит `defineConstraints: ["USING_VORTEX_ROOF_TRANSPARENCY"]`). Канон активации — `Vortex/Sdk/SdkSettingsSystem/README.ru.md`.

## Зависимости

- `Vortex.Core.Extensions.LogicExtensions` — `AddNew`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Accumulate`
- `Vortex.Unity.Extensions.ReactiveValues` — `Vector2Data`
- `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` — `AsyncTween`, `EaseType`
- `Vortex.Unity.EditorTools.Attributes` — `[AutoLink]`
- Sirenix Odin Inspector — `[InfoBox]`

## Архитектура

```
RoofTransparentSystem/
├── RoofTransparentBus.cs          ← static-шина: индекс целей, событие движения
├── RoofTransparentHandler.cs      ← MonoBehaviour на спрайте крыши: тригер-зоны и fade
├── TransparentFocusHandler.cs     ← MonoBehaviour на цели: публикует позицию
├── TriggerZone.cs                 ← MonoBehaviour-зона срабатывания (radius)
├── DefineSettings/
│   ├── SdkSettings.RoofTransparency.cs   ← partial-кусок SdkSettings
│   └── sdk.settings.system.ext.asmref
└── ru.vortex.sdk.ui.rooftransparent.asmdef
```

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `RoofTransparentBus` | `static class` | Реестр `Dictionary<Vector2Data, float>` целей; событие `OnUpdatePositions`, батчится через `TimeController.Accumulate` |
| `RoofTransparentHandler` | `MonoBehaviour` | Размещается на объекте крыши. Хранит массив `TriggerZone`, `SpriteRenderer` (через `[AutoLink]`), параметры `fadeTime`, `minAlpha`. На каждом срабатывании события считает попадание и запускает fade |
| `TransparentFocusHandler` | `MonoBehaviour` | Размещается на цели. Регистрирует `Vector2Data _positionContainer` в шине, обновляет позицию в `FixedUpdate` с дроссель `0.1s` |
| `TriggerZone` | `MonoBehaviour` | Зона-сфера с `Radius` (0.01–2). Рисует Gizmos в редакторе |

## Контракт

### Вход
- `RoofTransparentBus.Register(Vector2Data position, float size)` — регистрация цели
- `RoofTransparentBus.Unregister(Vector2Data position)` — снятие
- Положение цели обновляется в её `Vector2Data` (через `Set`)

### Выход
- Альфа-канал `SpriteRenderer.color.a` на объекте `RoofTransparentHandler` плавно меняется между `1.0` и `minAlpha`

### Гарантии
- Срабатывание происходит при `(position − triggerCenter).magnitude < triggerCenter.Radius + size`
- Длительность fade пропорциональна остатку пути: незавершённый tween не «стартует с нуля»
- Снятие с регистрации очищает подписку на `OnUpdateData`
- `OnDisable` хэндлера убивает активный tween

### Ограничения
- Прозрачность управляется только у `SpriteRenderer` (хардкод цвета через `new Color(1, 1, 1, f)`)
- Минимум одна `TriggerZone` обязательна — иначе `LogWarning` и handler не подписывается
- `OnValidate` авто-собирает `TriggerZone` из дочерних объектов, если массив пуст

## Использование

```csharp
// На целе (персонаж):
[RequireComponent(typeof(TransparentFocusHandler))]
public class Character : MonoBehaviour { /* ... */ }

// На крыше:
// 1. Добавить SpriteRenderer
// 2. Добавить RoofTransparentHandler (sprite авто-подвяжется через [AutoLink])
// 3. Создать дочерние GameObject с TriggerZone (или один на самом объекте)
// 4. Настроить fadeTime и minAlpha в инспекторе
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `triggersCenter.Length == 0` на `OnEnable` | `LogWarning`, подписка пропускается |
| Цель снята с регистрации во время fade | Tween продолжается до завершения (если хэндлер активен) |
| Несколько целей одновременно в зонах | Достаточно одного попадания — `isTransparent = true`, цикл обрывается |
| Handler деактивирован | `OnDisable` убивает tween и отписывается от шины |
| `ResetIndex()` | Полная очистка реестра и всех подписок |
