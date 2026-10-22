# 21. Атрибуты, reflection и dynamic

**Результат:** вы строите ограниченный валидатор по метаданным и понимаете, какие проверки
переносятся с компиляции на выполнение. Нужны главы 13, 16, 19–20.
Исходник — [MetadataValidator.cs](../../examples/RuntimeLab/MetadataValidator.cs).

## Тип, имя и метаданные

`typeof(T)` получает Type по известному типу; `value.GetType()` — фактический тип ненулевого объекта.
`nameof(value)` даёт строку имени на этапе компиляции и не читает значение.
Поэтому nameof не заменяет GetType, а typeof не создаёт экземпляр.

Reflection позволяет найти свойства и методы, проверить сигнатуры и вызвать их.
Атрибут добавляет метаданные, но не выполняет бизнес-правило сам.
Наш маркер объявляется так:

```csharp
[AttributeUsage(AttributeTargets.Property,
    AllowMultiple = false, Inherited = true)]
public sealed class NonBlankAttribute : Attribute;
```

У AttributeUsage есть область применения, разрешение повторов и наследование.
Имя атрибута при использовании можно сокращать до NonBlank.
Аргументы атрибутов ограничены допустимыми константами, typeof и массивами допустимых типов;
произвольный объект настроек туда не передаётся.
Targets позволяют указать, что аннотируется: например, `[assembly: ...]`, `[return: ...]`
или `[property: ...]` на параметре позиционной записи.
[Атрибуты и reflection](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/).

## Контракт небольшого валидатора

Валидируем **объявленный тип T**, а не все члены фактического подкласса.
В схему входят публичные свойства экземпляра, помеченные NonBlank.
Поддерживаются только string-свойства с публичным getter без индексных параметров.
null, пустая строка и пробелы дают имя свойства в списке ошибок; список сортируется ordinal.
Неподдерживаемый помеченный член — ошибка схемы, а не ошибка данных.

```csharp
public sealed class ImportOptions
{
    [NonBlank]
    public string? Name { get; init; }
}
```

Алгоритм: получить свойства с BindingFlags.Public | BindingFlags.Instance,
отобрать атрибут, проверить форму члена, вызвать GetValue и проверить строку.
Эта явная последовательность важна: индексатор нельзя вызвать без индексов,
а getter может выполнять код и выбрасывать исключение. Вызов через reflection обычно оборачивает
исключение getter в TargetInvocationException; в InnerException находится причина.

```powershell
dotnet run --project examples/RuntimeLab -- 21
dotnet run --project examples/RuntimeChecks
```

Ожидается `Name`, `0`, `GreaterThan`, `True`, `4` на отдельных строках.
Первая модель имеет null, вторая — допустимое имя.
Это учебная схема для собственных DTO. Не запускайте произвольные getter неизвестного плагина
ради «безопасного просмотра»: reflection-вызов остаётся исполнением кода.

## Создание объектов и generic-типы

`Activator.CreateInstance<T>()` подходит, если заранее известен T и доступен подходящий конструктор.
В простом generic API ограничение `where T : new()` и `new T()` яснее.
Для runtime-типа нужны отдельные проверки, доступен ли он, не абстрактен ли, не содержит ли
незакрытые generic-параметры, есть ли нужная сигнатура конструктора.

```csharp
// Фрагмент исследовательского JIT-приложения, не AOT-контракт.
Type open = typeof(List<>);
Type closed = open.MakeGenericType(typeof(int));
object list = Activator.CreateInstance(closed)
    ?? throw new InvalidOperationException("No instance");
Console.WriteLine(closed.IsGenericType); // True
```

MakeGenericType проверяет constraints. GetMethod только по имени может быть неоднозначен
при overload: выбирайте сигнатуру. Для многократных вызовов сначала измерьте стоимость;
кэш MethodInfo или заранее созданный делегат может устранить повторный поиск.
При плагинах учитывайте, что кэш Type/MethodInfo может удерживать AssemblyLoadContext.

