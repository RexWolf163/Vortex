# UIProviderSystem (Unity)

**Namespace:** `Vortex.Unity.UIProviderSystem`
**Сборка:** `ru.vortex.unity.uiprovider`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-адаптация шины `UIProvider`. Связывает Core-модели с MonoBehaviour-представлениями, обеспечивает анимированное открытие/закрытие через TweenerHub, перетаскивание окон с ограничением по экрану и декларативное управление через ScriptableObject-пресеты.

Интерфейс рассматривается как пустой контейнер без собственной логики. Компонент `UserInterface` используется как есть — наследование не предусмотрено. Всё поведение формируется композицией автономных компонентов: условия в пресете управляют видимостью, `TweenerHub` — анимацией, `UIDragHandler` — перетаскиванием, `CallUIHandler` — ручным вызовом. Логика, специфичная для конкретного окна, реализуется внешними компонентами на том же объекте, а не внутри `UserInterface`.

Возможности:

- `UserInterface` — MonoBehaviour-представление: регистрация, анимация открытия/закрытия через `TweenerHub[]`, drag-поддержка
- `UserInterfacePreset` — ScriptableObject-пресет: `RecordPreset<UserInterfaceData>`, тип и условия
- `CallUIHandler` / `CallUIClose` — обработчики кнопок открытия/закрытия/переключения
- `UIDragHandler` — перетаскивание окон через `IDragHandler` с привязкой к `CanvasScaler`
- `UnityUserInterfaceCondition` — абстрактная Unity-условие с `DisplayAsString` для Inspector
- Встроенные условия: `AutoLoadCondition`, `CloseOnOpenAnyUICondition`, `SaveLoadStartCondition`, `OrCondition`
- Кодогенерация условий через `Create > Vortex Templates > UI Condition`

Вне ответственности:

- Платформонезависимая шина, модели, проверка условий — Core-слой (`Vortex.Core.UIProviderSystem`)
- Вёрстка и визуальное оформление интерфейсов
- Логика обработки пользовательского ввода внутри интерфейса

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.UIProviderSystem` | `UIProvider`, `UserInterfaceData`, `UserInterfaceCondition`, `ConditionAnswer` |
| `Vortex.Core.DatabaseSystem` | `RecordPreset<T>`, `Database.GetRecord()` |
| `Vortex.Core.AppSystem` | `App.OnStart` (момент регистрации) |
| `Vortex.Core.SaveSystem` | `SaveController` (условие `SaveLoadStartCondition`) |
| `Vortex.Unity.AppSystem` | `TimeController.Call()` (отложенная регистрация) |
| `Vortex.Unity.UI.TweenerSystem` | `TweenerHub` (анимация открытия/закрытия) |
| `UnityEngine.UI` | `CanvasScaler` (масштаб для drag) |
| `Sirenix.OdinInspector` (опционально) | `DisplayAsString`, `HideReferenceObjectPicker` |

---

## Архитектура

```
UserInterfacePreset : RecordPreset<UserInterfaceData>
  ├── uiType: UserInterfaceTypes
  └── conditions: UnityUserInterfaceCondition[]

UserInterface : MonoBehaviour (partial)
  ├── preset: string (GUID)
  ├── tweeners: TweenerHub[]
  ├── dragZone: UIDragHandler
  ├── wndContainer: RectTransform
  │
  ├── OnEnable() → Register()
  │   ├── App.OnStart += Init / TimeController.Call(Init)
  │   └── Init():
  │       ├── UIProvider.Register(preset) → data
  │       ├── data.Init() (условия)
  │       ├── data.OnOpen += Open()
  │       ├── data.OnClose += Close()
  │       └── LoadDragOffset()
  │
  ├── OnDisable() → Unregister()
  │   ├── data.DeInit() (условия)
  │   └── UIProvider.Unregister(preset)
  │
  ├── Open() → tweeners[].Forward()
  ├── Close() → tweeners[].Back()
  └── UserInterfaceExtDrag (partial)
      ├── LoadDragOffset() → wndContainer.anchoredPosition
      ├── CalcPosition(delta) → clamp по Screen/CanvasScaler
      └── SetDragOffset(x, y) → data.Offset

CallUIHandler : MonoBehaviour
  ├── uiId: string (GUID)
  ├── closeUI: bool
  └── OnClick() → UIProvider.Open/Close/Toggle

CallUIClose : MonoBehaviour
  └── userInterface: UserInterface → Close()

UIDragHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
  ├── dragStateSwitcher: UIStateSwitcher
  ├── OnDrag(delta) → callback
  ├── OnPointerDown → switcher.On
  └── OnPointerUp → switcher.Off

UnityUserInterfaceCondition : UserInterfaceCondition
  └── [DisplayAsString] Name (для Inspector)

Встроенные условия:
  ├── AutoLoadCondition        → всегда Open, переоткрывается при Close
  ├── CloseOnOpenAnyUICondition → Close если открыт другой Common UI
  ├── SaveLoadStartCondition   → Open при Save/Load, Close при Idle
  └── OrCondition              → композитное: вложенные условия с приоритетом
