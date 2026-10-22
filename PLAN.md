# Программа практикума C# и .NET

Программа ведёт от первой строки кода к самостоятельной разработке и сопровождению приложения.
Базовая среда — **C# 14, .NET 10 LTS**, проверка актуальности: **22 октября 2026**.
[Версия языка](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) и
[поддержка платформы](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) проверяются отдельно.

## Как читать программу

Ниже сначала перечислен весь маршрут, включая темы будущих выпусков. **Наполнены главы 01–10**;
главы 11–60 пока имеют статус **план**. Их наличие здесь не означает готовность учебного текста.
[Оглавление написанных глав](README.md) и [порядок наполнения](PROGRESS.md) показывают фактическое состояние.

Общее ядро: 01–49 и 55–60; прикладная ветка: одна или несколько из 50–54. Знание синтаксиса само по себе
не заменяет SQL, HTTP, тестирование и эксплуатацию: эти темы включены в маршрут. Для первого трудоустройства
нужно глубоко пройти выбранную ветку, а остальные знать на уровне назначения и ограничений.

Оценки времени включают самостоятельное решение и исправления, а не только чтение. Весь маршрут с одной веткой
потребует нескольких сотен часов. При опыте другого языка разрешается проходить входные проверки вместо
повторного чтения; пропускать практику и контрольные работы нельзя. Скорость определяется прохождением проверок.

## Два обязательных способа изучения

1. **Репозиторий как самостоятельный учебник:** тематические каталоги, оглавление, переходы между главами,
   ссылки на задания и запускаемые примеры. Каждую главу можно читать отдельно в Markdown.
2. **Весь учебный материал как один PDF:** все написанные главы, лабораторные и приложения собираются
   в `dist/csharp.pdf` по единому порядку `book.json`. По мере наполнения в эту же сборку войдут все 60 глав.
   PDF содержит оглавление, закладки, номера страниц, кириллицу, читаемый код, таблицы и внутренние ссылки.

Исходник текста один — Markdown в репозитории; отдельная вручную поддерживаемая версия PDF не создаётся.
Команда сборки: `python tools/book.py build`. Глава считается готовой только после включения в оглавление,
проверки ссылок, сборки общего PDF и проверки читаемости её страниц. Лабораторные условия тоже входят в PDF;
исходный код проектов остаётся запускаемым в репозитории. Инструкция — в [README](README.md).

## Состав каждой главы

1. Результат и необходимые предыдущие знания.
2. Объяснение модели, затем синтаксис и небольшой разобранный пример.
3. Типичные ошибки, граничные случаи и способы диагностики.
4. Практика A — воспроизведение; B — самостоятельное решение; C — изменение требований.
5. Проверяемые условия готовности, вопросы и подсказки отдельно от задания.
6. Ссылки на первоисточники и связь со сквозным проектом.

Задачи имеют обозначения вида `03-A`; лабораторные — `L01`. В начале допускаются ручные сценарии;
после главы 36 для нового поведения обязательны автоматические проверки. Готовый пример открывают после
самостоятельной попытки. Ошибочный код всегда явно помечается как упражнение или контрпример.

## I. Первая самостоятельная программа

01–10; вход без опыта программирования; ориентир 30–45 часов.

### 01. C# и .NET, рабочая среда

**Статус:** написано.

1. Язык, компилятор Roslyn, IL, CLR, JIT и библиотеки.
2. SDK и runtime.
3. .NET и .NET Framework.
4. установка SDK и редактора.
5. CLI, csproj, slnx, TFM.
6. build, run, restore, publish.

**Практический результат:** Собрать и запустить приложение из терминала и отладчика.

### 02. Синтаксис и диагностика

**Статус:** написано.

1. Токены, идентификаторы, ключевые слова, литералы и комментарии.
2. выражение и инструкция.
3. блоки и область видимости.
4. top-level statements и Main.
5. using и пространства имён.
6. ошибки компиляции, исключения и логические ошибки.
7. breakpoint, step, watch, call stack.

**Практический результат:** Исправить три разных вида ошибок и объяснить каждый диагноз.

### 03. Типы, числа и операции

**Статус:** написано.

1. bool, char, целые знаковые и беззнаковые типы, nint/nuint, float/double/decimal.
2. литералы и суффиксы.
3. var, const, default.
4. преобразования.
5. checked/unchecked.
6. арифметика, сравнения, логика, биты и сдвиги.
7. приоритет и порядок вычисления.

