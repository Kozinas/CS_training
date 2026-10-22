# 24. Версии языка и чтение старого кода

**Результат:** вы читаете C# разных поколений, выбираете поддерживаемую конфигурацию
и обновляете код с сохранением контракта. Нужны главы 01–23.
Проверяемые новшества находятся в [Modern.cs](../../examples/RuntimeLab/Modern.cs).

## Три независимых настройки

SDK — набор инструментов, выбранный dotnet и global.json.
LangVersion ограничивает язык компилятора, TFM (например net10.0) выбирает контракт платформенных API.
Установка нового SDK не добавляет API новой платформы проекту с прежним TFM.
Некоторые языковые возможности требуют поддержки runtime или библиотечных типов.

В examples закреплены C# 14 и net10.0; global.json допускает обновление стабильного SDK 10.0
в пределах выбранной политики rollForward. Для воспроизведения используйте команды из examples
либо tools/verify.py, который выбирает этот рабочий каталог.
Значения latest/preview затрудняют повторение чужой сборки: доступный синтаксис зависит от SDK.

Книги по .NET Framework остаются полезны для базовых принципов, но старые AppDomain, GAC,
конфигурация приложений и веб-стек имеют другие ограничения. Не заменяйте названия механически.

## Карта поколений

Таблица — ориентиры для чтения, а не полный перечень каждого изменения.

| Версия C# | Что часто встретится в проекте |
| --- | --- |
| 1 | Классы, интерфейсы, события, делегаты; негeneric-коллекции |
| 2 | Generics, nullable value types, yield, partial types |
| 3 | LINQ, лямбды, var, anonymous types, extension methods |
| 4 | dynamic, optional/named arguments, generic variance |
| 5 | async/await, caller information |
| 6 | nameof, интерполяция, null-conditional доступ, exception filters |
| 7.x | Tuples, deconstruction, patterns, ref returns, readonly/ref struct |
| 8 | Nullable references, async streams, switch expressions, ranges |
| 9 | Records, init, top-level statements, расширенные patterns |
| 10 | Record structs, global using, file-scoped namespaces |
| 11 | Required, raw strings, list patterns, generic math, file types |
| 12 | Collection expressions, primary constructors, inline arrays |
| 13 | Params collections, ref struct interfaces, partial properties |
| 14 | Field-backed properties, extension blocks, partial events/constructors |

Проверяйте точные ограничения нужной версии по
[истории языка](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-version-history).
Семантика уже знакомых конструкций важнее запоминания годов выпуска.

## Миграция без скрытого изменения поведения

Сначала зафиксируйте входы, результаты, исключения, порядок, побочные эффекты и владение ресурсами.
Потом изменяйте одну причину за раз: SDK/TFM, nullable-аннотации, синтаксис, модель данных.

Для nullable-модернизации включите анализ на границе проекта или файла.
Аннотируйте допустимый null, исправьте пути инициализации и проверьте входы.
`!` подавляет предупреждение, но не проверяет объект во время работы.
`required` заставляет вызывающий код инициализировать член в предусмотренных языком сценариях;
оно само не валидирует пустую строку и не защищает от произвольной reflection-десериализации.

Замена class на record меняет равенство; замена цикла на LINQ может отложить выполнение;
замена массива на collection expression требует подходящего целевого типа.
Primary constructor обычного класса не создаёт публичные свойства автоматически.
Сохранение поведения надо доказать тестами, а не короткой записью.

## Что добавляет C# 13

`params ReadOnlySpan<int>` позволяет вызывать SumParams(2, 3, 4) без обязательного массива в API.
Конкретные выделения зависят от формы вызова. Ref struct может реализовывать интерфейс,
но не может быть упакован; generic-параметр должен разрешать `allows ref struct`.
Partial properties помогают генераторам разделять контракт и реализацию.

