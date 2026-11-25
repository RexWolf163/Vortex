# NetworkSystem (Unity)

**Namespace:** `Vortex.Unity.NetworkSystem`
**Сборки:**
- `ru.vortex.unity.networksystem`;
- `ru.vortex.unity.networksystem.newtonsoft` — собирается только при установленном `com.unity.nuget.newtonsoft-json`.

**Статус:** экспериментальный — API и инспектор могут меняться без сохранения совместимости.

## Назначение

Unity-слой сетевого модуля (ядро — `ru.vortex.core.networksystem`, см. его README).

- `NetworkSettingsAsset` — абстрактный базовый ассет настроек связи. Реализует `INetworkSettings`, содержит общие поля и визуальный сахар. Ассеты драйверов (ТЗ-2) наследуют его и добавляют параметры своего транспорта.
- `JsonUtilityBodyProvider` — провайдер тела по умолчанию.
- `NewtonsoftBodyProvider` — провайдер тела на Newtonsoft.Json: стиль имён, пропуск null.
- `BearerAuthProvider` — авторизация `Authorization: Bearer <токен>`, ключ источника токена.
- `networkLogs` в `DebugSettings` — подробные логи модуля.

## Драйвер UnityWebRequest

- `UwrNetworkDriver` — прокладка ядра к UnityWebRequest, без логики. Регистрируется сам, ядро принимает его, только если он выбран в DriverConfig.
- `UwrNetworkSettings` — ассет драйвера, создаётся автоматически в `Resources/Settings`. К общим полям добавлен лимит редиректов (32).
- **Тело:** байты отправляются из памяти. Если есть файловые части multipart, всё тело склеивается во временный файл `temporaryCachePath/VortexNetwork/` и отправляется потоком. Файл удаляется после попытки, папка очищается при старте.
  - Склейка выполняется синхронно при создании попытки: на очень больших файлах возможен фриз кадра.
- **Встроенный таймаут Unity = 0** — таймаут ведёт ядро.
- **Ошибки:** `ConnectionError` и `DataProcessingError` дают итог `Network`; HTTP-код классифицирует ядро.

| Возможность | Десктоп и мобильные | WebGL |
|---|---|---|
| Скачивание в файл / потоковое тело из файлов | да | нет |
| Прогресс отправки и скачивания | да (скачивание — только при `Content-Length`) | да |
| Лимит редиректов | да | нет (браузер) |
| Неустанавливаемые заголовки | Accept-Charset, Accept-Encoding, Access-Control-Request-*, Connection, Content-Length, Date, Dnt, Expect, Host, Keep-Alive, Origin, Referer, Te, Trailer, Transfer-Encoding, Upgrade, Via, X-Unity-Version | то же + Cookie, Cookie2, User-Agent |

- **WebGL.** Значения для WebGL подтверждаются на тестовой сборке.
- **CORS.** Сервер обязан отдавать заголовки `Access-Control-*`, иначе браузер не пропустит ответ.

Мок-имитатор — отдельная сборка `ru.vortex.unity.networksystem.mock`, см. `Mock/README.ru.md`.

**Переключение драйвера:** DriverConfig → Reload → драйвер для `NetworkController` → Save Config.

## Поля ассета

| Группа | Поля |
|---|---|
| Окружения | список окружений (ключ → хосты «имя → адрес»), активное окружение — выпадающий список ключей |
| Запросы | таймаут попытки (30 с), лимит выполняющихся (4), метод простых запросов (POST), заголовки по умолчанию, политика повторов (3 попытки; 1 с × 2, максимум 30 с; разброс 0.2; `Retry-After`; повторять: Network, Timeout, 408, 429, 500–599) |
| Лимиты | тело запроса (10 МБ), ответ (20 МБ) |
| Безопасность и логи | разрешённые http-хосты (маски), маскируемые ключи (`Authorization`), длина тела в логе (1024) |
| Провайдеры тела | по умолчанию (JsonUtility), исключения «маска → провайдер» |
| Авторизация | белый список «маска → провайдер авторизации» |

- Маски задаются через `[MaskSelector]` (пакет масок): подсветка ошибок и тестовая строка.
- Маски проверяются по адресу `схема://хост[:порт]/путь` без параметров.
- User-Agent собирается автоматически (`Application.productName/version`, платформа). Заголовок по умолчанию `User-Agent` его переопределяет.

## Ограничения

- Пустой провайдер в правиле или по умолчанию, а также отсутствующий тип (удалён Newtonsoft) — fail-fast при инициализации.
- Секреты в ассет не кладутся: токен регистрирует код игры (`NetworkController.RegisterTokenSource`).
- `JsonUtilityBodyProvider`: правила JsonUtility — поля, а не свойства; без словарей и массива верхнего уровня. Для таких DTO нужен `NewtonsoftBodyProvider`.