**Практический результат:** Рассчитать стоимость заказа и проверить округление и границы.

### 04. Ветвления и циклы

**Статус:** написано.

1. if/else.
2. switch statement и expression.
3. for, while, do, foreach.
4. break, continue, return.
5. короткое замыкание.
6. интервалы и инвариант цикла.
7. EOF.
8. трассировка и граничные случаи.
9. область применения goto.

**Практический результат:** Сделать интерактивный сумматор с устойчивым вводом.

### 05. Методы и декомпозиция

**Статус:** написано.

1. Параметры и аргументы.
2. возвращаемые значения и void.
3. static.
4. перегрузки.
5. именованные и необязательные аргументы.
6. params.
7. локальные функции.
8. передача по значению, ref/out/in.
9. рекурсия и стек.
10. чистые вычисления и побочные эффекты.

**Практический результат:** Разделить расчёт, разбор данных и вывод; реализовать Try-метод.

### 06. Строки, null и культура

**Статус:** написано.

1. string и UTF-16.
2. char, Rune и текстовый элемент.
3. неизменяемость.
4. escape, verbatim, raw и interpolation.
5. StringBuilder.
6. сравнение Ordinal и культурное сравнение.
7. Parse/TryParse, CultureInfo.
8. nullable value/reference types, ?. и ??, ??= и !.

**Практический результат:** Определить строгий формат суммы и корректно обработать пустой ввод.

### 07. Массивы и базовые коллекции

**Статус:** написано.

1. Одномерные, прямоугольные и ступенчатые массивы.
2. индексы и диапазоны.
3. копирование и общие ссылки.
4. List, Dictionary, HashSet, Queue, Stack.
5. коллекционные выражения.
6. TryGetValue.
7. сложность O(n) и O(1).
8. перебор и изменение.

**Практический результат:** Сгруппировать расходы по категориям без LINQ.

### 08. Классы и инварианты

**Статус:** написано.

1. class, объект и ссылка.
2. поля, свойства, методы и конструкторы.
3. this.
4. public/private/internal.
5. readonly и static.
6. авто-свойства, get/set/init.
7. object initializer.
8. required.
9. namespace и файл.
10. инкапсуляция и композиция.

**Практический результат:** Создать объект расхода, который нельзя оставить в недопустимом состоянии.

### 09. Ошибки, ресурсы и первые файлы

**Статус:** написано.

1. Exception.
2. try/catch/finally, throw и throw ex.
3. catch filters.
4. Try-паттерн.
5. IDisposable и using.
6. Path, File, StreamReader/Writer, UTF-8.
7. относительные пути и рабочая директория.
8. обработка I/O на границе приложения.

**Практический результат:** Прочитать текстовый файл, сообщить о повреждённой строке и закрыть ресурс.

### 10. Практикум ExpenseLedger CLI

**Статус:** написано.

1. Контракт входного файла.
2. разбор строк.
3. модель предметной области.
4. список расходов и агрегаты.
5. протокол ошибок и exit codes.
6. детерминированный вывод.
7. ручные проверки, автоматические сценарии и ревью.

**Практический результат:** Поставить консольный отчёт по расходам с проверяемыми критериями приёмки.

## II. Система типов и выразительный код

11–18; нужны 01–10; ориентир 35–50 часов.

### 11. Наследование и интерфейсы

**Статус:** план.

1. Базовый класс, base, abstract, virtual/override, sealed, new hiding.
2. полиморфизм.
3. интерфейсы и явная реализация.
4. default interface members.
5. static abstract/virtual members.
6. ковариантный результат.
7. композиция и подстановка.

**Практический результат:** Заменять хранилище расходов через интерфейс без изменения расчёта.

### 12. Структуры, записи и равенство

**Статус:** план.

1. struct, readonly struct, record class/struct.
2. primary constructors.
3. value/reference semantics.
4. boxing/unboxing.
5. with и поверхностное копирование.
6. Equals, GetHashCode, IEquatable, операторы.
7. enum и Flags.
8. ValueTuple и деконструкция.
9. ключи словаря.

**Практический результат:** Создать тип Money, определить равенство и проверить ключи коллекций.

### 13. Обобщения и контракты типов

**Статус:** план.

