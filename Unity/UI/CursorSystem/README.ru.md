# CursorSystem

**Namespace:** `Vortex.Unity.UI.CursorSystem`
**Сборка:** `ru.vortex.unity.cursorsystem`

---

## Назначение

Кастомный курсор для UGUI-проектов. Каждое состояние курсора описывается **набором** `CursorHoverEntry` из трёх спрайтов: `Default` (без нажатий), `Action` (LMB), `AltAction` (RMB), плюс флаг `HideCursor` (спрятать системный курсор под собственный оверлей). Один такой набор — базовый (вне hover-зон), остальные — hover-варианты, адресуемые по строковому **ключу** (`CursorHoverEntry.Name`) из UI-зон.

Наборы группируются в **пакеты по диапазонам разрешения** — контроллер выбирает подходящий пакет под текущий `Screen.height` (одни спрайты для 1080p, другие для 4K). Hover-ключ, отсутствующий в выбранном пакете, **наследуется от более раннего** (см. межпакетный фолбэк). Применение к системному курсору идёт через `Cursor.SetCursor` в режиме `ForceSoftware`, события мыши — через Unity Input System (без полинга).

Опционально `PointerModeGate` позволяет управлять этим же системным курсором с **геймпада** через `VirtualMouseInput` — стик двигает ОС-курсор (который стилизует контроллер), совместно с мышью.

Вне ответственности:
- Жесты, drag-логика, click-feedback в игровой механике — это уровень `AdvancedButton` / `InputBusSystem`.
- Курсор в world-space (на сцене как объект) — это другой паттерн, тут только системный курсор Unity.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.AppSystem` | `App.OnExit` — корректное освобождение InputAction'ов |
| `Vortex.Core.SettingsSystem` | `Settings.OnInit`, partial-расширение `SettingsModel` |
| `Vortex.Core.Extensions.ReactiveValues` | `BoolData`, `StringData` с owner-защищённой записью |
| `Vortex.Unity.SettingsSystem` | `SettingsPreset` — базовый класс конфига |
| `Vortex.Unity.Extensions.Editor` | `MenuConfigSearchController` — меню-команда поиска конфига (editor) |
| `Unity.InputSystem` | `InputAction` для LMB/RMB; `VirtualMouseInput` в `PointerModeGate` |
| `UnityEngine.UI.EventSystems` | `IPointerEnter/Exit` в `MouseHoverListener` |
| Sirenix Odin Inspector | `[BoxGroup]`, `[InfoBox]`, `[ValueDropdown]`, `[FoldoutGroup]` |

`SettingsModelExt/ru.vortex.settings.asmref` подкладывает partial-расширение модели настроек в сборку `ru.vortex.settings`, чтобы поля курсора жили в общем `SettingsModel`. Типы `CursorPack`, `CursorResolutionPack` и `CursorHoverEntry` тоже лежат в `SettingsModelExt/` и компилируются в сборку настроек — обратная ссылка из неё на пакет курсора невозможна (цикл), поэтому модели данных вынесены туда, где их видит и сборка настроек, и сам пакет.

---

## Архитектура

```
[CursorSettings] (SettingsPreset, SO)
   └── cursorPacks: CursorResolutionPack[]   (по возрастанию maxScreenHeight)
          ├── { maxScreenHeight, CursorPack }   ← пакет для разрешений ≤ maxScreenHeight
          └── ...
                  │  CursorPack = { CursorDefault: CursorHoverEntry,      ← базовый набор
                  │                 CursorOnHover: CursorHoverEntry[] }    ← hover-варианты по ключу
                  │
                  │  CursorHoverEntry = { Name, Default, Action(LMB),
                  │                       AltAction(RMB), HideCursor }
                  │  (через Settings.OnInit + partial SettingsModel)
                  ▼
[Settings.Data() in SettingsModel]
   └── CursorPacks: CursorResolutionPack[]

