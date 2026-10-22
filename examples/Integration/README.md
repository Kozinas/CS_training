# Примеры BCL, интеграций и импортёра

Главы 25–30 используют отдельную [Integration.slnx](Integration.slnx).
Предыдущая CSharpCourse.slnx остаётся самостоятельной и не требует внешних NuGet-пакетов.

## Подготовка и запуск

Из корня репозитория:

```powershell
Push-Location examples/Integration
try {
    dotnet restore Integration.slnx --locked-mode
    dotnet build Integration.slnx -c Release --no-restore
} finally {
    Pop-Location
}
dotnet run --project examples/Integration/BclLab -- 25
dotnet run --project examples/Integration/BclLab -- 26
dotnet run --project examples/Integration/BclLab -- 27
dotnet run --project examples/Integration/ImportCli -- --help
python tools/verify_integration.py
```

Первый restore требует доступа к nuget.org. При наличии пакетов в локальном кэше обычная сборка
не загружает их повторно. HTTP-проверки используют только loopback; внешнего API для тестов нет.
Проверочный скрипт хранит пакеты в .artifacts/packages и вызывает restore в locked mode.
При сборке из examples/Integration SDK выбирается global.json из examples; скрипт проверки делает так же.
При dotnet run из корня выбор SDK зависит от корневого окружения. Проверенная конфигурация — .NET 10, C# 14.

## Состав

| Проект | Назначение |
| --- | --- |
| InputFormats | DTO, source-generated JSON, Money converter, CsvHelper, XML, ZIP, ограниченные потоки |
| BclLab | Коллекции, generic math, Unicode, Regex, TimeZoneInfo/TimeProvider, resx |
| ImportCli | System.CommandLine, Host/DI/Options, HttpClient/resilience, Serilog, Spectre.Console |
| IntegrationChecks | Контракты форматов, checkpoint, DI и локальный HTTP-сервер |

InputFormats ссылается на прежнюю ExpenseLedger.Core; сама Core остаётся независимой.
IntegrationChecks ссылается на сборки примеров и запускает проверки их публичных типов;
ссылка на exe-проект не выполняет его точку входа.

## Проверенные пакеты

| Прямой пакет | Версия | Назначение |
| --- | --- | --- |
| CsvHelper | 33.1.0 | CSV quoting и логические записи |
| Microsoft.Extensions.Hosting | 10.0.13 | Host, конфигурация, DI и журналирование |
| Microsoft.Extensions.Http.Resilience | 10.11.0 | IHttpClientFactory и Polly resilience |
| System.CommandLine | 2.0.13 | Команды, опции, help и cancellation |
| Serilog.Extensions.Hosting | 10.0.0 | Интеграция ILogger с Serilog |
| Serilog.Sinks.Console | 6.1.1 | Диагностика в stderr |
| Spectre.Console | 0.57.2 | Необязательная таблица отчёта |

Прямые версии записаны в csproj, транзитивные зависимости — в packages.lock.json каждого проекта.
Обновление пакета требует обычного restore с пересмотром lock-файла и повторной проверки;
locked mode намеренно отклоняет несогласованный граф.
NuGet.Config действует в поддереве Integration и не меняет настройки пользовательского ConsoleApp1.

## Данные и ограничения

Готовые данные и договор находятся в [L03](../../labs/03-import-tool/README.md).
Собственные checkpoint/export создавайте в work. Пример не меняет лабораторные входные файлы.
CLI импортирует только JSON; CSV/XML/ZIP — отдельные запускаемые и проверяемые примеры главы 27.

Файловая блокировка защищает от одновременных писателей данного CLI.
Она не заменяет транзакционную БД и не доказывает сохранность после потери питания.
Вход и итоговый checkpoint ограничены по объёму; весь документ обрабатывается в памяти.
Native AOT, публикация службы ОС и TLS к публичному серверу в этой проверке не выполняются.