1. Generic типы и методы.
2. вывод типов.
3. where class/struct/notnull/unmanaged/new/base/interface/default.
4. allows ref struct.
5. variance in/out.
6. generic static state.
7. ограничения и static abstract для generic math.
8. nullable T.
9. обобщения и отражение.

**Практический результат:** Реализовать `Result<T>` и типобезопасный алгоритм без dynamic.

### 14. Делегаты, лямбды и события

**Статус:** план.

1. delegate, Action, Func, Predicate.
2. method group.
3. multicast и исключения.
4. анонимные функции.
5. замыкания и lifetime.
6. static lambdas.
7. event, подписка и отписка.
8. `EventHandler<T>`.
9. утечки через события.
10. async void в обработчиках.

**Практический результат:** Создать уведомления об изменениях и доказать освобождение подписчика.

### 15. Перечисление и итераторы

**Статус:** план.

1. IEnumerable/IEnumerator и generic варианты.
2. foreach pattern.
3. yield return/break.
4. отложенное выполнение.
5. повторное перечисление.
6. освобождение итератора.
7. lazy и eager.
8. бесконечные последовательности.
9. основы IAsyncEnumerable.

**Практический результат:** Сделать потоковый импорт большого файла без загрузки всего содержимого.

### 16. LINQ в памяти и в запросах

**Статус:** план.

1. Where/Select/SelectMany.
2. OrderBy/ThenBy.
3. GroupBy, Join/GroupJoin.
4. Any/All, First/Single и Default.
5. Aggregate, Count, Sum.
6. DistinctBy, Chunk.
7. query syntax.
8. materialization.
9. IEnumerable против IQueryable.
10. expression tree и перевод запроса.

**Практический результат:** Написать отчёты и найти ошибку из-за повторного перечисления.

### 17. Шаблоны и расширение синтаксиса

**Статус:** план.

1. is/as и pattern matching.
2. type, constant, property, positional, relational, logical, list и slice patterns.
3. guards и исчерпываемость.
4. extension methods/members.
5. перегрузка операторов и преобразований.
6. индексаторы.
7. deconstruct.
8. target-typed new.

**Практический результат:** Реализовать классификацию операций без хрупкой цепочки if.

### 18. Практикум библиотеки предметной области

**Статус:** план.

1. Разделение CLI и class library.
2. агрегаты и правила.
3. интерфейсы на границах.
4. равенство.
5. коллекции только для чтения.
6. документирование API.
7. совместимость.
8. ошибки как результат и исключения.

**Практический результат:** Вынести ExpenseLedger.Core и подтвердить независимость от UI и диска.

## III. Полная карта языка и CLR

19–24; нужны 11–18; ориентир 25–40 часов.

### 19. Сборки и модель исполнения

**Статус:** план.

1. PE, IL и metadata.
2. тип, assembly, module, namespace.
3. CTS/CLS.
4. загрузка типов и статическая инициализация.
5. JIT, tiered compilation, ReadyToRun, Native AOT.
6. public/internal и InternalsVisibleTo.
7. сборки и версии.

**Практический результат:** Проследить путь вызова от исходника до IL и машинного кода.

### 20. Память и время жизни

**Статус:** план.

1. Стек вызовов, managed heap и ссылки.
2. копирование значений.
3. GC roots, поколения, LOH и pinning.
4. finalizer и Dispose pattern.
5. WeakReference, SafeHandle.
6. IDisposable/IAsyncDisposable.
7. managed leaks.
8. владение ресурсом.

**Практический результат:** Найти удерживаемый объект и исправить время жизни ресурса.

### 21. Атрибуты, reflection и dynamic

**Статус:** план.

1. Атрибуты и targets.
2. typeof/GetType/nameof.
3. metadata inspection.
4. Activator, generics и reflection.
5. dynamic и runtime binder.
6. anonymous types.
7. expression trees.
8. trimming annotations.
9. рефлексия при AOT.
10. CallerMemberName/CallerArgumentExpression.

**Практический результат:** Сделать валидатор по метаданным и описать ограничения AOT.

### 22. Структура исходников и инструменты компилятора

**Статус:** план.

1. file-scoped namespaces и file-local types.
2. global/static/alias using и extern alias.
3. partial типы, методы, свойства, события, конструкторы.
4. #if/#define/#nullable/#pragma/#line/#error/#warning/#region.
5. XML docs.
6. analyzers, Roslyn syntax/semantic model, incremental source generators.