[CursorController] (static)
   ├── Settings.OnInit → Init() — читает пакеты, SelectPack по Screen.height, поднимает InputAction
   ├── SelectPack(packs) — выбор пакета под текущее разрешение (+ запоминает индекс для фолбэка)
   ├── RefreshResolution() — публичный перевыбор пакета после смены разрешения
   ├── InputAction "<Mouse>/leftButton"  → started/canceled → MouseKeys.LeftKeyPressed
   ├── InputAction "<Mouse>/rightButton" → started/canceled → MouseKeys.RightKeyPressed
   ├── OnHover(key) / OnUnHover(key) ← публичный API из view-слоя
   └── ApplyByPriority() — выбор набора (hover-ключ / база) → набор скрывает курсор или
                           ResolveHover(набор) → Cursor.SetCursor(ForceSoftware)

[MouseHoverListener] (MonoBehaviour, на UGUI-объектах)
   └── IPointerEnter/Exit → CursorController.OnHover(key) / OnUnHover(key)

[MouseKeyMap] (POCO, доступен через CursorController.MouseKeys)
   ├── BoolData   LeftKeyPressed
   ├── BoolData   RightKeyPressed
   └── StringData HoverKey     (пусто/null = нет hover)

[PointerModeGate] (MonoBehaviour, опционально — рядом с VirtualMouseInput)
   └── тоглит VirtualMouseInput по активному устройству (мышь ↔ геймпад):
       стик/пад → VMI варпит СИСТЕМНЫЙ курсор → CursorController стилизует его (один курсор)
