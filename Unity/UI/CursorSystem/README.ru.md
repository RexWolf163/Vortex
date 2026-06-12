# CursorSystem

**Namespace:** `Vortex.Unity.UI.CursorSystem`
**Сборка:** `ru.vortex.unity.cursorsystem`

---

## Назначение

Кастомный курсор для UGUI-проектов: дефолтный спрайт, отдельные спрайты на LMB/RMB и массив hover-вариантов, переключаемых по UI-зонам. Применение спрайта к системному курсору идёт через `Cursor.SetCursor`, события мыши — через Unity Input System (без полинга).

Вне ответственности:
- Жесты, drag-логика, click-feedback в игровой механике — это уровень `AdvancedButton` / `InputBusSystem`.
- Курсор в world-space (на сцене как объект) — это другой паттерн, тут только системный курсор Unity.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.AppSystem` | `App.OnExit` — корректное освобождение InputAction'ов |
| `Vortex.Core.SettingsSystem` | `Settings.OnInit`, partial-расширение `SettingsModel` |
| `Vortex.Core.Extensions.ReactiveValues` | `BoolData`, `IntData` с owner-защищённой записью |
| `Vortex.Unity.SettingsSystem` | `SettingsPreset` — базовый класс конфига |
| `Vortex.Unity.UI.UIComponents` | (опционально, через UIComponent в потребителях) |
| `Unity.InputSystem` | `InputAction` для LMB/RMB |
| `UnityEngine.UI.EventSystems` | `IPointerEnter/Exit` в `MouseHoverListener` |
| Sirenix Odin Inspector | `[BoxGroup]`, `[InfoBox]`, `[ValueDropdown]` |

`SettingsModelExt/ru.vortex.settings.asmref` подкладывает partial-расширение модели настроек в сборку `ru.vortex.settings`, чтобы поля курсора жили в общем `SettingsModel`.

---

## Архитектура

```
[CursorSettings] (SettingsPreset, SO)
   └── cursorDefault / cursorLeftMouseDown / cursorRightMouseDown / cursorOnHover[]
           │
           │  (через Settings.OnInit + partial SettingsModel)
           ▼
[Settings.Data() in SettingsModel]
   └── CursorDefault, CursorLeftMouseDown, CursorRightMouseDown, CursorOnHover[]

[CursorController] (static)
   ├── Settings.OnInit → Init() — читает поля настроек, поднимает InputAction
   ├── InputAction "<Mouse>/leftButton"  → started/canceled → MouseKeys.LeftKeyPressed
   ├── InputAction "<Mouse>/rightButton" → started/canceled → MouseKeys.RightKeyPressed
   ├── OnHover(index) / OnUnHover(index) ← публичный API из view-слоя
   └── ApplyByPriority() — LMB > RMB > Hover > Default → Cursor.SetCursor

[MouseHoverListener] (MonoBehaviour, на UGUI-объектах)
   └── IPointerEnter/Exit → CursorController.OnHover(index) / OnUnHover(index)

[MouseKeyMap] (POCO, доступен через CursorController.MouseKeys)
   ├── BoolData LeftKeyPressed
   ├── BoolData RightKeyPressed
   └── IntData  HoverIndex     (-1 = нет hover)