`lock` со статическим типом System.Threading.Lock использует его специальный протокол.
Преобразование к object меняет выбор механизма, поэтому тип объекта синхронизации важен.
Порядок захвата блокировок и взаимные блокировки изучаются в главе 32.
Другие изменения включают escape-последовательность `\e`, улучшения вывода method group
и правила выбора overload.
[Изменения C# 13](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13).

## C# 14 на запускаемых примерах

Свойство Text в Label использует синтезируемое поле, сохраняя проверку:

```csharp
public string Text
{
    get;
    set => field = string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("Text must not be blank.")
        : value.Trim();
} = "untitled";
```

Старую реализацию с явным _text можно заменить такой после проверки инвариантов.
Если идентификатор field уже существовал в accessor, используйте явное имя или @field,
чтобы устранить неоднозначность.

Null-conditional assignment пропускает вычисление правой части при null:

```csharp
Label? absent = null;
int calls = 0;
absent?.Text = (++calls).ToString();
Console.WriteLine(calls); // 0
```

`nameof(List<>)` возвращает List без закрытия generic-типа.
Новые span-преобразования участвуют в выборе overload и выводе типов;
после обновления компилятора проверяйте вызовы библиотек с близкими перегрузками.
Лямбда `(ref value) => value++` может выводить тип параметра из делегата RefChange.

Extension blocks уже использованы в главе 17; они позволяют группировать extension-члены,
в том числе свойства. Они не добавляют полей экземпляра в расширяемый тип.
Обзор синтаксиса — [C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14).

Counter демонстрирует instance compound assignment:

```csharp
public sealed class Counter
{
    public int Value { get; private set; }
    public void operator +=(int amount) =>
        Value = checked(Value + amount);
}
```

Здесь counter += 3 меняет существующий объект; другая ссылка alias видит Value=3.
Это осознанный контракт изменяемого счётчика, который нельзя без проверки переносить на Money
с семантикой значения. Старый путь через operator + мог возвращать новый объект.
PartialSample отдельно демонстрирует partial constructor, property и event в двух файлах.
Их общий результат проверяется вызовом, подпиской и отпиской.

```powershell
dotnet run --project examples/RuntimeLab -c Release -- 24
dotnet run --project examples/RuntimeChecks -c Release
```

Ожидаемый вывод демонстрации: `ledger`, `List`, `0`, `3`, `5`, `9`, `7`, `6`.
Это соответственно проверенное свойство, nameof, пропущенный побочный эффект, +=,
ref-лямбда, partial-члены, constrained ref struct и span-вызов.

## File-based приложения

Небольшую программу можно выполнить из одного .cs файла. В примере hello.cs директивы
`#:property` задают настройки; `#:package` добавляет пакет, `#:sdk` выбирает SDK,
`#:project` ссылается на проект. Shebang может использоваться при прямом запуске в Unix.
Директивы и возможности SDK нужно сверять с установленной версией:
справочник может описывать и preview.

```powershell
dotnet run --file examples/FileBased/hello.cs
```

Ожидается `file-based C# 14`. Пример не требует пакетов и явно отключает PublishAot;
публикация file-based приложений по умолчанию имеет особенности, отличные от обычного проекта.
Для нескольких проектов, тестов и явных зависимостей наш курс продолжает использовать solution.
[File-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps).

## Практика

**24-A.** Для своей среды запишите dotnet --version, TFM, LangVersion и путь global.json.
Критерий: объяснено, почему изменение только LangVersion не добавляет отсутствующий API платформы.

**24-B.** Напишите старый Label с _text и современный с field. Для `" ledger "` оба возвращают
ledger; пустая строка бросает ArgumentException и сохраняет прежнее значение.
Критерий: один набор проверок применяется к обеим реализациям, включая начальное значение.

**24-C.** Модернизируйте в своей копии один класс L02 и nullable-контракт его клиента.
Входы и ожидаемые результаты возьмите из лабораторной: дубликат, пустой набор, неверный месяц.
Критерий: прежние проверки проходят, нет массового подавления предупреждений;
каждая смена равенства, порядка или владения либо исключена, либо оформлена как отдельное требование.

## Допуск к BCL-блоку

Объясните сборку и JIT, найдите владельца ресурса, различите runtime reflection и compile-time generation.
Покажите запрет выхода stackalloc-ссылки и тест сохранения поведения при обновлении синтаксиса.
Следующие главы 25–30 развивают коллекции, текст, файлы, JSON и HTTP; их состояние — в
[программе](../../PLAN.md).

[Назад: 23](23-references.md) · [Подсказки](../appendices/hints.md) ·
[Ход наполнения](../../PROGRESS.md)
