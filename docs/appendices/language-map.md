# Карта покрытия языка C#

Карта помогает проверять полноту программы. Номер означает запланированное место темы,
а статус показывает наличие учебного текста. **Написано только 01–10**; строка «план» не означает,
что тему уже можно изучить здесь. Сверка состава выполнена с
[официальным справочником](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/)
и [описанием C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14).

| Область | Где изучается | Состояние |
| --- | --- | --- |
| Лексика, литералы, комментарии, имена, scope | 02–03 | Базовый материал написан |
| Числовые типы, bool/char/string, conversions, checked | 03, 06 | Базовый материал написан |
| Операции, precedence, short-circuit, биты и сдвиги | 03 | Введение написано |
| if/switch, циклы, break/continue/return, goto | 04, 17 | 04 написана, углубление в плане |
| Методы, static, overload, local functions, recursion, params | 05, 13, 24 | Основы написаны |
| ref/out/in, ref return, ref readonly, scoped | 05, 23 | Основы написаны, углубление в плане |
| Null, `Nullable<T>`, аннотации и анализ | 06, 13, 21, 24 | Основы написаны |
| Массивы, indices/ranges, collection expressions | 07, 23–25 | Основы написаны |
| class, constructors, fields, properties, accessibility | 08, 11–12, 24 | Основы написаны |
| init, required, readonly, object initializer | 08, 12 | Введение написано |
| Исключения, filters, using и IDisposable | 09, 20, 31 | Основы написаны |
| Наследование, override/new, abstract, sealed, interfaces | 11 | План |
| struct/readonly struct, records, primary constructors, with | 12, 24 | План |
| object, boxing/unboxing, enum/Flags, tuples, equality | 12, 20 | План |
| Generics, constraints, variance, generic math | 13 | План |
| delegate, lambda, closures, events | 14 | План |
| foreach pattern, IEnumerable, yield, iterator disposal | 15 | План |
| LINQ method/query syntax, IQueryable | 16 | План |
| Patterns, extension members, operators, indexers | 17, 24 | План |
| Assemblies, IL, metadata, CLR, JIT/AOT | 01, 19 | Обзор написан, углубление в плане |
| GC, finalization, managed/native lifetime | 20 | План |
| Attributes, reflection, dynamic, anonymous types, expressions | 21 | План |
| namespaces, aliases, global/static using, extern alias, file types | 02, 22 | Основы написаны, углубление в плане |
| partial, preprocessor, XML docs, Roslyn и source generators | 22 | План |
| Span/Memory, ref struct, unsafe, fixed, stackalloc, pointers | 23 | План |
| Function pointers, calling conventions, inline arrays, sizeof | 23, 55 | План |
| C# 13–14 и совместимость версий | 24 | План |
| async/await, awaitables, Task/ValueTask, async streams | 31 | План |
| lock, memory model, volatile, Interlocked | 32 | План |
| Interop, extern methods, marshaling, native ownership | 55 | План |

## Что находится за пределами синтаксиса

Работа с JSON, датами, файлами, HTTP, DI и коллекциями BCL изучается в 25–30.
Проверки, проектирование и инструменты — в 36–39; SQL и persistence — в 40–43;
веб и сервисы — в 44–49; UI, игры и вычисления — в 50–54.
Поставка и эксплуатация — в 55–60.

Таким образом, «знать язык» и «работать в C#-проекте» проверяются отдельно.
Редкие средства, например COM или unsafe, нужны не каждой роли, но для них задан явный маршрут.
Карта охватывает семейства возможностей; точные ограничения грамматики и комбинации модификаторов
проверяются по языковой спецификации и компилятору.

## Как дополнять карту

При добавлении новой главы обновите её статус и проверьте, что тема имеет объяснение, пример,
самостоятельное упражнение и критерий проверки. Если материал только упомянут в списке,
статус остаётся «план». Новая глава обязательно входит в book.json и общий PDF.
