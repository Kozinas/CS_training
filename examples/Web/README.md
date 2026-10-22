# Web — главы 44–49 и ExpenseLedger API

Два новых проекта: **LedgerWeb** (API, Razor Pages, Identity, gRPC, SignalR и worker) и **Web.Tests**
(интеграционные проверки). Solution также включает прежнюю ExpenseLedger.Core. Данные Web хранятся
в отдельной SQLite: файл из Data/L05 не совместим с новой схемой.

SDK **10.0.402**, C# 14, ASP.NET Core/EF Core **10.0.13**, HybridCache **10.11.0**,
gRPC **2.84.0**, OpenTelemetry **1.20.1**, xUnit **4.0.2**.
Прямые версии закреплены в Directory.Packages.props, транзитивные — в двух packages.lock.json.
Локальный dotnet-ef **10.0.13** закреплён отдельно. Проверка актуальности: 22 октября 2026.

## Сборка и проверки

Из корня репозитория:

```powershell
python -B tools/verify_web.py
```

Скрипт делает locked restore, Release build, проверку модели/миграции, 30 xUnit-кейсов и процессные сценарии.
Результаты: .artifacts/web/summary.json, load.json и trace-sample.txt. Нужен Python 3.10+ без внешних пакетов.
Проверки не требуют Docker, Redis, брокера, IdP или внешнего HTTP-сервиса.

Отдельные команды из examples/Web:

```powershell
dotnet restore Web.slnx --locked-mode
dotnet build Web.slnx -c Release --no-restore
dotnet test --project Web.Tests -c Release --no-build
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project LedgerWeb --configuration Release --no-build
```

## Локальный запуск с HTTPS

Следующие команды выполняются из examples/Web. Папка work исключена из Git.
Если сертификат разработки ещё не настроен, один раз выполните `dotnet dev-certs https --trust`
и подтвердите доверие в своей системе. Скрипт автоматической проверки этого не делает.

```powershell
New-Item -ItemType Directory -Force ../../work/web | Out-Null
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Ledger__Path = [IO.Path]::GetFullPath('../../work/web/ledger.db')
$env:Keys__Directory = [IO.Path]::GetFullPath('../../work/web/keys')
$env:Worker__Enabled = 'true'
dotnet run --project LedgerWeb -c Release --no-build -- --migrate
dotnet run --project LedgerWeb -c Release --no-build -- --urls https://localhost:7244
```

Миграция выполняется отдельной командой, повтор безопасен; обычный запуск сервера не меняет схему.
Чтобы сохранить wwwroot/site.css при запуске DLL напрямую, используйте рабочую папку LedgerWeb
или явно задайте content root. `dotnet run --project` устанавливает корень проекта.

Браузер: https://localhost:7244/Login. Документ API: https://localhost:7244/openapi/v1.json.
Razor Pages требуют HTTPS, поскольку cookie и antiforgery cookie имеют Secure.
Ниже пример входа в PowerShell; замените учебный пароль для собственных данных:

```powershell
$base = 'https://localhost:7244'
$credentials = @{ email = 'learner@example.test'; password = 'Course-Only-Passw0rd!' } | ConvertTo-Json
Invoke-RestMethod "$base/auth/register" -Method Post -ContentType application/json -Body $credentials
$login = Invoke-RestMethod "$base/auth/token" -Method Post -ContentType application/json -Body $credentials
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
$batch = Get-Content ../../labs/06-expense-api/data/batch.json -Raw -Encoding UTF8
Invoke-RestMethod "$base/api/v1/imports" -Method Post -Headers $headers -ContentType application/json -Body $batch
Invoke-RestMethod "$base/api/v1/report" -Headers $headers
```

Этот же email/пароль подходит для формы Login. Токен API opaque, **не JWT**; access token живёт 15 минут.
Refresh endpoint не реализован, хотя стандартный ответ содержит refreshToken: после истечения выполните вход снова.
Cookie не принимается API, а Bearer не заменяет browser login. Выход из формы не отзывает уже выданный bearer-токен.

## HTTP-договор

| Метод и путь | Назначение |
| --- | --- |
| POST /auth/register | Создание пользователя, 201 или 400 |
| POST /auth/token | Проверка пароля и выдача bearer, 200 или 401 |
| POST /api/v1/imports | Атомарный пакет с BatchId, 200/400/409/413 |
| GET /api/v1/expenses?after=0&size=20 | Страница своего списка, size 1–100 |
| GET /api/v1/expenses/{id} | Своя запись или 404 |
| PUT /api/v1/expenses/{id} | expectedVersion + amount, 204/400/404/409 |
| GET /api/v1/report | Отчёт по основным данным с кэшем по ревизии |
| GET /api/v1/projection | Отдельно доставленная фоновая сумма |
| GET /api/v1/rate | Необязательная HTTP-зависимость, 200/502/503/504 |
| GET /api/v1/admin | Учебная проверка роли admin, обычному пользователю 403 |
| GET /health/live и /health/ready | Доступность процесса и схемы БД |

API v1 требует bearer. Пример обновления тела: `{"expectedVersion":0,"amount":20.50}`.
Суммы входа — decimal, ответа — amountMinor в целых копейках. Неизвестные поля JSON отклоняются.
Входной Owner не существует; владелец берётся из NameIdentifier.
gRPC Reports.Get и SignalR /events используют ту же bearer-политику.
Пример .NET-клиентов есть в [DeliveryTests](Web.Tests/DeliveryTests.cs).

## Настройки и диагностика

| Переменная | Смысл |
| --- | --- |
| Ledger__Path | Обязательный путь к SQLite-файлу |
| Keys__Directory | Необязательный постоянный key ring; иначе действуют defaults платформы |
| Worker__Enabled | true по умолчанию; false оставляет outbox до следующего запуска worker |
| Rates__BaseUrl | Доверенный URL зависимости; default http://127.0.0.1:5099/ |
| Limits__ApiPermits | Положительный лимит запросов пользователя в минуту, default 1000 |
| Telemetry__Console | true включает консольный экспорт spans/metrics, default false |
| Web__AllowHttp | Только явный локальный HTTP-опыт, default false |

Без сервера на Rates__BaseUrl endpoint /rate вернёт 503, а расходы продолжат работать.
Тело ответа зависимости ограничено 8192 байтами, полное ожидание — двумя секундами.
OpenAPI доступен в Development/Testing; health не раскрывает детали SQLite.
Чтобы посмотреть trace, задайте Telemetry__Console=true перед запуском и вызовите /api/v1/rate.
Сопоставьте ASP.NET request span с HttpClient span; токены и пароли в журнал не записывайте.

## Проверенные границы

WebApplicationFactory использует настоящие Identity, EF/SQLite, Razor, gRPC и SignalR LongPolling.
Process-проверка отдельно запускает Kestrel по loopback HTTP, HTTP stub и перезапускает сервис.
Остановка процесса в этом опыте намеренная; она не доказывает устойчивость к отключению питания.
Load-опыт: 40 проверяемых чтений, concurrency=4, без утверждений о production throughput.

Кэш локальный; доставка outbox вызывает consumer внутри процесса с отдельной транзакцией Inbox.
Redis, RabbitMQ/MassTransit, внешний OIDC, MFA, email confirmation, browser WebSocket/reconnect,
TLS reverse proxy и публичное развёртывание — отдельные расширения. Нет общей квоты диска или числа пользователей;
TTL кэша не ограничивает жёстко память. Подготовка к публикации требует этих решений по требованиям продукта.

[Глава 44](../../docs/08-web/44-http-api.md) · [L06](../../labs/06-expense-api/README.md) · [Все примеры](../README.md)