**Практический результат:** Добавить анализатор правила и генератор кода с тестами.

### 23. Ссылки, span и unsafe

**Статус:** план.

1. ref locals/returns, ref readonly, scoped.
2. ref struct и escape analysis.
3. Span/ReadOnlySpan, Memory/ReadOnlyMemory.
4. stackalloc, ArrayPool и ownership.
5. pointers, fixed, sizeof, function pointers, unmanaged calling conventions.
6. inline arrays.
7. MemoryMarshal и границы безопасности.

**Практический результат:** Сравнить выделения памяти в двух парсерах и проверить корректность.

### 24. Версии языка и чтение старого кода

**Статус:** план.

1. История возможностей C# 1–14.
2. LangVersion, TFM и SDK.
3. nullable migration.
4. records, required, collection expressions и primary constructors.
5. C# 13 params collections, Lock, ref struct interfaces.
6. C# 14 field, extension blocks, null-conditional assignment, unbound nameof, span conversions, lambda modifiers, partial members, compound operators.
7. file-based apps.
8. stable против preview.

**Практический результат:** Перенести старый фрагмент на современный стиль, сохранив поведение.

## IV. Библиотеки .NET и внешние данные

25–30; нужны 11–18, обзор 19–20; ориентир 25–40 часов.

### 25. Коллекции и алгоритмы BCL

**Статус:** план.

1. ICollection/IList/IReadOnlyList.
2. Dictionary, SortedDictionary, SortedSet, PriorityQueue.
3. Immutable и Frozen.
4. Concurrent collections.
5. comparer и equality comparer.
6. поиск, сортировка, двоичный поиск.
7. capacity.
8. BigInteger и generic math.

**Практический результат:** Выбрать структуры для очереди работ и доказать порядок обработки.

### 26. Текст, время и регулярные выражения

**Статус:** план.

1. Encoding и Unicode normalization.
2. Rune/StringInfo.
3. Regex, timeout, NonBacktracking и GeneratedRegex.
4. DateTime/DateTimeOffset/DateOnly/TimeOnly/TimeSpan.
5. TimeZoneInfo и DST.
6. TimeProvider.
7. CultureInfo.
8. ресурсы локализации.
9. Guid, Random и криптографический RNG.

**Практический результат:** Импортировать даты с часовыми поясами и протестировать переход времени.

### 27. Файлы, потоки и сериализация

**Статус:** план.

1. File/Directory/Path, Stream, BufferedStream, async I/O.
2. JSON и System.Text.Json, converters и source generation.
3. XML и LINQ to XML.
4. CSV и CsvHelper.
5. ZIP.
6. format versions, размер входа, повреждение данных, замена файла.
7. запрет небезопасной десериализации.

**Практический результат:** Сделать версионируемый импорт/экспорт с проверкой испорченных данных.

### 28. HTTP и сетевые клиенты

**Статус:** план.

1. HTTP методы, статусы и заголовки.
2. HttpClient/handler lifetime.
3. IHttpClientFactory.
4. JSON, streams, timeout/cancellation.
5. retries/backoff/jitter и идемпотентность.
6. Microsoft.Extensions.Http.Resilience и Polly.
7. sockets, DNS, TLS, WebSocket обзор.

**Практический результат:** Написать клиент с отменой, ограниченным повтором и тестовым сервером.

### 29. Host, DI, настройки и журналы

**Статус:** план.

1. Generic Host.
2. dependency injection.
3. transient/scoped/singleton.
4. captive dependency.
5. Options и validation.
6. appsettings, environment variables и user secrets.
7. ILogger, structured logging и Serilog.
8. scope.
9. startup/shutdown.

**Практический результат:** Запустить импорт как службу с корректным завершением и диагностикой.

### 30. Практикум утилиты импорта

**Статус:** план.

1. Аргументы CLI и System.CommandLine.
2. Spectre.Console как необязательный UI.
3. конфигурация.
4. файлы и HTTP.
5. checkpoint.
6. обработка частично ошибочного набора.
7. отчёт об импорте.
8. повторный запуск.

**Практический результат:** Поставить воспроизводимый импортёр с отдельными ошибками каждой записи.

## V. Асинхронность, потоки и производительность

31–35; нужны 14–15, 20 и 27–29; ориентир 30–45 часов.