```

### Приоритеты применения спрайта

В `ApplyByPriority` (`CursorController.cs`) реализован каскад с явными `return`:

1. **LMB нажата + `cursorLeftMouseDown` не null** → ставим LMB-спрайт.
2. **RMB нажата + `cursorRightMouseDown` не null** → ставим RMB-спрайт.
3. **`HoverIndex >= 0` + `cursorOnHover[HoverIndex]` не null** → ставим hover-спрайт.
4. Иначе → дефолтный спрайт.

LMB переезжает RMB и hover. RMB переезжает hover. Hover активен только когда мышь не нажата.

### Аппаратный курсор

Если `cursorDefault` не задан в `CursorSettings`, `Init()` рано выходит, InputAction'ы не создаются, `Cursor.SetCursor` не зовётся — курсор остаётся системным (аппаратным). Это позволяет глобально отключить кастомный курсор, обнулив один SerializeField, без правки кода.

### Защита от alt-tab

Подписки на `InputAction.started/canceled` через Unity Input System — при потере фокуса окна InputSystem делает soft-reset устройства и шлёт `canceled` всем активным action'ам. `MouseKeys.LeftKeyPressed` / `RightKeyPressed` автоматически возвращаются в `false`, и `ApplyByPriority` откатывает курсор на default. Залипания LMB-курсора после alt-tab быть не может.

### Защита от гонки hover-зон

В `OnUnHover(index)` проверка `MouseKeys.HoverIndex != index`: если индекс уже не наш (другая зона перехватила hover в том же кадре), вызов игнорируется. Сценарий типичен для вложенных hover-зон в UGUI — EventSystem шлёт `OnPointerEnter` вложенной зоны до `OnPointerExit` родителя.

### Защита от внешней записи в `MouseKeys`

`Run()` (под `[RuntimeInitializeOnLoadMethod]`) вызывает `SetOwner(Key)` на каждом из трёх `BoolData`/`IntData`. После этого записать значение в `MouseKeys.LeftKeyPressed` снаружи нельзя — `Set(value, ownerKey)` бросит исключение, если `ownerKey` не совпадает. Снаружи доступно только чтение и подписка.

### Hotspot

В `Apply(Sprite)` hotspot курсора берётся из `Sprite.pivot` и инвертируется по Y: Unity Sprite использует систему координат bottom-left, а `Cursor.SetCursor` ожидает top-left. Дизайнер задаёт пивот спрайта обычным способом в импортере, инверсию делает контроллер.

---

## Использование

### 1. Создать пресет настроек

`Assets → Create → Vortex → CursorSettings` (точное меню зависит от того, как у тебя зарегистрирован `SettingsPreset`-pipeline в проекте).

Заполни:
- `cursorDefault` — основной спрайт (обязательно для активации системы).
- `cursorLeftMouseDown` / `cursorRightMouseDown` — опционально, для feedback на клик.
- `cursorOnHover[]` — массив спрайтов для разных типов UI-зон (на кнопку, на иконку инвентаря, на ссылку и т. п.).

### 2. Повесить `MouseHoverListener` на UGUI-элемент

```
EnemyPortrait (UGUI Image)
├── Image (Raycast Target ✓)
└── MouseHoverListener (index выбирается из dropdown в инспекторе)
```

Dropdown в инспекторе подтягивает имена спрайтов из активного `CursorSettings` — дизайнер не вспоминает индексы, выбирает по имени. Пункт «[NONE]» = `-1` отключает hover-смену для этой зоны (полезно, если зона должна получать клики, но курсор менять не нужно).

### 3. Программный hover

Если hover-зона не UGUI (world-space коллайдер, кастомная raycast-логика, hotkey-эмуляция) — зови напрямую:

```csharp
public class WorldHoverTrigger : MonoBehaviour
{
    [SerializeField] private int cursorIndex = 0;

    private void OnMouseEnter() => CursorController.OnHover(cursorIndex);
    private void OnMouseExit()  => CursorController.OnUnHover(cursorIndex);
}
```

### 4. Подписка на состояние мыши снаружи

```csharp
private void OnEnable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate += OnLmbChanged;
    CursorController.MouseKeys.HoverIndex.OnUpdate += OnHoverChanged;
}

private void OnDisable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate -= OnLmbChanged;
    CursorController.MouseKeys.HoverIndex.OnUpdate -= OnHoverChanged;
}

private void OnLmbChanged() { ... }
private void OnHoverChanged() { ... }
```

Writeback через `MouseKeys.LeftKeyPressed.Set(true, ?)` снаружи не пройдёт — owner закреплён за контроллером.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `cursorDefault == null` в настройках | Контроллер не активируется, курсор системный |
| `cursorLeftMouseDown == null` при нажатой LMB | Пропускаем ветку, идём дальше по приоритету (RMB → Hover → Default) |
| `cursorOnHover` пуст или `null` | На любой `OnHover(index >= 0)` → `IndexOutOfRangeException` (fail-fast: некорректная конфигурация) |
| `OnHover(index)` с индексом за пределами массива | `IndexOutOfRangeException` (fail-fast) |
| `cursorOnHover[index] == null` (валидный индекс, но спрайт пустой) | Откат на default |
| Alt-tab / потеря фокуса с нажатой кнопкой | InputSystem шлёт `canceled` → состояние сбрасывается → курсор откатывается на default |
| Вложенные hover-зоны (A содержит B) | Enter(B) выставит индекс B; поздний Exit(A) игнорируется (гонка) |
| Повторная инициализация (рестарт без выгрузки домена) | Старые InputAction'ы освобождаются перед поднятием новых |
| `App.OnExit` | `DisposeActions` освобождает InputAction'ы штатно |
| Открытие сцены без активного `EventSystem` | `MouseHoverListener` не получает Enter/Exit — курсор работает только по LMB/RMB и default |

Fail-fast политика на `cursorOnHover` намеренная: неверная конфигурация массива должна крашить рано и громко, чтобы дизайнер увидел проблему на этапе разработки, а не получал тихо «не тот курсор» на проде. См. `architecture _context.md` про fail-fast в ядре.

---

## Файловая структура

```
CursorSystem/
├── CursorController.cs                       # static-шина, подписки на InputSystem, приоритеты
├── CursorSettings.cs                         # SettingsPreset (SO) с 4 полями спрайтов
├── MouseHoverListener.cs                     # MonoBehaviour для UGUI-зон
├── MouseKeyMap.cs                            # POCO-модель: BoolData/IntData с owner-защитой
├── SettingsModelExt/
│   ├── SettingsModelExtCursor.cs             # partial SettingsModel с полями курсора
│   └── ru.vortex.settings.asmref             # подкладка partial-расширения в сборку настроек
└── ru.vortex.unity.cursorsystem.asmdef
```
