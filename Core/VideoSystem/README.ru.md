# VideoSystem (Core)

Контроллер управления разрешением экрана и режимом отображения.

## Назначение

Платформонезависимая шина для управления видеонастройками устройства: разрешение и режим экрана (полноэкранный, оконный и т.п.).

- Хранение реестров доступных разрешений и режимов экрана
- Получение и установка текущего разрешения
- Получение и установка режима экрана
- Делегирование операций драйверу через `IVideoDriver`

Вне ответственности: рендеринг, качество графики, VSync, частота кадров, настройки камеры.

## Зависимости

- `Vortex.Core.System` — `SystemController<T, TD>`, `Singleton<T>`, `ISystemDriver`

---

## VideoController

Статический контроллер. Наследует `SystemController<VideoController, IVideoDriver>`.

### Архитектура

```
VideoController (SystemController<VideoController, IVideoDriver>)
├── AvailableResolutions: List<string>     — реестр доступных разрешений
├── AvailableScreenModes: List<string>     — реестр доступных режимов
├── GetResolutionsList(): IReadOnlyList    — чтение реестра разрешений
├── GetScreenModes(): IReadOnlyList        — чтение реестра режимов
├── GetResolution(): string                — текущее разрешение
├── SetResolution(string)                  — установка разрешения
├── GetScreenMode(): string                — текущий режим экрана
├── SetScreenMode(string)                  — установка режима экрана
└── OnDriverConnect()                      — передача ссылок на реестры драйверу
```

### Контракт

**Вход:**
- Драйвер регистрируется через `VideoController.SetDriver(driver)`
- Драйвер заполняет реестры `AvailableResolutions` и `AvailableScreenModes` при `Init()`

**Выход:**
- Реестры доступных разрешений и режимов через `GetResolutionsList()` / `GetScreenModes()`
- Текущие значения через `GetResolution()` / `GetScreenMode()`
- Событие `VideoController.OnInit` после инициализации драйвера

**Гарантии:**
- Реестры передаются драйверу по ссылке через `SetLinks()` — драйвер заполняет их напрямую
- `OnDriverConnect()` вызывается до `Driver.Init()`

**Ограничения:**
- Все методы `Get`/`Set` делегируют напрямую в драйвер — при отсутствии драйвера будет `NullReferenceException`
- Реестры пусты до завершения `Driver.Init()`

---

## IVideoDriver

Интерфейс драйвера. Наследует `ISystemDriver`.

| Метод | Назначение |
|-------|-----------|
| `SetLinks(List<string>, List<string>)` | Получение ссылок на реестры контроллера |
| `SetScreenMode(string)` | Установка режима экрана |
| `GetScreenMode(): string` | Получение текущего режима |
| `SetResolution(string)` | Установка разрешения |
| `GetResolution(): string` | Получение текущего разрешения |

---

## ScreenMode

Enum режимов экрана. Зеркалит `UnityEngine.FullScreenMode` без платформенной зависимости.

| Значение | Описание |
|----------|----------|
| `ExclusiveFullScreen` | Эксклюзивный полноэкранный (Windows) |
| `FullScreenWindow` | Полноэкранное окно (все платформы) |
| `Windowed` | Оконный режим (десктоп) |
| `MaximizedWindow` | Развёрнутое окно (Windows, macOS) |

---

## Использование

```csharp
// Подписка на готовность
VideoController.OnInit += () =>
{
    var resolutions = VideoController.GetResolutionsList();
    var modes = VideoController.GetScreenModes();
    var current = VideoController.GetResolution();
};

// Установка разрешения
VideoController.SetResolution("1920x1080");

// Установка режима
VideoController.SetScreenMode("FullScreenWindow");
```

---

## Граничные случаи

| Сценарий | Поведение |
|----------|----------|
| Вызов `GetResolution()` до `OnInit` | `NullReferenceException` — драйвер не подключён |
| Драйвер не прошёл whitelist | `SetDriver` возвращает `false`, драйвер не подключается |
| Повторный `SetDriver` с тем же экземпляром | Пропускается (проверка `Driver.Equals`) |
| Повторный `SetDriver` с другим экземпляром | Старый драйвер отключается, новый инициализируется |
