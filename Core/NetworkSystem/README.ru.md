# NetworkSystem (Core)

**Namespace:** `Vortex.Core.NetworkSystem`
**Сборка:** `ru.vortex.core.networksystem`
**Статус:** экспериментальный — API может меняться без сохранения совместимости.

## Назначение

Единая точка HTTP-запросов. Контроллер (`NetworkController`, `SystemController` с драйвером) ведёт:
- очередь и лимит одновременных запросов;
- попытки, повторы, авторизацию;
- итоги и реестр активных запросов.

Драйвер — прокладка к транспорту без логики. Драйверы (UnityWebRequest, мок-имитатор) — отдельный пакет (ТЗ-2).

- Запрос `Query` собирается цепочкой и запирается при запуске.
- Итог — категория, а не исключение. Исключения бросаются только на ошибки программиста и конфигурации (fail-fast).
- Повторы по политике: только транзиентные ошибки, растущая пауза с разбросом, `Retry-After`.
- Тело отправляется через провайдер (по маске URL) и читается по требованию в нужный класс.
- Авторизация по белому списку масок. Токен выдаёт код игры через источник, секретов в настройках нет.
- Окружения (dev/stage/prod) и именованные хосты задаются в настройках.

## Зависимости

- `ru.vortex.system`, `ru.vortex.extensions`, `ru.vortex.logger`, `ru.vortex.app`, `ru.vortex.settings`;
- `ru.vortex.core.masksystem`;
- UniTask.

## Использование

```csharp
// Цепочка
var query = new Query()
    .SetMethod(HttpMethod.Get)
    .SetHost("api", "/v2/banners")          // именованный хост из настроек; или .SetUrl("https://…")
    .AddParam("lang", "ru").AddParam("scale", 1.5f)   // инвариантная культура: scale=1.5
    .OnOutcome(o => Debug.Log(o))
    .Send();

await query.Completion;
query.GetResult();                           // забрать: исключить из реестра (только после завершения, один раз)
if (query.Outcome.Value == QueryOutcome.Success && query.TryRead<BannerConfig>(out var config, out var error))
    Use(config);

// Простые методы: метод — из настроек; повторы только для GET
NetworkController.Send("https://example.com/log", new LogEntry());          // без результата, автоосвобождение
var q = await NetworkController.SendAsync(UrlData.FromHost("api", "/ping"), null);  // ждёт, освобождает, возвращает Query

// Авторизация: ключ указан у провайдера авторизации в настройках
NetworkController.RegisterTokenSource("partnerApi", async (forceNew, ct) => await Auth.GetToken(forceNew, ct));
```

## Модель

```
Query
├── параметры (заперты после Send): метод, UrlData, заголовки, тело, таймаут, повторы, автоосвобождение,
│   скачивание в файл, токен отмены, обработчики OnState / OnOutcome / OnUploadProgress / OnDownloadProgress
├── State    : EnumData<QueryState>    Draft → Queued → Running ⇄ RetryWait → Completed → Released
├── Outcome  : EnumData<QueryOutcome>  None, Success, NotModified, Network, Timeout, ClientError, ServerError,
│                                       TooLarge, BodyError, Cancelled, NetworkDisabled
├── UploadProgress / DownloadProgress : FloatData (неизвестный размер — 0, затем 1)
├── Result   : QueryResult             код, заголовки, тело/файл, длительность, попытки, сообщение
└── TryRead<T>, GetResult, Release, CloneDraft

UrlData     основа (имя хоста | полный URL) + путь (как есть) + параметры (типизированные, enum по имени)
RequestBody ObjectBody | BytesBody | TextBody | FormBody | MultipartBody (текст, байты, файл)
RetryPolicy maxAttempts, baseDelay, multiplier, maxDelay, jitter, respectRetryAfter, retryOn
```

Реактивные поля пишет только контроллер (owner-lock). Каждый обработчик вызывается сразу после изменения своего поля. Порядок при завершении: итог → обработчик итога → `Completed` → обработчик состояния → `Completion`.

## Контракты

- `INetworkSettings` — настройки связи. Только данные; логика (окружение, маски, валидация) — в контроллере.
- `INetworkDriver` — драйвер. Отдаёт настройки, объявляет возможности (свойства `Supports…` и набор `UnsettableHeaders`), создаёт объекты попытки.
- `INetworkAttempt` — объект попытки: `Start`, прогресс, `IsDone`, `GetResult`, `Abort`, `Dispose`. Таймаут, отмену и освобождение ведёт контроллер.
- `IBodyProvider` — объект ⇄ байты, плоская раскладка для GET.
- `IAuthProvider` — заголовки по токену.
- `TokenSource` — выдача токена (`forceNew` после 401).

## Fail-fast и итоги

**Исключение (ошибка программиста или конфигурации):**
- невалидные настройки при инициализации драйвера;
- драйвер не готов;
- неизвестный хост, не http(s), фрагмент `#`, http вне списка;
- неподдерживаемый тип параметра;
- тело у GET/HEAD;
- конфликт Content-Type;
- ошибка сериализации;
- неподдерживаемая возможность драйвера;
- правило авторизации на http-хосте;
- не зарегистрирован источник токена;
- запись в запрос после `Send`;
- `GetResult` до завершения, повторно или при автоосвобождении;
- чтение тела до завершения.

**Итог без обращения к сети:**
- сеть выключена → `NetworkDisabled`;
- тело больше лимита → `TooLarge`;
- нет файла части multipart → `BodyError`.

## Ограничения

- Только главный поток.
- Драйвер назначается один раз при старте, смены в рантайме нет.
- После добавления драйвера в DriverConfig: Reload → выбрать драйвер для `NetworkController` → Save Config.
- Повтор после обновления токена (401) — один, вне счёта политики.
- Логи: подробные — при `DebugSettings → networkLogs`. Значения ключей из `MaskedLogKeys` скрываются, тела обрезаются.
