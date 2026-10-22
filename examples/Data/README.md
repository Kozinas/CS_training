# Базы данных: SQL, Dapper и EF Core

Примеры глав [40–43](../../docs/07-data/40-sql.md) и [L05](../../labs/05-durable-storage/README.md).
Используется настоящий SQLite в отдельных файлах; отдельный сервер и Docker для этого блока не нужны.
Схему создают EF Core migrations, импорт выполняет Dapper, ADO.NET показывает параметры/reader,
EF Core реализует отчёт и обновление с concurrency token.

## Проекты и зависимости

| Проект | Назначение |
| --- | --- |
| Persistence | Схема, миграции, SqlLedger, EfLedger, online backup |
| DataCli | init/import/page/report/update/backup/integrity/plan |
| Persistence.Tests | 26 xUnit-тестов на временных физических SQLite-файлах |
| ExpenseLedger.Core и InputFormats | Прежняя модель и строгий JSON-формат; подключены ссылками |

SDK 10.0.402 и MTP выбраны в global.json. Версии Microsoft.Data.Sqlite,
Microsoft.EntityFrameworkCore.Sqlite/Design и dotnet-ef — **10.0.13**, Dapper — **2.1.89**,
xunit.v3.mtp-v2 — **4.0.2**, TRX extension — **2.5.1**.
CPM действует только в Data; разрешённые графы закреплены в трёх новых packages.lock.json.
Инструмент dotnet-ef устанавливается локально через [.config/dotnet-tools.json](.config/dotnet-tools.json).

```powershell
Push-Location examples/Data
try {
    dotnet restore Data.slnx --locked-mode
    dotnet build Data.slnx -c Release --no-restore
    dotnet tool restore
    dotnet test --project Persistence.Tests -c Release --no-build
} finally { Pop-Location }
```

Из корня полный проверочный прогон блока: `python tools/verify_data.py`.
Он также генерирует SQL, сравнивает его с сохранённым, применяет к пустой базе,
проверяет CLI, два процесса, резервную копию и восстановление. Отчёты — в .artifacts/data.
SQL-скрипт дополнительно исполняется SQLite из стандартного Python; основные тесты работают
через Microsoft.Data.Sqlite и EF Core. Это два движка SQLite, а не проверка серверного провайдера.

## Учебный запуск

Из корня, после сборки; для повторения с нуля выберите новое имя файла:

```powershell
New-Item -ItemType Directory -Force work | Out-Null
$dataCli = "examples/Data/DataCli/bin/Release/net10.0/DataCli.dll"
$batchId = "10000000-0000-0000-0000-000000000001"
dotnet $dataCli init work/ledger-l05.db
dotnet $dataCli import work/ledger-l05.db $batchId labs/05-durable-storage/data/batch.json
dotnet $dataCli report work/ledger-l05.db
dotnet $dataCli page work/ledger-l05.db 0001-01-01 0 2
dotnet $dataCli page work/ledger-l05.db 2026-01-10 2 2
dotnet $dataCli plan work/ledger-l05.db
dotnet $dataCli backup work/ledger-l05.db work/ledger-l05-backup.db
dotnet $dataCli integrity work/ledger-l05-backup.db
```

Первый импорт возвращает added=3, duplicates=1, replayed=false.
Повтор с тем же BatchId — тот же итог с replayed=true. Report содержит food=2000 и travel=3000
в сотых; page выводит суммы строками с двумя знаками.
Подробные коды ошибок и порядок сценариев находятся в L05.

## Схема и миграции

[InitialCreate](Persistence/Migrations/20261022174905_InitialCreate.cs) создаёт ограничения и индексы.
[AddExpenseNote](Persistence/Migrations/20261022175014_AddExpenseNote.cs) добавляет nullable Note.
[Полный SQL](sql/migrations.sql) предназначен для пустой базы, [запросы](sql/queries.sql) — для изучения SELECT/JOIN/NULL и плана.
Пример не смешивает EnsureCreated и Migrate.

```powershell
# Из examples/Data, после tool restore и Release build.
$efArgs = @("--project", "Persistence", "--startup-project", "DataCli",
    "--configuration", "Release", "--no-build")
dotnet ef migrations has-pending-model-changes @efArgs
dotnet ef migrations script 0 @efArgs
```

При изменении модели создайте новую миграцию в своей копии и обновите SQL, затем проверьте переход
со старыми данными. DesignTimeFactory использует :memory: для построения модели; CLI init
применяет миграции к явно переданному файлу. Для самостоятельного dotnet ef database update
нужно указать реальный --connection; схема в памяти не сохранится после завершения процесса.

## Что проверено и чего нет в образце

Тесты используют UNIQUE/FK/CHECK, rollback, реальную гонку соединений и устаревшие версии EF.
N+1 измеряется DbCommandInterceptor: четыре SELECT превращаются в один.
Backup проверяется отдельной восстановленной базой, включая историю миграций и квитанции.
Наличие SQL-плана с индексом не является benchmark на большой таблице.

Microsoft.Data.Sqlite не даёт настоящего async I/O; синхронный импорт проверяет token между операциями.
Pooling отключён для предсказуемого владения файлами. Между страницами нет общего snapshot,
а запись суммы не меняет поля сортировки Day/Sequence.
PostgreSQL/SQL Server, серверный pooling и контейнерная L05 не запускались.
Никаких миграций во внешнюю БД и CD-развёртывания workflow не выполняет.
