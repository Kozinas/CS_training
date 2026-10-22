# 36. Автоматические тесты

**Результат:** вы переводите договор в проверяемые примеры, выбираете границу теста,
управляете временем и зависимостями, читаете отчёты без ложных выводов.
Нужны главы 18, 28, 31 и 35. Примеры — [Engineering](../../examples/Engineering/README.md).

## Тест утверждает поведение

До этого курса проверки были исполняемыми программами и Python-сценариями.
Тестовый framework добавляет обнаружение кейсов, изоляцию, параметризацию и отчёты.
Он не определяет правильное поведение за вас. Ожидаемый результат нужно вывести из договора,
а не вычислить тем же алгоритмом, который проверяется.

Новый пример: расход может согласовываться автоматически, если сумма не выше лимита,
а возраст заявки находится в интервале от 0 включительно до 30 минут исключительно.
На точной границе 30 минут заявка уже просрочена. Возраст в будущем — отдельный исход.

```csharp
// Метод из тестового класса; перед ним using Course.Review и Xunit.
[Fact]
public void Exact_deadline_is_expired()
{
    var policy = ApprovalPolicy.Standard();
    var start = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    ApprovalDecision actual = policy.Evaluate(
        100m, "food", start, start.AddMinutes(30));

    Assert.Equal(ApprovalDecision.Expired, actual);
}
```

Arrange задаёт исходные данные, Act вызывает поведение, Assert сравнивает результат.
Разделение помогает увидеть причину падения. Не обязательно оставлять эти три слова комментариями
в каждом маленьком тесте; важно, чтобы структура читалась. Название описывает правило, а не «Test1».

## Fact, Theory и независимость

Fact — один сценарий, Theory — правило на нескольких наборах данных.
InlineData подходит для небольших констант; MemberData/ClassData — для сложных наборов.
Случаи границ нужны явно, даже если есть генерация случайных данных.

```csharp
// Фрагмент тестового класса.
[Theory]
[InlineData(1799, ApprovalDecision.AutoApproved)]
[InlineData(1800, ApprovalDecision.Expired)]
[InlineData(1801, ApprovalDecision.Expired)]
public void Age_boundary(int seconds, ApprovalDecision expected)
{
    var start = DateTimeOffset.UnixEpoch;
    Assert.Equal(expected, ApprovalPolicy.Standard().Evaluate(
        100m, "food", start, start.AddSeconds(seconds)));
}
```