```

### Регистрация и жизненный цикл

1. `UserInterface.OnEnable()` подписывается на `App.OnStart`
2. При `App.OnStart` (или через `TimeController.Call` если уже `Started`) вызывается `Init()`
3. `Init()` регистрирует пресет в `UIProvider`, инициализирует условия, подписывается на `OnOpen`/`OnClose`
4. Safe subscribe: если условия сразу возвращают `Open`, UI открывается немедленно
5. `OnDisable()` деинициализирует условия и снимает регистрацию

### Анимация

`UserInterface` хранит массив `TweenerHub[]`. При `Open()` вызывается `Forward()` на каждом, при `Close()` — `Back()`. Если `tweeners` пуст, открытие/закрытие происходит мгновенно.

### Перетаскивание

`UserInterfaceExtDrag` (partial) обрабатывает drag через `UIDragHandler`:
- `CalcPosition()` вычисляет новую позицию с учётом `CanvasScaler.scaleFactor`
- Позиция ограничена экранными границами (`Screen.width`, `Screen.height`)
- Offset сохраняется в `UserInterfaceData.Offset` → `GetDataForSave()` → `"x;y"`

### Композитное условие OrCondition

`OrCondition` содержит массив вложенных `UnityUserInterfaceCondition[]` и два поля:
- `conditionPriority` — ответ, возвращаемый при первом совпадении
- `notCondition` — ответ, если ни одно условие не совпало

При `Check()` перебирает вложенные условия; первое, вернувшее `conditionPriority`, определяет результат.

---

## Контракт

### Вход

- `UserInterfacePreset` в `Database` — GUID, тип, массив условий
- `UserInterface` на сцене — ссылка на пресет (GUID), tweeners, drag-компоненты
- `CallUIHandler` / `CallUIClose` — ссылка на GUID или `UserInterface`

### Выход

- Анимированное открытие/закрытие UI через `TweenerHub`
- Автоматическое управление видимостью через декларативные условия
- Сохранение позиции окна через `UserInterfaceData.Offset`

### API

| Компонент | Поле/Метод | Описание |
|-----------|-----------|----------|
| `UserInterface` | `preset` | GUID пресета из Database |
| `UserInterface` | `tweeners` | Массив TweenerHub для анимации |
| `UserInterface` | `dragZone` | UIDragHandler (опционально) |
| `UserInterface` | `wndContainer` | RectTransform контейнер окна (для drag) |
| `CallUIHandler` | `uiId` | GUID интерфейса |
| `CallUIHandler` | `closeUI` | `true` — закрыть, `false` — открыть |
| `UIDragHandler` | `OnDrag` | Callback с delta перемещения |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Регистрация требует `App.GetState() >= Starting` | Зависимость от инициализации App |
| `UIDragHandler` требует `CanvasScaler` в иерархии | Масштабирование delta для корректного перемещения |
| Drag-позиция — `(int, int)` | Целочисленные пиксели, сохраняется как `"x;y"` |
| `UserInterface` не наследуется | Различия задаются через пресет и условия |
| `OrCondition` не поддерживает вложенный `OrCondition` | Одноуровневая композиция |

---

## Использование

### Создание интерфейса

1. Создайте `UserInterfacePreset`: `Create > Database > UserInterface Preset`
2. Настройте `UIType` (`Common`, `Panel`, `Overlay`, `Popup`)
3. Добавьте условия в массив `Conditions` (или оставьте пустым для ручного управления)
4. На сцене создайте объект с компонентом `UserInterface`, укажите GUID пресета
5. Добавьте `TweenerHub` для анимации (опционально)

### Ручное управление через кнопку

```csharp
// Компонент CallUIHandler на кнопке:
// uiId = "settings-menu-guid"
// closeUI = false → открыть

// Или программно:
UIProvider.Open("settings-menu-guid");
UIProvider.Close("settings-menu-guid");
```

### Интерфейс с перетаскиванием

1. Добавьте `UIDragHandler` на зону заголовка окна
2. В `UserInterface` укажите `dragZone` и `wndContainer`
3. Убедитесь, что в иерархии есть `CanvasScaler`
4. Позиция сохраняется автоматически через `SaveSystem`

### Создание условия

```csharp
[Serializable]
public sealed class MyCondition : UnityUserInterfaceCondition
{
    protected override void Run()
    {
        SomeSystem.OnEvent += RunCallback;
        RunCallback(); // обязательно — немедленная проверка
    }

    public override void DeInit()
    {
        SomeSystem.OnEvent -= RunCallback;
    }

    public override ConditionAnswer Check()
    {
        return SomeSystem.IsActive
            ? ConditionAnswer.Open
            : ConditionAnswer.Close;
    }
}
```

---

## Редакторные инструменты

### Кодогенерация условий

Путь: `Assets > Create > Vortex Templates > UI Condition`

Генерирует шаблон `UnityUserInterfaceCondition` с заготовленными методами `Run()`, `DeInit()`, `Check()`. Требует ручной доработки: реализация логики, подписка на события, вызов `RunCallback()` в `Run()`.

### Debug-режим

Настройка: ассет `DebugSettings` → toggle `uiLogs`
Флаг: `Settings.Data().UiDebugMode`
Логирование: Register/Unregister в `UIProvider` при активном режиме

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `CanvasScaler` отсутствует при drag | Drag отключён, `Debug.LogError` |
| `UserInterface.OnEnable` до `App.Started` | Подписка на `App.OnStart`, регистрация отложена |
| `UserInterface.OnEnable` после `App.Started` | `TimeController.Call(Init)` — регистрация в следующем кадре |
| Пустой массив `tweeners` | Открытие/закрытие без анимации |
| `CallUIHandler` с невалидным `uiId` | `Log.Print(Error)`, no-op |
| Drag выходит за пределы экрана | Позиция ограничена `(0, 0)` — `(Screen.width, Screen.height)` |
| `OrCondition` — все вложенные `Idle` | Возвращает `notCondition` |
| Подписка на `data.OnOpen` при `IsOpen == true` | Callback вызывается немедленно (safe subscribe) |