```

### Выбор пакета по разрешению

`SelectPack(CursorResolutionPack[])` (`CursorController.cs`) выбирает пакет так:

1. Среди пакетов с `MaxScreenHeight >= Screen.height` берётся **минимальный** подходящий порог — самый «тесный» пакет, покрывающий текущее разрешение.
2. Если текущее разрешение выше всех порогов — берётся **самый крупный** пакет (наибольший `MaxScreenHeight`).

Пример: пакеты с порогами `1080`, `1440`, `2160`. При `Screen.height == 1440` → пакет `1440`. При `Screen.height == 3000` (выше всех) → пакет `2160`.

Помимо выбранного пакета контроллер запоминает его **индекс** — он нужен для межпакетного фолбэка hover-ключей.

### Межпакетный фолбэк hover-ключей

Пакеты **обязаны идти по возрастанию `MaxScreenHeight`** (первый = низкое разрешение). Hover-ключ (`CursorHoverEntry.Name`) резолвится `ResolveHoverEntry`: начиная с выбранного пакета и **вниз по массиву к первому**. Ключ, отсутствующий в выбранном (более «высоком») пакете, **наследуется от более раннего**; вверх фолбэка нет. Ключ, не найденный нигде, → базовый набор.

Это заменяет прежнее требование «одинаковая длина и порядок hover-массивов во всех наборах»: теперь у каждого пакета может быть свой набор ключей, а недостающие подхватываются из низкоразрешённого пакета. `CursorSettings.OnValidate` пишет `LogWarning`, если пакеты идут не по возрастанию `MaxScreenHeight` (от чего зависит фолбэк).

### Перевыбор после смены разрешения

```csharp
CursorController.RefreshResolution();
```

Публичный метод. Дёргать после применения видеонастроек (смена разрешения / режима окна) — контроллер перевыберет пакет под новый `Screen.height` и применит курсор по текущему состоянию. Состояние кнопок и hover сохраняется. No-op, если курсор аппаратный или контроллер не инициализирован.

### Применение курсора

`ApplyByPriority` (`CursorController.cs`) работает в два шага:

1. **Выбор набора.** Если активен hover-ключ (`HoverKey` не пуст) и он резолвится по пакетам (`ResolveHoverEntry`) → его набор. Иначе (вне зоны или ключ нигде не найден) → базовый набор пакета (`CursorDefault`).
2. **Внутри набора** (`ResolveHover`): нажата LMB → `Action`, нажата RMB → `AltAction`, иначе → `Default`. Незаполненное action-поле откатывается на `Default` набора; пустой `Default` hover-варианта → на `Default` базового набора (через `Apply(null)`).

Если у выбранного набора выставлен `HideCursor` — системный курсор скрывается (`Cursor.visible = false`, идемпотентно через `SetCursorHidden`), спрайт не ставится: набор рассчитан на собственный кастомный курсор-оверлей.

То есть нажатия LMB/RMB теперь действуют **внутри активного набора** (hover-зоны или базы), а не как отдельные глобальные курсоры.

### Аппаратный курсор

Если в `CursorSettings` список пакетов пуст (или `null`), `Init()` рано выходит, InputAction'ы не создаются, `Cursor.SetCursor` не зовётся — курсор остаётся системным (аппаратным). Это позволяет глобально отключить кастомный курсор, очистив список, без правки кода.

### Режим ForceSoftware

`Apply(Sprite)` зовёт `Cursor.SetCursor(texture, hotspot, CursorMode.ForceSoftware)`. Аппаратный курсор ОС ограничен по размеру и формату (на большинстве платформ — 32×32, фиксированный формат текстуры), и `CursorMode.Auto` отдаёт спрайт железу, обрезая/масштабируя его под эти лимиты. `ForceSoftware` заставляет движок рисовать курсор самостоятельно — спрайт отображается «как нарисован», любого размера и качества. Цена — курсор рисуется на кадр позже железного, что для кастомного курсора визуально незаметно.

### Защита от alt-tab

Подписки на `InputAction.started/canceled` через Unity Input System — при потере фокуса окна InputSystem делает soft-reset устройства и шлёт `canceled` всем активным action'ам. `MouseKeys.LeftKeyPressed` / `RightKeyPressed` автоматически возвращаются в `false`, и `ApplyByPriority` откатывает курсор на `Default` набора. Залипания action-курсора после alt-tab быть не может.

### Защита от гонки hover-зон

В `OnUnHover(key)` проверка `MouseKeys.HoverKey.Value != key`: если ключ уже не наш (другая зона перехватила hover в том же кадре), вызов игнорируется. Сценарий типичен для вложенных hover-зон в UGUI — EventSystem шлёт `OnPointerEnter` вложенной зоны до `OnPointerExit` родителя.

### Защита от внешней записи в `MouseKeys`

`Run()` (под `[RuntimeInitializeOnLoadMethod]`) вызывает `SetOwner(Key)` на `LeftKeyPressed`, `RightKeyPressed` и `HoverKey`. После этого записать значение снаружи нельзя — `Set(value, ownerKey)` откажет, если `ownerKey` не совпадает. Снаружи доступно только чтение и подписка.

### Hotspot

В `Apply(Sprite)` hotspot курсора берётся из `Sprite.pivot` и инвертируется по Y: Unity Sprite использует систему координат bottom-left, а `Cursor.SetCursor` ожидает top-left. Дизайнер задаёт пивот спрайта обычным способом в импортере, инверсию делает контроллер.

### Геймпад-курсор (PointerModeGate)

`PointerModeGate` (`PointerModeGate.cs`) — **опциональный** `MonoBehaviour`, позволяющий управлять тем же системным курсором, что стилизует контроллер, с геймпада — без второго экранного курсора.

Он ожидает `VirtualMouseInput` (Unity Input System, UGUI-модуль) на **том же объекте** (`[RequireComponent]`), настроенный в режиме **`Hardware Cursor If Available`**: в нём VMI варпит системную мышь от стика и своего графического курсора не рисует, поэтому визуал остаётся за `CursorController` (`Cursor.SetCursor` ForceSoftware по позиции ОС-курсора). Его `stickAction` / кнопки — инлайн-биндинги геймпада на самом компоненте, отдельно от `InputBusSystem`.

Проблема, которую решает гейт: в этом режиме VMI варпит системную мышь **каждый кадр** и «перехватывает» физическую мышь. Гейт держит VMI **выключенным** (мышь работает нативно, контроллер рисует по её позиции) и включает его **только** пока игрок реально двигает стик / жмёт геймпад-кнопки:

- **Приоритет у геймпада лишь пока стик/кнопка активны** — поэтому собственная варп-дельта не сбивает режим; отпустил стик → управление возвращается мыши по первому реальному движению.
- **При входе в геймпад-режим** позиция виртуальной мыши синхронизируется с реальной — без прыжка. При выходе системная мышь уже там, куда её отварпил VMI, — тоже без прыжка.
- **Кнопки в геймпад-режиме** даёт виртуальная мышь VMI (`<Mouse>/leftButton|rightButton`) — их читают и `CursorController` (Action/AltAction-спрайт), и UI-модуль (клик). Инъекции нет.
- **Физическая** мышь резолвится отдельно от виртуальной (в геймпад-режиме `Mouse.current` = виртуальная, детект по ней был бы неверен).

Поля: `stickDeadzone` (ниже — геймпад считается неактивным), `mouseMoveThreshold` (дельта реальной мыши, px/кадр, выше которой мышь перехватывает управление). Ссылка `virtualMouseInput` подхватывается с того же объекта, если оставить пустой.

---

## Использование

### 1. Создать пресет настроек

`Assets → Create → Vortex → CursorSettings` (точное меню зависит от того, как зарегистрирован `SettingsPreset`-pipeline в проекте). Быстрый доступ к существующему конфигу — меню **Tools → Vortex → Configs → Cursor Settings** (подсветит ассет в Project).

Заполни `cursorPacks` — массив пакетов по разрешениям, **по возрастанию `maxScreenHeight`**. В каждом пакете:
- `maxScreenHeight` — верхняя граница вертикального разрешения для этого пакета.
- `CursorDefault` — базовый набор (`CursorHoverEntry`): `Default` обязателен для активации системы; `Action`/`AltAction` опциональны (feedback на клик); `HideCursor` — спрятать системный курсор.
- `CursorOnHover[]` — hover-варианты. У каждого `Name` (ключ), свои `Default`/`Action`/`AltAction`/`HideCursor`.

> ⚠️ **Пакеты обязаны идти по возрастанию `maxScreenHeight`** — от этого зависит наследование hover-ключей от более раннего пакета. `OnValidate` предупредит о нарушении порядка. Длины hover-массивов совпадать больше не обязаны.

Минимальная конфигурация — один пакет с большим `maxScreenHeight` (например, `99999`): он применяется на любом разрешении.

### 2. Повесить `MouseHoverListener` на UGUI-элемент

```
EnemyPortrait (UGUI Image)
├── Image (Raycast Target ✓)
└── MouseHoverListener (key выбирается из dropdown в инспекторе)
```

Dropdown подтягивает **объединённый список уникальных ключей** (`CursorHoverEntry.Name`) по всем пакетам активного `CursorSettings` — у каждого пакета может быть свой набор. Дизайнер выбирает по имени. Пункт «[NONE]» = пустой ключ отключает hover-смену для зоны.

### 3. Перевыбор курсора после смены разрешения

```csharp
// В обработчике применения видеонастроек:
VideoController.ApplyResolution(newResolution);
CursorController.RefreshResolution();   // подхватит пакет под новый Screen.height
```

### 4. Программный hover

Если hover-зона не UGUI (world-space коллайдер, кастомная raycast-логика, hotkey-эмуляция) — зови напрямую по ключу:

```csharp
public class WorldHoverTrigger : MonoBehaviour
{
    [SerializeField] private string cursorKey = "interact";