### 31. Task и async await

**Статус:** план.

1. I/O-bound и CPU-bound.
2. `Task`/`Task<T>`, state machine, awaitable pattern.
3. исключения до/после await.
4. WhenAll/WhenAny.
5. CancellationTokenSource и deadline.
6. async all the way.
7. SynchronizationContext, ConfigureAwait.
8. deadlock .Result.
9. ValueTask.
10. async streams и await using.

**Практический результат:** Скачать набор данных с отменой и сохранением всех ошибок.

### 32. Потоки и синхронизация

**Статус:** план.

1. Thread/ThreadPool.
2. shared mutable state.
3. race и memory visibility.
4. lock и System.Threading.Lock.
5. Monitor, Interlocked, Volatile, SemaphoreSlim, Mutex.
6. deadlock и порядок захвата.
7. thread safety и reentrancy.
8. AsyncLocal.

**Практический результат:** Воспроизвести потерю обновления и исправить её с объяснением.

### 33. Параллельные конвейеры

**Статус:** план.

1. Parallel.ForEachAsync и PLINQ.
2. bounded concurrency.
3. Channels и backpressure.
4. producer/consumer.
5. TPL Dataflow обзор.
6. cancellation propagation.
7. completion и graceful shutdown.
8. IAsyncEnumerable.
9. периодические задачи.

**Практический результат:** Создать ограниченный конвейер импорта и доказать отсутствие потери записей.

### 34. Измерение и оптимизация

**Статус:** план.

1. Release и warmup.
2. BenchmarkDotNet.
3. время, allocations, throughput и percentiles.
4. dotnet-counters/trace/dump.
5. GC и threadpool starvation.
6. boxing, closure, LINQ hot path.
7. pools, spans, SIMD обзор.
8. измерение до/после.

**Практический результат:** Найти узкое место по профилю и подтвердить улучшение без изменения результата.

### 35. Практикум фонового обработчика

**Статус:** план.

1. BackgroundService.
2. конкурентные задания.
3. очередь.
4. лимиты.
5. отмена.
6. retries и poison message.
7. поведение при падении.
8. нагрузочный эксперимент.
9. диагностика.

**Практический результат:** Обработать поток операций с лимитом памяти и управляемым завершением.

## VI. Инженерная работа и качество

36–39; нужны 18 и 31; ориентир 25–40 часов.

### 36. Автоматические тесты

**Статус:** план.

1. xUnit как основной пример.
2. NUnit/MSTest как альтернативы.
3. Arrange/Act/Assert.
4. parameterized tests.
5. unit/integration/contract/e2e.
6. fakes и NSubstitute.
7. FsCheck обзор.
8. coverage и mutation testing.
9. тестирование времени и async.
10. Testcontainers.

**Практический результат:** Проверить бизнес-правила, сбои сети и реальные ограничения БД.

### 37. Проектирование и изменение кода

**Статус:** план.

1. Cohesion/coupling, SOLID с контрпримерами.
2. composition.
3. DI boundaries.
4. layered architecture и vertical slices.
5. Strategy, Factory, Decorator, Adapter, Observer.
6. CQRS по необходимости.
7. refactoring.
8. API contracts, errors, immutability.
9. code review.

**Практический результат:** Изменить бизнес-правило без дублирования и большого каскада изменений.

### 38. Git, сборка и NuGet

**Статус:** план.

1. Branch, commit, diff, merge и PR.
2. csproj/MSBuild/Directory.Build.props.
3. NuGet и central package management.
4. lock file, restore.
5. transitive dependencies и аудит.
6. analyzers/.editorconfig/dotnet format.
7. SDK pinning.
8. CI.
9. pack и SemVer.

**Практический результат:** Собрать библиотеку и проверить её потребление из другого проекта.

### 39. Практикум сопровождения

**Статус:** план.

1. Воспроизведение бага.
2. минимальный failing test.
3. отладчик и логи.
4. совместимость.
5. миграция API.
6. README и ADR.
7. review checklist.
8. измеримое определение готовности.

**Практический результат:** Принять чужой код, исправить дефект и подготовить проверяемое изменение.

## VII. Данные и базы данных

40–43; нужны 16, 27, 31 и 36; ориентир 25–40 часов.

### 40. SQL и реляционная модель

**Статус:** план.