## Что обещает trimming-аннотация

Trimming удаляет недостижимый код при публикации. Если нужный член определяется строкой в runtime,
анализатор может не доказать, что его следует сохранить.
У Validate параметр типа помечен так:

```csharp
// Часть сигнатуры; using System.Diagnostics.CodeAnalysis.
public static IReadOnlyList<string> Validate<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties)] T>(T item)
    where T : class
```

Аннотация передаёт требование сохранить публичные свойства T по цепочке вызовов.
Она не делает любой reflection автоматически совместимым с AOT.
RequiresUnreferencedCode предупреждает о несовместимости с trimming;
RequiresDynamicCode — о потребности создавать исполняемый код во время работы.
Подавление предупреждения не добавляет отсутствующий код.
[Подготовка библиотек к trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming).

В этом выпуске проверено поведение в JIT-сборке. Публикация с trimming/Native AOT пока не проверена:
для неё нужен отдельный маленький consumer, параметры публикации и тест его готового бинарного файла.
Альтернатива для известного набора DTO — прямой валидатор или генерация исходников, как в главе 22.

## dynamic, var и анонимные типы

`var` сохраняет статический тип, выведенный компилятором. `dynamic` откладывает разрешение
операций до выполнения: строка поддерживает Length, но вызов Missing завершится
RuntimeBinderException. Доступность членов продолжает иметь значение.
dynamic не превращает объект в словарь и не делает его свойства необязательными.
Для данных извне сначала выберите явный DTO либо API чтения данных.
[Семантика dynamic](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interop/using-type-dynamic).

Анонимный тип `new { Category = "food", Count = 2 }` удобен для локальной проекции LINQ.
Его свойства доступны только для чтения; форма типа определяется компилятором.
Для публичного возвращаемого контракта используйте именованный тип.
Возврат object с расчётом на dynamic скрывает договор от вызывающего кода.

## Expression trees и информация о вызывающем коде

```csharp
Expression<Func<int, bool>> positive = n => n > 0;
Console.WriteLine(positive.Body.NodeType); // GreaterThan
Console.WriteLine(positive.Compile(preferInterpretation: true)(3));
```

Expression хранит структуру выражения, Func — вызываемый делегат.
Дерево можно анализировать и переводить в другой язык; провайдер БД поддерживает только свой набор узлов.
Не всякий C#-синтаксис представим в expression tree. Compile не переводит запрос в SQL.
В примере выбран интерпретатор; это не доказательство AOT-совместимости всего приложения.
[Деревья выражений](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/).

CallerMemberName и CallerArgumentExpression подставляют сведения о месте вызова
в необязательные параметры во время компиляции. Require из примера использует второй атрибут:
при `Require(total > 0)` сообщение содержит текст `total > 0`.
Это полезно для диагностики; вызывающий код может передать параметр явно, поэтому это не удостоверение личности.

## Практика

**21-A.** Получите Name, PropertyType и наличие NonBlank у ImportOptions.
Ожидаются Name, System.String и True. Критерий: отдельно объяснены typeof и GetType.

**21-B.** Добавьте DTO с двумя помеченными строками Code и Name.
Для null/пробелов получите `Code,Name`, для двух заполненных строк — пустой список.
Критерий: порядок устойчив; помеченный int и бросающий getter имеют явно различимую диагностику.

**21-C.** Напишите прямой валидатор той же схемы без reflection, сохранив результаты B.
Критерий: набор входов общий для двух реализаций; в отчёте описаны цена изменения схемы,
польза генератора и отдельные проверки, необходимые для AOT. Не называйте обычный build AOT-проверкой.

## Самопроверка

Кто выполняет правило атрибута? Почему var не похож на dynamic?
Почему typeof(T) и item.GetType() дают разные договоры наследования?
Когда reflection вызывает пользовательский код? Что trimming-аннотация обязует сохранить?

[Назад: 20](20-lifetime.md) · [Далее: 22](22-compiler.md) ·
[Подсказки](../appendices/hints.md)
