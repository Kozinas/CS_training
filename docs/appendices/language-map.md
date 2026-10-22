# Карта покрытия языка C#

Карта помогает проверять полноту программы. Номер означает запланированное место темы,
а статус показывает наличие учебного текста. **Написаны главы 01–49**; строка «план» не означает,
что тему уже можно изучить здесь. Сверка состава выполнена с
[официальным справочником](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/)
и [описанием C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14).

| Область | Где изучается | Состояние |
| --- | --- | --- |
| Лексика, литералы, комментарии, имена, scope | 02–03 | Базовый материал написан |
| Числовые типы, bool/char/string, conversions, checked | 03, 06 | Базовый материал написан |
| Операции, precedence, short-circuit, биты и сдвиги | 03 | Введение написано |
| if/switch, циклы, break/continue/return, goto | 04, 17 | Написано |
| Методы, static, overload, local functions, recursion, params | 05, 13, 24 | Основы написаны |
| ref/out/in, ref return, ref readonly, scoped | 05, 23 | Написано |
| Null, `Nullable<T>`, аннотации и анализ | 06, 13, 21, 24 | Основы написаны |
| Массивы, indices/ranges, collection expressions | 07, 23–25 | Основы написаны |
| class, constructors, fields, properties, accessibility | 08, 11–12, 24 | Основы написаны |
| init, required, readonly, object initializer | 08, 12 | Введение написано |
| Исключения, filters, using и IDisposable | 09, 20, 31 | Основы написаны |
| Наследование, override/new, abstract, sealed, interfaces | 11 | Написано |
| struct/readonly struct, records, primary constructors, with | 12, 24 | Написано |
| object, boxing/unboxing, enum/Flags, tuples, equality | 12, 20 | Написано |
| Generics, constraints, variance, generic math | 13 | Написано; ref-ограничения углубляются в 23 |
| delegate, lambda, closures, events | 14 | Написано |
| foreach pattern, IEnumerable, yield, iterator disposal | 15, 31 | Написано; есть проверки async-перечисления |
| LINQ method/query syntax, IQueryable | 16 | Написано; перевод в SQL проверен в 42 |
| Patterns, extension members, operators, indexers | 17, 24 | Написано |
| Assemblies, IL, metadata, CLR, JIT/AOT | 01, 19 | Написано; AOT-публикация далее |
| GC, finalization, managed/native lifetime | 20 | Написано; async-disposal и проверки в 31 |
| Attributes, reflection, dynamic, anonymous types, expressions | 21 | Написано |
| namespaces, aliases, global/static using, extern alias, file types | 02, 22 | Написано |
| partial, preprocessor, XML docs, Roslyn и source generators | 22 | Написано; есть учебный хост с проверками |
| Span/Memory, ref struct, unsafe, fixed, stackalloc, pointers | 23 | Написано |
| Function pointers, calling conventions, inline arrays, sizeof | 23, 55 | Основы написаны; полный interop в 55 |
| C# 13–14 и совместимость версий | 24 | Написано; конкурентность Lock проверена в 32 |
| async/await, awaitables, Task/ValueTask, async streams | 31 | Написано и проверено |
| lock, memory model, volatile, Interlocked | 32 | Написано; Lock/Interlocked и гонки проверены |
| Interop, extern methods, marshaling, native ownership | 55 | План |

## Что находится за пределами синтаксиса

Работа с JSON, датами, HTTP, DI и журналами написана в 25–30 и проверяется в L03.
Ограниченная конкурентность, Channels, BackgroundService и измерения написаны в 31–35 и проверяются в L04.
Тестирование, проектирование, Git, CI и NuGet написаны и проверены в 36–39.
SQL, ADO.NET/Dapper, EF Core, миграции и транзакции написаны в 40–43 и проверены в L05.
Веб и сервисы написаны в 44–49 и проверены в L06: API, Identity, Razor, gRPC/SignalR и outbox.
Далее запланированы UI, игры и вычисления в 50–54.
Поставка и эксплуатация — в 55–60.

Таким образом, «знать язык» и «работать в C#-проекте» проверяются отдельно.
Редкие средства, например COM или unsafe, нужны не каждой роли, но для них задан явный маршрут.
Карта охватывает семейства возможностей; точные ограничения грамматики и комбинации модификаторов
проверяются по языковой спецификации и компилятору.

## Как дополнять карту

При добавлении новой главы обновите её статус и проверьте, что тема имеет объяснение, пример,
самостоятельное упражнение и критерий проверки. Если материал только упомянут в списке,
статус остаётся «план». Новая глава обязательно входит в book.json и общий PDF.