    private void OnMouseEnter() => CursorController.OnHover(cursorKey);
    private void OnMouseExit()  => CursorController.OnUnHover(cursorKey);
}
```

### 5. Подписка на состояние мыши снаружи

```csharp
private void OnEnable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate += OnLmbChanged;
    CursorController.MouseKeys.HoverKey.OnUpdate += OnHoverChanged;
}

private void OnDisable()
{
    CursorController.MouseKeys.LeftKeyPressed.OnUpdate -= OnLmbChanged;
    CursorController.MouseKeys.HoverKey.OnUpdate -= OnHoverChanged;
}

private void OnLmbChanged(bool pressed) { ... }
private void OnHoverChanged(string key) { ... }
```

Writeback через `MouseKeys.LeftKeyPressed.Set(true, ?)` снаружи не пройдёт — owner закреплён за контроллером.

### 6. Геймпад-курсор от стика (опционально)

1. На объект с `EventSystem` / `InputSystemUIInputModule` добавь **`VirtualMouseInput`**: `Cursor Mode = Hardware Cursor If Available` (`Cursor Graphic`/`Cursor Transform` оставь пустыми — визуал за `CursorController`), `Stick Action = <Gamepad>/leftStick` (Value/Vector2), `Left Button = <Gamepad>/buttonSouth`, `Right Button = <Gamepad>/buttonEast` (→ AltAction), скролл опционально.
2. Добавь **`PointerModeGate`** на тот же объект. Ссылка на `VirtualMouseInput` подхватится сама (`RequireComponent`).
3. Готово: мышь работает как раньше (контроллер стилизует ОС-курсор); стик двигает тот же курсор и кликает по кнопкам; над зоной с `HideCursor` курсор скрывается как обычно, позиция при этом трекается.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Список `cursorPacks` пуст или `null` | Контроллер не активируется, курсор системный |
| Выбранный пакет без `CursorDefault.Default` | `Apply` упадёт на `_defaultSet.Default.texture` (fail-fast: базовый набор обязан иметь `Default`) |
| `Screen.height` выше всех порогов | Берётся самый крупный пакет (наибольший `maxScreenHeight`) |
| `Screen.height` ниже всех порогов | Берётся пакет с минимальным порогом |
| Пакеты идут не по возрастанию `maxScreenHeight` | `OnValidate` → `LogWarning`; межпакетный фолбэк ключей будет некорректен |
| Hover-ключ есть в раннем пакете, но нет в выбранном | Наследуется от более раннего (вниз к первому) |
| Hover-ключ не найден ни в одном пакете | Откат на базовый набор |
| `Action`/`AltAction` набора == null при нажатии | Откат на `Default` набора |
| `Default` hover-варианта == null | Откат на `Default` базового набора (`Apply(null)`) |
| У активного набора `HideCursor == true` | Системный курсор скрывается, спрайт не ставится |
| `RefreshResolution()` при аппаратном курсоре / до Init | No-op |
| Alt-tab / потеря фокуса с нажатой кнопкой | InputSystem шлёт `canceled` → состояние сбрасывается → курсор откатывается на `Default` |
| Вложенные hover-зоны (A содержит B) | Enter(B) выставит ключ B; поздний Exit(A) игнорируется (гонка) |
| Повторная инициализация (рестарт без выгрузки домена) | Старые InputAction'ы освобождаются перед поднятием новых |
| `App.OnExit` | `DisposeActions` освобождает InputAction'ы штатно |
| Открытие сцены без активного `EventSystem` | `MouseHoverListener` не получает Enter/Exit — курсор работает только по нажатиям базового набора |
| Нет `VirtualMouseInput` на объекте | `PointerModeGate` требует его (`[RequireComponent]`) — Unity добавит/сохранит |
| `VirtualMouseInput` не в режиме `Hardware Cursor` | В `Software Cursor` VMI рисует свой графический курсор → второй курсор поверх стилизованного; нужен Hardware-режим |
| Нет физической мыши (только геймпад) | Гейт пропускает синк позиции; VMI включается по стику и ведёт курсор от своей стартовой позиции |
| `PointerModeGate` выключен в рантайме | VMI принудительно гасится — курсор возвращается к физической мыши |

Fail-fast политика на отсутствие `Default` базового набора намеренная: неверная конфигурация должна крашить рано и громко, чтобы дизайнер увидел проблему на этапе разработки, а не получал тихо «не тот курсор» на проде. См. `architecture_context.md` про fail-fast в ядре.

---

## Файловая структура

```
CursorSystem/
├── CursorController.cs                       # static-шина, выбор пакета, подписки на InputSystem, резолв набора/спрайта
├── CursorSettings.cs                         # SettingsPreset (SO) с массивом CursorResolutionPack + OnValidate
├── MouseHoverListener.cs                     # MonoBehaviour для UGUI-зон (hover-ключ через ValueDropdown)
├── MouseKeyMap.cs                            # POCO-модель: BoolData/StringData с owner-защитой
├── PointerModeGate.cs                        # MonoBehaviour: гейт геймпад↔мышь для VirtualMouseInput (опционально)
├── Editor/
│   └── MenuController.cs                     # Tools/Vortex/Configs/Cursor Settings — поиск конфига
├── SettingsModelExt/
│   ├── CursorHoverEntry.cs                   # набор Default/Action/AltAction + Name + HideCursor
│   ├── CursorPack.cs                         # базовый набор + массив hover-вариантов (в сборке настроек)
│   ├── CursorResolutionPack.cs               # CursorPack + порог maxScreenHeight (в сборке настроек)
│   ├── SettingsModelExtCursor.cs             # partial SettingsModel с полем CursorPacks
│   └── ru.vortex.settings.asmref             # подкладка типов + partial в сборку настроек
└── ru.vortex.unity.cursorsystem.asmdef
```
