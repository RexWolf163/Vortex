# CursorSystem

**Namespace:** `Vortex.Unity.UI.CursorSystem`
**Сборка:** `ru.vortex.unity.cursorsystem`

---

## Назначение

Кастомный курсор для UGUI-проектов: дефолтный спрайт, отдельные спрайты на LMB/RMB и массив hover-вариантов, переключаемых по UI-зонам. Курсоры группируются в **наборы по диапазонам разрешения** — контроллер выбирает подходящий набор под текущий `Screen.height` (одни спрайты для 1080p, другие для 4K). Применение спрайта к системному курсору идёт через `Cursor.SetCursor` в режиме `ForceSoftware`, события мыши — через Unity Input System (без полинга).

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
| `Unity.InputSystem` | `InputAction` для LMB/RMB |
| `UnityEngine.UI.EventSystems` | `IPointerEnter/Exit` в `MouseHoverListener` |
| Sirenix Odin Inspector | `[BoxGroup]`, `[InfoBox]`, `[ValueDropdown]` |

`SettingsModelExt/ru.vortex.settings.asmref` подкладывает partial-расширение модели настроек в сборку `ru.vortex.settings`, чтобы поля курсора жили в общем `SettingsModel`. Типы `CursorPack` и `CursorResolutionPack` тоже лежат в `SettingsModelExt/` и компилируются в сборку настроек — обратная ссылка из неё на пакет курсора невозможна (цикл), поэтому модели данных вынесены туда, где их видит и сборка настроек, и сам пакет.

---

## Архитектура

```
[CursorSettings] (SettingsPreset, SO)
   └── cursorPacks: CursorResolutionPack[]
          ├── { maxScreenHeight, CursorPack }   ← набор для разрешений ≤ maxScreenHeight
          ├── { maxScreenHeight, CursorPack }
          └── ...
                  │  CursorPack = { cursorDefault, cursorLeftMouseDown,
                  │                 cursorRightMouseDown, cursorOnHover[] }
                  │
                  │  (через Settings.OnInit + partial SettingsModel)
                  ▼
[Settings.Data() in SettingsModel]
   └── CursorPacks: CursorResolutionPack[]

[CursorController] (static)
   ├── Settings.OnInit → Init() — читает наборы, SelectPack по Screen.height, поднимает InputAction
   ├── SelectPack(packs) — выбор набора под текущее разрешение
   ├── RefreshResolution() — публичный перевыбор набора после смены разрешения
   ├── InputAction "<Mouse>/leftButton"  → started/canceled → MouseKeys.LeftKeyPressed
   ├── InputAction "<Mouse>/rightButton" → started/canceled → MouseKeys.RightKeyPressed
   ├── OnHover(index) / OnUnHover(index) ← публичный API из view-слоя
   └── ApplyByPriority() — LMB > RMB > Hover > Default → Cursor.SetCursor(ForceSoftware)

[MouseHoverListener] (MonoBehaviour, на UGUI-объектах)
   └── IPointerEnter/Exit → CursorController.OnHover(index) / OnUnHover(index)

[MouseKeyMap] (POCO, доступен через CursorController.MouseKeys)
   ├── BoolData LeftKeyPressed
   ├── BoolData RightKeyPressed
   └── IntData  HoverIndex     (-1 = нет hover)
```

### Выбор набора по разрешению

`SelectPack(CursorResolutionPack[])` (`CursorController.cs`) выбирает набор так:

1. Среди наборов с `MaxScreenHeight >= Screen.height` берётся **минимальный** подходящий порог — самый «тесный» набор, покрывающий текущее разрешение.
2. Если текущее разрешение выше всех порогов — берётся **самый крупный** набор (наибольший `MaxScreenHeight`).

Пример: наборы с порогами `1080`, `1440`, `2160`. При `Screen.height == 1440` → набор `1440`. При `Screen.height == 3000` (выше всех) → набор `2160`.

`cursorOnHover[]` **должен быть одинаковой длины и порядка во всех наборах** — индекс `MouseHoverListener.index` общий для всех разрешений. `CursorSettings.OnValidate` пишет `LogWarning`, если длины hover-массивов разошлись между наборами.

### Перевыбор после смены разрешения

```csharp
CursorController.RefreshResolution();
```

Публичный метод. Дёргать после применения видеонастроек (смена разрешения / режима окна) — контроллер перевыберет набор под новый `Screen.height` и применит спрайт через приоритеты. Состояние кнопок и hover сохраняется. No-op, если курсор аппаратный или контроллер не инициализирован.

### Приоритеты применения спрайта

В `ApplyByPriority` (`CursorController.cs`) реализован каскад с явными `return`:

1. **LMB нажата + `cursorLeftMouseDown` не null** → ставим LMB-спрайт.
2. **RMB нажата + `cursorRightMouseDown` не null** → ставим RMB-спрайт.
3. **`HoverIndex >= 0` + `cursorOnHover[HoverIndex]` не null** → ставим hover-спрайт.
4. Иначе → дефолтный спрайт.

LMB переезжает RMB и hover. RMB переезжает hover. Hover активен только когда мышь не нажата.

### Аппаратный курсор

Если в `CursorSettings` список наборов пуст (или `null`), `Init()` рано выходит, InputAction'ы не создаются, `Cursor.SetCursor` не зовётся — курсор остаётся системным (аппаратным). Это позволяет глобально отключить кастомный курсор, очистив список, без правки кода.

### Режим ForceSoftware