Тесты могут выполняться параллельно. Новый экземпляр класса не устраняет общий static,
общую БД, файлы с одинаковым именем или изменение environment. Используйте уникальные ресурсы,
восстанавливайте изменённое состояние, выбирайте осознанную область fixture.
IClassFixture разделяет подготовку внутри класса; collection fixture — между классами коллекции.
Общий дорогой fixture не должен неявно связывать порядок результатов.
[xUnit.net v3](https://xunit.net/docs/getting-started/v3/getting-started).

## Framework и runner — разные части

xUnit, NUnit и MSTest предоставляют модель тестов и assertions. VSTest и Microsoft.Testing.Platform
обнаруживают/запускают тесты и подключают отчёты. Версия runner влияет на CLI и расширения.
В Engineering используется xUnit.net v3, пакет xunit.v3.mtp-v2 **4.0.2**, MTP v2 и SDK 10.0.402.
Имя поколения framework и номер пакета не обязаны совпадать.

| Framework | Сценарий / параметризация | Организация подготовки |
| --- | --- | --- |
| xUnit | Fact / Theory | Конструктор, IDisposable/IAsyncLifetime, fixtures |
| NUnit | Test / TestCase | SetUp/TearDown, OneTimeSetUp |
| MSTest | TestMethod / DataRow | TestInitialize/TestCleanup, ClassInitialize |

Это альтернативы для той же задачи; не стоит подключать все три в один небольшой учебный проект.
При чтении чужого кода проверяйте версии, правила жизненного цикла и настройки параллелизма.
Примеры курса реализованы на xUnit; NUnit/MSTest здесь рассматриваются сравнительно.

```powershell
Push-Location examples/Engineering
try {
    dotnet restore Engineering.slnx --locked-mode
    dotnet build Engineering.slnx -c Release --no-restore
    dotnet test --project ReviewRules.Tests -c Release --no-build
    dotnet test --project Integration.Tests -c Release --no-build
} finally { Pop-Location }
```

global.json выбирает MTP в этом поддереве. Команду запускайте из указанной папки:
SDK и runner выбираются по рабочему каталогу. `--project` здесь является опцией нового dotnet test.
Не переносите механически VSTest-команду `--collect:"XPlat Code Coverage"` в MTP-проект.
[Выбор платформы](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test).

## Границы тестирования

| Вид | Что проверяем в курсе | Что из этого не следует |
| --- | --- | --- |
| Unit | Чистую ApprovalPolicy; координацию ReviewService с подменой sink | Работоспособность настоящей БД |
| Contract | Один договор IExpenseStore для Dictionary и List | Транзакционность или многопоточность |
| Integration | Настоящий HttpClient, DI/resilience и loopback TCP | Доступность публичного сервера |
| Process / end-to-end | CLI, exit code, stdout/stderr, файл либо потребление пакета | Корректность всех инфраструктур production |

Названия описывают выбранную границу; один и тот же тест может быть интеграционным для библиотеки
и частью end-to-end сценария для пользователя. Полезнее записать реальные зависимости, чем спорить о названии.
Быстрые тесты дают раннюю обратную связь, интеграционные ловят ошибки на стыках.
Нельзя заменить весь набор проверок большим количеством одинаковых unit-тестов.

## Подмены и NSubstitute

Stub выдаёт подготовленный ответ; fake имеет упрощённую рабочую реализацию;
spy запоминает взаимодействия; mock проверяет ожидаемое взаимодействие.
Эти роли могут сочетаться в одном объекте. Подменяйте внешний эффект на границе,
а не каждую вспомогательную функцию внутри проверяемого класса.

В [ServiceTests](../../examples/Engineering/ReviewRules.Tests/ServiceTests.cs)
NSubstitute **6.2.0** создаёт IReviewSink, моделирует IOException и проверяет, что запись выполнена
ровно один раз с нужным Id/решением/token. Настоящая policy при этом остаётся в тесте.

```csharp
// Фрагмент теста с using NSubstitute.
var sink = Substitute.For<IReviewSink>();
sink.SaveAsync(Arg.Any<ReviewRecord>(), Arg.Any<CancellationToken>())
    .Returns(_ => ValueTask.FromException(new IOException("injected")));
```

Проверка количества вызовов оправдана, когда повтор — значимое нарушение договора.
Проверять точный порядок всех внутренних методов обычно хрупко: безопасный рефакторинг ломает тест.
NSubstitute над интерфейсом предсказуем; невиртуальный метод класса не перехватывается
и может выполнить настоящий эффект. [Создание подмен](https://nsubstitute.github.io/help/creating-a-substitute/),
[проверка вызовов](https://nsubstitute.github.io/help/received-calls/).

## Время, async и отмена

Тесты async возвращают Task и действительно await проверяемую операцию.
Assert.ThrowsAsync ожидает точный тип исключения; ThrowsAnyAsync подходит для семейства
OperationCanceledException, включающего TaskCanceledException. Не используйте async void.
Тайм-аут теста обнаруживает зависание, но не заменяет контролируемое расписание.

FakeTimeProvider из пакета **10.11.0** позволяет двигать часы без Sleep.
В примере Advance переводит время с «на один тик раньше дедлайна» на точную границу;
другой тест завершает Task.Delay, который использует тот же TimeProvider.
Если код читает DateTimeOffset.UtcNow напрямую или создаёт обычный таймер,
подмена часов его не контролирует. [FakeTimeProvider](https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing).

Передавайте TestContext.Current.CancellationToken в долгие операции теста.
Отдельный CTS используйте для моделирования проверяемой отмены; различайте её и остановку самого теста.
Сигнал TaskCompletionSource задаёт порядок, а конечный timeout ограничивает время обнаружения дефекта.
В уже написанной L04 так проверяются заполненные очереди и освобождение всех участников.

## Coverage, свойства и мутации

Coverage показывает исполненные строки/ветви, но не правильность assertions.
Тест, который вызывает метод и ничего не проверяет, способен дать высокий процент.
В этом выпуске отчёт ReviewRules показывает 100% строк и ветвей для данного измерения;
это наблюдение о трассе выполнения, а не сертификат качества.

```powershell
# Из examples/Engineering; сохраняет полный отчёт вне исходников.
$coverageArgs = @("--project", "ReviewRules.Tests", "-c", "Release",
    "--no-build", "--coverage", "--coverage-output-format", "cobertura",
    "--coverage-output", "coverage.cobertura.xml", "--results-directory",
    "../../.artifacts/engineering/coverage")
dotnet test @coverageArgs
```

Версия расширения Microsoft.Testing.Extensions.CodeCoverage — **18.12.0**.
[Coverage на MTP](https://xunit.net/docs/getting-started/v3/code-coverage-with-mtp).
Мутационный тест намеренно меняет условие и проверяет, замечают ли тесты изменение.
`python tools/reproduce_boundary.py` в отдельной копии заменяет `>=` на `>`;
компиляция должна пройти, а Exact_deadline_is_expired — упасть с Expired/AutoApproved.
Это одна контролируемая мутация, а не полный mutation score. Stryker.NET автоматизирует множество мутаций;
его отдельный запуск в этот выпуск не входит. [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/).

Property-based testing задаёт свойства для сгенерированных входов: например, увеличение суммы
не должно превращать ManualReview в AutoApproved при прочих равных.
FsCheck генерирует значения и уменьшает контрпример через shrinking; сохраняйте seed и минимальный
провал как регрессионный пример. Ограничения генератора должны соответствовать домену.
В коде курса есть небольшой детерминированный перебор этого свойства; FsCheck представлен обзором,
а не установленным test runner. [FsCheck](https://fscheck.github.io/FsCheck/).

## Настоящая БД через Testcontainers

Fake хранилища не воспроизводит SQL, collation, уникальные ограничения и транзакции настоящей СУБД.
Testcontainers создаёт контейнер для интеграционного теста, ожидает готовности и освобождает ресурсы.
Используйте динамический порт и уникальные данные; не полагайтесь на общую заранее подготовленную БД.
[Testcontainers PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/).

Отдельный [Container.Tests](../../examples/Engineering/Container.Tests/PostgresTests.cs)
содержит минимальную проверку: UUID primary key запрещает второй INSERT того же Id;
ожидается SQLSTATE 23505 и одна оставшаяся строка. Команда:
`python tools/verify_engineering.py --containers`. Требуется работающий Linux Docker engine.
Подробный смысл SQL и проектирование схемы изучаются в 40–43; сейчас это проверка границы инфраструктуры.

Проект восстановлен и скомпилирован. В среде выпуска daemon Docker недоступен,
поэтому контейнерный тест **не выполнялся**. Обычный прогон явно сообщает это;
явный запрос --containers без daemon завершается ошибкой, а не успешным skip.

## Контрольные вопросы

1. Почему 100% покрытия не доказывает правильность границы дедлайна?
2. Чем ошибка сборки мутированной версии отличается от обнаруженной тестом мутации?
3. Как доказать, что async-метод не вернул успех до завершения записи?

## Практика

**36-A.** Напишите Theory для возраста −1, 0, 1799, 1800, 1801 секунд.
Ожидаются FutureSubmission, AutoApproved, AutoApproved, Expired, Expired.
Критерий: фиксированное начальное время, отдельный кейс каждого значения, нет Sleep и текущих часов ОС.

**36-B.** Проверьте ReviewService с FakeTimeProvider и подменой sink: успех, IOException, отмена до записи
и отмена ожидающей записи. Критерий: успешный ответ не приходит раньше завершения sink,
ошибка не превращается в успех, token передан, после предварительной отмены вызовов записи нет.

**36-C.** Добавьте свойство монотонности лимита и интеграционный кейс с реальной БД.
При повторном Id ожидается unique violation; in-memory fake не засчитывается вместо БД.
Критерий: сохранён контрпример/seed для property-test, контейнер очищается при падении assertion,
отсутствие Docker сообщается отдельно от успешных тестов. Этот уровень требует своей Docker-среды.

[Назад: 35](../05-concurrency/35-worker.md) · [Далее: 37](37-design.md) · [Подсказки](../appendices/hints.md)
