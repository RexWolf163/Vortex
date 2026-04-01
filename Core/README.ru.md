# Vortex Core

**Namespace:** `Vortex.Core.*`
**Платформа:** чистый C# + UniTask
**Файлов:** ~89 (.cs), 16 систем

---

## Зачем нужен Core

Core — это фундамент Vortex. Всё, что здесь лежит, не знает ни про Unity, ни про конкретный проект. Ни одного `MonoBehaviour`, ни одного `UnityEngine`. Это чистый C#, который описывает **как устроено приложение** — без привязки к тому, где оно запущено.

Идея простая: если завтра понадобится перенести логику на сервер, в консольное приложение или на другой движок — Core переедет без изменений. Всё платформозависимое живёт выше, в слое Unity.

На практике Core решает три задачи:

1. **Определяет контракты.** Интерфейсы `IProcess`, `ISaveable`, `ISystemDriver` — это обещания, которые платформенный слой обязан выполнить. Core говорит «что нужно», Unity отвечает «как это сделать».

2. **Реализует доменно-нейтральную логику.** Шина данных (`Database`), загрузчик (`Loader`), сохранения (`SaveController`), настройки (`Settings`), UI-провайдер (`UIProvider`), аудио (`AudioProvider`), логические цепочки (`LogicChains`), параметрические карты (`ParameterMaps`) — всё это работает на уровне абстракций, без привязки к конкретным ассетам или движку.

3. **Предоставляет базовые примитивы.** `Singleton<T>`, `SystemController<T, TD>`, `ReactiveValue<T>`, `ActionExt` — строительные блоки, из которых собираются системы на всех уровнях.

---

## Про UniTask

В Core есть ровно одна внешняя зависимость, которая выходит за рамки стандартной библиотеки .NET — это **UniTask** (`Cysharp.Threading.Tasks`).

Она используется в четырёх файлах:
- `IProcess.RunAsync()` — контракт асинхронной загрузки
- `ISaveable.GetSaveData()` / `OnLoad()` — контракт сохранения/загрузки
- `Loader.Run()` / `Loading()` — оркестрация загрузки
- `DatabaseExtSave` — сериализация записей с `await UniTask.Yield()` для батчинга

Почему UniTask, а не `System.Threading.Tasks.Task`? Компромисс. UniTask даёт совместимость с WebGL-сборками Unity, где стандартный `Task` не работает корректно из-за однопоточной модели браузера. При этом API практически идентичен: `async UniTask` вместо `async Task`, тот же `CancellationToken`, тот же `await`.

Если проекту не нужен WebGL и требуется строго .NET-зависимый слой — замена механическая: `UniTask` → `Task`, `UniTask<T>` → `Task<T>`, `UniTask.Yield()` → `Task.Yield()`, `UniTask.CompletedTask` → `Task.CompletedTask`. Четыре файла, поиск-замена. Никакой архитектурной перестройки.

---

## Архитектурные принципы

### Шина вместо DI

Vortex не использует DI-контейнеры. Вместо этого — статическая шина данных `Database`, которая хранит `Dictionary<GUID, Record>`. Любая система получает данные через `Database.GetRecord<T>(id)`, а не через конструктор или инъекцию.

Это осознанный выбор: шина проще в отладке (одна точка доступа), не требует конфигурации привязок и естественно ложится на модель Unity, где объекты создаются движком, а не программистом.

### Контроллер владеет логикой

Данные лежат в моделях. Решения принимает контроллер. UI только отображает состояние и сообщает о действиях пользователя — но никогда не меняет данные напрямую.

### SystemController + Singleton

`SystemController<T, TD>` наследует `Singleton<T>` и добавляет контракт драйвера: платформенный слой регистрирует `ISystemDriver`, Core управляет жизненным циклом. `DriversGenericList.WhiteList` валидирует допустимые типы драйверов на этапе регистрации.

### ReactiveValue

`ReactiveValue<T>` — обёртка над значением с событием `OnUpdate`. Специализации `IntData`, `BoolData`, `FloatData`, `StringData` дают implicit-операторы для прозрачного использования (`IntData count = 5;`). Модели строятся из реактивных полей — подписчики получают уведомления без polling.

### IProcess и топологическая загрузка

Каждый модуль реализует `IProcess`: `RunAsync()` для загрузки и `WaitingFor()` для объявления зависимостей. `Loader` автоматически строит порядок загрузки — аналог топологической сортировки. Циклические зависимости детектируются и приводят к `App.Exit()`.

---

## Состав

| Система | Что делает | Ключевые типы |
|---------|-----------|---------------|
| **System** | Базовые абстракции | `Singleton<T>`, `SystemController<T,TD>`, `ReactiveValue<T>`, `IProcess`, `DateTimeTimer` |
| **DatabaseSystem** | Шина данных | `Database`, `Record` |
| **AppSystem** | Жизненный цикл | `App` (static), `AppModel`, `AppStates` |
| **LoaderSystem** | Загрузчик | `Loader` — регистрация, топологическая сортировка, `async Run()` |
| **SaveSystem** | Сохранения | `SaveController`, `ISaveable`, `SaveData`, `SaveFolder` |
| **SettingsSystem** | Настройки | `Settings`, `SettingsModel` (partial, расширяется другими системами) |
| **UIProviderSystem** | Управление UI | `UIProvider`, `UserInterfaceData`, `UserInterfaceCondition` |
| **AudioSystem** | Аудио | `AudioProvider`, `AudioSample`, `MusicSample`, `SoundSample` |
| **LocalizationSystem** | Локализация | `Localization`, `StringExt` |
| **LoggerSystem** | Логирование | `Log`, `LogData`, `LogLevel` |
| **LogicChainsSystem** | Цепочки логики | `LogicChains`, `LogicChain`, `ChainStep`, `Connector` |
| **MappedParametersSystem** | Параметрические карты | `ParameterMaps`, `IMappedModel`, `GenericParameter` |
| **ComplexModelSystem** | Составные модели | `ComplexModel` |
| **DebugSystem** | Отладка | `SettingsModelExtDebug` — partial-расширение `SettingsModel` |
| **Extensions** | Утилиты | `ActionExt`, `SerializeController`, `Crypto`, `ListExt` |

---

## Partial-классы

Крупные системы в Core разнесены по файлам через `partial`:

- `Database` — основной класс + `DatabaseExtSave` (реализация `ISaveable`)
- `UIProvider` — основной класс + `UIProviderExtRegister` + `UIProviderExtEvents`
- `App` — основной класс + `AppExtEvents`
- `SettingsModel` — пустой partial, расширяемый другими системами (`SettingsModelExtDebug`, `SettingsModelExtUnity` в слое Unity)

Это не наследование и не композиция — это разбиение одного класса по тематическому признаку для читаемости.

---

## Границы слоя

Core **не делает**:
- Не загружает файлы с диска и не знает про файловую систему
- Не создаёт GameObjects и не управляет сценами
- Не рисует UI и не проигрывает звук
- Не обращается к сети
- Не зависит от Unity API (кроме UniTask, см. выше)

Core **определяет**:
- Как данные хранятся и извлекаются (Database, Record, GUID)
- Как системы инициализируются и в каком порядке (Loader, IProcess)
- Как состояние сохраняется и восстанавливается (SaveController, ISaveable)
- Как UI решает, что показывать (UIProvider, Conditions)
- Как настройки попадают в систему (Settings, SettingsModel)
- Как компоненты реагируют на изменения (ReactiveValue, ActionExt)

Всё, что требует конкретной платформы — делегируется через интерфейсы драйверов (`ISystemDriver`, `IDriver`) в слой Unity.