`Apply(Sprite)` зовёт `Cursor.SetCursor(texture, hotspot, CursorMode.ForceSoftware)`. Аппаратный курсор ОС ограничен по размеру и формату (на большинстве платформ — 32×32, фиксированный формат текстуры), и `CursorMode.Auto` отдаёт спрайт железу, обрезая/масштабируя его под эти лимиты. `ForceSoftware` заставляет движок рисовать курсор самостоятельно — спрайт отображается «как нарисован», любого размера и качества. Цена — курсор рисуется на кадр позже железного, что для кастомного курсора визуально незаметно.

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

Заполни `cursorPacks` — массив наборов по разрешениям. В каждом наборе:
- `maxScreenHeight` — верхняя граница вертикального разрешения для этого набора.
- `Pack.cursorDefault` — основной спрайт (обязателен для активации системы).
- `Pack.cursorLeftMouseDown` / `cursorRightMouseDown` — опционально, для feedback на клик.
- `Pack.cursorOnHover[]` — массив спрайтов для разных типов UI-зон.

> ⚠️ **Длина и порядок `cursorOnHover[]` обязаны совпадать во всех наборах** — `MouseHoverListener.index` адресует один и тот же логический hover во всех разрешениях. `OnValidate` предупредит о расхождении.

Минимальная конфигурация — один набор с большим `maxScreenHeight` (например, `99999`): он будет применяться на любом разрешении.

### 2. Повесить `MouseHoverListener` на UGUI-элемент

```
EnemyPortrait (UGUI Image)
├── Image (Raycast Target ✓)
└── MouseHoverListener (index выбирается из dropdown в инспекторе)
```

Dropdown в инспекторе подтягивает имена спрайтов из самого крупного набора активного `CursorSettings` (индексы общие для всех разрешений) — дизайнер не вспоминает числа, выбирает по имени. Пункт «[NONE]» = `-1` отключает hover-смену для этой зоны. Пустые слоты массива показываются как `[EMPTY] {i}`.

### 3. Перевыбор курсора после смены разрешения

```csharp
// В обработчике применения видеонастроек:
VideoController.ApplyResolution(newResolution);
CursorController.RefreshResolution();   // подхватит набор под новый Screen.height
```

### 4. Программный hover

Если hover-зона не UGUI (world-space коллайдер, кастомная raycast-логика, hotkey-эмуляция) — зови напрямую:

```csharp
public class WorldHoverTrigger : MonoBehaviour
{
    [SerializeField] private int cursorIndex = 0;

    private void OnMouseEnter() => CursorController.OnHover(cursorIndex);
    private void OnMouseExit()  => CursorController.OnUnHover(cursorIndex);
}
```

### 5. Подписка на состояние мыши снаружи

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
| Список `cursorPacks` пуст или `null` | Контроллер не активируется, курсор системный |
| Выбранный набор без `cursorDefault` | `Apply` упадёт на `_cursorDefault.texture` (fail-fast: набор обязан иметь дефолт) |
| `Screen.height` выше всех порогов | Берётся самый крупный набор (наибольший `maxScreenHeight`) |
| `Screen.height` ниже всех порогов | Берётся набор с минимальным порогом |
| Длины `cursorOnHover[]` разошлись между наборами | `OnValidate` → `LogWarning`; в рантайме индекс адресует «не тот» hover |
| `cursorLeftMouseDown == null` при нажатой LMB | Пропускаем ветку, идём дальше по приоритету (RMB → Hover → Default) |
| `cursorOnHover` пуст / индекс за пределами | На любой `OnHover(index >= 0)` → `IndexOutOfRangeException` (fail-fast) |
| `cursorOnHover[index] == null` (валидный индекс, пустой спрайт) | Откат на default |
| `RefreshResolution()` при аппаратном курсоре / до Init | No-op |
| Alt-tab / потеря фокуса с нажатой кнопкой | InputSystem шлёт `canceled` → состояние сбрасывается → курсор откатывается на default |
| Вложенные hover-зоны (A содержит B) | Enter(B) выставит индекс B; поздний Exit(A) игнорируется (гонка) |
| Повторная инициализация (рестарт без выгрузки домена) | Старые InputAction'ы освобождаются перед поднятием новых |
| `App.OnExit` | `DisposeActions` освобождает InputAction'ы штатно |
| Открытие сцены без активного `EventSystem` | `MouseHoverListener` не получает Enter/Exit — курсор работает только по LMB/RMB и default |

Fail-fast политика на `cursorOnHover` и отсутствие `cursorDefault` намеренная: неверная конфигурация должна крашить рано и громко, чтобы дизайнер увидел проблему на этапе разработки, а не получал тихо «не тот курсор» на проде. См. `architecture _context.md` про fail-fast в ядре.

---

## Файловая структура

```
CursorSystem/
├── CursorController.cs                       # static-шина, выбор набора, подписки на InputSystem, приоритеты
├── CursorSettings.cs                         # SettingsPreset (SO) с массивом CursorResolutionPack + OnValidate
├── MouseHoverListener.cs                     # MonoBehaviour для UGUI-зон
├── MouseKeyMap.cs                            # POCO-модель: BoolData/IntData с owner-защитой
├── SettingsModelExt/
│   ├── CursorPack.cs                         # набор из 4 спрайтов (в сборке настроек)
│   ├── CursorResolutionPack.cs               # CursorPack + порог maxScreenHeight (в сборке настроек)
│   ├── SettingsModelExtCursor.cs             # partial SettingsModel с полем CursorPacks
│   └── ru.vortex.settings.asmref             # подкладка типов + partial в сборку настроек
└── ru.vortex.unity.cursorsystem.asmdef
```
