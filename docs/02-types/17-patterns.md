# 17. Шаблоны и расширение синтаксиса

**Результат:** вы выражаете проверки формы данных через patterns и добавляете удобные операции,
сохраняя понятные правила типов. Нужны главы 12–16.
Запуск: `dotnet run --project examples/TypesAndQueries -- 17`.

## Проверка типа и формы

`value is string text` проверяет тип и при успехе создаёт локальную переменную text.
Такой pattern не совпадает с null. `value as string` возвращает строку или null и применим
к ссылочным и nullable-типам; `as int` недопустим. Проверка типа не является произвольным
числовым преобразованием: упакованный int не совпадает с pattern long.

```csharp
static string Describe(object? value) => value switch
{
    null => "missing",
    int n when n < 0 => "negative integer",
    int => "integer",
    string { Length: 0 } => "empty text",
    string text => $"text length={text.Length}",
    _ => "other"
};
```

Ветки проверяются по порядку. `when` — дополнительное условие.
Property pattern сначала требует non-null значение, затем проверяет члены.
`is null` проверяет отсутствие ссылки без вызова перегруженного оператора ==.

## Relational, logical и вложенные patterns

```csharp
static string Classify(decimal amount) => amount switch
{
    < 0m => "invalid",
    0m => "zero",
    > 0m and < 1000m => "small",
    _ => "large"
};
```

Relational pattern сравнивает с константой. Для изменяемого limit используйте when:
`decimal value when value >= limit => ...`.
Ключевые слова not, and, or объединяют patterns; они не являются произвольной заменой
логических операторов && и || внутри любого выражения. Скобки делают приоритет очевидным.

Для Money из L02 `money is { Amount: >= 1000m }` проверяет значение свойства.
У Expense можно написать `{ Amount: { Amount: >= 1000m } }`
или extended property pattern `{ Amount.Amount: >= 1000m }`.
Не помещайте побочные действия в getter, от вызова которого зависит pattern:
порядок вычисления независимых подшаблонов не стоит превращать в бизнес-логику.

Компилятор сообщает о недостижимых ветках и некоторых неохваченных значениях.
Резервная ветка `_ => ...` нужна, когда вход шире явно перечисленных случаев.
Даже enum может содержать неименованное числовое значение.

## Позиционные и списковые patterns

```csharp
// Point — record struct Point(int X, int Y) из главы 12.
bool onVerticalAxis = point is (0, _);
bool startsWithReport = arguments is ["report", var month];
```

Позиционный pattern использует Deconstruct. Для обычного класса можно объявить
`void Deconstruct(out int x, out int y)` самостоятельно.
Имена локальных переменных при деконструкции не меняют тип исходного объекта.

List pattern сопоставляет длину и элементы индексируемого значения.
В `[1, .., 9]` slice допускает середину произвольной длины; `[.. var rest]` захватывает срез.
Это не универсальный способ обработать любой IEnumerable: нужны доступные Count/Length и индексатор
либо поддерживаемая compiler pattern форма. Захват среза массива может создавать новую копию.

## Extension methods и C# 14

Классическая запись расширения:

```csharp
public static class MoneyPresentation
{
    public static string Display(this Money money) =>
        money.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
```

Money — тип из L02. Вызов `money.Display()` превращается в статический вызов метода.
Расширение не получает доступ к private-состоянию типа и не меняет его виртуальные методы.
Если подходящий обычный член экземпляра существует, он имеет приоритет.
Выбор extension method зависит от статических типов и доступных namespace.

В C# 14 тот же static-класс может содержать блок:

```csharp
extension(Money money)
{
    public bool IsZero => money.Amount == 0m;
}
```

Этот фрагмент помещается внутрь MoneyPresentation. Теперь доступно `Money.Zero.IsZero`.
Extension blocks допускают и статические расширения, а также некоторые операторы.
Старый синтаксис с this остаётся полезным для чтения существующих библиотек.
Специфичные ограничения и новые варианты языка собраны в главе 24.

## Операторы, преобразования и индексаторы

Владелец Money может объявить `public static Money operator +(Money a, Money b)`.
Сложение должно сохранять привычный смысл и инварианты. Для точного денежного типа нельзя
молча округлять недопустимую дробь только потому, что используется оператор.

Пользовательское преобразование задаётся через `implicit operator` или `explicit operator`.
Неявное преобразование уместно, когда потеря информации и неожиданная ошибка не являются обычным
результатом. Для потенциально ошибочного ввода понятнее явная фабрика или Try-метод.
Перегрузка == требует согласования с Equals/GetHashCode; часть операторов объявляется парами.

Индексатор выражает доступ к элементу:

```csharp
public sealed class Names
{
    private readonly string[] _items = ["Ann", "Bob"];
    public string this[int index] => _items[index];
}
```

У вызова `names[1]` результат Bob. Индексатор может иметь несколько параметров и get/set,
но не должен скрывать неожиданно дорогую сетевую операцию за видом простого чтения массива.

## Практика

**17-A.** Реализуйте Describe. Критерии: null → missing, −1 → negative integer,
0 → integer, пустая строка → empty text, "ab" → text length=2.
Отдельно проверьте long 1: он должен попасть в other.

**17-B.** Разберите массив аргументов с patterns: ["report", month] и ["help"].
Критерии: лишний аргумент отклоняется; полученный month затем проходит настоящую проверку формата,
поскольку совпадение формы массива не валидирует календарное значение.

**17-C.** Добавьте Display как extension method и IsZero как extension property C# 14.
Критерии: Money(1.5m).Display() → 1.50; default(Money).IsZero → true.
Объясните, почему эти расширения не превращают Money в новый подкласс.

## Проверка понимания

Когда property pattern исключает null? Чем when отличается от relational pattern?
Почему List pattern не заменяет foreach для любого источника?
Почему extension method не выполняет виртуальную подстановку? Когда оператор ухудшает понятность API?

[Patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns) ·
[Extension members](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)

[Подсказки](../appendices/hints.md) · [Далее 18](18-library.md)