1. Таблицы, ключи, constraints, NULL и трёхзначная логика.
2. SELECT/JOIN/GROUP BY.
3. индексы и query plan.
4. normalization.
5. transactions, isolation и concurrency.
6. SQLite и PostgreSQL/SQL Server.
7. migrations.

**Практический результат:** Спроектировать схему расходов и обосновать индексы.

### 41. ADO.NET и Dapper

**Статус:** план.

1. Connection/command/reader и pooling.
2. parameters.
3. transactions.
4. async и cancellation.
5. mapping.
6. Dapper Query/Execute.
7. SQL injection.
8. streaming vs buffering.
9. provider-specific types.

**Практический результат:** Сделать параметризованный репозиторий и проверить rollback.

### 42. Entity Framework Core

**Статус:** план.

1. DbContext lifetime.
2. entities и DTO.
3. mapping, navigation и relationships.
4. tracking/AsNoTracking.
5. projection.
6. eager/explicit/lazy loading и N+1.
7. LINQ translation.
8. migrations.
9. concurrency tokens.
10. transactions и bulk operations.

**Практический результат:** Заменить файловое хранилище БД и устранить N+1.

### 43. Практикум надёжного хранения

**Статус:** план.

1. Unique constraints.
2. optimistic concurrency.
3. keyset pagination.
4. batch import.
5. migrations в CI и развертывании.
6. Testcontainers.
7. backup/restore.
8. query logging.
9. идемпотентность.

**Практический результат:** Доказать отсутствие дублей и потери обновлений при конкурирующих запросах.

## VIII. ASP.NET Core и сервисы

44–49; нужны 28–29, 31, 36 и 40–43; ориентир 40–65 часов.

### 44. HTTP API на ASP.NET Core

**Статус:** план.

1. Kestrel и request pipeline.
2. middleware order.
3. routing.
4. minimal APIs и controllers.
5. binding/validation.
6. DTO.
7. ProblemDetails.
8. status codes.
9. OpenAPI.
10. pagination и versioning.

**Практический результат:** Опубликовать локальный API с проверяемым HTTP-контрактом.

### 45. MVC, Razor Pages и браузер

**Статус:** план.

1. MVC и server rendering.
2. Razor syntax.
3. forms, model binding и validation.
4. layout/partials.
5. cookies, sessions, static assets.
6. HTML/CSS/JS минимум.
7. antiforgery и XSS.
8. различие API и браузерного приложения.

**Практический результат:** Создать страницу списка и форму редактирования с обработкой ошибок.

### 46. Идентификация и защита API

**Статус:** план.

1. Authentication/authorization.
2. Identity.
3. cookies, bearer, OAuth2/OIDC.
4. claims, roles, policies и ownership.
5. password hashing через стандартные средства.
6. CORS/CSRF/XSS.
7. HTTPS.
8. rate limiting.
9. секреты и журналирование.
10. threat modeling.

**Практический результат:** Добавить пользователей и доказать изоляцию их данных негативными тестами.

### 47. Кэш, фоновые задачи и наблюдаемость

**Статус:** план.

1. IMemoryCache/HybridCache, distributed cache и Redis.
2. invalidation и stampede.
3. BackgroundService, Quartz/Hangfire обзор.
4. health checks.
5. structured logs.
6. metrics/tracing и OpenTelemetry.
7. correlation.
8. service configuration.

**Практический результат:** Добавить кэш с корректной инвалидацией и найти медленный запрос по trace.

### 48. Связь между сервисами

**Статус:** план.

1. gRPC и Protobuf.
2. SignalR.
3. очереди и RabbitMQ/MassTransit обзор.
4. delivery semantics.
5. outbox/inbox.
6. duplicate handling.
7. retry storms.
8. idempotency key.
9. eventual consistency.
10. modular monolith и границы микросервисов.

**Практический результат:** Обработать повторно доставленное сообщение без повторного эффекта.

### 49. Практикум ExpenseLedger API

**Статус:** план.

1. API, БД, identity, импорт и фоновые задачи.
2. integration tests через WebApplicationFactory.
3. OpenAPI.
4. observability.
5. load test.
6. отказ внешнего сервиса.
7. инструкции запуска.

**Практический результат:** Передать сервис другому разработчику с тестами и сценарием восстановления.

## IX. Прикладные специализации

50–54; выбирается одна ветка после общего ядра; ориентир 25–60 часов на ветку.

