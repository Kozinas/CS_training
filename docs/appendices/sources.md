# Источники и версии

База курса — C# 14 и .NET 10. Дата сверки: 22 октября 2026. Учебные объяснения и задания
написаны самостоятельно; книги используются как дополнительное чтение.
Содержимое папки Books не включается в PDF курса.

## Официальные источники

| Назначение | Источник |
| --- | --- |
| Синтаксис и семантика | [C# language reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/) |
| Нормативная модель и feature specifications | [C# specification](https://learn.microsoft.com/en-us/dotnet/csharp/specification/) |
| Версия языка | [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) |
| Поддержка SDK/runtime | [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| Работа с проектами | [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) |
| Стандартная библиотека | [.NET API browser](https://learn.microsoft.com/en-us/dotnet/api/) |
| Nullable-анализ | [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/null-safety/nullable-reference-types) |
| Сравнение строк | [String best practices](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings) |
| Веб-разработка | [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/) |
| ORM | [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) |
| Объектная модель | [Интерфейсы](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/interface), [records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record) |
| Generic-контракты | [Ограничения типов](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters) |
| Отложенное выполнение | [Yield](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield), [LINQ](https://learn.microsoft.com/en-us/dotnet/standard/linq/deferred-execution-lazy-evaluation) |
| Современный синтаксис | [Patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns), [extension members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods) |

Справочник языка обновляется и может упоминать preview-возможности; в курс их включают только с явной пометкой.
Существование API в документации не подтверждает его доступность в выбранном TFM.
Запускаемые примеры компилируются под net10.0 и LangVersion 14.0.

## Книги в репозитории

| Книга | Файл в Books | Для чего использовать |
| --- | --- | --- |
| Adam Freeman, Pro ASP.NET Core 6, 9th edition, 2022 | `Adam_Freeman_Pro_ASP_NET_Core_6_Develop_Cloud_Ready_Web_Applications.pdf` | HTTP pipeline, MVC, Razor Pages, Blazor; главы 44–51 |
| Xiaodi Yan, Web API Development with ASP.NET Core 8, 2024 | `Xiaodi_Yan_Web_API_Development_with_ASP_NET_Core_8_Learn_techniques.pdf` | Проектирование и проверка API; главы 44–49 |
| Jeffrey Richter, CLR via C#, русское издание | `Мастер_класс_Джеффри_Рихтер_CLR_via_C#_Программирование_на_платформе.pdf` | Типы, CLR, ресурсы и конкурентность; главы 11–24, 31–32 |

В первых двух файлах сверены титульные страницы. У файла Рихтера начальные страницы представлены
без извлекаемого текста; номер издания здесь не утверждается.
Вместо ненадёжных номеров PDF-страниц ищите соответствующую тему в оглавлении своего издания.
Для начала курса книги не обязательны.

В старых книгах встречаются API и настройки эпохи .NET Framework, ASP.NET Core 6 или 8.
Изучайте объяснение задачи, затем проверяйте современный способ по документации.
Например, рекомендации о hosting model, nullable, пакетах и версиях SDK нельзя переносить механически.

## Внешние библиотеки

Для блока 25–30 проверены стабильные версии из nuget.org, приведённые ниже.
Транзитивный граф закреплён в packages.lock.json; воспроизведение использует locked restore.
Примеры 01–24 по-прежнему не используют внешние NuGet-пакеты.
CompilerLab использует Roslyn из выбранного SDK (проверен 10.0.402); это учебный хост, связанный с его версией.

## Инструменты книги

PDF собирается из Markdown с помощью
[markdown-it-py](https://markdown-it-py.readthedocs.io/en/latest/using.html) и
[ReportLab](https://www.reportlab.com/docs/reportlab-userguide.pdf).
Кириллические TrueType-шрифты встраиваются в PDF. Проверка структуры использует pypdf.
Основа структуры и сборщика адаптирована из соседнего курса SystemVerilog пользователя;
учебный текст и примеры здесь относятся к C#.

## Дополнение для глав 19–24

| Тема | Первичный источник |
| --- | --- |
| Сборки и загрузка | [Assemblies](https://learn.microsoft.com/en-us/dotnet/standard/assembly/) |
| Достижимость и поколения | [GC fundamentals](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals) |
| Владение ресурсами | [Dispose](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose) |
| Trimming | [Подготовка библиотек](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming) |
| Генерация исходников | [Roslyn cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) |
| Время жизни буферов | [Memory/Span guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines) |
| История совместимости | [C# versions](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-version-history) |
| Запуск одного исходника | [File-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps) |

В справочнике unsafe уже встречаются правила preview C# 15; примеры курса используют только C# 14.
Поведение проверено компилятором и тестами, а возможность AOT-публикации отдельно не заявляется.

## Источники и пакеты глав 25–30

| Пакет / область | Проверенная версия | Официальный источник |
| --- | --- | --- |
| CsvHelper | 33.1.0 | [Документация](https://joshclose.github.io/CsvHelper/getting-started/) |
| Microsoft.Extensions.Hosting | 10.0.13 | [Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) |
| Microsoft.Extensions.Http.Resilience | 10.11.0 | [HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) |
| System.CommandLine | 2.0.13 | [Обзор API](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) |
| Serilog.Extensions.Hosting | 10.0.0 | [Репозиторий](https://github.com/serilog/serilog-extensions-hosting) |
| Serilog.Sinks.Console | 6.1.1 | [Репозиторий sink](https://github.com/serilog/serilog-sinks-console) |
| Spectre.Console | 0.57.2 | [Table](https://spectreconsole.net/widgets/table/) |
| System.Text.Json | В составе .NET 10 | [Source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) |
| Unicode | В составе .NET 10 | [Character encoding](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction) |
| Generic math | В составе .NET 10 | [Числовые интерфейсы](https://learn.microsoft.com/en-us/dotnet/standard/generics/math) |

Доступность конкретных версий сверена с официальным NuGet flat-container index, затем выполнены restore,
сборка и контрактные проверки. Номера пакетов Host и Resilience имеют разные циклы выпуска.
Полный список прямых зависимостей и команды — в [примерах интеграций](../../examples/Integration/README.md).

## Источники и измерения глав 31–35

| Область | Первичный источник |
| --- | --- |
| Task и композиция | [Async](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/), [WhenAll](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall?view=net-10.0) |
| Контексты продолжений | [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) |
| Синхронизация | [lock](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock), [модель памяти](https://github.com/dotnet/runtime/blob/main/docs/design/specs/Memory-model.md) |
| Очереди и потоки данных | [Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels), [Dataflow](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library) |
| Жизненный цикл | [BackgroundService в .NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/extensions/10.0/backgroundservice-executeasync-task), [scope](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service) |
| BenchmarkDotNet 0.15.8 | [Руководство](https://benchmarkdotnet.org/articles/guides/getting-started.html) |
| dotnet diagnostics 10.0.750501 | [counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters), [trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace), [dump](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump) |

Пакеты закреплены в csproj/lock-файлах; диагностические инструменты устанавливаются локально по точной версии.
BenchmarkDotNet, trace и counters действительно запускались; для dump проверены установка и версия,
снятие/анализ дампа не выполнялись. Числа измерений относятся только к описанной среде и нагрузке.
Сохранённые результаты связаны с [главой 34](../05-concurrency/34-performance.md).

## Источники и пакеты глав 36–39

| Область / пакет | Проверенная версия | Первичный источник |
| --- | --- | --- |
| xunit.v3.mtp-v2 | 4.0.2 | [Начало работы](https://xunit.net/docs/getting-started/v3/getting-started), [MTP](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) |
| Microsoft.Testing.Extensions.CodeCoverage | 18.12.0 | [Coverage](https://xunit.net/docs/getting-started/v3/code-coverage-with-mtp) |
| Microsoft.Testing.Extensions.TrxReport | 2.5.1 | [dotnet test](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test) |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 | [FakeTimeProvider](https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing) |
| NSubstitute | 6.2.0 | [Создание подмен](https://nsubstitute.github.io/help/creating-a-substitute/) |
| Testcontainers.PostgreSql | 4.16.0 | [Модуль PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/) |
| Npgsql | 10.0.3 | [Основы](https://www.npgsql.org/doc/basic-usage.html) |
| NuGet / MSBuild | SDK 10.0.402 | [CPM](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management), [lock](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) |
| Git / версии API | Версия API — учебная 1.1.0 | [Merge](https://git-scm.com/docs/git-merge), [SemVer](https://semver.org/) |
| GitHub Actions | Actions закреплены SHA | [CI для .NET](https://docs.github.com/en/actions/tutorials/build-and-test-code/net) |

Версии проверены через NuGet и locked restore на 22 октября 2026. CPM и пять новых lock-файлов
находятся в [Engineering](../../examples/Engineering/README.md). Для PostgreSQL закреплён digest образа;
контейнерный проект скомпилирован, но выполнение в среде выпуска не проверено из-за отсутствующего daemon.
NUnit/MSTest, [FsCheck](https://fscheck.github.io/FsCheck/) и
[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) даны как альтернативы/углубление,
без заявления об их отдельном запуске. Контролируемая мутация дедлайна выполнена собственным скриптом.

## Источники и пакеты глав 40–43

| Область / пакет | Версия в примере | Первичный источник |
| --- | --- | --- |
| SQLite: SELECT, план и изоляция | Движок из провайдера | [SELECT](https://www.sqlite.org/lang_select.html), [EXPLAIN](https://www.sqlite.org/eqp.html), [isolation](https://www.sqlite.org/isolation.html) |
| Microsoft.Data.Sqlite | 10.0.13 | [Параметры](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/parameters), [async](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async), [backup](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup) |
| Dapper | 2.1.89 | [Официальный репозиторий](https://github.com/DapperLib/Dapper) |
| EF Core Sqlite / Design | 10.0.13 | [DbContext](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/), [запросы](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) |
| dotnet-ef | 10.0.13 | [Миграции](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) |
| EF Core: конкурентность и bulk | 10.0.13 | [Tokens](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [ExecuteUpdate](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), [pagination](https://learn.microsoft.com/en-us/ef/core/querying/pagination) |
| Серверные отличия | Сравнение, без запуска | [Изоляция PostgreSQL](https://www.postgresql.org/docs/current/transaction-iso.html), [SQL Server rowversion](https://learn.microsoft.com/en-us/sql/t-sql/data-types/rowversion-transact-sql) |

Версии NuGet проверены на 22 октября 2026. В [Data](../../examples/Data/README.md) хранятся CPM,
три новых lock-файла и manifest локального dotnet-ef. SQLite-тесты работают с реальными файлами;
они не подтверждают переносимость SQL и поведения на PostgreSQL/SQL Server.
Процент покрытия здесь не измерялся; проверены конкретные сценарии из L05.
