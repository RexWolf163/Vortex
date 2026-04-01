# LoaderSystem (Unity)

**Namespace:** `Vortex.Unity.Components.LoaderSystem`
**Сборка:** `ui.vortex.unity.components`

---

## Назначение

Unity-компоненты для запуска и визуализации процесса загрузки приложения.

Возможности:

- Автоматический запуск `Loader.Run()` при наступлении `AppStates.Starting`
- Визуализация прогресса: имя модуля, номер шага, процент выполнения
- Переключение визуальных состояний (Waiting → Loading → Completed)

Вне ответственности:

- Логика загрузки (реализована в `Vortex.Core.LoaderSystem`)
- Регистрация модулей

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.LoaderSystem` | `Loader` — оркестрация загрузки |
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStateChanged`, `AppStates` |
| `Vortex.Core.LocalizationSystem` | `TryTranslate()` — локализация имени модуля |
| `Vortex.Unity.UI` | `UIComponent`, `UIStateSwitcher` |

---

## Компоненты

### LoaderStarter

MonoBehaviour — триггер запуска `Loader.Run()`.

- `OnEnable`: если `AppStates >= Starting` — запускает немедленно, иначе подписывается на `App.OnStarting`
- `OnDisable`: отписка от `App.OnStarting`

Размещается на сцене загрузки. Один экземпляр на сцену.

### LoaderView

MonoBehaviour — визуализация прогресса загрузки.

Три визуальных состояния через `UIStateSwitcher`:

| Состояние | Условие |
|-----------|---------|
| `Waiting` | `AppStates` < `Starting` |
| `Loading` | `AppStates == Starting` |
| `Completed` | `AppStates == Running` |

Обновление текста каждые 0.3 секунды (Coroutine). Формат задаётся паттерном:

```
{0} ({1}) → {2}: {3}%
 ↓    ↓      ↓     ↓
шаг  всего  имя  процент
```

Имя модуля пропускается через `TryTranslate()` для локализации.

#### Настройка в Inspector

| Поле | Тип | Описание |
|------|-----|----------|
| `switcher` | `UIStateSwitcher` | Переключатель визуальных состояний |
| `uiComponent` | `UIComponent` | Текстовый компонент для отображения прогресса |
| `loadingTextPattern` | `string` | Паттерн форматирования (по умолчанию `{0} ({1}) → {2}: {3}%`) |

---

## Использование

1. Создать сцену загрузки
2. Добавить `LoaderStarter` на любой GameObject
3. (Опционально) Добавить `LoaderView` с настроенным `UIStateSwitcher` и `UIComponent`
4. Модули регистрируются через `Loader.Register()` в драйверах до наступления `AppStates.Starting`

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `LoaderStarter` активирован после `AppStates.Starting` | `Loader.Run()` вызывается немедленно |
| `LoaderView` без `switcher` | null-check, переключение пропускается |
| `GetCurrentLoadingData()` возвращает `null` | Текст не обновляется |
| `ProcessData.Size == 0` | Процент = 0 (деление защищено) |
| `App.OnStateChanged` → `Running` | LoaderView отписывается от событий, Coroutine останавливается |