### 50. Windows desktop и кроссплатформенный desktop

**Статус:** план.

1. WinForms и WPF.
2. XAML, data binding, commands, INotifyPropertyChanged.
3. MVVM и CommunityToolkit.Mvvm.
4. dispatcher/UI thread.
5. async commands.
6. Avalonia обзор.
7. installer и updates.
8. тестируемая view model.

**Практический результат:** Создать desktop-клиент API с отменой запроса и состояниями загрузки.

### 51. Blazor

**Статус:** план.

1. Components, parameters, lifecycle, binding.
2. routing и forms.
3. render modes, server/WebAssembly.
4. DI scopes.
5. JS interop.
6. authentication state.
7. API client.
8. bUnit.
9. UI loading/error states.

**Практический результат:** Создать веб-клиент с компонентами и тестами пользовательских состояний.

### 52. Мобильные приложения и .NET MAUI

**Статус:** план.

1. Workloads и ограничения платформ.
2. XAML/MVVM.
3. navigation, permissions, lifecycle.
4. offline storage.
5. connectivity.
6. async UI.
7. packaging и signing.
8. платформенные интеграции.

**Практический результат:** Сделать мобильный журнал с локальной очередью синхронизации.

### 53. C# в игровых движках

**Статус:** план.

1. Unity и Godot .NET как разные среды.
2. доступные версии языка/runtime.
3. сцены, компоненты и lifecycle.
4. update loop и delta time.
5. события.
6. coroutine против Task.
7. allocations/GC на кадр.
8. serialization.
9. тестируемая игровая логика.

**Практический результат:** Реализовать инвентарь и сохранение без привязки правил к сцене.

### 54. Вычисления и обработка данных

**Статус:** план.

1. System.Numerics, vectors и SIMD.
2. MathNet.Numerics.
3. ML.NET и ONNX Runtime обзор.
4. tensors и batching.
5. CSV/JSON pipelines.
6. точность и воспроизводимость.
7. границы C# и Python.

**Практический результат:** Построить воспроизводимый анализ расходов и измерить скорость обработки.

## X. Интеграция, поставка и итоговая работа

55–60; нужны общее ядро и выбранная ветка; ориентир 30–50 часов.

### 55. Interop и специальные среды

**Статус:** план.

1. P/Invoke и LibraryImport.
2. marshaling, ABI, calling convention.
3. SafeHandle и native ownership.
4. COM и Windows APIs.
5. Native AOT и trimming.
6. plugins/AssemblyLoadContext и unload.
7. process boundary.
8. sandbox misconceptions.

**Практический результат:** Вызвать нативную функцию и корректно освободить принадлежащий ей ресурс.

### 56. Существующие проекты на .NET Framework

**Статус:** план.

1. Framework против современного .NET.
2. packages.config, старый csproj, binding redirects и GAC.
3. AppDomain.
4. WCF, Web Forms, EF6 и ASP.NET MVC5 обзор.
5. Windows-only зависимости.
6. .NET Standard.
7. постепенная миграция и characterization tests.

**Практический результат:** Составить и проверить план миграции отдельной библиотеки.

### 57. Публикация и CI/CD

**Статус:** план.

1. Framework-dependent/self-contained.
2. RID.
3. single file, trimming/AOT.
4. Docker multi-stage.
5. Linux и Windows differences.
6. environment config и secrets.
7. CI test/build/publish.
8. migration strategy.
9. health/readiness.
10. rollback.

**Практический результат:** Развернуть приложение в чистом окружении по одной инструкции.

### 58. Эксплуатация и разбор сбоев

**Статус:** план.

1. SLO и пользовательские симптомы.
2. logs/metrics/traces.
3. CPU, memory, dump и threadpool.
4. timeout/cancellation cascade.
5. graceful shutdown.
6. backup/restore drill.
7. dependency outages.
8. incident report и исправляющий тест.

**Практический результат:** Воспроизвести сбой, локализовать причину и восстановить работу.

### 59. Итоговый проект

**Статус:** план.

1. Требования, архитектурное решение и ADR.
2. API или UI выбранной ветки.
3. БД.
4. импорт/интеграция.
5. auth.
6. фоновые задачи.
7. CI.
8. метрики.
9. тесты.
10. документация.
11. нагрузка.
12. review и передача.

**Практический результат:** Сдать ExpenseLedger v3 или равный по сложности проект по рубрике.

### 60. Проверка знаний и дальнейший маршрут

**Статус:** план.

1. Карточки по всему языку.
2. чтение незнакомого кода.
3. объяснение времени жизни и асинхронности.
4. поиск по официальной документации.
5. диагностика багов.
6. оценка компромиссов.
7. собеседование и разбор пробелов.
8. план углубления.

**Практический результат:** Защитить решения и самостоятельно выполнить изменение требований.

## Сквозной проект и контрольные точки

| Этап | Главы | Что предъявить |
| --- | --- | --- |
| L01 | 01–10 | CLI читает файл, считает отчёт и корректно отклоняет ошибочные данные |
| L02 | 11–18 | Библиотека предметной области с независимыми правилами и контрактами |
| L03 | 19–30 | Импорт/экспорт, настройки, HTTP-клиент и управляемые ресурсы |
| L04 | 31–35 | Ограниченный асинхронный конвейер с отменой и измерениями |
| L05 | 36–43 | Тесты и настоящее хранилище с транзакциями и контролем конкуренции |
| L06 | 44–49 | API с изоляцией пользователей, интеграционными тестами и диагностикой |
| L07 | 50–58 | Клиент выбранной ветки и воспроизводимая поставка |
| L08 | 59–60 | Самостоятельный проект, разбор сбоя и защита изменения требований |

Каждая следующая работа сохраняет уже реализованные правила и добавляет новую ответственность.
Сложность растёт от отдельной функции к работе с состоянием, затем к I/O, конкуренции и отказам внешних систем.

## Карта библиотек

Это рабочий набор для курса, а не рейтинг. Сначала изучается стандартный API и задача, затем сторонний инструмент.
Версию каждого внешнего пакета нужно фиксировать при написании и проверке соответствующей главы.

| Задача | Основной инструмент | Сопоставление или дополнительный обзор | Главы |
| --- | --- | --- | --- |
| Данные в памяти | BCL Collections, LINQ | Immutable, Frozen | 07, 15–16, 25 |
| Форматы | System.Text.Json, XML | CsvHelper | 27 |
| HTTP и устойчивость | HttpClient, Microsoft.Extensions.Http.Resilience | Polly | 28 |
| Сборка приложения | Microsoft.Extensions.Hosting/DependencyInjection/Options/Logging | Serilog | 29 |
| CLI | System.CommandLine | Spectre.Console | 30 |
| Конкуренция | Task, Channels | TPL Dataflow | 31–35 |
| Тесты | xUnit, WebApplicationFactory | NUnit, MSTest, NSubstitute, FsCheck, Testcontainers | 36, 43, 49 |
| Измерения | BenchmarkDotNet, dotnet diagnostics | OpenTelemetry | 34, 47, 58 |
| Доступ к БД | EF Core, ADO.NET | Dapper | 40–43 |
| Веб | ASP.NET Core, Identity, OpenAPI | FluentValidation как вариант валидации | 44–49 |
| Интеграция | gRPC, SignalR | Redis, RabbitMQ, MassTransit, Quartz, Hangfire | 47–48 |
| UI | WPF/WinForms, Blazor, MAUI | Avalonia, CommunityToolkit.Mvvm, bUnit | 50–52 |
| Игры | Unity или Godot .NET | API конкретного движка | 53 |
| Вычисления | System.Numerics | MathNet.Numerics, ML.NET, ONNX Runtime | 54 |

## Критерии завершения всего курса

- Учащийся объясняет семантику значений и ссылок, null, обобщений, LINQ, исключений, async и освобождения ресурсов.
- Пишет приложение с внешними данными и выбранным UI/API, самостоятельно находит нужные API в документации.
- Проверяет корректность, границы, отмену, конкуренцию и отказ зависимостей; умеет читать чужие тесты.
- Пользуется Git, отладчиком, профилировщиком, пакетами и CI; воспроизводит сборку на чистой машине.
- Обосновывает модель БД, границы ответственности и решения по безопасности и производительности.
- Передаёт работоспособный проект с инструкцией, тестами, диагностикой и описанием ограничений.

Полнота языка контролируется [картой покрытия](docs/appendices/language-map.md).
Работа во всех существующих C#-средах требует знания их собственных API; курс даёт общее ядро и маршруты специализации.
